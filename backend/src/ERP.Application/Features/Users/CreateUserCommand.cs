using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Users;

/// <summary>Create a user inside the caller's tenant, optionally granting a business membership.</summary>
public record CreateUserCommand(
    string FullName,
    string Mobile,
    string? Email,
    string Password,
    Guid? BusinessId,
    string? RoleCode) : IRequest<Result<UserDto>>;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Mobile).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(200);
        RuleFor(x => x.RoleCode).NotEmpty().When(x => x.BusinessId is not null)
            .WithMessage("RoleCode is required when assigning a business.");
    }
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityQueries _identity;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _uow;

    public CreateUserCommandHandler(
        ICurrentUser currentUser, IIdentityQueries identity, IPasswordHasher passwordHasher, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _identity = identity;
        _passwordHasher = passwordHasher;
        _uow = uow;
    }

    public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var tenantId = await UserGuards.EnsureTenantOwnerAsync(_currentUser, ct);

        var mobile = request.Mobile.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        if (await _identity.FindByLoginAsync(mobile, ct) is not null ||
            (email is not null && await _identity.FindByLoginAsync(email, ct) is not null))
        {
            return Result<UserDto>.Fail("resource.conflict", "A user with that mobile or email already exists.");
        }

        var user = new User
        {
            TenantId = tenantId,
            FullName = request.FullName.Trim(),
            Mobile = mobile,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password)
        };
        await _uow.Repository<User>().AddAsync(user, ct);

        if (request.BusinessId is { } businessId)
        {
            // The business must belong to the caller's tenant.
            if (await _identity.GetBusinessTenantIdAsync(businessId, ct) != tenantId)
                return Result<UserDto>.Fail("resource.not_found", "Business not found in your tenant.");

            var role = await _uow.Repository<Role>().Query()
                .FirstOrDefaultAsync(r => r.Code == request.RoleCode, ct);
            if (role is null)
                return Result<UserDto>.Fail("resource.not_found", "Unknown role.");

            await _uow.Repository<UserBusiness>().AddAsync(new UserBusiness
            {
                UserId = user.Id,
                BusinessId = businessId,
                RoleId = role.Id
            }, ct);
        }

        await _uow.SaveChangesAsync(ct);

        return Result<UserDto>.Ok(
            new UserDto(user.Id, user.FullName, user.Mobile, user.Email, user.IsActive, user.IsSuperAdmin));
    }
}

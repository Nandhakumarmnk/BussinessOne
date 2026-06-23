using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Enums;
using ERP.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Businesses;

// ---- Create ----

public record CreateBusinessCommand(string Name, string BusinessTypeCode, string? GstNumber, string? Address)
    : IRequest<Result<BusinessDto>>;

public class CreateBusinessCommandValidator : AbstractValidator<CreateBusinessCommand>
{
    public CreateBusinessCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.BusinessTypeCode).NotEmpty()
            .Must(c => BusinessTypeCodes.All.Contains(c)).WithMessage("Unknown business type code.");
        RuleFor(x => x.GstNumber).MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(300);
    }
}

public class CreateBusinessCommandHandler : IRequestHandler<CreateBusinessCommand, Result<BusinessDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;

    public CreateBusinessCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<BusinessDto>> Handle(CreateBusinessCommand request, CancellationToken ct)
    {
        var tenantId = await AccessGuard.RequireTenantOwnerAsync(_currentUser, ct);

        var type = await _uow.Repository<BusinessType>().Query()
            .FirstOrDefaultAsync(t => t.Code == request.BusinessTypeCode, ct);
        if (type is null)
            return Result<BusinessDto>.Fail("resource.not_found", "Unknown business type.");

        var duplicate = await _uow.Repository<Business>().Query()
            .AnyAsync(b => b.TenantId == tenantId && b.Name == request.Name.Trim(), ct);
        if (duplicate)
            return Result<BusinessDto>.Fail("resource.conflict", "A business with that name already exists.");

        var ownerRole = await _uow.Repository<Role>().Query().FirstAsync(r => r.Code == RoleCodes.Owner, ct);

        var business = new Business
        {
            TenantId = tenantId,
            BusinessTypeId = type.Id,
            Name = request.Name.Trim(),
            GstNumber = request.GstNumber,
            Address = request.Address
        };
        await _uow.Repository<Business>().AddAsync(business, ct);

        // The creator (owner) becomes a member with the OWNER role.
        await _uow.Repository<UserBusiness>().AddAsync(new UserBusiness
        {
            UserId = _currentUser.UserId!.Value,
            BusinessId = business.Id,
            RoleId = ownerRole.Id
        }, ct);

        await _uow.SaveChangesAsync(ct);

        return Result<BusinessDto>.Ok(new BusinessDto(
            business.Id, business.Name, type.Code, type.Name,
            business.GstNumber, business.Address, business.IsActive, RoleCodes.Owner));
    }
}

// ---- Update ----

public record UpdateBusinessCommand(Guid Id, string Name, string? GstNumber, string? Address, bool IsActive)
    : IRequest<Result<BusinessDto>>;

public class UpdateBusinessCommandValidator : AbstractValidator<UpdateBusinessCommand>
{
    public UpdateBusinessCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.GstNumber).MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(300);
    }
}

public class UpdateBusinessCommandHandler : IRequestHandler<UpdateBusinessCommand, Result<BusinessDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;

    public UpdateBusinessCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<BusinessDto>> Handle(UpdateBusinessCommand request, CancellationToken ct)
    {
        var tenantId = await AccessGuard.RequireTenantOwnerAsync(_currentUser, ct);

        var business = await _uow.Repository<Business>().GetByIdAsync(request.Id, ct);
        if (business is null || business.TenantId != tenantId)
            throw new NotFoundException("Business not found.");

        business.Name = request.Name.Trim();
        business.GstNumber = request.GstNumber;
        business.Address = request.Address;
        business.IsActive = request.IsActive;
        _uow.Repository<Business>().Update(business);
        await _uow.SaveChangesAsync(ct);

        var type = await _uow.Repository<BusinessType>().GetByIdAsync(business.BusinessTypeId, ct);

        return Result<BusinessDto>.Ok(new BusinessDto(
            business.Id, business.Name, type?.Code ?? "", type?.Name ?? "",
            business.GstNumber, business.Address, business.IsActive, RoleCodes.Owner));
    }
}

// ---- Delete (soft) ----

public record DeleteBusinessCommand(Guid Id) : IRequest<Result>;

public class DeleteBusinessCommandHandler : IRequestHandler<DeleteBusinessCommand, Result>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;

    public DeleteBusinessCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result> Handle(DeleteBusinessCommand request, CancellationToken ct)
    {
        var tenantId = await AccessGuard.RequireTenantOwnerAsync(_currentUser, ct);

        var business = await _uow.Repository<Business>().GetByIdAsync(request.Id, ct);
        if (business is null || business.TenantId != tenantId)
            throw new NotFoundException("Business not found.");

        _uow.Repository<Business>().Remove(business);   // interceptor converts to soft delete
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

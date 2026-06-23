using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Features.Auth.Common;
using ERP.Application.Features.Auth.Login;
using ERP.Domain.Enums;
using ERP.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Auth.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<LoginResponse>>
{
    private readonly IIdentityQueries _identity;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _uow;
    private readonly AuthSessionService _session;

    public RegisterCommandHandler(
        IIdentityQueries identity, IPasswordHasher passwordHasher, IUnitOfWork uow, AuthSessionService session)
    {
        _identity = identity;
        _passwordHasher = passwordHasher;
        _uow = uow;
        _session = session;
    }

    public async Task<Result<LoginResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        var mobile = request.Mobile.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        if (await _identity.FindByLoginAsync(mobile, ct) is not null ||
            (email is not null && await _identity.FindByLoginAsync(email, ct) is not null))
        {
            return Result<LoginResponse>.Fail("resource.conflict", "A user with that mobile or email already exists.");
        }

        var tenant = new Tenant { Name = request.TenantName.Trim() };
        var owner = new User
        {
            TenantId = tenant.Id,
            FullName = request.FullName.Trim(),
            Mobile = mobile,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password)
        };

        await _uow.Repository<Tenant>().AddAsync(tenant, ct);   // OwnerUserId set after first save (avoids FK cycle)
        await _uow.Repository<User>().AddAsync(owner, ct);

        if (!string.IsNullOrWhiteSpace(request.FirstBusinessName))
        {
            var type = await _uow.Repository<BusinessType>().Query()
                .FirstOrDefaultAsync(bt => bt.Code == request.FirstBusinessTypeCode, ct);
            if (type is null)
                return Result<LoginResponse>.Fail("resource.not_found", "Unknown business type.");

            var ownerRole = await _uow.Repository<Role>().Query()
                .FirstAsync(r => r.Code == RoleCodes.Owner, ct);

            var business = new Business
            {
                TenantId = tenant.Id,
                BusinessTypeId = type.Id,
                Name = request.FirstBusinessName.Trim()
            };
            await _uow.Repository<Business>().AddAsync(business, ct);
            await _uow.Repository<UserBusiness>().AddAsync(new UserBusiness
            {
                UserId = owner.Id,
                BusinessId = business.Id,
                RoleId = ownerRole.Id
            }, ct);
        }

        await _uow.SaveChangesAsync(ct);

        tenant.OwnerUserId = owner.Id;
        _uow.Repository<Tenant>().Update(tenant);

        var response = await _session.IssueAsync(owner, ct);
        return Result<LoginResponse>.Ok(response);
    }
}

using ERP.Application.Common.Interfaces;
using ERP.Application.Features.Auth.Login;
using ERP.Domain.Identity;

namespace ERP.Application.Features.Auth.Common;

/// <summary>
/// Issues an authenticated session (access token + persisted rotating refresh token + memberships).
/// Shared by login, register and refresh so token handling lives in exactly one place.
/// </summary>
public class AuthSessionService
{
    private readonly IJwtService _jwt;
    private readonly IUnitOfWork _uow;
    private readonly IIdentityQueries _identity;
    private readonly IDateTime _clock;

    public AuthSessionService(IJwtService jwt, IUnitOfWork uow, IIdentityQueries identity, IDateTime clock)
    {
        _jwt = jwt;
        _uow = uow;
        _identity = identity;
        _clock = clock;
    }

    public async Task<LoginResponse> IssueAsync(User user, CancellationToken ct)
    {
        var access = _jwt.CreateAccessToken(user);

        var raw = _jwt.CreateRefreshToken();
        await _uow.Repository<RefreshToken>().AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _jwt.HashRefreshToken(raw),
            ExpiresAt = _clock.UtcNow.AddDays(30),
            CreatedAt = _clock.UtcNow
        }, ct);

        user.LastLoginAt = _clock.UtcNow;
        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(ct);

        var memberships = await _identity.GetMembershipsAsync(user.Id, ct);

        return new LoginResponse(
            access.Token,
            access.ExpiresInSeconds,
            raw,
            new UserSummary(user.Id, user.FullName, user.Mobile, user.Email, user.IsSuperAdmin),
            memberships);
    }
}

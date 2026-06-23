using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Features.Auth.Common;
using ERP.Application.Features.Auth.Login;
using ERP.Domain.Identity;
using MediatR;

namespace ERP.Application.Features.Auth.Refresh;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private const string Invalid = "auth.invalid_refresh";

    private readonly IIdentityQueries _identity;
    private readonly IJwtService _jwt;
    private readonly IUnitOfWork _uow;
    private readonly IDateTime _clock;
    private readonly AuthSessionService _session;

    public RefreshTokenCommandHandler(
        IIdentityQueries identity, IJwtService jwt, IUnitOfWork uow, IDateTime clock, AuthSessionService session)
    {
        _identity = identity;
        _jwt = jwt;
        _uow = uow;
        _clock = clock;
        _session = session;
    }

    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var hash = _jwt.HashRefreshToken(request.RefreshToken);
        var token = await _identity.GetRefreshTokenAsync(hash, ct);

        if (token is null || token.RevokedAt is not null || token.ExpiresAt <= _clock.UtcNow)
            return Result<LoginResponse>.Fail(Invalid, "Refresh token is invalid or expired.");

        var user = await _identity.GetUserAsync(token.UserId, ct);
        if (user is null || !user.IsActive)
            return Result<LoginResponse>.Fail(Invalid, "Refresh token is invalid or expired.");

        // Rotate: revoke the presented token; IssueAsync persists the revoke + the new token.
        token.RevokedAt = _clock.UtcNow;
        _uow.Repository<RefreshToken>().Update(token);

        var response = await _session.IssueAsync(user, ct);
        return Result<LoginResponse>.Ok(response);
    }
}

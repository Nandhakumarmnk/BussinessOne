using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Identity;
using MediatR;

namespace ERP.Application.Features.Auth.Logout;

public record LogoutCommand(string RefreshToken) : IRequest<Result>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IIdentityQueries _identity;
    private readonly IJwtService _jwt;
    private readonly IUnitOfWork _uow;
    private readonly IDateTime _clock;

    public LogoutCommandHandler(IIdentityQueries identity, IJwtService jwt, IUnitOfWork uow, IDateTime clock)
    {
        _identity = identity;
        _jwt = jwt;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result.Ok();

        var token = await _identity.GetRefreshTokenAsync(_jwt.HashRefreshToken(request.RefreshToken), ct);
        if (token is { RevokedAt: null })
        {
            token.RevokedAt = _clock.UtcNow;
            _uow.Repository<RefreshToken>().Update(token);
            await _uow.SaveChangesAsync(ct);
        }
        return Result.Ok();   // idempotent
    }
}

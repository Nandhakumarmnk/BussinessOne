using ERP.Domain.Identity;

namespace ERP.Application.Common.Interfaces;

public record AccessToken(string Token, DateTime ExpiresAtUtc, int ExpiresInSeconds);

/// <summary>Issues access tokens and (raw + hashed) refresh tokens.</summary>
public interface IJwtService
{
    AccessToken CreateAccessToken(User user);

    /// <summary>Cryptographically-random opaque refresh token (returned to the client).</summary>
    string CreateRefreshToken();

    /// <summary>One-way hash of a refresh token (what we store server-side).</summary>
    string HashRefreshToken(string rawToken);
}

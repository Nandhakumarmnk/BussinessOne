namespace ERP.Infrastructure.Identity;

/// <summary>Bound from the "Jwt" configuration section.</summary>
public class JwtOptions
{
    public string Issuer { get; set; } = "business-one";
    public string Audience { get; set; } = "business-one-clients";
    /// <summary>HMAC signing key — must be at least 32 chars (256 bits) for HS256.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

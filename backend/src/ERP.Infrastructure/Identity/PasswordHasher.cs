using System.Security.Cryptography;
using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Identity;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing. Format:
/// <c>pbkdf2.SHA256.{iterations}.{saltB64}.{hashB64}</c>.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
        return string.Join('.',
            "pbkdf2", Algorithm.Name, Iterations,
            Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 5 || parts[0] != "pbkdf2") return false;

        if (!int.TryParse(parts[2], out var iterations)) return false;
        var algorithm = new HashAlgorithmName(parts[1]);
        var salt = Convert.FromBase64String(parts[3]);
        var key = Convert.FromBase64String(parts[4]);

        var attempt = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, algorithm, key.Length);
        return CryptographicOperations.FixedTimeEquals(attempt, key);
    }
}

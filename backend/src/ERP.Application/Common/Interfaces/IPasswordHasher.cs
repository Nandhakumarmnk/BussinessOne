namespace ERP.Application.Common.Interfaces;

/// <summary>Hashes and verifies user passwords (PBKDF2 in the pilot; see Infrastructure).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

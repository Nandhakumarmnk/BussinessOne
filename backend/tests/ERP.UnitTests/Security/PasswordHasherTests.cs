using ERP.Infrastructure.Identity;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verify_returns_true_for_correct_password()
    {
        var hash = _hasher.Hash("Owner@123");

        _hasher.Verify("Owner@123", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_returns_false_for_wrong_password()
    {
        var hash = _hasher.Hash("Owner@123");

        _hasher.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Hashes_are_salted_and_therefore_unique()
    {
        _hasher.Hash("same").Should().NotBe(_hasher.Hash("same"));
    }

    [Fact]
    public void Verify_is_safe_against_malformed_hash()
    {
        _hasher.Verify("x", "not-a-valid-hash").Should().BeFalse();
    }
}

using System.IdentityModel.Tokens.Jwt;
using ERP.Domain.Identity;
using ERP.Infrastructure.Identity;
using ERP.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.UnitTests.Auth;

public class JwtServiceTests
{
    private static JwtService CreateSut() => new(
        Options.Create(new JwtOptions
        {
            Issuer = "business-one",
            Audience = "business-one-clients",
            SigningKey = "test-signing-key-must-be-at-least-32-characters",
            AccessTokenMinutes = 15
        }),
        new FixedClock(new DateTime(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void CreateAccessToken_embeds_subject_and_expiry()
    {
        var sut = CreateSut();
        var user = new User { FullName = "Demo Owner", Mobile = "9000000001", IsSuperAdmin = false };

        var token = sut.CreateAccessToken(user);
        token.ExpiresInSeconds.Should().Be(15 * 60);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "sa" && c.Value == "false");
    }

    [Fact]
    public void HashRefreshToken_is_deterministic_and_distinct()
    {
        var sut = CreateSut();
        var raw = sut.CreateRefreshToken();

        sut.HashRefreshToken(raw).Should().Be(sut.HashRefreshToken(raw));
        sut.HashRefreshToken(raw).Should().NotBe(sut.HashRefreshToken(sut.CreateRefreshToken()));
    }
}

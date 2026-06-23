using ERP.Application.Features.Auth.Register;
using ERP.Application.Features.Businesses;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Auth;

public class ValidatorTests
{
    private readonly RegisterCommandValidator _register = new();
    private readonly CreateBusinessCommandValidator _createBusiness = new();

    [Fact]
    public void Register_valid_command_passes()
    {
        var cmd = new RegisterCommand("Demo Group", "Owner", "9000000001", "o@x.com", "Owner@123", null, null);
        _register.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Register_rejects_short_password()
    {
        var cmd = new RegisterCommand("Demo Group", "Owner", "9000000001", null, "short", null, null);
        _register.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Register_requires_business_type_when_business_name_given()
    {
        var cmd = new RegisterCommand("Demo Group", "Owner", "9000000001", null, "Owner@123", "Sri Transport", null);
        _register.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Register_rejects_unknown_business_type()
    {
        var cmd = new RegisterCommand("Demo Group", "Owner", "9000000001", null, "Owner@123", "Sri Transport", "WIDGETS");
        _register.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateBusiness_rejects_unknown_type()
    {
        _createBusiness.Validate(new CreateBusinessCommand("Sri Transport", "WIDGETS", null, null))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateBusiness_accepts_known_type()
    {
        _createBusiness.Validate(new CreateBusinessCommand("Sri Transport", "TRANSPORT", null, null))
            .IsValid.Should().BeTrue();
    }
}

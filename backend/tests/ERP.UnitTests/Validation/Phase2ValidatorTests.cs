using ERP.Application.Features.Customers;
using ERP.Application.Features.Employees;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Validation;

public class Phase2ValidatorTests
{
    [Fact]
    public void RecordSalary_rejects_paid_above_amount()
    {
        var v = new RecordSalaryCommandValidator();
        var cmd = new RecordSalaryCommand(Guid.NewGuid(), new DateOnly(2026, 6, 1), 10000m, 12000m, null, null);
        v.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RecordSalary_accepts_valid()
    {
        var v = new RecordSalaryCommandValidator();
        var cmd = new RecordSalaryCommand(Guid.NewGuid(), new DateOnly(2026, 6, 1), 10000m, 8000m, null, null);
        v.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void MarkAttendance_rejects_invalid_status()
    {
        var v = new MarkAttendanceCommandValidator();
        v.Validate(new MarkAttendanceCommand(Guid.NewGuid(), new DateOnly(2026, 6, 23), "holiday"))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void RecordCollection_rejects_bad_mode_and_nonpositive_amount()
    {
        var v = new RecordCollectionCommandValidator();
        v.Validate(new RecordCollectionCommand(Guid.NewGuid(), new DateOnly(2026, 6, 23), 0m, "crypto", null))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void RecordCollection_accepts_valid()
    {
        var v = new RecordCollectionCommandValidator();
        v.Validate(new RecordCollectionCommand(Guid.NewGuid(), new DateOnly(2026, 6, 23), 500m, "upi", "UTR123"))
            .IsValid.Should().BeTrue();
    }
}

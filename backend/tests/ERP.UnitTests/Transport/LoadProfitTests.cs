using ERP.Domain.Transport;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Transport;

public class LoadProfitTests
{
    [Fact]
    public void Profit_is_amount_minus_all_expenses()
    {
        var load = new Load
        {
            LoadAmount = 18000m,
            LoadmanCharges = 800m,
            FuelExpense = 4200m,
            MaintenanceExpense = 600m,
            DriverCharges = 1500m,
            OtherExpense = 300m
        };
        load.Recalculate();

        // 18000 - (800 + 4200 + 600 + 1500 + 300) = 10600
        load.TotalExpenses.Should().Be(7400m);
        load.Profit.Should().Be(10600m);
    }

    [Fact]
    public void Profit_can_be_negative_when_expenses_exceed_amount()
    {
        var load = new Load { LoadAmount = 5000m, FuelExpense = 6000m };
        load.Recalculate();
        load.Profit.Should().Be(-1000m);
    }
}

public class LoadCreditTests
{
    private static LoadCredit NewCredit(decimal amount)
    {
        var c = new LoadCredit { LoadAmount = amount, PaidAmount = 0 };
        c.Recalculate();
        return c;
    }

    [Fact]
    public void New_credit_is_open_with_full_balance()
    {
        var c = NewCredit(10000m);
        c.Status.Should().Be("open");
        c.BalanceAmount.Should().Be(10000m);
    }

    [Fact]
    public void Partial_payment_sets_partial_status()
    {
        var c = NewCredit(10000m);
        c.ApplyPayment(4000m).Should().BeTrue();
        c.PaidAmount.Should().Be(4000m);
        c.BalanceAmount.Should().Be(6000m);
        c.Status.Should().Be("partial");
    }

    [Fact]
    public void Full_payment_settles_the_credit()
    {
        var c = NewCredit(10000m);
        c.ApplyPayment(10000m).Should().BeTrue();
        c.BalanceAmount.Should().Be(0m);
        c.Status.Should().Be("settled");
    }

    [Fact]
    public void Overpayment_is_rejected()
    {
        var c = NewCredit(10000m);
        c.ApplyPayment(12000m).Should().BeFalse();
        c.PaidAmount.Should().Be(0m);     // unchanged
        c.Status.Should().Be("open");
    }
}

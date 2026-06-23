using ERP.Domain.Cctv;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Cctv;

public class SaleTotalsTests
{
    [Fact]
    public void Sale_totals_sum_lines_tax_and_charges()
    {
        var sale = new Sale { InstallationCharges = 2000m, LabourCharges = 1500m };

        // 4 cameras @ 3200 + 18% tax
        var line = new SaleLine { Quantity = 4m, Rate = 3200m, TaxPercentage = 18m };
        line.ComputeTotal();
        sale.Lines.Add(line);
        sale.RecalculateTotals();

        sale.SubTotal.Should().Be(12800m);          // 4 * 3200
        sale.TaxAmount.Should().Be(2304m);           // 12800 * 18%
        sale.TotalAmount.Should().Be(18604m);        // 12800 + 2304 + 2000 + 1500
        line.LineTotal.Should().Be(15104m);          // 12800 + 2304
    }

    [Fact]
    public void Balance_is_total_minus_paid()
    {
        var sale = new Sale { PaidAmount = 5000m };
        var line = new SaleLine { Quantity = 1m, Rate = 10000m, TaxPercentage = 0m };
        line.ComputeTotal();
        sale.Lines.Add(line);
        sale.RecalculateTotals();

        sale.TotalAmount.Should().Be(10000m);
        sale.Balance.Should().Be(5000m);
    }
}

public class PurchaseOrderStateTests
{
    private static PurchaseOrder Draft()
    {
        var po = new PurchaseOrder();
        var line = new PurchaseOrderLine { Quantity = 10m, Rate = 3000m, TaxPercentage = 18m };
        line.ComputeTotal();
        po.Lines.Add(line);
        po.RecalculateTotal();
        return po;
    }

    [Fact]
    public void Total_is_sum_of_line_totals()
    {
        var po = Draft();
        po.TotalAmount.Should().Be(35400m);   // 10 * 3000 * 1.18
        po.Status.Should().Be(PoStatus.Draft);
    }

    [Fact]
    public void Happy_path_transitions_draft_to_received()
    {
        var po = Draft();
        po.Submit().Should().BeTrue();
        po.Status.Should().Be(PoStatus.Pending);
        po.Approve(Guid.NewGuid(), new DateTime(2026, 6, 23, 0, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
        po.Status.Should().Be(PoStatus.Approved);
        po.ApprovedBy.Should().NotBeNull();
        po.Receive().Should().BeTrue();
        po.Status.Should().Be(PoStatus.Received);
    }

    [Fact]
    public void Cannot_approve_a_draft_directly()
    {
        var po = Draft();
        po.Approve(Guid.NewGuid(), default).Should().BeFalse();
        po.Status.Should().Be(PoStatus.Draft);
    }

    [Fact]
    public void Cannot_receive_before_approval()
    {
        var po = Draft();
        po.Submit();
        po.Receive().Should().BeFalse();    // still pending, not approved
        po.Status.Should().Be(PoStatus.Pending);
    }

    [Fact]
    public void Received_po_cannot_be_cancelled()
    {
        var po = Draft();
        po.Submit();
        po.Approve(Guid.NewGuid(), default);
        po.Receive();
        po.Cancel().Should().BeFalse();
        po.Status.Should().Be(PoStatus.Received);
    }
}

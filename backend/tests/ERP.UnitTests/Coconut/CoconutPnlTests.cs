using ERP.Application.Features.Coconut;
using ERP.Domain.Coconut;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.UnitTests.Common;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Coconut;

public class CoconutPnlTests
{
    [Fact]
    public async Task Batch_pnl_is_sales_minus_purchase_labour_transport()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);

        var product = new Product { BusinessId = businessId, Name = "Copra" };
        db.Products.Add(product);

        var batch = new CoconutBatch
        {
            BusinessId = businessId, ProductId = product.Id, BatchNumber = "CB-08", PurchaseAmount = 60000m
        };
        db.CoconutBatches.Add(batch);

        db.CoconutLabourCharges.Add(new CoconutLabourCharge { BusinessId = businessId, BatchId = batch.Id, Amount = 5400m });
        db.CoconutTransportCharges.Add(new CoconutTransportCharge { BusinessId = businessId, BatchId = batch.Id, Amount = 4000m });
        db.CoconutBatchSales.Add(new CoconutBatchSale { BusinessId = businessId, BatchId = batch.Id, SaleQuantity = 900m, SaleValue = 70000m });
        db.CoconutBatchSales.Add(new CoconutBatchSale { BusinessId = businessId, BatchId = batch.Id, SaleQuantity = 300m, SaleValue = 14000m });
        await db.SaveChangesAsync();

        var handler = new GetCoconutBatchPnlQueryHandler(
            new Repository<CoconutBatch>(db), new Repository<Product>(db), new Repository<CoconutLabourCharge>(db),
            new Repository<CoconutTransportCharge>(db), new Repository<CoconutBatchSale>(db));

        var pnl = await handler.Handle(new GetCoconutBatchPnlQuery(batch.Id), default);

        pnl.ProductName.Should().Be("Copra");
        pnl.Purchase.Should().Be(60000m);
        pnl.LabourCost.Should().Be(5400m);
        pnl.TransportCost.Should().Be(4000m);
        pnl.TotalSales.Should().Be(84000m);     // 70000 + 14000
        pnl.TotalCost.Should().Be(69400m);      // 60000 + 5400 + 4000
        pnl.Profit.Should().Be(14600m);
    }
}

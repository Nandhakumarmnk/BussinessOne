using ERP.Application.Features.Farm;
using ERP.Domain.Farm;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.UnitTests.Common;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Farm;

public class FarmDomainTests
{
    [Fact]
    public void FeedEntry_amount_is_quantity_times_rate()
    {
        var entry = new FeedEntry { Quantity = 50m, Rate = 30m };
        entry.ComputeAmount();
        entry.Amount.Should().Be(1500m);
    }

    [Fact]
    public void Wallet_credit_and_debit_track_balance()
    {
        var wallet = new Wallet();
        wallet.Credit(20000m);
        wallet.Balance.Should().Be(20000m);

        wallet.Debit(6800m).Should().BeTrue();
        wallet.Balance.Should().Be(13200m);
    }

    [Fact]
    public void Wallet_rejects_overdraft()
    {
        var wallet = new Wallet();
        wallet.Credit(5000m);
        wallet.Debit(8000m).Should().BeFalse();
        wallet.Balance.Should().Be(5000m);   // unchanged
    }
}

public class FarmPnlTests
{
    [Fact]
    public async Task Batch_pnl_reconciles_sales_minus_all_costs()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);

        var batch = new FarmBatch { BusinessId = businessId, BatchNumber = "GT-03", AnimalType = "goat", PurchaseAmount = 120000m };
        db.FarmBatches.Add(batch);

        var feed = new FeedEntry { BusinessId = businessId, BatchId = batch.Id, FeedId = Guid.NewGuid(), Quantity = 1560m, Rate = 30m };
        feed.ComputeAmount();   // 46,800
        db.FeedEntries.Add(feed);

        db.MedicalRecords.Add(new MedicalRecord { BusinessId = businessId, BatchId = batch.Id, MedicineName = "Vax", Amount = 5000m, DoctorCharges = 2000m });
        db.BatchExpenses.Add(new BatchExpense { BusinessId = businessId, BatchId = batch.Id, ExpenseKind = "labour", Amount = 8000m });
        db.BatchSales.Add(new BatchSale { BusinessId = businessId, BatchId = batch.Id, SaleQuantity = 40, SaleAmount = 220000m });
        await db.SaveChangesAsync();

        var handler = new GetBatchPnlQueryHandler(
            new Repository<FarmBatch>(db), new Repository<FeedEntry>(db), new Repository<MedicalRecord>(db),
            new Repository<BatchExpense>(db), new Repository<BatchSale>(db));

        var pnl = await handler.Handle(new GetBatchPnlQuery(batch.Id), default);

        pnl.FeedCost.Should().Be(46800m);
        pnl.MedicalCost.Should().Be(7000m);
        pnl.LabourCost.Should().Be(8000m);
        pnl.TotalSales.Should().Be(220000m);
        pnl.TotalCost.Should().Be(181800m);   // 120000 + 46800 + 7000 + 8000
        pnl.Profit.Should().Be(38200m);
    }
}

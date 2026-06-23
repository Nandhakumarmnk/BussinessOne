using ERP.Application.Features.Customers;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.UnitTests.Customers;

public class CustomerLedgerServiceTests
{
    [Fact]
    public async Task Append_maintains_running_balance_and_outstanding()
    {
        var businessId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 23);

        await using var db = TestDb.Create(businessId);
        var uow = new UnitOfWork(db);
        var ledger = new CustomerLedgerService(uow);

        // Opening debit (customer owes 1000), then a 400 collection (credit).
        var opening = await ledger.AppendAsync(businessId, customerId, date, "opening", null, 1000m, 0m, default);
        await uow.SaveChangesAsync();

        var collection = await ledger.AppendAsync(businessId, customerId, date, "collection", null, 0m, 400m, default);
        await uow.SaveChangesAsync();

        opening.RunningBalance.Should().Be(1000m);
        collection.RunningBalance.Should().Be(600m);

        var outstanding = await db.CustomerLedger
            .Where(l => l.CustomerId == customerId)
            .SumAsync(l => l.Debit - l.Credit);
        outstanding.Should().Be(600m);
    }
}

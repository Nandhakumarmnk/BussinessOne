using ERP.Application.Common.Interfaces;
using ERP.Application.Features.Accounting;
using ERP.Domain.Exceptions;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.UnitTests.Accounting;

public class JournalServiceTests
{
    private static readonly DateOnly Date = new(2026, 6, 23);

    [Fact]
    public async Task Balanced_posting_creates_journal_ledger_and_accounts()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var uow = new UnitOfWork(db);
        var svc = new JournalService(uow);

        await svc.PostAsync(businessId, Date, "expense", null, "Diesel", new[]
        {
            new JournalLine(AccountCodes.Expenses, 4200m, 0m),
            new JournalLine(AccountCodes.Cash, 0m, 4200m)
        }, default);
        await uow.SaveChangesAsync();

        (await db.JournalTransactions.CountAsync()).Should().Be(1);
        (await db.LedgerEntries.CountAsync()).Should().Be(2);
        (await db.Accounts.CountAsync()).Should().Be(2);   // 5000 + 1000 auto-created

        var debits = await db.LedgerEntries.SumAsync(l => l.Debit);
        var credits = await db.LedgerEntries.SumAsync(l => l.Credit);
        debits.Should().Be(credits).And.Be(4200m);
    }

    [Fact]
    public async Task Unbalanced_posting_throws()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var svc = new JournalService(new UnitOfWork(db));

        var act = () => svc.PostAsync(businessId, Date, "manual", null, "bad", new[]
        {
            new JournalLine(AccountCodes.Expenses, 100m, 0m),
            new JournalLine(AccountCodes.Cash, 0m, 50m)
        }, default);

        await act.Should().ThrowAsync<DomainException>().Where(e => e.Code == "accounting.unbalanced");
    }

    [Fact]
    public async Task Line_with_both_debit_and_credit_throws()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var svc = new JournalService(new UnitOfWork(db));

        var act = () => svc.PostAsync(businessId, Date, "manual", null, "bad", new[]
        {
            new JournalLine(AccountCodes.Expenses, 100m, 100m)
        }, default);

        await act.Should().ThrowAsync<DomainException>();
    }
}

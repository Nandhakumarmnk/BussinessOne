using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Customers;
using ERP.Domain.Expenses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Dashboard;

public record DashboardSummaryDto(
    decimal TodayIncome,
    decimal TodayExpense,
    decimal MonthIncome,
    decimal MonthExpense,
    decimal TotalProfit,
    decimal PendingCredits,
    decimal PendingCollections);

[HasPermission(Permissions.DashboardView)]
public record GetDashboardSummaryQuery(DateOnly? AsOf) : IRequest<DashboardSummaryDto>;

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IRepository<Expense> _expenses;
    private readonly IRepository<Collection> _collections;
    private readonly IRepository<CustomerLedgerEntry> _ledger;
    private readonly IDateTime _clock;

    public GetDashboardSummaryQueryHandler(
        IRepository<Expense> expenses, IRepository<Collection> collections,
        IRepository<CustomerLedgerEntry> ledger, IDateTime clock)
    {
        _expenses = expenses;
        _collections = collections;
        _ledger = ledger;
        _clock = clock;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken ct)
    {
        // All repositories are auto-scoped to the active business by the global query filter.
        var today = request.AsOf ?? DateOnly.FromDateTime(_clock.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        // Income (collections received) — generic until verticals add load/sale revenue.
        var todayIncome = await _collections.Query().Where(c => c.CollectionDate == today).SumAsync(c => c.Amount, ct);
        var monthIncome = await _collections.Query()
            .Where(c => c.CollectionDate >= monthStart && c.CollectionDate < monthEnd).SumAsync(c => c.Amount, ct);
        var totalIncome = await _collections.Query().SumAsync(c => c.Amount, ct);

        var todayExpense = await _expenses.Query().Where(e => e.ExpenseDate == today).SumAsync(e => e.Amount, ct);
        var monthExpense = await _expenses.Query()
            .Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate < monthEnd).SumAsync(e => e.Amount, ct);
        var totalExpense = await _expenses.Query().SumAsync(e => e.Amount, ct);

        // Outstanding receivable across all customers (Σ debit − Σ credit).
        var outstanding = await _ledger.Query().SumAsync(l => l.Debit - l.Credit, ct);

        return new DashboardSummaryDto(
            TodayIncome: todayIncome,
            TodayExpense: todayExpense,
            MonthIncome: monthIncome,
            MonthExpense: monthExpense,
            TotalProfit: totalIncome - totalExpense,
            PendingCredits: outstanding,
            PendingCollections: outstanding);   // refined once verticals distinguish credit vs. due-to-collect
    }
}

using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Customers;
using ERP.Domain.Expenses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Accounting;

// Cross-business cash-basis statements derived from the unified money primitives:
// receipts (collections) and payments (expenses). These tie out with the dashboard by construction.

public record CashBookRowDto(DateOnly Date, string Description, decimal In, decimal Out, decimal Balance);
public record ProfitLossDto(decimal TotalIncome, decimal TotalExpense, decimal NetProfit);
public record CreditTrackingRowDto(Guid CustomerId, string CustomerName, decimal Outstanding);
public record CollectionRowDto(Guid Id, DateOnly Date, Guid CustomerId, decimal Amount, string Mode, string? Reference);

[HasPermission(Permissions.AccountingView)]
public record GetCashBookQuery(DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<CashBookRowDto>>;

[HasPermission(Permissions.AccountingView)]
public record GetProfitLossQuery(DateOnly? From, DateOnly? To) : IRequest<ProfitLossDto>;

[HasPermission(Permissions.AccountingView)]
public record GetCreditTrackingQuery : IRequest<IReadOnlyList<CreditTrackingRowDto>>;

[HasPermission(Permissions.AccountingView)]
public record GetCollectionTrackingQuery(DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<CollectionRowDto>>;

public class GetCashBookQueryHandler : IRequestHandler<GetCashBookQuery, IReadOnlyList<CashBookRowDto>>
{
    private readonly IRepository<Collection> _collections;
    private readonly IRepository<Expense> _expenses;
    public GetCashBookQueryHandler(IRepository<Collection> collections, IRepository<Expense> expenses)
    {
        _collections = collections;
        _expenses = expenses;
    }

    public async Task<IReadOnlyList<CashBookRowDto>> Handle(GetCashBookQuery request, CancellationToken ct)
    {
        var col = _collections.Query();
        var exp = _expenses.Query();
        if (request.From is { } f) { col = col.Where(c => c.CollectionDate >= f); exp = exp.Where(e => e.ExpenseDate >= f); }
        if (request.To is { } t) { col = col.Where(c => c.CollectionDate <= t); exp = exp.Where(e => e.ExpenseDate <= t); }

        var ins = await col.Select(c => new { Date = c.CollectionDate, Amount = c.Amount, c.Reference }).ToListAsync(ct);
        var outs = await exp.Select(e => new { Date = e.ExpenseDate, Amount = e.Amount, e.Description }).ToListAsync(ct);

        var rows = ins.Select(i => (i.Date, Desc: i.Reference ?? "Collection", In: i.Amount, Out: 0m))
            .Concat(outs.Select(o => (o.Date, Desc: o.Description ?? "Expense", In: 0m, Out: o.Amount)))
            .OrderBy(x => x.Date)
            .ToList();

        var result = new List<CashBookRowDto>(rows.Count);
        decimal balance = 0;
        foreach (var r in rows)
        {
            balance += r.In - r.Out;
            result.Add(new CashBookRowDto(r.Date, r.Desc, r.In, r.Out, balance));
        }
        return result;
    }
}

public class GetProfitLossQueryHandler : IRequestHandler<GetProfitLossQuery, ProfitLossDto>
{
    private readonly IRepository<Collection> _collections;
    private readonly IRepository<Expense> _expenses;
    public GetProfitLossQueryHandler(IRepository<Collection> collections, IRepository<Expense> expenses)
    {
        _collections = collections;
        _expenses = expenses;
    }

    public async Task<ProfitLossDto> Handle(GetProfitLossQuery request, CancellationToken ct)
    {
        var col = _collections.Query();
        var exp = _expenses.Query();
        if (request.From is { } f) { col = col.Where(c => c.CollectionDate >= f); exp = exp.Where(e => e.ExpenseDate >= f); }
        if (request.To is { } t) { col = col.Where(c => c.CollectionDate <= t); exp = exp.Where(e => e.ExpenseDate <= t); }

        var income = await col.SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        var expense = await exp.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        return new ProfitLossDto(income, expense, income - expense);
    }
}

public class GetCreditTrackingQueryHandler : IRequestHandler<GetCreditTrackingQuery, IReadOnlyList<CreditTrackingRowDto>>
{
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<CustomerLedgerEntry> _ledger;
    public GetCreditTrackingQueryHandler(IRepository<Customer> customers, IRepository<CustomerLedgerEntry> ledger)
    {
        _customers = customers;
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<CreditTrackingRowDto>> Handle(GetCreditTrackingQuery request, CancellationToken ct)
    {
        var ledger = _ledger;
        var rows = await _customers.Query()
            .Select(c => new CreditTrackingRowDto(c.Id, c.Name,
                ledger.Query().Where(l => l.CustomerId == c.Id).Sum(l => l.Debit - l.Credit)))
            .ToListAsync(ct);
        return rows.Where(r => r.Outstanding != 0).OrderByDescending(r => r.Outstanding).ToList();
    }
}

public class GetCollectionTrackingQueryHandler : IRequestHandler<GetCollectionTrackingQuery, IReadOnlyList<CollectionRowDto>>
{
    private readonly IRepository<Collection> _collections;
    public GetCollectionTrackingQueryHandler(IRepository<Collection> collections) => _collections = collections;

    public async Task<IReadOnlyList<CollectionRowDto>> Handle(GetCollectionTrackingQuery request, CancellationToken ct)
    {
        var q = _collections.Query();
        if (request.From is { } f) q = q.Where(c => c.CollectionDate >= f);
        if (request.To is { } t) q = q.Where(c => c.CollectionDate <= t);
        return await q.OrderByDescending(c => c.CollectionDate)
            .Select(c => new CollectionRowDto(c.Id, c.CollectionDate, c.CustomerId, c.Amount, c.Mode, c.Reference))
            .ToListAsync(ct);
    }
}

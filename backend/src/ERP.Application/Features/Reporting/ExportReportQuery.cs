using System.Globalization;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Reporting;
using ERP.Application.Common.Security;
using ERP.Domain.Customers;
using ERP.Domain.Exceptions;
using ERP.Domain.Expenses;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Reporting;

[HasPermission(Permissions.ReportGenerate)]
public record ExportReportQuery(string ReportKey, string Format, DateOnly? From, DateOnly? To) : IRequest<ReportFile>;

public class ExportReportQueryValidator : AbstractValidator<ExportReportQuery>
{
    private static readonly string[] Formats = { "pdf", "excel" };
    public ExportReportQueryValidator()
    {
        RuleFor(x => x.ReportKey).NotEmpty();
        RuleFor(x => x.Format).Must(f => Formats.Contains(f.ToLowerInvariant())).WithMessage("Format must be 'pdf' or 'excel'.");
    }
}

public class ExportReportQueryHandler : IRequestHandler<ExportReportQuery, ReportFile>
{
    private readonly IReportExporter _exporter;
    private readonly IRepository<Expense> _expenses;
    private readonly IRepository<Collection> _collections;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<CustomerLedgerEntry> _ledger;

    public ExportReportQueryHandler(
        IReportExporter exporter, IRepository<Expense> expenses, IRepository<Collection> collections,
        IRepository<Customer> customers, IRepository<CustomerLedgerEntry> ledger)
    {
        _exporter = exporter;
        _expenses = expenses;
        _collections = collections;
        _customers = customers;
        _ledger = ledger;
    }

    private static string Money(decimal d) => d.ToString("N2", CultureInfo.InvariantCulture);
    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd");

    public async Task<ReportFile> Handle(ExportReportQuery request, CancellationToken ct)
    {
        var table = request.ReportKey.ToLowerInvariant() switch
        {
            "expenses" => await ExpensesTable(request, ct),
            "collections" => await CollectionsTable(request, ct),
            "credit-outstanding" => await OutstandingTable(ct),
            "profit-loss" => await ProfitLossTable(request, ct),
            _ => throw new DomainException("report.unknown_key", $"Unknown report key '{request.ReportKey}'.")
        };
        return _exporter.Export(table, request.Format.ToLowerInvariant());
    }

    private async Task<ReportTable> ExpensesTable(ExportReportQuery r, CancellationToken ct)
    {
        var q = _expenses.Query();
        if (r.From is { } f) q = q.Where(e => e.ExpenseDate >= f);
        if (r.To is { } t) q = q.Where(e => e.ExpenseDate <= t);
        var rows = await q.OrderBy(e => e.ExpenseDate)
            .Select(e => new { e.ExpenseDate, e.Description, e.Amount }).ToListAsync(ct);

        return new ReportTable
        {
            Title = "Expenses",
            Subtitle = Range(r.From, r.To),
            Columns = new[] { new ReportColumn("Date"), new ReportColumn("Description"), new ReportColumn("Amount", true) },
            Rows = rows.Select(x => (IReadOnlyList<string>)new[] { Day(x.ExpenseDate), x.Description ?? "", Money(x.Amount) }).ToList(),
            TotalsRow = new[] { "Total", "", Money(rows.Sum(x => x.Amount)) }
        };
    }

    private async Task<ReportTable> CollectionsTable(ExportReportQuery r, CancellationToken ct)
    {
        var q = _collections.Query();
        if (r.From is { } f) q = q.Where(c => c.CollectionDate >= f);
        if (r.To is { } t) q = q.Where(c => c.CollectionDate <= t);
        var rows = await q.OrderBy(c => c.CollectionDate)
            .Select(c => new { c.CollectionDate, c.Mode, c.Reference, c.Amount }).ToListAsync(ct);

        return new ReportTable
        {
            Title = "Collections",
            Subtitle = Range(r.From, r.To),
            Columns = new[] { new ReportColumn("Date"), new ReportColumn("Mode"), new ReportColumn("Reference"), new ReportColumn("Amount", true) },
            Rows = rows.Select(x => (IReadOnlyList<string>)new[] { Day(x.CollectionDate), x.Mode, x.Reference ?? "", Money(x.Amount) }).ToList(),
            TotalsRow = new[] { "Total", "", "", Money(rows.Sum(x => x.Amount)) }
        };
    }

    private async Task<ReportTable> OutstandingTable(CancellationToken ct)
    {
        var ledger = _ledger;
        var rows = (await _customers.Query()
            .Select(c => new { c.Name, Outstanding = ledger.Query().Where(l => l.CustomerId == c.Id).Sum(l => l.Debit - l.Credit) })
            .ToListAsync(ct))
            .Where(x => x.Outstanding != 0).OrderByDescending(x => x.Outstanding).ToList();

        return new ReportTable
        {
            Title = "Outstanding Receivables",
            Columns = new[] { new ReportColumn("Customer"), new ReportColumn("Outstanding", true) },
            Rows = rows.Select(x => (IReadOnlyList<string>)new[] { x.Name, Money(x.Outstanding) }).ToList(),
            TotalsRow = new[] { "Total", Money(rows.Sum(x => x.Outstanding)) }
        };
    }

    private async Task<ReportTable> ProfitLossTable(ExportReportQuery r, CancellationToken ct)
    {
        var col = _collections.Query();
        var exp = _expenses.Query();
        if (r.From is { } f) { col = col.Where(c => c.CollectionDate >= f); exp = exp.Where(e => e.ExpenseDate >= f); }
        if (r.To is { } t) { col = col.Where(c => c.CollectionDate <= t); exp = exp.Where(e => e.ExpenseDate <= t); }
        var income = await col.SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        var expense = await exp.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        return new ReportTable
        {
            Title = "Profit & Loss (cash basis)",
            Subtitle = Range(r.From, r.To),
            Columns = new[] { new ReportColumn("Item"), new ReportColumn("Amount", true) },
            Rows = new List<IReadOnlyList<string>>
            {
                new[] { "Income (collections)", Money(income) },
                new[] { "Expense", Money(expense) }
            },
            TotalsRow = new[] { "Net Profit", Money(income - expense) }
        };
    }

    private static string? Range(DateOnly? from, DateOnly? to)
        => from is null && to is null ? null : $"{(from is { } f ? Day(f) : "…")} to {(to is { } t ? Day(t) : "…")}";
}

using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Expenses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Expenses;

public record ReportBucketDto(string Period, decimal Total);

[HasPermission(Permissions.Expense.Manage)]
public record GetExpenseReportQuery(string Period, DateOnly? From, DateOnly? To)
    : IRequest<IReadOnlyList<ReportBucketDto>>;

public class GetExpenseReportQueryHandler : IRequestHandler<GetExpenseReportQuery, IReadOnlyList<ReportBucketDto>>
{
    private readonly IRepository<Expense> _expenses;
    public GetExpenseReportQueryHandler(IRepository<Expense> expenses) => _expenses = expenses;

    public async Task<IReadOnlyList<ReportBucketDto>> Handle(GetExpenseReportQuery request, CancellationToken ct)
    {
        var query = _expenses.Query();
        if (request.From is { } from) query = query.Where(e => e.ExpenseDate >= from);
        if (request.To is { } to) query = query.Where(e => e.ExpenseDate <= to);

        // Per-day totals are computed in SQL; periodic bucketing happens in memory (bounded set).
        var daily = await query
            .GroupBy(e => e.ExpenseDate)
            .Select(g => new { Date = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        IEnumerable<ReportBucketDto> buckets = request.Period.ToLowerInvariant() switch
        {
            "monthly" => daily.GroupBy(d => $"{d.Date.Year:D4}-{d.Date.Month:D2}")
                              .Select(g => new ReportBucketDto(g.Key, g.Sum(x => x.Total))),
            "yearly"  => daily.GroupBy(d => $"{d.Date.Year:D4}")
                              .Select(g => new ReportBucketDto(g.Key, g.Sum(x => x.Total))),
            _         => daily.Select(d => new ReportBucketDto(d.Date.ToString("yyyy-MM-dd"), d.Total))
        };

        return buckets.OrderBy(b => b.Period).ToList();
    }
}

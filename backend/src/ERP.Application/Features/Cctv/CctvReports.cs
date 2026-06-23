using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Cctv;
using ERP.Domain.Customers;
using ERP.Domain.Employees;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Cctv;

public record ItemSalesRowDto(Guid ItemId, string ItemName, decimal QuantitySold, decimal Revenue);
public record RevenueBucketDto(string Period, decimal Revenue);
public record EmployeePerformanceRowDto(Guid EmployeeId, string EmployeeName, int ClosedComplaints);
public record ServiceReportDto(int Open, int InProgress, int Closed, IReadOnlyList<EmployeePerformanceRowDto> Performance);
public record CctvOutstandingRowDto(Guid CustomerId, string CustomerName, int Invoices, decimal Balance);

[HasPermission(Permissions.Cctv.SaleCreate)]
public record GetItemSalesReportQuery(DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<ItemSalesRowDto>>;

[HasPermission(Permissions.Cctv.SaleCreate)]
public record GetCctvRevenueReportQuery(string Period, DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<RevenueBucketDto>>;

[HasPermission(Permissions.Cctv.ServiceManage)]
public record GetServiceReportQuery : IRequest<ServiceReportDto>;

[HasPermission(Permissions.Cctv.SaleCreate)]
public record GetCctvOutstandingQuery : IRequest<IReadOnlyList<CctvOutstandingRowDto>>;

public class GetItemSalesReportQueryHandler : IRequestHandler<GetItemSalesReportQuery, IReadOnlyList<ItemSalesRowDto>>
{
    private readonly IRepository<SaleLine> _lines;
    private readonly IRepository<Sale> _sales;
    private readonly IRepository<Item> _items;
    public GetItemSalesReportQueryHandler(IRepository<SaleLine> lines, IRepository<Sale> sales, IRepository<Item> items)
    {
        _lines = lines;
        _sales = sales;
        _items = items;
    }

    public async Task<IReadOnlyList<ItemSalesRowDto>> Handle(GetItemSalesReportQuery request, CancellationToken ct)
    {
        // Joining to (business-scoped) sales restricts lines to this business + date range.
        var sales = _sales.Query();
        if (request.From is { } from) sales = sales.Where(s => s.SaleDate >= from);
        if (request.To is { } to) sales = sales.Where(s => s.SaleDate <= to);

        var rows = await (from line in _lines.Query()
                          join sale in sales on line.SaleId equals sale.Id
                          group line by line.ItemId into g
                          select new { ItemId = g.Key, Qty = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.LineTotal) })
            .ToListAsync(ct);

        var names = await _items.Query().ToDictionaryAsync(i => i.Id, i => i.ItemName, ct);
        return rows.Select(r => new ItemSalesRowDto(r.ItemId, names.GetValueOrDefault(r.ItemId, ""), r.Qty, r.Revenue))
            .OrderByDescending(r => r.Revenue).ToList();
    }
}

public class GetCctvRevenueReportQueryHandler : IRequestHandler<GetCctvRevenueReportQuery, IReadOnlyList<RevenueBucketDto>>
{
    private readonly IRepository<Sale> _sales;
    public GetCctvRevenueReportQueryHandler(IRepository<Sale> sales) => _sales = sales;

    public async Task<IReadOnlyList<RevenueBucketDto>> Handle(GetCctvRevenueReportQuery request, CancellationToken ct)
    {
        var q = _sales.Query();
        if (request.From is { } from) q = q.Where(s => s.SaleDate >= from);
        if (request.To is { } to) q = q.Where(s => s.SaleDate <= to);

        var daily = await q.GroupBy(s => s.SaleDate)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(x => x.TotalAmount) })
            .ToListAsync(ct);

        IEnumerable<RevenueBucketDto> buckets = request.Period.ToLowerInvariant() switch
        {
            "daily"  => daily.Select(d => new RevenueBucketDto(d.Date.ToString("yyyy-MM-dd"), d.Revenue)),
            "yearly" => daily.GroupBy(d => $"{d.Date.Year:D4}").Select(g => new RevenueBucketDto(g.Key, g.Sum(x => x.Revenue))),
            _        => daily.GroupBy(d => $"{d.Date.Year:D4}-{d.Date.Month:D2}").Select(g => new RevenueBucketDto(g.Key, g.Sum(x => x.Revenue)))
        };
        return buckets.OrderBy(b => b.Period).ToList();
    }
}

public class GetServiceReportQueryHandler : IRequestHandler<GetServiceReportQuery, ServiceReportDto>
{
    private readonly IRepository<ServiceComplaint> _complaints;
    private readonly IRepository<Employee> _employees;
    public GetServiceReportQueryHandler(IRepository<ServiceComplaint> complaints, IRepository<Employee> employees)
    {
        _complaints = complaints;
        _employees = employees;
    }

    public async Task<ServiceReportDto> Handle(GetServiceReportQuery request, CancellationToken ct)
    {
        var counts = await _complaints.Query()
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountFor(string s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;

        var perf = await _complaints.Query()
            .Where(c => c.Status == ServiceStatus.Closed && c.AssignedEmployeeId != null)
            .GroupBy(c => c.AssignedEmployeeId!.Value)
            .Select(g => new { EmployeeId = g.Key, Closed = g.Count() })
            .ToListAsync(ct);

        var names = await _employees.Query().ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        var performance = perf
            .Select(p => new EmployeePerformanceRowDto(p.EmployeeId, names.GetValueOrDefault(p.EmployeeId, ""), p.Closed))
            .OrderByDescending(p => p.ClosedComplaints).ToList();

        return new ServiceReportDto(
            CountFor(ServiceStatus.Open), CountFor(ServiceStatus.InProgress), CountFor(ServiceStatus.Closed), performance);
    }
}

public class GetCctvOutstandingQueryHandler : IRequestHandler<GetCctvOutstandingQuery, IReadOnlyList<CctvOutstandingRowDto>>
{
    private readonly IRepository<Sale> _sales;
    private readonly IRepository<Customer> _customers;
    public GetCctvOutstandingQueryHandler(IRepository<Sale> sales, IRepository<Customer> customers)
    {
        _sales = sales;
        _customers = customers;
    }

    public async Task<IReadOnlyList<CctvOutstandingRowDto>> Handle(GetCctvOutstandingQuery request, CancellationToken ct)
    {
        var rows = await _sales.Query()
            .Where(s => s.CustomerId != null && s.TotalAmount > s.PaidAmount)
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Invoices = g.Count(), Balance = g.Sum(x => x.TotalAmount - x.PaidAmount) })
            .ToListAsync(ct);

        var names = await _customers.Query().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return rows.Select(r => new CctvOutstandingRowDto(r.CustomerId, names.GetValueOrDefault(r.CustomerId, ""), r.Invoices, r.Balance))
            .OrderByDescending(r => r.Balance).ToList();
    }
}

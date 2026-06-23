using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Coconut;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Coconut;

public record CoconutPnlDto(
    Guid BatchId, string BatchNumber, Guid ProductId, string? ProductName,
    decimal Purchase, decimal LabourCost, decimal TransportCost, decimal TotalSales)
{
    public decimal TotalCost => Purchase + LabourCost + TransportCost;
    public decimal Profit => TotalSales - TotalCost;
}

public record ProductProfitRowDto(Guid ProductId, string? ProductName, int Batches, decimal TotalSales, decimal TotalCost, decimal Profit);
public record PeriodProfitDto(string Period, decimal Profit);

[HasPermission(Permissions.Coconut.BatchManage)]
public record GetCoconutBatchPnlQuery(Guid BatchId) : IRequest<CoconutPnlDto>;

[HasPermission(Permissions.Coconut.BatchManage)]
public record GetCoconutBatchProfitReportQuery(string? Status) : IRequest<IReadOnlyList<CoconutPnlDto>>;

[HasPermission(Permissions.Coconut.BatchManage)]
public record GetProductProfitReportQuery : IRequest<IReadOnlyList<ProductProfitRowDto>>;

[HasPermission(Permissions.Coconut.BatchManage)]
public record GetCoconutProfitByPeriodQuery(string Period) : IRequest<IReadOnlyList<PeriodProfitDto>>;

internal static class CoconutPnl
{
    public static IQueryable<CoconutPnlDto> Project(
        IQueryable<CoconutBatch> batches, IRepository<Product> products, IRepository<CoconutLabourCharge> labour,
        IRepository<CoconutTransportCharge> transport, IRepository<CoconutBatchSale> sales)
        => batches.Select(b => new CoconutPnlDto(
            b.Id, b.BatchNumber, b.ProductId,
            products.Query().Where(p => p.Id == b.ProductId).Select(p => p.Name).FirstOrDefault(),
            b.PurchaseAmount,
            labour.Query().Where(l => l.BatchId == b.Id).Sum(l => (decimal?)l.Amount) ?? 0m,
            transport.Query().Where(t => t.BatchId == b.Id).Sum(t => (decimal?)t.Amount) ?? 0m,
            sales.Query().Where(s => s.BatchId == b.Id).Sum(s => (decimal?)s.SaleValue) ?? 0m));
}

public class GetCoconutBatchPnlQueryHandler : IRequestHandler<GetCoconutBatchPnlQuery, CoconutPnlDto>
{
    private readonly IRepository<CoconutBatch> _batches;
    private readonly IRepository<Product> _products;
    private readonly IRepository<CoconutLabourCharge> _labour;
    private readonly IRepository<CoconutTransportCharge> _transport;
    private readonly IRepository<CoconutBatchSale> _sales;
    public GetCoconutBatchPnlQueryHandler(IRepository<CoconutBatch> batches, IRepository<Product> products,
        IRepository<CoconutLabourCharge> labour, IRepository<CoconutTransportCharge> transport, IRepository<CoconutBatchSale> sales)
    {
        _batches = batches; _products = products; _labour = labour; _transport = transport; _sales = sales;
    }

    public async Task<CoconutPnlDto> Handle(GetCoconutBatchPnlQuery request, CancellationToken ct)
        => await CoconutPnl.Project(_batches.Query().Where(b => b.Id == request.BatchId), _products, _labour, _transport, _sales)
            .FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Batch not found.");
}

public class GetCoconutBatchProfitReportQueryHandler : IRequestHandler<GetCoconutBatchProfitReportQuery, IReadOnlyList<CoconutPnlDto>>
{
    private readonly IRepository<CoconutBatch> _batches;
    private readonly IRepository<Product> _products;
    private readonly IRepository<CoconutLabourCharge> _labour;
    private readonly IRepository<CoconutTransportCharge> _transport;
    private readonly IRepository<CoconutBatchSale> _sales;
    public GetCoconutBatchProfitReportQueryHandler(IRepository<CoconutBatch> batches, IRepository<Product> products,
        IRepository<CoconutLabourCharge> labour, IRepository<CoconutTransportCharge> transport, IRepository<CoconutBatchSale> sales)
    {
        _batches = batches; _products = products; _labour = labour; _transport = transport; _sales = sales;
    }

    public async Task<IReadOnlyList<CoconutPnlDto>> Handle(GetCoconutBatchProfitReportQuery request, CancellationToken ct)
    {
        var q = _batches.Query();
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(b => b.Status == request.Status);
        return await CoconutPnl.Project(q.OrderByDescending(b => b.PurchaseDate), _products, _labour, _transport, _sales).ToListAsync(ct);
    }
}

public class GetProductProfitReportQueryHandler : IRequestHandler<GetProductProfitReportQuery, IReadOnlyList<ProductProfitRowDto>>
{
    private readonly IRepository<CoconutBatch> _batches;
    private readonly IRepository<Product> _products;
    private readonly IRepository<CoconutLabourCharge> _labour;
    private readonly IRepository<CoconutTransportCharge> _transport;
    private readonly IRepository<CoconutBatchSale> _sales;
    public GetProductProfitReportQueryHandler(IRepository<CoconutBatch> batches, IRepository<Product> products,
        IRepository<CoconutLabourCharge> labour, IRepository<CoconutTransportCharge> transport, IRepository<CoconutBatchSale> sales)
    {
        _batches = batches; _products = products; _labour = labour; _transport = transport; _sales = sales;
    }

    public async Task<IReadOnlyList<ProductProfitRowDto>> Handle(GetProductProfitReportQuery request, CancellationToken ct)
    {
        var perBatch = await CoconutPnl.Project(_batches.Query(), _products, _labour, _transport, _sales).ToListAsync(ct);
        return perBatch
            .GroupBy(b => new { b.ProductId, b.ProductName })
            .Select(g => new ProductProfitRowDto(
                g.Key.ProductId, g.Key.ProductName, g.Count(),
                g.Sum(x => x.TotalSales), g.Sum(x => x.TotalCost), g.Sum(x => x.Profit)))
            .OrderByDescending(r => r.Profit)
            .ToList();
    }
}

public class GetCoconutProfitByPeriodQueryHandler : IRequestHandler<GetCoconutProfitByPeriodQuery, IReadOnlyList<PeriodProfitDto>>
{
    private readonly IRepository<CoconutBatch> _batches;
    private readonly IRepository<CoconutLabourCharge> _labour;
    private readonly IRepository<CoconutTransportCharge> _transport;
    private readonly IRepository<CoconutBatchSale> _sales;
    public GetCoconutProfitByPeriodQueryHandler(IRepository<CoconutBatch> batches,
        IRepository<CoconutLabourCharge> labour, IRepository<CoconutTransportCharge> transport, IRepository<CoconutBatchSale> sales)
    {
        _batches = batches; _labour = labour; _transport = transport; _sales = sales;
    }

    public async Task<IReadOnlyList<PeriodProfitDto>> Handle(GetCoconutProfitByPeriodQuery request, CancellationToken ct)
    {
        // Cash-basis net per day: sales(+) − purchases(−) − labour(−) − transport(−).
        var net = new Dictionary<DateOnly, decimal>();
        void Add(DateOnly d, decimal v) => net[d] = net.GetValueOrDefault(d) + v;

        foreach (var s in await _sales.Query().GroupBy(x => x.SaleDate).Select(g => new { g.Key, V = g.Sum(x => x.SaleValue) }).ToListAsync(ct)) Add(s.Key, s.V);
        foreach (var p in await _batches.Query().GroupBy(x => x.PurchaseDate).Select(g => new { g.Key, V = g.Sum(x => x.PurchaseAmount) }).ToListAsync(ct)) Add(p.Key, -p.V);
        foreach (var l in await _labour.Query().GroupBy(x => x.ChargeDate).Select(g => new { g.Key, V = g.Sum(x => x.Amount) }).ToListAsync(ct)) Add(l.Key, -l.V);
        foreach (var t in await _transport.Query().GroupBy(x => x.ChargeDate).Select(g => new { g.Key, V = g.Sum(x => x.Amount) }).ToListAsync(ct)) Add(t.Key, -t.V);

        IEnumerable<PeriodProfitDto> buckets = request.Period.ToLowerInvariant() switch
        {
            "daily"  => net.Select(kv => new PeriodProfitDto(kv.Key.ToString("yyyy-MM-dd"), kv.Value)),
            "yearly" => net.GroupBy(kv => $"{kv.Key.Year:D4}").Select(g => new PeriodProfitDto(g.Key, g.Sum(x => x.Value))),
            _        => net.GroupBy(kv => $"{kv.Key.Year:D4}-{kv.Key.Month:D2}").Select(g => new PeriodProfitDto(g.Key, g.Sum(x => x.Value)))
        };
        return buckets.OrderBy(b => b.Period).ToList();
    }
}

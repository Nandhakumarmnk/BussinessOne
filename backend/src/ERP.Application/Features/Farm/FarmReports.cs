using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Farm;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Farm;

public record BatchPnlDto(
    Guid BatchId, string BatchNumber, string? BatchName, decimal Purchase, decimal FeedCost,
    decimal MedicalCost, decimal LabourCost, decimal OtherCost, decimal TotalSales)
{
    public decimal TotalCost => Purchase + FeedCost + MedicalCost + LabourCost + OtherCost;
    public decimal Profit => TotalSales - TotalCost;
}

public record FeedConsumptionDto(decimal TotalQuantity, decimal TotalAmount);
public record FarmProfitSummaryDto(
    decimal TotalSales, decimal TotalPurchase, decimal TotalFeed, decimal TotalMedical,
    decimal TotalLabour, decimal TotalOther)
{
    public decimal TotalCost => TotalPurchase + TotalFeed + TotalMedical + TotalLabour + TotalOther;
    public decimal Profit => TotalSales - TotalCost;
}

[HasPermission(Permissions.Farm.BatchManage)]
public record GetBatchPnlQuery(Guid BatchId) : IRequest<BatchPnlDto>;

[HasPermission(Permissions.Farm.BatchManage)]
public record GetBatchProfitReportQuery(string? Status) : IRequest<IReadOnlyList<BatchPnlDto>>;

[HasPermission(Permissions.Farm.BatchManage)]
public record GetFeedConsumptionQuery(Guid? BatchId) : IRequest<FeedConsumptionDto>;

[HasPermission(Permissions.Farm.BatchManage)]
public record GetFarmProfitSummaryQuery : IRequest<FarmProfitSummaryDto>;

// Shared P&L projection (correlated subqueries over the cost tables).
internal static class FarmPnl
{
    public static IQueryable<BatchPnlDto> Project(
        IQueryable<FarmBatch> batches, IRepository<FeedEntry> feeds, IRepository<MedicalRecord> medical,
        IRepository<BatchExpense> expenses, IRepository<BatchSale> sales)
        => batches.Select(b => new BatchPnlDto(
            b.Id, b.BatchNumber, b.BatchName, b.PurchaseAmount,
            feeds.Query().Where(f => f.BatchId == b.Id).Sum(f => (decimal?)f.Amount) ?? 0m,
            medical.Query().Where(m => m.BatchId == b.Id).Sum(m => (decimal?)(m.Amount + m.DoctorCharges)) ?? 0m,
            expenses.Query().Where(e => e.BatchId == b.Id && e.ExpenseKind == "labour").Sum(e => (decimal?)e.Amount) ?? 0m,
            expenses.Query().Where(e => e.BatchId == b.Id && e.ExpenseKind == "other").Sum(e => (decimal?)e.Amount) ?? 0m,
            sales.Query().Where(s => s.BatchId == b.Id).Sum(s => (decimal?)s.SaleAmount) ?? 0m));
}

public class GetBatchPnlQueryHandler : IRequestHandler<GetBatchPnlQuery, BatchPnlDto>
{
    private readonly IRepository<FarmBatch> _batches;
    private readonly IRepository<FeedEntry> _feeds;
    private readonly IRepository<MedicalRecord> _medical;
    private readonly IRepository<BatchExpense> _expenses;
    private readonly IRepository<BatchSale> _sales;
    public GetBatchPnlQueryHandler(IRepository<FarmBatch> batches, IRepository<FeedEntry> feeds,
        IRepository<MedicalRecord> medical, IRepository<BatchExpense> expenses, IRepository<BatchSale> sales)
    {
        _batches = batches; _feeds = feeds; _medical = medical; _expenses = expenses; _sales = sales;
    }

    public async Task<BatchPnlDto> Handle(GetBatchPnlQuery request, CancellationToken ct)
        => await FarmPnl.Project(_batches.Query().Where(b => b.Id == request.BatchId), _feeds, _medical, _expenses, _sales)
            .FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Batch not found.");
}

public class GetBatchProfitReportQueryHandler : IRequestHandler<GetBatchProfitReportQuery, IReadOnlyList<BatchPnlDto>>
{
    private readonly IRepository<FarmBatch> _batches;
    private readonly IRepository<FeedEntry> _feeds;
    private readonly IRepository<MedicalRecord> _medical;
    private readonly IRepository<BatchExpense> _expenses;
    private readonly IRepository<BatchSale> _sales;
    public GetBatchProfitReportQueryHandler(IRepository<FarmBatch> batches, IRepository<FeedEntry> feeds,
        IRepository<MedicalRecord> medical, IRepository<BatchExpense> expenses, IRepository<BatchSale> sales)
    {
        _batches = batches; _feeds = feeds; _medical = medical; _expenses = expenses; _sales = sales;
    }

    public async Task<IReadOnlyList<BatchPnlDto>> Handle(GetBatchProfitReportQuery request, CancellationToken ct)
    {
        var q = _batches.Query();
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(b => b.Status == request.Status);
        var rows = await FarmPnl.Project(q.OrderByDescending(b => b.StartDate), _feeds, _medical, _expenses, _sales).ToListAsync(ct);
        return rows;
    }
}

public class GetFeedConsumptionQueryHandler : IRequestHandler<GetFeedConsumptionQuery, FeedConsumptionDto>
{
    private readonly IRepository<FeedEntry> _feeds;
    public GetFeedConsumptionQueryHandler(IRepository<FeedEntry> feeds) => _feeds = feeds;

    public async Task<FeedConsumptionDto> Handle(GetFeedConsumptionQuery request, CancellationToken ct)
    {
        var q = _feeds.Query();
        if (request.BatchId is { } batchId) q = q.Where(f => f.BatchId == batchId);
        var qty = await q.SumAsync(f => (decimal?)f.Quantity, ct) ?? 0m;
        var amount = await q.SumAsync(f => (decimal?)f.Amount, ct) ?? 0m;
        return new FeedConsumptionDto(qty, amount);
    }
}

public class GetFarmProfitSummaryQueryHandler : IRequestHandler<GetFarmProfitSummaryQuery, FarmProfitSummaryDto>
{
    private readonly IRepository<FarmBatch> _batches;
    private readonly IRepository<FeedEntry> _feeds;
    private readonly IRepository<MedicalRecord> _medical;
    private readonly IRepository<BatchExpense> _expenses;
    private readonly IRepository<BatchSale> _sales;
    public GetFarmProfitSummaryQueryHandler(IRepository<FarmBatch> batches, IRepository<FeedEntry> feeds,
        IRepository<MedicalRecord> medical, IRepository<BatchExpense> expenses, IRepository<BatchSale> sales)
    {
        _batches = batches; _feeds = feeds; _medical = medical; _expenses = expenses; _sales = sales;
    }

    public async Task<FarmProfitSummaryDto> Handle(GetFarmProfitSummaryQuery request, CancellationToken ct)
    {
        var purchase = await _batches.Query().SumAsync(b => (decimal?)b.PurchaseAmount, ct) ?? 0m;
        var feed = await _feeds.Query().SumAsync(f => (decimal?)f.Amount, ct) ?? 0m;
        var medical = await _medical.Query().SumAsync(m => (decimal?)(m.Amount + m.DoctorCharges), ct) ?? 0m;
        var labour = await _expenses.Query().Where(e => e.ExpenseKind == "labour").SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var other = await _expenses.Query().Where(e => e.ExpenseKind == "other").SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var sales = await _sales.Query().SumAsync(s => (decimal?)s.SaleAmount, ct) ?? 0m;
        return new FarmProfitSummaryDto(sales, purchase, feed, medical, labour, other);
    }
}

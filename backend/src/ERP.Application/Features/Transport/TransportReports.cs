using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Customers;
using ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Transport;

public record VehicleProfitRowDto(Guid VehicleId, string VehicleNumber, int LoadCount, decimal TotalAmount, decimal TotalProfit);
public record DriverProfitRowDto(Guid DriverId, string DriverName, int LoadCount, decimal TotalAmount, decimal TotalProfit);
public record ProfitBucketDto(string Period, decimal TotalAmount, decimal TotalProfit);
public record TransportOutstandingRowDto(Guid CustomerId, string CustomerName, int OpenCredits, decimal Balance);

[HasPermission(Permissions.Transport.LoadView)]
public record GetVehicleProfitReportQuery(DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<VehicleProfitRowDto>>;

[HasPermission(Permissions.Transport.LoadView)]
public record GetDriverProfitReportQuery(DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<DriverProfitRowDto>>;

[HasPermission(Permissions.Transport.LoadView)]
public record GetTransportProfitReportQuery(string Period, DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<ProfitBucketDto>>;

[HasPermission(Permissions.Transport.LoadView)]
public record GetTransportOutstandingQuery : IRequest<IReadOnlyList<TransportOutstandingRowDto>>;

public class GetVehicleProfitReportQueryHandler
    : IRequestHandler<GetVehicleProfitReportQuery, IReadOnlyList<VehicleProfitRowDto>>
{
    private readonly IRepository<Load> _loads;
    private readonly IRepository<Vehicle> _vehicles;
    public GetVehicleProfitReportQueryHandler(IRepository<Load> loads, IRepository<Vehicle> vehicles)
    {
        _loads = loads;
        _vehicles = vehicles;
    }

    public async Task<IReadOnlyList<VehicleProfitRowDto>> Handle(GetVehicleProfitReportQuery request, CancellationToken ct)
    {
        var q = Filter(_loads.Query(), request.From, request.To).Where(l => l.VehicleId != null);
        var rows = await q.GroupBy(l => l.VehicleId!.Value)
            .Select(g => new { VehicleId = g.Key, LoadCount = g.Count(), TotalAmount = g.Sum(x => x.LoadAmount), TotalProfit = g.Sum(x => x.Profit) })
            .ToListAsync(ct);

        var names = await _vehicles.Query().ToDictionaryAsync(v => v.Id, v => v.VehicleNumber, ct);
        return rows.Select(r => new VehicleProfitRowDto(
            r.VehicleId, names.GetValueOrDefault(r.VehicleId, ""), r.LoadCount, r.TotalAmount, r.TotalProfit))
            .OrderByDescending(r => r.TotalProfit).ToList();
    }

    internal static IQueryable<Load> Filter(IQueryable<Load> q, DateOnly? from, DateOnly? to)
    {
        if (from is { } f) q = q.Where(l => l.LoadDate >= f);
        if (to is { } t) q = q.Where(l => l.LoadDate <= t);
        return q;
    }
}

public class GetDriverProfitReportQueryHandler
    : IRequestHandler<GetDriverProfitReportQuery, IReadOnlyList<DriverProfitRowDto>>
{
    private readonly IRepository<Load> _loads;
    private readonly IRepository<Driver> _drivers;
    public GetDriverProfitReportQueryHandler(IRepository<Load> loads, IRepository<Driver> drivers)
    {
        _loads = loads;
        _drivers = drivers;
    }

    public async Task<IReadOnlyList<DriverProfitRowDto>> Handle(GetDriverProfitReportQuery request, CancellationToken ct)
    {
        var q = GetVehicleProfitReportQueryHandler.Filter(_loads.Query(), request.From, request.To)
            .Where(l => l.DriverId != null);
        var rows = await q.GroupBy(l => l.DriverId!.Value)
            .Select(g => new { DriverId = g.Key, LoadCount = g.Count(), TotalAmount = g.Sum(x => x.LoadAmount), TotalProfit = g.Sum(x => x.Profit) })
            .ToListAsync(ct);

        var names = await _drivers.Query().ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        return rows.Select(r => new DriverProfitRowDto(
            r.DriverId, names.GetValueOrDefault(r.DriverId, ""), r.LoadCount, r.TotalAmount, r.TotalProfit))
            .OrderByDescending(r => r.TotalProfit).ToList();
    }
}

public class GetTransportProfitReportQueryHandler
    : IRequestHandler<GetTransportProfitReportQuery, IReadOnlyList<ProfitBucketDto>>
{
    private readonly IRepository<Load> _loads;
    public GetTransportProfitReportQueryHandler(IRepository<Load> loads) => _loads = loads;

    public async Task<IReadOnlyList<ProfitBucketDto>> Handle(GetTransportProfitReportQuery request, CancellationToken ct)
    {
        var q = GetVehicleProfitReportQueryHandler.Filter(_loads.Query(), request.From, request.To);
        var daily = await q.GroupBy(l => l.LoadDate)
            .Select(g => new { Date = g.Key, Amount = g.Sum(x => x.LoadAmount), Profit = g.Sum(x => x.Profit) })
            .ToListAsync(ct);

        IEnumerable<ProfitBucketDto> buckets = request.Period.ToLowerInvariant() switch
        {
            "monthly" => daily.GroupBy(d => $"{d.Date.Year:D4}-{d.Date.Month:D2}")
                              .Select(g => new ProfitBucketDto(g.Key, g.Sum(x => x.Amount), g.Sum(x => x.Profit))),
            "yearly"  => daily.GroupBy(d => $"{d.Date.Year:D4}")
                              .Select(g => new ProfitBucketDto(g.Key, g.Sum(x => x.Amount), g.Sum(x => x.Profit))),
            _         => daily.Select(d => new ProfitBucketDto(d.Date.ToString("yyyy-MM-dd"), d.Amount, d.Profit))
        };
        return buckets.OrderBy(b => b.Period).ToList();
    }
}

public class GetTransportOutstandingQueryHandler
    : IRequestHandler<GetTransportOutstandingQuery, IReadOnlyList<TransportOutstandingRowDto>>
{
    private readonly IRepository<LoadCredit> _credits;
    private readonly IRepository<Customer> _customers;
    public GetTransportOutstandingQueryHandler(IRepository<LoadCredit> credits, IRepository<Customer> customers)
    {
        _credits = credits;
        _customers = customers;
    }

    public async Task<IReadOnlyList<TransportOutstandingRowDto>> Handle(GetTransportOutstandingQuery request, CancellationToken ct)
    {
        var rows = await _credits.Query().Where(c => c.BalanceAmount > 0)
            .GroupBy(c => c.CustomerId)
            .Select(g => new { CustomerId = g.Key, OpenCredits = g.Count(), Balance = g.Sum(x => x.BalanceAmount) })
            .ToListAsync(ct);

        var names = await _customers.Query().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return rows.Select(r => new TransportOutstandingRowDto(
            r.CustomerId, names.GetValueOrDefault(r.CustomerId, ""), r.OpenCredits, r.Balance))
            .OrderByDescending(r => r.Balance).ToList();
    }
}

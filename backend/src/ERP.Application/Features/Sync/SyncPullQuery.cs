using System.Linq.Expressions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Cctv;
using ERP.Domain.Coconut;
using ERP.Domain.Common;
using ERP.Domain.Customers;
using ERP.Domain.Expenses;
using ERP.Domain.Farm;
using ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sync;

public record SyncItemDto(Guid Id, string Name, string? Extra);

/// <summary>Picker masters changed since the last cursor, so the mobile app can refresh offline caches.</summary>
public record SyncPullDto(
    DateTime Cursor,
    IReadOnlyList<SyncItemDto> Customers,
    IReadOnlyList<SyncItemDto> Vehicles,
    IReadOnlyList<SyncItemDto> Drivers,
    IReadOnlyList<SyncItemDto> Items,
    IReadOnlyList<SyncItemDto> Feeds,
    IReadOnlyList<SyncItemDto> Products,
    IReadOnlyList<SyncItemDto> ExpenseTypes);

[HasPermission(Permissions.DashboardView)]
public record SyncPullQuery(DateTime? Since) : IRequest<SyncPullDto>;

public class SyncPullQueryHandler : IRequestHandler<SyncPullQuery, SyncPullDto>
{
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IRepository<Driver> _drivers;
    private readonly IRepository<Item> _items;
    private readonly IRepository<Feed> _feeds;
    private readonly IRepository<Product> _products;
    private readonly IRepository<ExpenseType> _expenseTypes;
    private readonly IDateTime _clock;

    public SyncPullQueryHandler(
        IRepository<Customer> customers, IRepository<Vehicle> vehicles, IRepository<Driver> drivers,
        IRepository<Item> items, IRepository<Feed> feeds, IRepository<Product> products,
        IRepository<ExpenseType> expenseTypes, IDateTime clock)
    {
        _customers = customers;
        _vehicles = vehicles;
        _drivers = drivers;
        _items = items;
        _feeds = feeds;
        _products = products;
        _expenseTypes = expenseTypes;
        _clock = clock;
    }

    public async Task<SyncPullDto> Handle(SyncPullQuery request, CancellationToken ct)
    {
        var since = request.Since;
        return new SyncPullDto(
            Cursor: _clock.UtcNow,
            Customers: await Pull(_customers, since, c => new SyncItemDto(c.Id, c.Name, c.Mobile), ct),
            Vehicles: await Pull(_vehicles, since, v => new SyncItemDto(v.Id, v.VehicleNumber, v.VehicleType), ct),
            Drivers: await Pull(_drivers, since, d => new SyncItemDto(d.Id, d.Name, d.Mobile), ct),
            Items: await Pull(_items, since, i => new SyncItemDto(i.Id, i.ItemName, i.ItemCode), ct),
            Feeds: await Pull(_feeds, since, f => new SyncItemDto(f.Id, f.FeedName, f.FeedType), ct),
            Products: await Pull(_products, since, p => new SyncItemDto(p.Id, p.Name, p.Category), ct),
            ExpenseTypes: await Pull(_expenseTypes, since, e => new SyncItemDto(e.Id, e.Name, null), ct));
    }

    private static async Task<IReadOnlyList<SyncItemDto>> Pull<T>(
        IRepository<T> repo, DateTime? since, Expression<Func<T, SyncItemDto>> selector, CancellationToken ct)
        where T : BaseEntity
    {
        var query = repo.Query();
        if (since is { } s) query = query.Where(e => (e.UpdatedAt ?? e.CreatedAt) > s);
        return await query.Select(selector).ToListAsync(ct);
    }
}

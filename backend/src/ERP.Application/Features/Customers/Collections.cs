using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Customers;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Customers;

public record LedgerEntryDto(
    Guid Id, DateOnly EntryDate, string RefType, Guid? RefId, decimal Debit, decimal Credit, decimal RunningBalance);

public record CollectionDto(
    Guid Id, Guid CustomerId, DateOnly CollectionDate, decimal Amount, string Mode, string? Reference);

public record OutstandingRowDto(Guid CustomerId, string CustomerName, decimal Outstanding);

// ---- Ledger ----

[HasPermission(Permissions.Customer.Manage)]
public record GetCustomerLedgerQuery(Guid CustomerId, DateOnly? From, DateOnly? To)
    : IRequest<IReadOnlyList<LedgerEntryDto>>;

public class GetCustomerLedgerQueryHandler : IRequestHandler<GetCustomerLedgerQuery, IReadOnlyList<LedgerEntryDto>>
{
    private readonly IRepository<CustomerLedgerEntry> _ledger;
    public GetCustomerLedgerQueryHandler(IRepository<CustomerLedgerEntry> ledger) => _ledger = ledger;

    public async Task<IReadOnlyList<LedgerEntryDto>> Handle(GetCustomerLedgerQuery request, CancellationToken ct)
    {
        var query = _ledger.Query().Where(l => l.CustomerId == request.CustomerId);
        if (request.From is { } from) query = query.Where(l => l.EntryDate >= from);
        if (request.To is { } to) query = query.Where(l => l.EntryDate <= to);

        return await query.OrderBy(l => l.EntryDate).ThenBy(l => l.CreatedAt)
            .Select(l => new LedgerEntryDto(l.Id, l.EntryDate, l.RefType, l.RefId, l.Debit, l.Credit, l.RunningBalance))
            .ToListAsync(ct);
    }
}

// ---- Outstanding (all customers with a balance) ----

[HasPermission(Permissions.Customer.Manage)]
public record GetOutstandingQuery : IRequest<IReadOnlyList<OutstandingRowDto>>;

public class GetOutstandingQueryHandler : IRequestHandler<GetOutstandingQuery, IReadOnlyList<OutstandingRowDto>>
{
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<CustomerLedgerEntry> _ledger;
    public GetOutstandingQueryHandler(IRepository<Customer> customers, IRepository<CustomerLedgerEntry> ledger)
    {
        _customers = customers;
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<OutstandingRowDto>> Handle(GetOutstandingQuery request, CancellationToken ct)
    {
        var ledger = _ledger;
        var rows = await _customers.Query()
            .Select(c => new OutstandingRowDto(
                c.Id, c.Name,
                ledger.Query().Where(l => l.CustomerId == c.Id).Sum(l => l.Debit - l.Credit)))
            .ToListAsync(ct);

        return rows.Where(r => r.Outstanding != 0)
            .OrderByDescending(r => r.Outstanding)
            .ToList();
    }
}

// ---- Collections ----

[HasPermission(Permissions.Customer.Manage)]
public record GetCollectionsQuery(DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<CollectionDto>>;

[HasPermission(Permissions.Customer.CollectionRecord)]
public record RecordCollectionCommand(Guid CustomerId, DateOnly CollectionDate, decimal Amount, string Mode, string? Reference)
    : IRequest<Result<CollectionDto>>;

public class RecordCollectionCommandValidator : AbstractValidator<RecordCollectionCommand>
{
    private static readonly string[] Modes = { "cash", "upi", "bank", "cheque" };
    public RecordCollectionCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Mode).Must(m => Modes.Contains(m)).WithMessage("Invalid payment mode.");
    }
}

public class GetCollectionsQueryHandler : IRequestHandler<GetCollectionsQuery, IReadOnlyList<CollectionDto>>
{
    private readonly IRepository<Collection> _repo;
    public GetCollectionsQueryHandler(IRepository<Collection> repo) => _repo = repo;

    public async Task<IReadOnlyList<CollectionDto>> Handle(GetCollectionsQuery request, CancellationToken ct)
    {
        var query = _repo.Query();
        if (request.From is { } from) query = query.Where(c => c.CollectionDate >= from);
        if (request.To is { } to) query = query.Where(c => c.CollectionDate <= to);

        return await query.OrderByDescending(c => c.CollectionDate)
            .Select(c => new CollectionDto(c.Id, c.CustomerId, c.CollectionDate, c.Amount, c.Mode, c.Reference))
            .ToListAsync(ct);
    }
}

public class RecordCollectionCommandHandler : IRequestHandler<RecordCollectionCommand, Result<CollectionDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly CustomerLedgerService _ledger;

    public RecordCollectionCommandHandler(ICurrentUser currentUser, IUnitOfWork uow, CustomerLedgerService ledger)
    {
        _currentUser = currentUser;
        _uow = uow;
        _ledger = ledger;
    }

    public async Task<Result<CollectionDto>> Handle(RecordCollectionCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);

        if (await _uow.Repository<Customer>().GetByIdAsync(request.CustomerId, ct) is null)
            throw new NotFoundException("Customer not found.");

        var collection = new Collection
        {
            BusinessId = businessId,
            CustomerId = request.CustomerId,
            CollectionDate = request.CollectionDate,
            Amount = request.Amount,
            Mode = request.Mode,
            Reference = request.Reference
        };
        await _uow.Repository<Collection>().AddAsync(collection, ct);

        // A collection credits the customer ledger (reduces what they owe).
        await _ledger.AppendAsync(businessId, request.CustomerId, request.CollectionDate,
            "collection", collection.Id, 0, request.Amount, ct);

        await _uow.SaveChangesAsync(ct);
        return Result<CollectionDto>.Ok(new CollectionDto(
            collection.Id, collection.CustomerId, collection.CollectionDate, collection.Amount,
            collection.Mode, collection.Reference));
    }
}

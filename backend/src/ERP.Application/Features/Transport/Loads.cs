using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Application.Features.Customers;
using ERP.Domain.Customers;
using ERP.Domain.Transport;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Transport;

public record LoadDto(
    Guid Id, string LoadNumber, string? LoadName, Guid? CustomerId, Guid? VehicleId, Guid? DriverId,
    string? Source, string? Destination, decimal LoadAmount, decimal LoadmanCharges, decimal FuelExpense,
    decimal MaintenanceExpense, decimal DriverCharges, decimal OtherExpense, decimal Profit,
    DateOnly LoadDate, string Status);

[HasPermission(Permissions.Transport.LoadView)]
public record GetLoadsQuery(DateOnly? From, DateOnly? To, Guid? VehicleId, Guid? DriverId)
    : IRequest<IReadOnlyList<LoadDto>>;

[HasPermission(Permissions.Transport.LoadView)]
public record GetLoadQuery(Guid Id) : IRequest<LoadDto>;

[HasPermission(Permissions.Transport.LoadCreate)]
public record CreateLoadCommand(
    string LoadNumber, string? LoadName, Guid? CustomerId, Guid? VehicleId, Guid? DriverId,
    string? Source, string? Destination, DateOnly LoadDate, decimal LoadAmount, decimal LoadmanCharges,
    decimal FuelExpense, decimal MaintenanceExpense, decimal DriverCharges, decimal OtherExpense, string? Status)
    : IRequest<Result<LoadDto>>;

[HasPermission(Permissions.Transport.LoadCreate)]
public record UpdateLoadCommand(
    Guid Id, string? LoadName, Guid? VehicleId, Guid? DriverId, string? Source, string? Destination,
    DateOnly LoadDate, decimal LoadAmount, decimal LoadmanCharges, decimal FuelExpense,
    decimal MaintenanceExpense, decimal DriverCharges, decimal OtherExpense, string Status)
    : IRequest<Result<LoadDto>>;

[HasPermission(Permissions.Transport.LoadCreate)]
public record DeleteLoadCommand(Guid Id) : IRequest<Result>;

public class CreateLoadCommandValidator : AbstractValidator<CreateLoadCommand>
{
    public CreateLoadCommandValidator()
    {
        RuleFor(x => x.LoadNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.LoadAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LoadmanCharges).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FuelExpense).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaintenanceExpense).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DriverCharges).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OtherExpense).GreaterThanOrEqualTo(0);
    }
}

public class UpdateLoadCommandValidator : AbstractValidator<UpdateLoadCommand>
{
    public UpdateLoadCommandValidator()
    {
        RuleFor(x => x.LoadAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).NotEmpty();
    }
}

internal static class LoadMap
{
    public static LoadDto ToDto(Load l) => new(
        l.Id, l.LoadNumber, l.LoadName, l.CustomerId, l.VehicleId, l.DriverId, l.Source, l.Destination,
        l.LoadAmount, l.LoadmanCharges, l.FuelExpense, l.MaintenanceExpense, l.DriverCharges, l.OtherExpense,
        l.Profit, l.LoadDate, l.Status);
}

public class GetLoadsQueryHandler : IRequestHandler<GetLoadsQuery, IReadOnlyList<LoadDto>>
{
    private readonly IRepository<Load> _repo;
    public GetLoadsQueryHandler(IRepository<Load> repo) => _repo = repo;

    public async Task<IReadOnlyList<LoadDto>> Handle(GetLoadsQuery request, CancellationToken ct)
    {
        var q = _repo.Query();
        if (request.From is { } from) q = q.Where(l => l.LoadDate >= from);
        if (request.To is { } to) q = q.Where(l => l.LoadDate <= to);
        if (request.VehicleId is { } v) q = q.Where(l => l.VehicleId == v);
        if (request.DriverId is { } d) q = q.Where(l => l.DriverId == d);

        return await q.OrderByDescending(l => l.LoadDate)
            .Select(l => new LoadDto(l.Id, l.LoadNumber, l.LoadName, l.CustomerId, l.VehicleId, l.DriverId,
                l.Source, l.Destination, l.LoadAmount, l.LoadmanCharges, l.FuelExpense, l.MaintenanceExpense,
                l.DriverCharges, l.OtherExpense, l.Profit, l.LoadDate, l.Status))
            .ToListAsync(ct);
    }
}

public class GetLoadQueryHandler : IRequestHandler<GetLoadQuery, LoadDto>
{
    private readonly IRepository<Load> _repo;
    public GetLoadQueryHandler(IRepository<Load> repo) => _repo = repo;

    public async Task<LoadDto> Handle(GetLoadQuery request, CancellationToken ct)
    {
        var load = await _repo.Query().FirstOrDefaultAsync(l => l.Id == request.Id, ct)
                   ?? throw new NotFoundException("Load not found.");
        return LoadMap.ToDto(load);
    }
}

public class CreateLoadCommandHandler : IRequestHandler<CreateLoadCommand, Result<LoadDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly CustomerLedgerService _ledger;

    public CreateLoadCommandHandler(ICurrentUser currentUser, IUnitOfWork uow, CustomerLedgerService ledger)
    {
        _currentUser = currentUser;
        _uow = uow;
        _ledger = ledger;
    }

    public async Task<Result<LoadDto>> Handle(CreateLoadCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var number = request.LoadNumber.Trim();

        if (await _uow.Repository<Load>().Query().AnyAsync(l => l.LoadNumber == number, ct))
            return Result<LoadDto>.Fail("resource.conflict", "A load with that number already exists.");

        var load = new Load
        {
            BusinessId = businessId,
            LoadNumber = number,
            LoadName = request.LoadName,
            CustomerId = request.CustomerId,
            VehicleId = request.VehicleId,
            DriverId = request.DriverId,
            Source = request.Source,
            Destination = request.Destination,
            LoadDate = request.LoadDate,
            LoadAmount = request.LoadAmount,
            LoadmanCharges = request.LoadmanCharges,
            FuelExpense = request.FuelExpense,
            MaintenanceExpense = request.MaintenanceExpense,
            DriverCharges = request.DriverCharges,
            OtherExpense = request.OtherExpense,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "completed" : request.Status
        };
        load.Recalculate();
        await _uow.Repository<Load>().AddAsync(load, ct);

        // Billing the customer creates a credit and debits their ledger.
        if (request.CustomerId is { } customerId && load.LoadAmount > 0)
        {
            if (await _uow.Repository<Customer>().GetByIdAsync(customerId, ct) is null)
                return Result<LoadDto>.Fail("resource.not_found", "Customer not found.");

            var credit = new LoadCredit
            {
                BusinessId = businessId,
                LoadId = load.Id,
                CustomerId = customerId,
                LoadAmount = load.LoadAmount,
                PaidAmount = 0
            };
            credit.Recalculate();
            await _uow.Repository<LoadCredit>().AddAsync(credit, ct);

            await _ledger.AppendAsync(businessId, customerId, load.LoadDate, "load", load.Id, load.LoadAmount, 0, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<LoadDto>.Ok(LoadMap.ToDto(load));
    }
}

public class UpdateLoadCommandHandler : IRequestHandler<UpdateLoadCommand, Result<LoadDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly CustomerLedgerService _ledger;

    public UpdateLoadCommandHandler(ICurrentUser currentUser, IUnitOfWork uow, CustomerLedgerService ledger)
    {
        _currentUser = currentUser;
        _uow = uow;
        _ledger = ledger;
    }

    public async Task<Result<LoadDto>> Handle(UpdateLoadCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var load = await _uow.Repository<Load>().GetByIdAsync(request.Id, ct);
        if (load is null) throw new NotFoundException("Load not found.");

        var oldAmount = load.LoadAmount;

        load.LoadName = request.LoadName;
        load.VehicleId = request.VehicleId;
        load.DriverId = request.DriverId;
        load.Source = request.Source;
        load.Destination = request.Destination;
        load.LoadDate = request.LoadDate;
        load.LoadAmount = request.LoadAmount;
        load.LoadmanCharges = request.LoadmanCharges;
        load.FuelExpense = request.FuelExpense;
        load.MaintenanceExpense = request.MaintenanceExpense;
        load.DriverCharges = request.DriverCharges;
        load.OtherExpense = request.OtherExpense;
        load.Status = request.Status;
        load.Recalculate();
        _uow.Repository<Load>().Update(load);

        // Keep the customer credit + ledger in sync if the billed amount changed.
        if (load.CustomerId is { } customerId && request.LoadAmount != oldAmount)
        {
            var creditRef = await _uow.Repository<LoadCredit>().Query().FirstOrDefaultAsync(c => c.LoadId == load.Id, ct);
            if (creditRef is not null)
            {
                var credit = (await _uow.Repository<LoadCredit>().GetByIdAsync(creditRef.Id, ct))!;
                credit.LoadAmount = request.LoadAmount;
                credit.Recalculate();
                _uow.Repository<LoadCredit>().Update(credit);

                var delta = request.LoadAmount - oldAmount;   // +ve = bill more (debit), -ve = bill less (credit)
                await _ledger.AppendAsync(businessId, customerId, load.LoadDate, "adjustment", load.Id,
                    delta > 0 ? delta : 0, delta < 0 ? -delta : 0, ct);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return Result<LoadDto>.Ok(LoadMap.ToDto(load));
    }
}

public class DeleteLoadCommandHandler : IRequestHandler<DeleteLoadCommand, Result>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly CustomerLedgerService _ledger;

    public DeleteLoadCommandHandler(ICurrentUser currentUser, IUnitOfWork uow, CustomerLedgerService ledger)
    {
        _currentUser = currentUser;
        _uow = uow;
        _ledger = ledger;
    }

    public async Task<Result> Handle(DeleteLoadCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var load = await _uow.Repository<Load>().GetByIdAsync(request.Id, ct);
        if (load is null) throw new NotFoundException("Load not found.");

        var creditRef = await _uow.Repository<LoadCredit>().Query().FirstOrDefaultAsync(c => c.LoadId == load.Id, ct);
        if (creditRef is not null)
        {
            if (creditRef.PaidAmount > 0)
                return Result.Fail("resource.conflict", "Cannot delete a load with payments. Reverse the collection first.");

            var credit = (await _uow.Repository<LoadCredit>().GetByIdAsync(creditRef.Id, ct))!;
            // Reverse the original debit so the customer's outstanding nets to zero.
            await _ledger.AppendAsync(businessId, credit.CustomerId, load.LoadDate, "adjustment", load.Id,
                0, credit.LoadAmount, ct);
            _uow.Repository<LoadCredit>().Remove(credit);
        }

        _uow.Repository<Load>().Remove(load);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

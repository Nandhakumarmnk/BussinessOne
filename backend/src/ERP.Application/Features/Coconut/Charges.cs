using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Coconut;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Coconut;

// ---- Labour charges ----

public record LabourChargeDto(Guid Id, Guid BatchId, string? LabourName, decimal Amount, DateOnly ChargeDate);

[HasPermission(Permissions.Coconut.ChargeRecord)]
public record GetLabourChargesQuery(Guid BatchId) : IRequest<IReadOnlyList<LabourChargeDto>>;

[HasPermission(Permissions.Coconut.ChargeRecord)]
public record AddLabourChargeCommand(Guid BatchId, string? LabourName, decimal Amount, DateOnly ChargeDate)
    : IRequest<Result<LabourChargeDto>>;

public class AddLabourChargeCommandValidator : AbstractValidator<AddLabourChargeCommand>
{
    public AddLabourChargeCommandValidator() => RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
}

public class GetLabourChargesQueryHandler : IRequestHandler<GetLabourChargesQuery, IReadOnlyList<LabourChargeDto>>
{
    private readonly IRepository<CoconutLabourCharge> _repo;
    public GetLabourChargesQueryHandler(IRepository<CoconutLabourCharge> repo) => _repo = repo;

    public async Task<IReadOnlyList<LabourChargeDto>> Handle(GetLabourChargesQuery request, CancellationToken ct)
        => await _repo.Query().Where(c => c.BatchId == request.BatchId).OrderByDescending(c => c.ChargeDate)
            .Select(c => new LabourChargeDto(c.Id, c.BatchId, c.LabourName, c.Amount, c.ChargeDate)).ToListAsync(ct);
}

public class AddLabourChargeCommandHandler : IRequestHandler<AddLabourChargeCommand, Result<LabourChargeDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public AddLabourChargeCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<LabourChargeDto>> Handle(AddLabourChargeCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        if (await _uow.Repository<CoconutBatch>().GetByIdAsync(request.BatchId, ct) is null)
            throw new NotFoundException("Batch not found.");

        var charge = new CoconutLabourCharge
        {
            BusinessId = businessId,
            BatchId = request.BatchId,
            LabourName = request.LabourName,
            Amount = request.Amount,
            ChargeDate = request.ChargeDate
        };
        await _uow.Repository<CoconutLabourCharge>().AddAsync(charge, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<LabourChargeDto>.Ok(new LabourChargeDto(charge.Id, charge.BatchId, charge.LabourName, charge.Amount, charge.ChargeDate));
    }
}

// ---- Transport charges ----

public record TransportChargeDto(Guid Id, Guid BatchId, string? Vehicle, decimal Amount, DateOnly ChargeDate);

[HasPermission(Permissions.Coconut.ChargeRecord)]
public record GetTransportChargesQuery(Guid BatchId) : IRequest<IReadOnlyList<TransportChargeDto>>;

[HasPermission(Permissions.Coconut.ChargeRecord)]
public record AddTransportChargeCommand(Guid BatchId, string? Vehicle, decimal Amount, DateOnly ChargeDate)
    : IRequest<Result<TransportChargeDto>>;

public class AddTransportChargeCommandValidator : AbstractValidator<AddTransportChargeCommand>
{
    public AddTransportChargeCommandValidator() => RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
}

public class GetTransportChargesQueryHandler : IRequestHandler<GetTransportChargesQuery, IReadOnlyList<TransportChargeDto>>
{
    private readonly IRepository<CoconutTransportCharge> _repo;
    public GetTransportChargesQueryHandler(IRepository<CoconutTransportCharge> repo) => _repo = repo;

    public async Task<IReadOnlyList<TransportChargeDto>> Handle(GetTransportChargesQuery request, CancellationToken ct)
        => await _repo.Query().Where(c => c.BatchId == request.BatchId).OrderByDescending(c => c.ChargeDate)
            .Select(c => new TransportChargeDto(c.Id, c.BatchId, c.Vehicle, c.Amount, c.ChargeDate)).ToListAsync(ct);
}

public class AddTransportChargeCommandHandler : IRequestHandler<AddTransportChargeCommand, Result<TransportChargeDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public AddTransportChargeCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<TransportChargeDto>> Handle(AddTransportChargeCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        if (await _uow.Repository<CoconutBatch>().GetByIdAsync(request.BatchId, ct) is null)
            throw new NotFoundException("Batch not found.");

        var charge = new CoconutTransportCharge
        {
            BusinessId = businessId,
            BatchId = request.BatchId,
            Vehicle = request.Vehicle,
            Amount = request.Amount,
            ChargeDate = request.ChargeDate
        };
        await _uow.Repository<CoconutTransportCharge>().AddAsync(charge, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<TransportChargeDto>.Ok(new TransportChargeDto(charge.Id, charge.BatchId, charge.Vehicle, charge.Amount, charge.ChargeDate));
    }
}

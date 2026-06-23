using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Transport;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Transport;

public record VehicleDto(
    Guid Id, string VehicleNumber, string? VehicleType, string? Model, string? FuelType,
    string? RcDetails, string? InsuranceDetails, DateOnly? InsuranceExpiry, bool IsActive);

[HasPermission(Permissions.Transport.VehicleManage)]
public record GetVehiclesQuery : IRequest<IReadOnlyList<VehicleDto>>;

[HasPermission(Permissions.Transport.VehicleManage)]
public record CreateVehicleCommand(
    string VehicleNumber, string? VehicleType, string? Model, string? FuelType,
    string? RcDetails, string? InsuranceDetails, DateOnly? InsuranceExpiry) : IRequest<Result<VehicleDto>>;

[HasPermission(Permissions.Transport.VehicleManage)]
public record UpdateVehicleCommand(
    Guid Id, string VehicleNumber, string? VehicleType, string? Model, string? FuelType,
    string? RcDetails, string? InsuranceDetails, DateOnly? InsuranceExpiry, bool IsActive) : IRequest<Result<VehicleDto>>;

[HasPermission(Permissions.Transport.VehicleManage)]
public record DeleteVehicleCommand(Guid Id) : IRequest<Result>;

public class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator() => RuleFor(x => x.VehicleNumber).NotEmpty().MaximumLength(20);
}

public class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator() => RuleFor(x => x.VehicleNumber).NotEmpty().MaximumLength(20);
}

internal static class VehicleMap
{
    public static VehicleDto ToDto(Vehicle v) => new(
        v.Id, v.VehicleNumber, v.VehicleType, v.Model, v.FuelType, v.RcDetails, v.InsuranceDetails,
        v.InsuranceExpiry, v.IsActive);
}

public class GetVehiclesQueryHandler : IRequestHandler<GetVehiclesQuery, IReadOnlyList<VehicleDto>>
{
    private readonly IRepository<Vehicle> _repo;
    public GetVehiclesQueryHandler(IRepository<Vehicle> repo) => _repo = repo;

    public async Task<IReadOnlyList<VehicleDto>> Handle(GetVehiclesQuery request, CancellationToken ct)
        => await _repo.Query().OrderBy(v => v.VehicleNumber)
            .Select(v => new VehicleDto(v.Id, v.VehicleNumber, v.VehicleType, v.Model, v.FuelType,
                v.RcDetails, v.InsuranceDetails, v.InsuranceExpiry, v.IsActive))
            .ToListAsync(ct);
}

public class CreateVehicleCommandHandler : IRequestHandler<CreateVehicleCommand, Result<VehicleDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreateVehicleCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<VehicleDto>> Handle(CreateVehicleCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var number = request.VehicleNumber.Trim();

        if (await _uow.Repository<Vehicle>().Query().AnyAsync(v => v.VehicleNumber == number, ct))
            return Result<VehicleDto>.Fail("resource.conflict", "A vehicle with that number already exists.");

        var vehicle = new Vehicle
        {
            BusinessId = businessId,
            VehicleNumber = number,
            VehicleType = request.VehicleType,
            Model = request.Model,
            FuelType = request.FuelType,
            RcDetails = request.RcDetails,
            InsuranceDetails = request.InsuranceDetails,
            InsuranceExpiry = request.InsuranceExpiry
        };
        await _uow.Repository<Vehicle>().AddAsync(vehicle, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<VehicleDto>.Ok(VehicleMap.ToDto(vehicle));
    }
}

public class UpdateVehicleCommandHandler : IRequestHandler<UpdateVehicleCommand, Result<VehicleDto>>
{
    private readonly IUnitOfWork _uow;
    public UpdateVehicleCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<VehicleDto>> Handle(UpdateVehicleCommand request, CancellationToken ct)
    {
        var vehicle = await _uow.Repository<Vehicle>().GetByIdAsync(request.Id, ct);
        if (vehicle is null) throw new NotFoundException("Vehicle not found.");

        vehicle.VehicleNumber = request.VehicleNumber.Trim();
        vehicle.VehicleType = request.VehicleType;
        vehicle.Model = request.Model;
        vehicle.FuelType = request.FuelType;
        vehicle.RcDetails = request.RcDetails;
        vehicle.InsuranceDetails = request.InsuranceDetails;
        vehicle.InsuranceExpiry = request.InsuranceExpiry;
        vehicle.IsActive = request.IsActive;
        _uow.Repository<Vehicle>().Update(vehicle);
        await _uow.SaveChangesAsync(ct);
        return Result<VehicleDto>.Ok(VehicleMap.ToDto(vehicle));
    }
}

public class DeleteVehicleCommandHandler : IRequestHandler<DeleteVehicleCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteVehicleCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteVehicleCommand request, CancellationToken ct)
    {
        var vehicle = await _uow.Repository<Vehicle>().GetByIdAsync(request.Id, ct);
        if (vehicle is null) throw new NotFoundException("Vehicle not found.");
        _uow.Repository<Vehicle>().Remove(vehicle);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

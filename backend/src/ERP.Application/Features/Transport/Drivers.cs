using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Transport;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Transport;

public record DriverDto(Guid Id, string Name, string? Mobile, string DriverType, decimal Salary, bool IsActive);

[HasPermission(Permissions.Transport.DriverManage)]
public record GetDriversQuery : IRequest<IReadOnlyList<DriverDto>>;

[HasPermission(Permissions.Transport.DriverManage)]
public record CreateDriverCommand(string Name, string? Mobile, string DriverType, decimal Salary)
    : IRequest<Result<DriverDto>>;

[HasPermission(Permissions.Transport.DriverManage)]
public record UpdateDriverCommand(Guid Id, string Name, string? Mobile, string DriverType, decimal Salary, bool IsActive)
    : IRequest<Result<DriverDto>>;

[HasPermission(Permissions.Transport.DriverManage)]
public record DeleteDriverCommand(Guid Id) : IRequest<Result>;

public class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    private static readonly string[] Types = { "self", "salaried" };
    public CreateDriverCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DriverType).Must(t => Types.Contains(t)).WithMessage("Driver type must be 'self' or 'salaried'.");
        RuleFor(x => x.Salary).GreaterThanOrEqualTo(0);
    }
}

public class UpdateDriverCommandValidator : AbstractValidator<UpdateDriverCommand>
{
    private static readonly string[] Types = { "self", "salaried" };
    public UpdateDriverCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DriverType).Must(t => Types.Contains(t)).WithMessage("Driver type must be 'self' or 'salaried'.");
        RuleFor(x => x.Salary).GreaterThanOrEqualTo(0);
    }
}

public class GetDriversQueryHandler : IRequestHandler<GetDriversQuery, IReadOnlyList<DriverDto>>
{
    private readonly IRepository<Driver> _repo;
    public GetDriversQueryHandler(IRepository<Driver> repo) => _repo = repo;

    public async Task<IReadOnlyList<DriverDto>> Handle(GetDriversQuery request, CancellationToken ct)
        => await _repo.Query().OrderBy(d => d.Name)
            .Select(d => new DriverDto(d.Id, d.Name, d.Mobile, d.DriverType, d.Salary, d.IsActive))
            .ToListAsync(ct);
}

public class CreateDriverCommandHandler : IRequestHandler<CreateDriverCommand, Result<DriverDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreateDriverCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<DriverDto>> Handle(CreateDriverCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var driver = new Driver
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Mobile = request.Mobile,
            DriverType = request.DriverType,
            Salary = request.Salary
        };
        await _uow.Repository<Driver>().AddAsync(driver, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<DriverDto>.Ok(new DriverDto(driver.Id, driver.Name, driver.Mobile, driver.DriverType, driver.Salary, driver.IsActive));
    }
}

public class UpdateDriverCommandHandler : IRequestHandler<UpdateDriverCommand, Result<DriverDto>>
{
    private readonly IUnitOfWork _uow;
    public UpdateDriverCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<DriverDto>> Handle(UpdateDriverCommand request, CancellationToken ct)
    {
        var driver = await _uow.Repository<Driver>().GetByIdAsync(request.Id, ct);
        if (driver is null) throw new NotFoundException("Driver not found.");

        driver.Name = request.Name.Trim();
        driver.Mobile = request.Mobile;
        driver.DriverType = request.DriverType;
        driver.Salary = request.Salary;
        driver.IsActive = request.IsActive;
        _uow.Repository<Driver>().Update(driver);
        await _uow.SaveChangesAsync(ct);
        return Result<DriverDto>.Ok(new DriverDto(driver.Id, driver.Name, driver.Mobile, driver.DriverType, driver.Salary, driver.IsActive));
    }
}

public class DeleteDriverCommandHandler : IRequestHandler<DeleteDriverCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteDriverCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteDriverCommand request, CancellationToken ct)
    {
        var driver = await _uow.Repository<Driver>().GetByIdAsync(request.Id, ct);
        if (driver is null) throw new NotFoundException("Driver not found.");
        _uow.Repository<Driver>().Remove(driver);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

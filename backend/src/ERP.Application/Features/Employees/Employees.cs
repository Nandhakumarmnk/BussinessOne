using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Employees;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Employees;

public record EmployeeDto(
    Guid Id, string Name, string? Mobile, string? Address, DateOnly? JoiningDate,
    decimal Salary, string Status);

// ---- Queries (reads are auto-scoped to the active business by the global query filter) ----

[HasPermission(Permissions.Employee.Manage)]
public record GetEmployeesQuery : IRequest<IReadOnlyList<EmployeeDto>>;

[HasPermission(Permissions.Employee.Manage)]
public record GetEmployeeQuery(Guid Id) : IRequest<EmployeeDto>;

public class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, IReadOnlyList<EmployeeDto>>
{
    private readonly IRepository<Employee> _repo;
    public GetEmployeesQueryHandler(IRepository<Employee> repo) => _repo = repo;

    public async Task<IReadOnlyList<EmployeeDto>> Handle(GetEmployeesQuery request, CancellationToken ct)
        => await _repo.Query().OrderBy(e => e.Name)
            .Select(e => new EmployeeDto(e.Id, e.Name, e.Mobile, e.Address, e.JoiningDate, e.Salary, e.Status))
            .ToListAsync(ct);
}

public class GetEmployeeQueryHandler : IRequestHandler<GetEmployeeQuery, EmployeeDto>
{
    private readonly IRepository<Employee> _repo;
    public GetEmployeeQueryHandler(IRepository<Employee> repo) => _repo = repo;

    public async Task<EmployeeDto> Handle(GetEmployeeQuery request, CancellationToken ct)
        => await _repo.Query().Where(e => e.Id == request.Id)
            .Select(e => new EmployeeDto(e.Id, e.Name, e.Mobile, e.Address, e.JoiningDate, e.Salary, e.Status))
            .FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Employee not found.");
}

// ---- Commands ----

[HasPermission(Permissions.Employee.Manage)]
public record CreateEmployeeCommand(
    string Name, string? Mobile, string? Address, DateOnly? JoiningDate, decimal Salary, string? Status)
    : IRequest<Result<EmployeeDto>>;

[HasPermission(Permissions.Employee.Manage)]
public record UpdateEmployeeCommand(
    Guid Id, string Name, string? Mobile, string? Address, DateOnly? JoiningDate, decimal Salary, string Status)
    : IRequest<Result<EmployeeDto>>;

[HasPermission(Permissions.Employee.Manage)]
public record DeleteEmployeeCommand(Guid Id) : IRequest<Result>;

public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Salary).GreaterThanOrEqualTo(0);
    }
}

public class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Salary).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).NotEmpty();
    }
}

public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Result<EmployeeDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreateEmployeeCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<EmployeeDto>> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var employee = new Employee
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Mobile = request.Mobile,
            Address = request.Address,
            JoiningDate = request.JoiningDate,
            Salary = request.Salary,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "active" : request.Status
        };
        await _uow.Repository<Employee>().AddAsync(employee, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<EmployeeDto>.Ok(new EmployeeDto(
            employee.Id, employee.Name, employee.Mobile, employee.Address, employee.JoiningDate,
            employee.Salary, employee.Status));
    }
}

public class UpdateEmployeeCommandHandler : IRequestHandler<UpdateEmployeeCommand, Result<EmployeeDto>>
{
    private readonly IUnitOfWork _uow;
    public UpdateEmployeeCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<EmployeeDto>> Handle(UpdateEmployeeCommand request, CancellationToken ct)
    {
        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.Id, ct);
        if (employee is null) throw new NotFoundException("Employee not found.");

        employee.Name = request.Name.Trim();
        employee.Mobile = request.Mobile;
        employee.Address = request.Address;
        employee.JoiningDate = request.JoiningDate;
        employee.Salary = request.Salary;
        employee.Status = request.Status;
        _uow.Repository<Employee>().Update(employee);
        await _uow.SaveChangesAsync(ct);

        return Result<EmployeeDto>.Ok(new EmployeeDto(
            employee.Id, employee.Name, employee.Mobile, employee.Address, employee.JoiningDate,
            employee.Salary, employee.Status));
    }
}

public class DeleteEmployeeCommandHandler : IRequestHandler<DeleteEmployeeCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteEmployeeCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteEmployeeCommand request, CancellationToken ct)
    {
        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.Id, ct);
        if (employee is null) throw new NotFoundException("Employee not found.");
        _uow.Repository<Employee>().Remove(employee);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

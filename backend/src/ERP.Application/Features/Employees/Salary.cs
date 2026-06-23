using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Employees;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Employees;

public record SalaryRecordDto(
    Guid Id, Guid EmployeeId, DateOnly PeriodMonth, decimal Amount, decimal PaidAmount, DateOnly? PaidOn, string? Note)
{
    public decimal Balance => Amount - PaidAmount;
}

public record MonthlySalaryRowDto(Guid EmployeeId, string EmployeeName, decimal Amount, decimal PaidAmount)
{
    public decimal Balance => Amount - PaidAmount;
}

[HasPermission(Permissions.Employee.Manage)]
public record RecordSalaryCommand(
    Guid EmployeeId, DateOnly PeriodMonth, decimal Amount, decimal PaidAmount, DateOnly? PaidOn, string? Note)
    : IRequest<Result<SalaryRecordDto>>;

[HasPermission(Permissions.Employee.Manage)]
public record GetSalaryHistoryQuery(Guid EmployeeId) : IRequest<IReadOnlyList<SalaryRecordDto>>;

[HasPermission(Permissions.Employee.Manage)]
public record GetMonthlySalaryReportQuery(int Year, int Month) : IRequest<IReadOnlyList<MonthlySalaryRowDto>>;

public class RecordSalaryCommandValidator : AbstractValidator<RecordSalaryCommand>
{
    public RecordSalaryCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidAmount).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.Amount)
            .WithMessage("Paid amount cannot exceed the salary amount.");
    }
}

public class RecordSalaryCommandHandler : IRequestHandler<RecordSalaryCommand, Result<SalaryRecordDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public RecordSalaryCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<SalaryRecordDto>> Handle(RecordSalaryCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);

        // Employee must exist in the active business (query filter enforces the scope).
        if (await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct) is null)
            throw new NotFoundException("Employee not found.");

        var period = new DateOnly(request.PeriodMonth.Year, request.PeriodMonth.Month, 1);

        var record = await _uow.Repository<SalaryHistory>().Query()
            .FirstOrDefaultAsync(s => s.EmployeeId == request.EmployeeId && s.PeriodMonth == period, ct);

        if (record is null)
        {
            record = new SalaryHistory
            {
                BusinessId = businessId,
                EmployeeId = request.EmployeeId,
                PeriodMonth = period,
                Amount = request.Amount,
                PaidAmount = request.PaidAmount,
                PaidOn = request.PaidOn,
                Note = request.Note
            };
            await _uow.Repository<SalaryHistory>().AddAsync(record, ct);
        }
        else
        {
            // Re-fetch tracked for update (Query() is no-tracking).
            var tracked = await _uow.Repository<SalaryHistory>().GetByIdAsync(record.Id, ct);
            tracked!.Amount = request.Amount;
            tracked.PaidAmount = request.PaidAmount;
            tracked.PaidOn = request.PaidOn;
            tracked.Note = request.Note;
            _uow.Repository<SalaryHistory>().Update(tracked);
            record = tracked;
        }

        await _uow.SaveChangesAsync(ct);
        return Result<SalaryRecordDto>.Ok(new SalaryRecordDto(
            record.Id, record.EmployeeId, record.PeriodMonth, record.Amount, record.PaidAmount, record.PaidOn, record.Note));
    }
}

public class GetSalaryHistoryQueryHandler : IRequestHandler<GetSalaryHistoryQuery, IReadOnlyList<SalaryRecordDto>>
{
    private readonly IRepository<SalaryHistory> _repo;
    public GetSalaryHistoryQueryHandler(IRepository<SalaryHistory> repo) => _repo = repo;

    public async Task<IReadOnlyList<SalaryRecordDto>> Handle(GetSalaryHistoryQuery request, CancellationToken ct)
        => await _repo.Query().Where(s => s.EmployeeId == request.EmployeeId)
            .OrderByDescending(s => s.PeriodMonth)
            .Select(s => new SalaryRecordDto(s.Id, s.EmployeeId, s.PeriodMonth, s.Amount, s.PaidAmount, s.PaidOn, s.Note))
            .ToListAsync(ct);
}

public class GetMonthlySalaryReportQueryHandler
    : IRequestHandler<GetMonthlySalaryReportQuery, IReadOnlyList<MonthlySalaryRowDto>>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<SalaryHistory> _salary;

    public GetMonthlySalaryReportQueryHandler(IRepository<Employee> employees, IRepository<SalaryHistory> salary)
    {
        _employees = employees;
        _salary = salary;
    }

    public async Task<IReadOnlyList<MonthlySalaryRowDto>> Handle(GetMonthlySalaryReportQuery request, CancellationToken ct)
    {
        var period = new DateOnly(request.Year, request.Month, 1);
        var salary = _salary;

        return await _employees.Query()
            .Where(e => e.Status == "active")
            .OrderBy(e => e.Name)
            .Select(e => new MonthlySalaryRowDto(
                e.Id, e.Name,
                salary.Query().Where(s => s.EmployeeId == e.Id && s.PeriodMonth == period)
                    .Select(s => (decimal?)s.Amount).FirstOrDefault() ?? 0m,
                salary.Query().Where(s => s.EmployeeId == e.Id && s.PeriodMonth == period)
                    .Select(s => (decimal?)s.PaidAmount).FirstOrDefault() ?? 0m))
            .ToListAsync(ct);
    }
}

using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Employees;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Employees;

public record AttendanceDto(Guid Id, Guid EmployeeId, DateOnly AttendanceDate, string Status);

[HasPermission(Permissions.Employee.AttendanceMark)]
public record MarkAttendanceCommand(Guid EmployeeId, DateOnly AttendanceDate, string Status)
    : IRequest<Result<AttendanceDto>>;

[HasPermission(Permissions.Employee.Manage)]
public record GetAttendanceQuery(Guid EmployeeId, int Year, int Month) : IRequest<IReadOnlyList<AttendanceDto>>;

public class MarkAttendanceCommandValidator : AbstractValidator<MarkAttendanceCommand>
{
    private static readonly string[] Valid = { "present", "absent", "half", "leave" };
    public MarkAttendanceCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Status).Must(s => Valid.Contains(s)).WithMessage("Invalid attendance status.");
    }
}

public class MarkAttendanceCommandHandler : IRequestHandler<MarkAttendanceCommand, Result<AttendanceDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public MarkAttendanceCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<AttendanceDto>> Handle(MarkAttendanceCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);

        if (await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct) is null)
            throw new NotFoundException("Employee not found.");

        var existing = await _uow.Repository<Attendance>().Query()
            .FirstOrDefaultAsync(a => a.EmployeeId == request.EmployeeId && a.AttendanceDate == request.AttendanceDate, ct);

        Attendance record;
        if (existing is null)
        {
            record = new Attendance
            {
                BusinessId = businessId,
                EmployeeId = request.EmployeeId,
                AttendanceDate = request.AttendanceDate,
                Status = request.Status
            };
            await _uow.Repository<Attendance>().AddAsync(record, ct);
        }
        else
        {
            record = (await _uow.Repository<Attendance>().GetByIdAsync(existing.Id, ct))!;
            record.Status = request.Status;
            _uow.Repository<Attendance>().Update(record);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<AttendanceDto>.Ok(new AttendanceDto(record.Id, record.EmployeeId, record.AttendanceDate, record.Status));
    }
}

public class GetAttendanceQueryHandler : IRequestHandler<GetAttendanceQuery, IReadOnlyList<AttendanceDto>>
{
    private readonly IRepository<Attendance> _repo;
    public GetAttendanceQueryHandler(IRepository<Attendance> repo) => _repo = repo;

    public async Task<IReadOnlyList<AttendanceDto>> Handle(GetAttendanceQuery request, CancellationToken ct)
    {
        var from = new DateOnly(request.Year, request.Month, 1);
        var to = from.AddMonths(1);
        return await _repo.Query()
            .Where(a => a.EmployeeId == request.EmployeeId && a.AttendanceDate >= from && a.AttendanceDate < to)
            .OrderBy(a => a.AttendanceDate)
            .Select(a => new AttendanceDto(a.Id, a.EmployeeId, a.AttendanceDate, a.Status))
            .ToListAsync(ct);
    }
}

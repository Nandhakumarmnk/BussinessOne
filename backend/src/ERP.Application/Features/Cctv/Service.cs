using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Cctv;
using ERP.Domain.Customers;
using ERP.Domain.Employees;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Cctv;

public record ServiceComplaintDto(
    Guid Id, string ComplaintNumber, Guid CustomerId, string? CustomerName, string? IssueDescription,
    Guid? AssignedEmployeeId, string? AssignedEmployeeName, string Status, DateTime OpenedAt, DateTime? ClosedAt);

[HasPermission(Permissions.Cctv.ServiceManage)]
public record GetServiceComplaintsQuery(string? Status) : IRequest<IReadOnlyList<ServiceComplaintDto>>;

[HasPermission(Permissions.Cctv.ServiceManage)]
public record CreateServiceComplaintCommand(
    string ComplaintNumber, Guid CustomerId, string? IssueDescription, Guid? AssignedEmployeeId)
    : IRequest<Result<ServiceComplaintDto>>;

[HasPermission(Permissions.Cctv.ServiceManage)]
public record UpdateServiceStatusCommand(Guid Id, string Status) : IRequest<Result>;

[HasPermission(Permissions.Cctv.ServiceManage)]
public record AssignServiceComplaintCommand(Guid Id, Guid EmployeeId) : IRequest<Result>;

public class CreateServiceComplaintCommandValidator : AbstractValidator<CreateServiceComplaintCommand>
{
    public CreateServiceComplaintCommandValidator()
    {
        RuleFor(x => x.ComplaintNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}

public class GetServiceComplaintsQueryHandler
    : IRequestHandler<GetServiceComplaintsQuery, IReadOnlyList<ServiceComplaintDto>>
{
    private readonly IRepository<ServiceComplaint> _complaints;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Employee> _employees;
    public GetServiceComplaintsQueryHandler(
        IRepository<ServiceComplaint> complaints, IRepository<Customer> customers, IRepository<Employee> employees)
    {
        _complaints = complaints;
        _customers = customers;
        _employees = employees;
    }

    public async Task<IReadOnlyList<ServiceComplaintDto>> Handle(GetServiceComplaintsQuery request, CancellationToken ct)
    {
        var customers = _customers;
        var employees = _employees;
        var q = _complaints.Query();
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(c => c.Status == request.Status);

        return await q.OrderByDescending(c => c.OpenedAt)
            .Select(c => new ServiceComplaintDto(
                c.Id, c.ComplaintNumber, c.CustomerId,
                customers.Query().Where(cu => cu.Id == c.CustomerId).Select(cu => cu.Name).FirstOrDefault(),
                c.IssueDescription, c.AssignedEmployeeId,
                employees.Query().Where(e => e.Id == c.AssignedEmployeeId).Select(e => e.Name).FirstOrDefault(),
                c.Status, c.OpenedAt, c.ClosedAt))
            .ToListAsync(ct);
    }
}

public class CreateServiceComplaintCommandHandler
    : IRequestHandler<CreateServiceComplaintCommand, Result<ServiceComplaintDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly IDateTime _clock;
    public CreateServiceComplaintCommandHandler(ICurrentUser currentUser, IUnitOfWork uow, IDateTime clock)
    {
        _currentUser = currentUser;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<ServiceComplaintDto>> Handle(CreateServiceComplaintCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var number = request.ComplaintNumber.Trim();
        if (await _uow.Repository<ServiceComplaint>().Query().AnyAsync(c => c.ComplaintNumber == number, ct))
            return Result<ServiceComplaintDto>.Fail("resource.conflict", "A complaint with that number already exists.");

        if (await _uow.Repository<Customer>().GetByIdAsync(request.CustomerId, ct) is null)
            return Result<ServiceComplaintDto>.Fail("resource.not_found", "Customer not found.");

        if (request.AssignedEmployeeId is { } empId &&
            await _uow.Repository<Employee>().GetByIdAsync(empId, ct) is null)
            return Result<ServiceComplaintDto>.Fail("resource.not_found", "Assigned employee not found.");

        var complaint = new ServiceComplaint
        {
            BusinessId = businessId,
            ComplaintNumber = number,
            CustomerId = request.CustomerId,
            IssueDescription = request.IssueDescription,
            AssignedEmployeeId = request.AssignedEmployeeId,
            OpenedAt = _clock.UtcNow
        };
        await _uow.Repository<ServiceComplaint>().AddAsync(complaint, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ServiceComplaintDto>.Ok(new ServiceComplaintDto(
            complaint.Id, complaint.ComplaintNumber, complaint.CustomerId, null, complaint.IssueDescription,
            complaint.AssignedEmployeeId, null, complaint.Status, complaint.OpenedAt, complaint.ClosedAt));
    }
}

public class UpdateServiceStatusCommandHandler : IRequestHandler<UpdateServiceStatusCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IDateTime _clock;
    public UpdateServiceStatusCommandHandler(IUnitOfWork uow, IDateTime clock)
    {
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result> Handle(UpdateServiceStatusCommand request, CancellationToken ct)
    {
        var complaint = await _uow.Repository<ServiceComplaint>().GetByIdAsync(request.Id, ct);
        if (complaint is null) throw new NotFoundException("Complaint not found.");
        if (!complaint.ChangeStatus(request.Status, _clock.UtcNow))
            return Result.Fail("validation.failed", "Invalid status. Use open, in_progress or closed.");
        _uow.Repository<ServiceComplaint>().Update(complaint);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

public class AssignServiceComplaintCommandHandler : IRequestHandler<AssignServiceComplaintCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public AssignServiceComplaintCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(AssignServiceComplaintCommand request, CancellationToken ct)
    {
        var complaint = await _uow.Repository<ServiceComplaint>().GetByIdAsync(request.Id, ct);
        if (complaint is null) throw new NotFoundException("Complaint not found.");
        if (await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct) is null)
            return Result.Fail("resource.not_found", "Employee not found.");

        complaint.AssignedEmployeeId = request.EmployeeId;
        _uow.Repository<ServiceComplaint>().Update(complaint);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

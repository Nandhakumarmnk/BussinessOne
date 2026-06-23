using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Customers;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Customers;

public record CustomerDto(
    Guid Id, string Name, string? Mobile, string? Address, string? GstNumber,
    decimal CreditLimit, decimal Outstanding);

[HasPermission(Permissions.Customer.Manage)]
public record GetCustomersQuery : IRequest<IReadOnlyList<CustomerDto>>;

[HasPermission(Permissions.Customer.Manage)]
public record GetCustomerQuery(Guid Id) : IRequest<CustomerDto>;

[HasPermission(Permissions.Customer.Manage)]
public record CreateCustomerCommand(
    string Name, string? Mobile, string? Address, string? GstNumber, decimal CreditLimit, decimal OpeningBalance)
    : IRequest<Result<CustomerDto>>;

[HasPermission(Permissions.Customer.Manage)]
public record UpdateCustomerCommand(
    Guid Id, string Name, string? Mobile, string? Address, string? GstNumber, decimal CreditLimit)
    : IRequest<Result<CustomerDto>>;

[HasPermission(Permissions.Customer.Manage)]
public record DeleteCustomerCommand(Guid Id) : IRequest<Result>;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OpeningBalance).GreaterThanOrEqualTo(0);
    }
}

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}

// Outstanding is computed from the ledger via a correlated subquery.
internal static class CustomerProjection
{
    public static IQueryable<CustomerDto> Project(IQueryable<Customer> q, IRepository<CustomerLedgerEntry> ledger)
        => q.OrderBy(c => c.Name).Select(c => new CustomerDto(
            c.Id, c.Name, c.Mobile, c.Address, c.GstNumber, c.CreditLimit,
            ledger.Query().Where(l => l.CustomerId == c.Id).Sum(l => l.Debit - l.Credit)));
}

public class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, IReadOnlyList<CustomerDto>>
{
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<CustomerLedgerEntry> _ledger;
    public GetCustomersQueryHandler(IRepository<Customer> customers, IRepository<CustomerLedgerEntry> ledger)
    {
        _customers = customers;
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken ct)
        => await CustomerProjection.Project(_customers.Query(), _ledger).ToListAsync(ct);
}

public class GetCustomerQueryHandler : IRequestHandler<GetCustomerQuery, CustomerDto>
{
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<CustomerLedgerEntry> _ledger;
    public GetCustomerQueryHandler(IRepository<Customer> customers, IRepository<CustomerLedgerEntry> ledger)
    {
        _customers = customers;
        _ledger = ledger;
    }

    public async Task<CustomerDto> Handle(GetCustomerQuery request, CancellationToken ct)
        => await CustomerProjection.Project(_customers.Query().Where(c => c.Id == request.Id), _ledger)
            .FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Customer not found.");
}

public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly CustomerLedgerService _ledger;
    private readonly IDateTime _clock;

    public CreateCustomerCommandHandler(
        ICurrentUser currentUser, IUnitOfWork uow, CustomerLedgerService ledger, IDateTime clock)
    {
        _currentUser = currentUser;
        _uow = uow;
        _ledger = ledger;
        _clock = clock;
    }

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var customer = new Customer
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Mobile = request.Mobile,
            Address = request.Address,
            GstNumber = request.GstNumber,
            CreditLimit = request.CreditLimit
        };
        await _uow.Repository<Customer>().AddAsync(customer, ct);

        if (request.OpeningBalance > 0)
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow);
            await _ledger.AppendAsync(businessId, customer.Id, today, "opening", null, request.OpeningBalance, 0, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<CustomerDto>.Ok(new CustomerDto(
            customer.Id, customer.Name, customer.Mobile, customer.Address, customer.GstNumber,
            customer.CreditLimit, request.OpeningBalance));
    }
}

public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Result<CustomerDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IRepository<CustomerLedgerEntry> _ledger;
    public UpdateCustomerCommandHandler(IUnitOfWork uow, IRepository<CustomerLedgerEntry> ledger)
    {
        _uow = uow;
        _ledger = ledger;
    }

    public async Task<Result<CustomerDto>> Handle(UpdateCustomerCommand request, CancellationToken ct)
    {
        var customer = await _uow.Repository<Customer>().GetByIdAsync(request.Id, ct);
        if (customer is null) throw new NotFoundException("Customer not found.");

        customer.Name = request.Name.Trim();
        customer.Mobile = request.Mobile;
        customer.Address = request.Address;
        customer.GstNumber = request.GstNumber;
        customer.CreditLimit = request.CreditLimit;
        _uow.Repository<Customer>().Update(customer);
        await _uow.SaveChangesAsync(ct);

        var outstanding = await _ledger.Query().Where(l => l.CustomerId == customer.Id).SumAsync(l => l.Debit - l.Credit, ct);
        return Result<CustomerDto>.Ok(new CustomerDto(
            customer.Id, customer.Name, customer.Mobile, customer.Address, customer.GstNumber,
            customer.CreditLimit, outstanding));
    }
}

public class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteCustomerCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteCustomerCommand request, CancellationToken ct)
    {
        var customer = await _uow.Repository<Customer>().GetByIdAsync(request.Id, ct);
        if (customer is null) throw new NotFoundException("Customer not found.");
        _uow.Repository<Customer>().Remove(customer);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

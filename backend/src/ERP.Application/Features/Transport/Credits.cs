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

public record CreditDto(
    Guid Id, Guid LoadId, string? LoadNumber, Guid CustomerId, string? CustomerName,
    decimal LoadAmount, decimal PaidAmount, decimal BalanceAmount, string Status);

[HasPermission(Permissions.Transport.CreditManage)]
public record GetCreditsQuery(string? Status) : IRequest<IReadOnlyList<CreditDto>>;

[HasPermission(Permissions.Transport.CreditManage)]
public record RecordCreditPaymentCommand(Guid CreditId, decimal Amount, string Mode, DateOnly? PaymentDate)
    : IRequest<Result<CreditDto>>;

public class RecordCreditPaymentCommandValidator : AbstractValidator<RecordCreditPaymentCommand>
{
    private static readonly string[] Modes = { "cash", "upi", "bank", "cheque" };
    public RecordCreditPaymentCommandValidator()
    {
        RuleFor(x => x.CreditId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Mode).Must(m => Modes.Contains(m)).WithMessage("Invalid payment mode.");
    }
}

public class GetCreditsQueryHandler : IRequestHandler<GetCreditsQuery, IReadOnlyList<CreditDto>>
{
    private readonly IRepository<LoadCredit> _credits;
    private readonly IRepository<Load> _loads;
    private readonly IRepository<Customer> _customers;

    public GetCreditsQueryHandler(IRepository<LoadCredit> credits, IRepository<Load> loads, IRepository<Customer> customers)
    {
        _credits = credits;
        _loads = loads;
        _customers = customers;
    }

    public async Task<IReadOnlyList<CreditDto>> Handle(GetCreditsQuery request, CancellationToken ct)
    {
        var loads = _loads;
        var customers = _customers;
        var q = _credits.Query();
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(c => c.Status == request.Status);

        return await q.OrderByDescending(c => c.BalanceAmount)
            .Select(c => new CreditDto(
                c.Id, c.LoadId,
                loads.Query().Where(l => l.Id == c.LoadId).Select(l => l.LoadNumber).FirstOrDefault(),
                c.CustomerId,
                customers.Query().Where(cu => cu.Id == c.CustomerId).Select(cu => cu.Name).FirstOrDefault(),
                c.LoadAmount, c.PaidAmount, c.BalanceAmount, c.Status))
            .ToListAsync(ct);
    }
}

public class RecordCreditPaymentCommandHandler : IRequestHandler<RecordCreditPaymentCommand, Result<CreditDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly CustomerLedgerService _ledger;
    private readonly IDateTime _clock;

    public RecordCreditPaymentCommandHandler(
        ICurrentUser currentUser, IUnitOfWork uow, CustomerLedgerService ledger, IDateTime clock)
    {
        _currentUser = currentUser;
        _uow = uow;
        _ledger = ledger;
        _clock = clock;
    }

    public async Task<Result<CreditDto>> Handle(RecordCreditPaymentCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);

        var credit = await _uow.Repository<LoadCredit>().GetByIdAsync(request.CreditId, ct);
        if (credit is null) throw new NotFoundException("Credit not found.");

        if (!credit.ApplyPayment(request.Amount))
            return Result<CreditDto>.Fail("transport.payment_exceeds_balance", "Payment exceeds the outstanding balance.");

        _uow.Repository<LoadCredit>().Update(credit);

        var date = request.PaymentDate ?? DateOnly.FromDateTime(_clock.UtcNow);

        // A payment credits the customer ledger and is recorded as a collection (income).
        await _ledger.AppendAsync(businessId, credit.CustomerId, date, "collection", credit.Id, 0, request.Amount, ct);
        await _uow.Repository<Collection>().AddAsync(new Collection
        {
            BusinessId = businessId,
            CustomerId = credit.CustomerId,
            CollectionDate = date,
            Amount = request.Amount,
            Mode = request.Mode,
            Reference = $"Load credit {credit.LoadId}"
        }, ct);

        await _uow.SaveChangesAsync(ct);

        var loadNumber = await _uow.Repository<Load>().Query()
            .Where(l => l.Id == credit.LoadId).Select(l => l.LoadNumber).FirstOrDefaultAsync(ct);

        return Result<CreditDto>.Ok(new CreditDto(
            credit.Id, credit.LoadId, loadNumber, credit.CustomerId, null,
            credit.LoadAmount, credit.PaidAmount, credit.BalanceAmount, credit.Status));
    }
}

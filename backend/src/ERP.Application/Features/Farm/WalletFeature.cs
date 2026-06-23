using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Farm;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Farm;

public record WalletDto(decimal Balance);
public record WalletTransactionDto(Guid Id, DateOnly TxnDate, string Direction, decimal Amount, string? Reason);

[HasPermission(Permissions.Farm.WalletManage)]
public record GetWalletQuery : IRequest<WalletDto>;

[HasPermission(Permissions.Farm.WalletManage)]
public record GetWalletTransactionsQuery : IRequest<IReadOnlyList<WalletTransactionDto>>;

[HasPermission(Permissions.Farm.WalletManage)]
public record AddWalletMoneyCommand(decimal Amount, string? Reason, DateOnly? Date) : IRequest<Result<WalletDto>>;

[HasPermission(Permissions.Farm.WalletManage)]
public record UseWalletMoneyCommand(decimal Amount, string? Reason, DateOnly? Date) : IRequest<Result<WalletDto>>;

public class AddWalletMoneyCommandValidator : AbstractValidator<AddWalletMoneyCommand>
{
    public AddWalletMoneyCommandValidator() => RuleFor(x => x.Amount).GreaterThan(0);
}

public class UseWalletMoneyCommandValidator : AbstractValidator<UseWalletMoneyCommand>
{
    public UseWalletMoneyCommandValidator() => RuleFor(x => x.Amount).GreaterThan(0);
}

public class GetWalletQueryHandler : IRequestHandler<GetWalletQuery, WalletDto>
{
    private readonly IRepository<Wallet> _wallets;
    public GetWalletQueryHandler(IRepository<Wallet> wallets) => _wallets = wallets;

    public async Task<WalletDto> Handle(GetWalletQuery request, CancellationToken ct)
    {
        var balance = await _wallets.Query().Select(w => (decimal?)w.Balance).FirstOrDefaultAsync(ct);
        return new WalletDto(balance ?? 0m);
    }
}

public class GetWalletTransactionsQueryHandler : IRequestHandler<GetWalletTransactionsQuery, IReadOnlyList<WalletTransactionDto>>
{
    private readonly IRepository<WalletTransaction> _txns;
    public GetWalletTransactionsQueryHandler(IRepository<WalletTransaction> txns) => _txns = txns;

    public async Task<IReadOnlyList<WalletTransactionDto>> Handle(GetWalletTransactionsQuery request, CancellationToken ct)
        => await _txns.Query().OrderByDescending(t => t.TxnDate).ThenByDescending(t => t.CreatedAt)
            .Select(t => new WalletTransactionDto(t.Id, t.TxnDate, t.Direction, t.Amount, t.Reason))
            .ToListAsync(ct);
}

/// <summary>Shared get-or-create + transaction logic for the per-business wallet.</summary>
internal static class WalletOps
{
    public static async Task<Wallet> GetOrCreateAsync(IUnitOfWork uow, Guid businessId, CancellationToken ct)
    {
        var existing = await uow.Repository<Wallet>().Query().Select(w => w.Id).FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty)
            return (await uow.Repository<Wallet>().GetByIdAsync(existing, ct))!;

        var wallet = new Wallet { BusinessId = businessId };
        await uow.Repository<Wallet>().AddAsync(wallet, ct);
        return wallet;
    }
}

public class AddWalletMoneyCommandHandler : IRequestHandler<AddWalletMoneyCommand, Result<WalletDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly IDateTime _clock;
    public AddWalletMoneyCommandHandler(ICurrentUser currentUser, IUnitOfWork uow, IDateTime clock)
    {
        _currentUser = currentUser;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<WalletDto>> Handle(AddWalletMoneyCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var wallet = await WalletOps.GetOrCreateAsync(_uow, businessId, ct);
        wallet.Credit(request.Amount);
        _uow.Repository<Wallet>().Update(wallet);

        await _uow.Repository<WalletTransaction>().AddAsync(new WalletTransaction
        {
            BusinessId = businessId,
            WalletId = wallet.Id,
            TxnDate = request.Date ?? DateOnly.FromDateTime(_clock.UtcNow),
            Direction = "credit",
            Amount = request.Amount,
            Reason = request.Reason
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return Result<WalletDto>.Ok(new WalletDto(wallet.Balance));
    }
}

public class UseWalletMoneyCommandHandler : IRequestHandler<UseWalletMoneyCommand, Result<WalletDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly IDateTime _clock;
    public UseWalletMoneyCommandHandler(ICurrentUser currentUser, IUnitOfWork uow, IDateTime clock)
    {
        _currentUser = currentUser;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<WalletDto>> Handle(UseWalletMoneyCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var wallet = await WalletOps.GetOrCreateAsync(_uow, businessId, ct);

        if (!wallet.Debit(request.Amount))
            return Result<WalletDto>.Fail("farm.wallet_insufficient", "Insufficient wallet balance.");

        _uow.Repository<Wallet>().Update(wallet);
        await _uow.Repository<WalletTransaction>().AddAsync(new WalletTransaction
        {
            BusinessId = businessId,
            WalletId = wallet.Id,
            TxnDate = request.Date ?? DateOnly.FromDateTime(_clock.UtcNow),
            Direction = "debit",
            Amount = request.Amount,
            Reason = request.Reason
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return Result<WalletDto>.Ok(new WalletDto(wallet.Balance));
    }
}

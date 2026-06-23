using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Accounting;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Accounting;

public record AccountDto(Guid Id, string Code, string Name, string Type, bool IsActive);
public record JournalLineViewDto(string AccountCode, string AccountName, decimal Debit, decimal Credit);
public record JournalTxnDto(Guid Id, DateOnly TxnDate, string SourceModule, string? Narration, IReadOnlyList<JournalLineViewDto> Lines);
public record LedgerLineDto(DateOnly Date, string AccountCode, string AccountName, string? Narration, decimal Debit, decimal Credit, decimal Balance);

[HasPermission(Permissions.AccountingView)]
public record GetAccountsQuery : IRequest<IReadOnlyList<AccountDto>>;

[HasPermission(Permissions.AccountingView)]
public record GetJournalQuery(DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<JournalTxnDto>>;

[HasPermission(Permissions.AccountingView)]
public record GetLedgerQuery(Guid? AccountId, DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<LedgerLineDto>>;

public record PostJournalLine(string AccountCode, decimal Debit, decimal Credit);

[HasPermission(Permissions.AccountingView)]
public record PostJournalCommand(DateOnly Date, string? Narration, IReadOnlyList<PostJournalLine> Lines) : IRequest<Result>;

public class PostJournalCommandValidator : AbstractValidator<PostJournalCommand>
{
    public PostJournalCommandValidator()
        => RuleFor(x => x.Lines).NotEmpty().WithMessage("A journal entry needs at least one line.");
}

public class GetAccountsQueryHandler : IRequestHandler<GetAccountsQuery, IReadOnlyList<AccountDto>>
{
    private readonly IRepository<Account> _accounts;
    public GetAccountsQueryHandler(IRepository<Account> accounts) => _accounts = accounts;

    public async Task<IReadOnlyList<AccountDto>> Handle(GetAccountsQuery request, CancellationToken ct)
        => await _accounts.Query().OrderBy(a => a.Code)
            .Select(a => new AccountDto(a.Id, a.Code, a.Name, a.Type, a.IsActive)).ToListAsync(ct);
}

public class GetJournalQueryHandler : IRequestHandler<GetJournalQuery, IReadOnlyList<JournalTxnDto>>
{
    private readonly IRepository<JournalTransaction> _journals;
    private readonly IRepository<LedgerEntry> _ledger;
    private readonly IRepository<Account> _accounts;
    public GetJournalQueryHandler(IRepository<JournalTransaction> journals, IRepository<LedgerEntry> ledger, IRepository<Account> accounts)
    {
        _journals = journals;
        _ledger = ledger;
        _accounts = accounts;
    }

    public async Task<IReadOnlyList<JournalTxnDto>> Handle(GetJournalQuery request, CancellationToken ct)
    {
        var q = _journals.Query();
        if (request.From is { } f) q = q.Where(j => j.TxnDate >= f);
        if (request.To is { } t) q = q.Where(j => j.TxnDate <= t);
        var txns = await q.OrderByDescending(j => j.TxnDate).ThenByDescending(j => j.CreatedAt).ToListAsync(ct);

        var ids = txns.Select(t => t.Id).ToList();
        var lines = await _ledger.Query().Where(l => ids.Contains(l.JournalTransactionId)).ToListAsync(ct);
        var accounts = await _accounts.Query().ToDictionaryAsync(a => a.Id, a => new { a.Code, a.Name }, ct);

        return txns.Select(t => new JournalTxnDto(t.Id, t.TxnDate, t.SourceModule, t.Narration,
            lines.Where(l => l.JournalTransactionId == t.Id)
                 .Select(l => new JournalLineViewDto(
                     accounts.TryGetValue(l.AccountId, out var a) ? a.Code : "",
                     accounts.TryGetValue(l.AccountId, out var a2) ? a2.Name : "",
                     l.Debit, l.Credit)).ToList()))
            .ToList();
    }
}

public class GetLedgerQueryHandler : IRequestHandler<GetLedgerQuery, IReadOnlyList<LedgerLineDto>>
{
    private readonly IRepository<JournalTransaction> _journals;
    private readonly IRepository<LedgerEntry> _ledger;
    private readonly IRepository<Account> _accounts;
    public GetLedgerQueryHandler(IRepository<JournalTransaction> journals, IRepository<LedgerEntry> ledger, IRepository<Account> accounts)
    {
        _journals = journals;
        _ledger = ledger;
        _accounts = accounts;
    }

    public async Task<IReadOnlyList<LedgerLineDto>> Handle(GetLedgerQuery request, CancellationToken ct)
    {
        var entries = _ledger.Query();
        if (request.AccountId is { } accId) entries = entries.Where(l => l.AccountId == accId);

        var rows = await (
            from l in entries
            join j in _journals.Query() on l.JournalTransactionId equals j.Id
            join a in _accounts.Query() on l.AccountId equals a.Id
            where (request.From == null || j.TxnDate >= request.From) && (request.To == null || j.TxnDate <= request.To)
            orderby j.TxnDate, j.CreatedAt
            select new { j.TxnDate, a.Code, a.Name, j.Narration, l.Debit, l.Credit }
        ).ToListAsync(ct);

        var result = new List<LedgerLineDto>(rows.Count);
        decimal balance = 0;
        foreach (var r in rows)
        {
            balance += r.Debit - r.Credit;
            result.Add(new LedgerLineDto(r.TxnDate, r.Code, r.Name, r.Narration, r.Debit, r.Credit, balance));
        }
        return result;
    }
}

public class PostJournalCommandHandler : IRequestHandler<PostJournalCommand, Result>
{
    private readonly ICurrentUser _currentUser;
    private readonly IJournalService _journal;
    private readonly IUnitOfWork _uow;
    public PostJournalCommandHandler(ICurrentUser currentUser, IJournalService journal, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _journal = journal;
        _uow = uow;
    }

    public async Task<Result> Handle(PostJournalCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var lines = request.Lines.Select(l => new JournalLine(l.AccountCode, l.Debit, l.Credit)).ToList();

        // JournalService enforces balance (throws DomainException -> 422 if debits != credits).
        await _journal.PostAsync(businessId, request.Date, "manual", null, request.Narration ?? "Manual entry", lines, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

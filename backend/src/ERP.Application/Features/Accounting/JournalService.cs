using ERP.Application.Common.Interfaces;
using ERP.Domain.Accounting;
using ERP.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Accounting;

public class JournalService : IJournalService
{
    private const decimal Tolerance = 0.005m;

    private static readonly Dictionary<string, (string Name, string Type)> Chart = new()
    {
        [AccountCodes.Cash] = ("Cash", AccountTypes.Asset),
        [AccountCodes.AccountsReceivable] = ("Accounts Receivable", AccountTypes.Asset),
        [AccountCodes.AccountsPayable] = ("Accounts Payable", AccountTypes.Liability),
        [AccountCodes.OwnerEquity] = ("Owner Equity", AccountTypes.Equity),
        [AccountCodes.SalesIncome] = ("Sales Income", AccountTypes.Income),
        [AccountCodes.Expenses] = ("Expenses", AccountTypes.Expense),
    };

    private readonly IUnitOfWork _uow;
    public JournalService(IUnitOfWork uow) => _uow = uow;

    public async Task PostAsync(
        Guid businessId, DateOnly date, string sourceModule, Guid? sourceId, string narration,
        IReadOnlyList<JournalLine> lines, CancellationToken ct = default)
    {
        if (lines is null || lines.Count == 0)
            throw new DomainException("accounting.empty_entry", "A journal entry needs at least two lines.");

        decimal debitTotal = 0, creditTotal = 0;
        foreach (var line in lines)
        {
            if (line.Debit < 0 || line.Credit < 0)
                throw new DomainException("accounting.invalid_amount", "Ledger amounts cannot be negative.");
            if ((line.Debit == 0) == (line.Credit == 0))
                throw new DomainException("accounting.invalid_line", "Each line must have exactly one of debit or credit.");
            debitTotal += line.Debit;
            creditTotal += line.Credit;
        }

        if (Math.Abs(debitTotal - creditTotal) > Tolerance)
            throw new DomainException("accounting.unbalanced",
                $"Journal not balanced: debits {debitTotal} != credits {creditTotal}.");

        var accounts = await ResolveAccountsAsync(businessId, lines, ct);

        var journal = new JournalTransaction
        {
            BusinessId = businessId,
            TxnDate = date,
            SourceModule = sourceModule,
            SourceId = sourceId,
            Narration = narration
        };
        foreach (var line in lines)
        {
            journal.Lines.Add(new LedgerEntry
            {
                BusinessId = businessId,
                AccountId = accounts[line.AccountCode],
                Debit = line.Debit,
                Credit = line.Credit
            });
        }
        await _uow.Repository<JournalTransaction>().AddAsync(journal, ct);
    }

    private async Task<Dictionary<string, Guid>> ResolveAccountsAsync(
        Guid businessId, IReadOnlyList<JournalLine> lines, CancellationToken ct)
    {
        var codes = lines.Select(l => l.AccountCode).Distinct().ToList();
        var map = await _uow.Repository<Account>().Query()
            .Where(a => codes.Contains(a.Code))
            .ToDictionaryAsync(a => a.Code, a => a.Id, ct);

        foreach (var code in codes.Where(c => !map.ContainsKey(c)))
        {
            var (name, type) = Chart.TryGetValue(code, out var def) ? def : (code, AccountTypes.Asset);
            var account = new Account { BusinessId = businessId, Code = code, Name = name, Type = type };
            await _uow.Repository<Account>().AddAsync(account, ct);
            map[code] = account.Id;
        }
        return map;
    }
}

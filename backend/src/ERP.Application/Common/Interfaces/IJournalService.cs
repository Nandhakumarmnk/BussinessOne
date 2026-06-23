namespace ERP.Application.Common.Interfaces;

/// <summary>One side of a journal posting. Exactly one of Debit/Credit must be non-zero.</summary>
public record JournalLine(string AccountCode, decimal Debit, decimal Credit);

/// <summary>
/// Posts a balanced double-entry journal transaction (debits == credits), lazily creating any
/// missing chart-of-accounts accounts. Adds to the unit of work; the caller commits, so the
/// journal is persisted atomically with the originating money event.
/// </summary>
public interface IJournalService
{
    Task PostAsync(
        Guid businessId, DateOnly date, string sourceModule, Guid? sourceId, string narration,
        IReadOnlyList<JournalLine> lines, CancellationToken ct = default);
}

/// <summary>Default chart-of-accounts codes.</summary>
public static class AccountCodes
{
    public const string Cash = "1000";
    public const string AccountsReceivable = "1100";
    public const string AccountsPayable = "2000";
    public const string OwnerEquity = "3000";
    public const string SalesIncome = "4000";
    public const string Expenses = "5000";
}

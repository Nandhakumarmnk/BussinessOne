using ERP.Domain.Common;

namespace ERP.Domain.Accounting;

public static class AccountTypes
{
    public const string Asset = "asset";
    public const string Liability = "liability";
    public const string Income = "income";
    public const string Expense = "expense";
    public const string Equity = "equity";
}

/// <summary>A chart-of-accounts account, scoped per business.</summary>
public class Account : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = AccountTypes.Asset;
    public bool IsActive { get; set; } = true;
}

/// <summary>A balanced journal entry (header). Each financial event posts one of these.</summary>
public class JournalTransaction : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public DateOnly TxnDate { get; set; }
    public string SourceModule { get; set; } = string.Empty;   // expense | collection | manual | ...
    public Guid? SourceId { get; set; }
    public string? Narration { get; set; }

    public List<LedgerEntry> Lines { get; set; } = new();
}

/// <summary>One side of a journal entry. Exactly one of Debit/Credit is non-zero.</summary>
public class LedgerEntry : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid JournalTransactionId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

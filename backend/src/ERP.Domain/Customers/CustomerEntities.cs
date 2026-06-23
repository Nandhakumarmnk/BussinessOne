using ERP.Domain.Common;

namespace ERP.Domain.Customers;

public class Customer : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? Address { get; set; }
    public string? GstNumber { get; set; }
    public decimal CreditLimit { get; set; }
}

/// <summary>
/// Double-entry-style customer ledger. Debit = amount the customer owes us (loads/sales/opening),
/// Credit = amount received (collections). Outstanding = Σdebit − Σcredit.
/// </summary>
public class CustomerLedgerEntry : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid CustomerId { get; set; }
    public DateOnly EntryDate { get; set; }
    public string RefType { get; set; } = "opening";  // opening | load | sale | collection | adjustment
    public Guid? RefId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }

    public Customer? Customer { get; set; }
}

public class Collection : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid CustomerId { get; set; }
    public DateOnly CollectionDate { get; set; }
    public decimal Amount { get; set; }
    public string Mode { get; set; } = "cash";   // cash | upi | bank | cheque
    public string? Reference { get; set; }

    public Customer? Customer { get; set; }
}

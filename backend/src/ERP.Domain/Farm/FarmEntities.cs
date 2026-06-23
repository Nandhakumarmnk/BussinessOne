using ERP.Domain.Common;

namespace ERP.Domain.Farm;

public static class AnimalTypes
{
    public const string Goat = "goat";
    public const string Hen = "hen";
    public const string Cow = "cow";
    public static readonly string[] All = { Goat, Hen, Cow };
}

public class FarmBatch : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string? BatchName { get; set; }
    public string AnimalType { get; set; } = AnimalTypes.Goat;
    public DateOnly StartDate { get; set; }
    public int QuantityPurchased { get; set; }
    public decimal PurchaseAmount { get; set; }
    public string Status { get; set; } = "active";   // active | sold | closed
}

public class Feed : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string FeedName { get; set; } = string.Empty;
    public string? FeedType { get; set; }
    public string Uom { get; set; } = "kg";
    public decimal Rate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FeedEntry : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid BatchId { get; set; }
    public Guid FeedId { get; set; }
    public DateOnly EntryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; private set; }

    public void ComputeAmount() => Amount = Math.Round(Quantity * Rate, 2, MidpointRounding.AwayFromZero);
}

public class MedicalRecord : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid BatchId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal DoctorCharges { get; set; }
    public DateOnly RecordDate { get; set; }

    public decimal Total => Amount + DoctorCharges;
}

public class BatchExpense : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid BatchId { get; set; }
    public string ExpenseKind { get; set; } = "labour";   // labour | other (feed/medical have own tables)
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? Description { get; set; }
}

public class BatchSale : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid BatchId { get; set; }
    public DateOnly SaleDate { get; set; }
    public int SaleQuantity { get; set; }
    public decimal? TotalWeight { get; set; }
    public decimal SaleAmount { get; set; }
    public Guid? CustomerId { get; set; }
}

public class Wallet : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public decimal Balance { get; private set; }

    public void Credit(decimal amount) => Balance += amount;

    /// <summary>Debits the wallet; returns false if it would go negative.</summary>
    public bool Debit(decimal amount)
    {
        if (amount <= 0 || amount > Balance) return false;
        Balance -= amount;
        return true;
    }
}

public class WalletTransaction : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid WalletId { get; set; }
    public DateOnly TxnDate { get; set; }
    public string Direction { get; set; } = "credit";   // credit | debit
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string? RefType { get; set; }
    public Guid? RefId { get; set; }
}

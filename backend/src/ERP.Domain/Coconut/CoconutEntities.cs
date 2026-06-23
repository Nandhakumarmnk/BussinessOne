using ERP.Domain.Common;

namespace ERP.Domain.Coconut;

public class Product : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;     // Coconut, Copra, Coconut Powder, Coconut Oil
    public string? Category { get; set; }
    public string Uom { get; set; } = "kg";
    public bool IsActive { get; set; } = true;
}

public class CoconutBatch : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid ProductId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly PurchaseDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal PurchaseAmount { get; set; }
    public string Status { get; set; } = "active";   // active | sold | closed
}

public class CoconutLabourCharge : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid BatchId { get; set; }
    public string? LabourName { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ChargeDate { get; set; }
}

public class CoconutTransportCharge : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid BatchId { get; set; }
    public string? Vehicle { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ChargeDate { get; set; }
}

public class CoconutBatchSale : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid BatchId { get; set; }
    public DateOnly SaleDate { get; set; }
    public decimal SaleQuantity { get; set; }
    public decimal SaleValue { get; set; }
    public Guid? CustomerId { get; set; }
}

using ERP.Domain.Common;

namespace ERP.Domain.Cctv;

public class Item : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Uom { get; set; } = "nos";
    public string? HsnCode { get; set; }
    public decimal Rate { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Supplier : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? GstNumber { get; set; }
    public string? Address { get; set; }
}

// ---- Purchase Order ----

public static class PoStatus
{
    public const string Draft = "draft";
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Received = "received";
    public const string Cancelled = "cancelled";
}

public class PurchaseOrder : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public DateOnly PoDate { get; set; }
    public decimal TotalAmount { get; private set; }
    public string Status { get; private set; } = PoStatus.Draft;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Note { get; set; }

    public List<PurchaseOrderLine> Lines { get; set; } = new();

    public void RecalculateTotal() => TotalAmount = Lines.Sum(l => l.LineTotal);

    public bool Submit()  { if (Status != PoStatus.Draft) return false; Status = PoStatus.Pending; return true; }
    public bool Approve(Guid userId, DateTime nowUtc)
    {
        if (Status != PoStatus.Pending) return false;
        Status = PoStatus.Approved; ApprovedBy = userId; ApprovedAt = nowUtc; return true;
    }
    public bool Receive() { if (Status != PoStatus.Approved) return false; Status = PoStatus.Received; return true; }
    public bool Cancel()
    {
        if (Status is PoStatus.Received or PoStatus.Cancelled) return false;
        Status = PoStatus.Cancelled; return true;
    }
}

public class PurchaseOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal LineTotal { get; private set; }

    public void ComputeTotal()
        => LineTotal = Math.Round(Quantity * Rate * (1 + TaxPercentage / 100m), 2, MidpointRounding.AwayFromZero);
}

// ---- Sale ----

public class Sale : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public DateOnly SaleDate { get; set; }
    public decimal InstallationCharges { get; set; }
    public decimal LabourCharges { get; set; }
    public decimal SubTotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = "completed";   // draft | completed | cancelled

    public List<SaleLine> Lines { get; set; } = new();

    public decimal Balance => TotalAmount - PaidAmount;

    public void RecalculateTotals()
    {
        SubTotal = Lines.Sum(l => l.BaseAmount);
        TaxAmount = Lines.Sum(l => l.TaxAmount);
        TotalAmount = SubTotal + TaxAmount + InstallationCharges + LabourCharges;
    }
}

public class SaleLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal LineTotal { get; private set; }

    public decimal BaseAmount => Math.Round(Quantity * Rate, 2, MidpointRounding.AwayFromZero);
    public decimal TaxAmount => Math.Round(BaseAmount * TaxPercentage / 100m, 2, MidpointRounding.AwayFromZero);

    public void ComputeTotal() => LineTotal = BaseAmount + TaxAmount;
}

// ---- Service ----

public static class ServiceStatus
{
    public const string Open = "open";
    public const string InProgress = "in_progress";
    public const string Closed = "closed";
}

public class ServiceComplaint : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string ComplaintNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string? IssueDescription { get; set; }
    public Guid? AssignedEmployeeId { get; set; }
    public string Status { get; private set; } = ServiceStatus.Open;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public bool ChangeStatus(string status, DateTime nowUtc)
    {
        switch (status)
        {
            case ServiceStatus.Open:
            case ServiceStatus.InProgress:
                Status = status; ClosedAt = null; return true;
            case ServiceStatus.Closed:
                Status = ServiceStatus.Closed; ClosedAt = nowUtc; return true;
            default:
                return false;
        }
    }
}

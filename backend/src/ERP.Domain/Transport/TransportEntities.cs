using ERP.Domain.Common;

namespace ERP.Domain.Transport;

public class Vehicle : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string? VehicleType { get; set; }
    public string? Model { get; set; }
    public string? FuelType { get; set; }
    public string? RcDetails { get; set; }
    public string? InsuranceDetails { get; set; }
    public DateOnly? InsuranceExpiry { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Driver : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string DriverType { get; set; } = "salaried";   // self | salaried
    public decimal Salary { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Load : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string LoadNumber { get; set; } = string.Empty;
    public string? LoadName { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public string? Source { get; set; }
    public string? Destination { get; set; }

    public decimal LoadAmount { get; set; }
    public decimal LoadmanCharges { get; set; }
    public decimal FuelExpense { get; set; }
    public decimal MaintenanceExpense { get; set; }
    public decimal DriverCharges { get; set; }
    public decimal OtherExpense { get; set; }

    /// <summary>Persisted, but always derived from the inputs via <see cref="Recalculate"/>.</summary>
    public decimal Profit { get; private set; }

    public DateOnly LoadDate { get; set; }
    public string Status { get; set; } = "completed";   // planned | in_transit | completed | cancelled

    public decimal TotalExpenses =>
        LoadmanCharges + FuelExpense + MaintenanceExpense + DriverCharges + OtherExpense;

    /// <summary>Load Profit = Load Amount − (Loadman + Fuel + Maintenance + Driver + Other).</summary>
    public void Recalculate() => Profit = LoadAmount - TotalExpenses;
}

public class LoadCredit : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid LoadId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal LoadAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; private set; }
    public string Status { get; private set; } = "open";   // open | partial | settled

    public void Recalculate()
    {
        BalanceAmount = LoadAmount - PaidAmount;
        Status = BalanceAmount <= 0 ? "settled" : PaidAmount > 0 ? "partial" : "open";
    }

    /// <summary>Applies a payment; returns false if it would exceed the outstanding balance.</summary>
    public bool ApplyPayment(decimal amount)
    {
        if (amount <= 0 || PaidAmount + amount > LoadAmount) return false;
        PaidAmount += amount;
        Recalculate();
        return true;
    }
}

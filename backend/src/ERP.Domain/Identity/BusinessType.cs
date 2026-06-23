namespace ERP.Domain.Identity;

/// <summary>
/// Platform-level reference: Transport / CCTV / Farm / Coconut.
/// Drives which modules a business exposes. Not tenant-scoped.
/// </summary>
public class BusinessType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;   // TRANSPORT | CCTV | FARM | COCONUT
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Business> Businesses { get; set; } = new List<Business>();
}

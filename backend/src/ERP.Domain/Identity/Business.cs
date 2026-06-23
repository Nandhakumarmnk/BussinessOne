using ERP.Domain.Common;

namespace ERP.Domain.Identity;

/// <summary>
/// An operational unit of a single business type under a tenant.
/// The <c>business_id</c> on transactional tables points here.
/// </summary>
public class Business : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid BusinessTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GstNumber { get; set; }
    public string? Address { get; set; }
    public string Currency { get; set; } = "INR";
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public BusinessType? BusinessType { get; set; }
    public ICollection<UserBusiness> Members { get; set; } = new List<UserBusiness>();
}

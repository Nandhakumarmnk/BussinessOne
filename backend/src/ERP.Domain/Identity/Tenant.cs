using ERP.Domain.Common;

namespace ERP.Domain.Identity;

/// <summary>
/// Top-level account / ownership boundary. One Business Owner ≈ one Tenant.
/// </summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Timezone { get; set; } = "Asia/Kolkata";
    public Guid? OwnerUserId { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Business> Businesses { get; set; } = new List<Business>();
    public ICollection<User> Users { get; set; } = new List<User>();
}

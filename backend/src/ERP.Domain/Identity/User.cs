using ERP.Domain.Common;

namespace ERP.Domain.Identity;

/// <summary>
/// An authenticated principal. Belongs to a tenant (null for platform Super Admin).
/// Rights are granted per-business via <see cref="UserBusiness"/>.
/// </summary>
public class User : BaseEntity
{
    public Guid? TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsSuperAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<UserBusiness> Memberships { get; set; } = new List<UserBusiness>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

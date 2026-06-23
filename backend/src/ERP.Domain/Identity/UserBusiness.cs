using ERP.Domain.Common;

namespace ERP.Domain.Identity;

/// <summary>
/// A user's membership in a business with a specific role.
/// RBAC is granted here (per-business), not globally.
/// </summary>
public class UserBusiness : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid RoleId { get; set; }

    public User? User { get; set; }
    public Business? Business { get; set; }
    public Role? Role { get; set; }
}

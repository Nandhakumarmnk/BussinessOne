namespace ERP.Domain.Identity;

/// <summary>Join row granting a permission to a role (composite key).</summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}

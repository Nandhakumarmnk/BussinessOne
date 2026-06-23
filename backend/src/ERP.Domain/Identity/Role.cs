namespace ERP.Domain.Identity;

/// <summary>System role (Super Admin, Owner, Manager, Employee, Driver, Labour).</summary>
public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

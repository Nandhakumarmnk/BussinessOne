namespace ERP.Domain.Identity;

/// <summary>A granular capability, coded as <c>module.action</c> (e.g. transport.load.create).</summary>
public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

namespace ERP.Application.Common.Security;

/// <summary>
/// Declares the permission a command/query requires. Enforced centrally by the
/// AuthorizationBehavior in the MediatR pipeline (defense-in-depth alongside the UI gating).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class HasPermissionAttribute : Attribute
{
    public string Permission { get; }

    public HasPermissionAttribute(string permission) => Permission = permission;
}

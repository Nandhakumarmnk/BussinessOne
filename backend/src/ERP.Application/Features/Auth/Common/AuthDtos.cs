namespace ERP.Application.Features.Auth.Common;

public record UserSummary(Guid Id, string FullName, string Mobile, string? Email, bool IsSuperAdmin);

/// <summary>A business the user can access, with their role and resolved permissions.</summary>
public record MembershipDto(
    Guid BusinessId,
    string BusinessName,
    string BusinessTypeCode,
    string Role,
    IReadOnlyList<string> Permissions);

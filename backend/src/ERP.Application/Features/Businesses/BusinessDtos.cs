namespace ERP.Application.Features.Businesses;

public record BusinessDto(
    Guid Id,
    string Name,
    string BusinessTypeCode,
    string BusinessTypeName,
    string? GstNumber,
    string? Address,
    bool IsActive,
    string? Role);   // the caller's role in this business (null if none)

public record MemberDto(
    Guid UserId,
    string FullName,
    string Mobile,
    string RoleCode,
    string RoleName);

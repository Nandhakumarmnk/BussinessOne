namespace ERP.Domain.Enums;

/// <summary>Stable codes for the four supported business verticals.</summary>
public static class BusinessTypeCodes
{
    public const string Transport = "TRANSPORT";
    public const string Cctv = "CCTV";
    public const string Farm = "FARM";
    public const string Coconut = "COCONUT";

    public static readonly string[] All = { Transport, Cctv, Farm, Coconut };
}

/// <summary>Stable codes for system roles.</summary>
public static class RoleCodes
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string Owner = "OWNER";
    public const string Manager = "MANAGER";
    public const string Employee = "EMPLOYEE";
    public const string Driver = "DRIVER";
    public const string Labour = "LABOUR";

    public static readonly string[] All = { SuperAdmin, Owner, Manager, Employee, Driver, Labour };
}

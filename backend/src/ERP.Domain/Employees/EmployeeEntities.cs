using ERP.Domain.Common;

namespace ERP.Domain.Employees;

public class Employee : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid? UserId { get; set; }          // optional app login
    public string Name { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? Address { get; set; }
    public DateOnly? JoiningDate { get; set; }
    public decimal Salary { get; set; }
    public Guid? RoleId { get; set; }
    public string Status { get; set; } = "active";   // active | inactive | terminated
}

public class SalaryHistory : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PeriodMonth { get; set; }   // first day of the month
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? Note { get; set; }

    public Employee? Employee { get; set; }
}

public class Attendance : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public string Status { get; set; } = "present";  // present | absent | half | leave

    public Employee? Employee { get; set; }
}

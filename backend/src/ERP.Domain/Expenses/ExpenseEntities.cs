using ERP.Domain.Common;

namespace ERP.Domain.Expenses;

public class ExpenseType : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Expense : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; set; }
    public Guid? ExpenseTypeId { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? AttachmentKey { get; set; }   // Cloud Storage object key

    public ExpenseType? ExpenseType { get; set; }
}

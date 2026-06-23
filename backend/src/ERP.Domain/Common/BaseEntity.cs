namespace ERP.Domain.Common;

/// <summary>
/// Base for entities that carry audit + soft-delete columns.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public void SoftDelete(DateTime nowUtc)
    {
        IsDeleted = true;
        DeletedAt = nowUtc;
    }
}

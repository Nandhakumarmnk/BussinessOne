using ERP.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Entity).IsRequired().HasMaxLength(80);
        b.Property(x => x.Action).IsRequired().HasMaxLength(10);
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.Property(x => x.OldValues).HasColumnType("jsonb");
        b.Property(x => x.NewValues).HasColumnType("jsonb");

        b.HasIndex(x => new { x.Entity, x.EntityId });
        b.HasIndex(x => new { x.BusinessId, x.CreatedAt });
    }
}

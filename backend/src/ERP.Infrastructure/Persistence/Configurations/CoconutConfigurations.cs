using ERP.Domain.Coconut;
using ERP.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.Category).HasMaxLength(60);
        b.Property(x => x.Uom).IsRequired().HasMaxLength(20);
        b.HasIndex(x => new { x.BusinessId, x.Name }).IsUnique();
    }
}

public class CoconutBatchConfiguration : IEntityTypeConfiguration<CoconutBatch>
{
    public void Configure(EntityTypeBuilder<CoconutBatch> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.BatchNumber).IsRequired().HasMaxLength(30);
        b.Property(x => x.Status).IsRequired().HasMaxLength(10);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BusinessId, x.BatchNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.Status });
    }
}

public class CoconutLabourChargeConfiguration : IEntityTypeConfiguration<CoconutLabourCharge>
{
    public void Configure(EntityTypeBuilder<CoconutLabourCharge> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.LabourName).HasMaxLength(120);
        b.HasOne<CoconutBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.BatchId);
    }
}

public class CoconutTransportChargeConfiguration : IEntityTypeConfiguration<CoconutTransportCharge>
{
    public void Configure(EntityTypeBuilder<CoconutTransportCharge> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Vehicle).HasMaxLength(60);
        b.HasOne<CoconutBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.BatchId);
    }
}

public class CoconutBatchSaleConfiguration : IEntityTypeConfiguration<CoconutBatchSale>
{
    public void Configure(EntityTypeBuilder<CoconutBatchSale> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<CoconutBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BatchId, x.SaleDate });
    }
}

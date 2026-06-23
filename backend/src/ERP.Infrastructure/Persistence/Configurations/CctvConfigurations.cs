using ERP.Domain.Cctv;
using ERP.Domain.Customers;
using ERP.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ItemCode).IsRequired().HasMaxLength(40);
        b.Property(x => x.ItemName).IsRequired().HasMaxLength(150);
        b.Property(x => x.Uom).IsRequired().HasMaxLength(20);
        b.Property(x => x.HsnCode).HasMaxLength(20);
        b.Property(x => x.TaxPercentage).HasPrecision(5, 2);
        b.HasIndex(x => new { x.BusinessId, x.ItemCode }).IsUnique();
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Mobile).HasMaxLength(20);
        b.Property(x => x.GstNumber).HasMaxLength(20);
        b.Property(x => x.Address).HasMaxLength(300);
        b.HasIndex(x => x.BusinessId);
    }
}

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.PoNumber).IsRequired().HasMaxLength(30);
        b.Property(x => x.Status).IsRequired().HasMaxLength(12);
        b.Property(x => x.Note).HasMaxLength(300);
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.BusinessId, x.PoNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.Status });
    }
}

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.TaxPercentage).HasPrecision(5, 2);
        b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PurchaseOrderId);
    }
}

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(30);
        b.Property(x => x.Status).IsRequired().HasMaxLength(12);
        b.Ignore(x => x.Balance);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.BusinessId, x.InvoiceNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.SaleDate });
    }
}

public class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.TaxPercentage).HasPrecision(5, 2);
        b.Ignore(x => x.BaseAmount);
        b.Ignore(x => x.TaxAmount);
        b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SaleId);
    }
}

public class ServiceComplaintConfiguration : IEntityTypeConfiguration<ServiceComplaint>
{
    public void Configure(EntityTypeBuilder<ServiceComplaint> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ComplaintNumber).IsRequired().HasMaxLength(30);
        b.Property(x => x.Status).IsRequired().HasMaxLength(12);
        b.Property(x => x.IssueDescription).HasMaxLength(500);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.AssignedEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BusinessId, x.ComplaintNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.Status });
    }
}

using ERP.Domain.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.VehicleNumber).IsRequired().HasMaxLength(20);
        b.Property(x => x.VehicleType).HasMaxLength(40);
        b.Property(x => x.Model).HasMaxLength(80);
        b.Property(x => x.FuelType).HasMaxLength(20);
        b.Property(x => x.RcDetails).HasMaxLength(200);
        b.Property(x => x.InsuranceDetails).HasMaxLength(200);
        b.HasIndex(x => new { x.BusinessId, x.VehicleNumber }).IsUnique();
    }
}

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Mobile).HasMaxLength(20);
        b.Property(x => x.DriverType).IsRequired().HasMaxLength(10);
        b.HasIndex(x => x.BusinessId);
    }
}

public class LoadConfiguration : IEntityTypeConfiguration<Load>
{
    public void Configure(EntityTypeBuilder<Load> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.LoadNumber).IsRequired().HasMaxLength(30);
        b.Property(x => x.LoadName).HasMaxLength(120);
        b.Property(x => x.Source).HasMaxLength(120);
        b.Property(x => x.Destination).HasMaxLength(120);
        b.Property(x => x.Status).IsRequired().HasMaxLength(15);
        b.Ignore(x => x.TotalExpenses);

        b.HasOne<ERP.Domain.Customers.Customer>().WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Driver>().WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.BusinessId, x.LoadNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.LoadDate });
        b.HasIndex(x => x.VehicleId);
        b.HasIndex(x => x.DriverId);
    }
}

public class LoadCreditConfiguration : IEntityTypeConfiguration<LoadCredit>
{
    public void Configure(EntityTypeBuilder<LoadCredit> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).IsRequired().HasMaxLength(10);
        b.HasOne<Load>().WithMany().HasForeignKey(x => x.LoadId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ERP.Domain.Customers.Customer>().WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.LoadId).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.Status });
        b.ToTable(t => t.HasCheckConstraint("ck_load_credits_paid_le_amount", "paid_amount <= load_amount"));
    }
}

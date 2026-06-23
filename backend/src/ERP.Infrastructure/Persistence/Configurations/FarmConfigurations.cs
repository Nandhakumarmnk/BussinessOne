using ERP.Domain.Customers;
using ERP.Domain.Farm;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class FarmBatchConfiguration : IEntityTypeConfiguration<FarmBatch>
{
    public void Configure(EntityTypeBuilder<FarmBatch> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.BatchNumber).IsRequired().HasMaxLength(30);
        b.Property(x => x.BatchName).HasMaxLength(120);
        b.Property(x => x.AnimalType).IsRequired().HasMaxLength(10);
        b.Property(x => x.Status).IsRequired().HasMaxLength(10);
        b.HasIndex(x => new { x.BusinessId, x.BatchNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.Status });
    }
}

public class FeedConfiguration : IEntityTypeConfiguration<Feed>
{
    public void Configure(EntityTypeBuilder<Feed> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.FeedName).IsRequired().HasMaxLength(120);
        b.Property(x => x.FeedType).HasMaxLength(60);
        b.Property(x => x.Uom).IsRequired().HasMaxLength(20);
        b.HasIndex(x => x.BusinessId);
    }
}

public class FeedEntryConfiguration : IEntityTypeConfiguration<FeedEntry>
{
    public void Configure(EntityTypeBuilder<FeedEntry> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<FarmBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Feed>().WithMany().HasForeignKey(x => x.FeedId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BatchId, x.EntryDate });
    }
}

public class MedicalRecordConfiguration : IEntityTypeConfiguration<MedicalRecord>
{
    public void Configure(EntityTypeBuilder<MedicalRecord> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.MedicineName).IsRequired().HasMaxLength(120);
        b.Ignore(x => x.Total);
        b.HasOne<FarmBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BatchId, x.RecordDate });
    }
}

public class BatchExpenseConfiguration : IEntityTypeConfiguration<BatchExpense>
{
    public void Configure(EntityTypeBuilder<BatchExpense> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ExpenseKind).IsRequired().HasMaxLength(10);
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasOne<FarmBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.BatchId);
    }
}

public class BatchSaleConfiguration : IEntityTypeConfiguration<BatchSale>
{
    public void Configure(EntityTypeBuilder<BatchSale> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<FarmBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BatchId, x.SaleDate });
    }
}

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.BusinessId).IsUnique();   // one wallet per business
    }
}

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Direction).IsRequired().HasMaxLength(6);
        b.Property(x => x.Reason).HasMaxLength(200);
        b.Property(x => x.RefType).HasMaxLength(30);
        b.HasOne<Wallet>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.WalletId, x.TxnDate });
    }
}

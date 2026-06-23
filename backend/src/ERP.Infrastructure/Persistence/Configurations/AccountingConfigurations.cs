using ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(20);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.Type).IsRequired().HasMaxLength(12);
        b.HasIndex(x => new { x.BusinessId, x.Code }).IsUnique();
    }
}

public class JournalTransactionConfiguration : IEntityTypeConfiguration<JournalTransaction>
{
    public void Configure(EntityTypeBuilder<JournalTransaction> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.SourceModule).IsRequired().HasMaxLength(30);
        b.Property(x => x.Narration).HasMaxLength(300);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.JournalTransactionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.BusinessId, x.TxnDate });
    }
}

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.BusinessId, x.AccountId });
        b.ToTable(t => t.HasCheckConstraint("ck_ledger_debit_xor_credit", "(debit = 0) <> (credit = 0)"));
    }
}

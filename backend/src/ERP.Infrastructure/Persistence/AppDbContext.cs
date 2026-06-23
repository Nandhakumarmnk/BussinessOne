using System.Linq.Expressions;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Accounting;
using ERP.Domain.Auditing;
using ERP.Domain.Cctv;
using ERP.Domain.Coconut;
using ERP.Domain.Common;
using ERP.Domain.Customers;
using ERP.Domain.Employees;
using ERP.Domain.Expenses;
using ERP.Domain.Farm;
using ERP.Domain.Identity;
using ERP.Domain.Transport;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ITenantContext? _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
        : base(options) => _tenant = tenant;

    /// <summary>Design-time / migration constructor (no request tenant context).</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<BusinessType> BusinessTypes => Set<BusinessType>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserBusiness> UserBusinesses => Set<UserBusiness>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Common modules (Phase 2) — all business-scoped.
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<SalaryHistory> SalaryHistory => Set<SalaryHistory>();
    public DbSet<Attendance> Attendance => Set<Attendance>();
    public DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerLedgerEntry> CustomerLedger => Set<CustomerLedgerEntry>();
    public DbSet<Collection> Collections => Set<Collection>();

    // Business 1 — Goods Transport (Phase 3).
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Load> Loads => Set<Load>();
    public DbSet<LoadCredit> LoadCredits => Set<LoadCredit>();

    // Business 2 — Electronics & CCTV (Phase 4).
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<ServiceComplaint> ServiceComplaints => Set<ServiceComplaint>();

    // Business 3 — Farm Management (Phase 5).
    public DbSet<FarmBatch> FarmBatches => Set<FarmBatch>();
    public DbSet<Feed> Feeds => Set<Feed>();
    public DbSet<FeedEntry> FeedEntries => Set<FeedEntry>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<BatchExpense> BatchExpenses => Set<BatchExpense>();
    public DbSet<BatchSale> BatchSales => Set<BatchSale>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    // Business 4 — Coconut Business (Phase 6).
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CoconutBatch> CoconutBatches => Set<CoconutBatch>();
    public DbSet<CoconutLabourCharge> CoconutLabourCharges => Set<CoconutLabourCharge>();
    public DbSet<CoconutTransportCharge> CoconutTransportCharges => Set<CoconutTransportCharge>();
    public DbSet<CoconutBatchSale> CoconutBatchSales => Set<CoconutBatchSale>();

    // Accounting GL (Phase 7).
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalTransaction> JournalTransactions => Set<JournalTransaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    // Sync / idempotency (Phase 8).
    public DbSet<ERP.Domain.Sync.IdempotencyRecord> IdempotencyRecords => Set<ERP.Domain.Sync.IdempotencyRecord>();

    /// <summary>
    /// Current business for the tenant query filter. Null for Super Admin or when no business
    /// is selected (filter then matches all non-deleted rows). Referenced by the global filter
    /// below; EF re-evaluates it per query.
    /// </summary>
    public Guid? CurrentBusinessId => _tenant?.IsSuperAdmin == true ? null : _tenant?.BusinessId;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All money is numeric(14,2).
        configurationBuilder.Properties<decimal>().HavePrecision(14, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Generic global query filters: soft-delete for every BaseEntity, plus tenant scoping
        // for any IBusinessScoped entity (activates automatically as verticals are added).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (!typeof(BaseEntity).IsAssignableFrom(clr)) continue;

            var param = Expression.Parameter(clr, "e");
            Expression body = Expression.Not(Expression.Property(param, nameof(BaseEntity.IsDeleted)));

            if (typeof(IBusinessScoped).IsAssignableFrom(clr))
            {
                var ctxBusinessId = Expression.Property(Expression.Constant(this), nameof(CurrentBusinessId));
                var entityBusinessId = Expression.Convert(
                    Expression.Property(param, nameof(IBusinessScoped.BusinessId)), typeof(Guid?));
                var noActiveBusiness = Expression.Equal(ctxBusinessId, Expression.Constant(null, typeof(Guid?)));
                var matchesBusiness = Expression.Equal(entityBusinessId, ctxBusinessId);
                body = Expression.AndAlso(body, Expression.OrElse(noActiveBusiness, matchesBusiness));
            }

            modelBuilder.Entity(clr).HasQueryFilter(Expression.Lambda(body, param));
        }

        base.OnModelCreating(modelBuilder);
    }
}

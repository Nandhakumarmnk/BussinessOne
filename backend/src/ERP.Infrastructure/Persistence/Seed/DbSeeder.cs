using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Enums;
using ERP.Domain.Identity;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seed: business types, roles, permissions, role→permission mapping, and a small
/// demo tenant (Super Admin + Owner + one Transport business). Safe to run on every startup.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        await SeedBusinessTypesAsync(db, ct);
        await SeedRolesAsync(db, ct);
        await SeedPermissionsAsync(db, ct);
        await SeedRolePermissionsAsync(db, ct);
        await SeedDemoDataAsync(db, hasher, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedBusinessTypesAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.BusinessTypes.Select(x => x.Code).ToListAsync(ct);
        var wanted = new (string Code, string Name)[]
        {
            (BusinessTypeCodes.Transport, "Goods Transport"),
            (BusinessTypeCodes.Cctv, "Electronics & CCTV"),
            (BusinessTypeCodes.Farm, "Farm Management"),
            (BusinessTypeCodes.Coconut, "Coconut Business"),
        };
        foreach (var (code, name) in wanted.Where(w => !existing.Contains(w.Code)))
            db.BusinessTypes.Add(new BusinessType { Code = code, Name = name });
    }

    private static async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Roles.Select(x => x.Code).ToListAsync(ct);
        var wanted = new (string Code, string Name)[]
        {
            (RoleCodes.SuperAdmin, "Super Admin"),
            (RoleCodes.Owner, "Business Owner"),
            (RoleCodes.Manager, "Manager"),
            (RoleCodes.Employee, "Employee"),
            (RoleCodes.Driver, "Driver"),
            (RoleCodes.Labour, "Labour"),
        };
        foreach (var (code, name) in wanted.Where(w => !existing.Contains(w.Code)))
            db.Roles.Add(new Role { Code = code, Name = name });
    }

    private static async Task SeedPermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Permissions.Select(x => x.Code).ToListAsync(ct);
        foreach (var code in AllPermissions.Where(c => !existing.Contains(c)))
            db.Permissions.Add(new Permission { Code = code, Description = code });
    }

    private static async Task SeedRolePermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        // Needs roles + permissions persisted first.
        await db.SaveChangesAsync(ct);

        var roles = await db.Roles.ToListAsync(ct);
        var permissions = await db.Permissions.ToDictionaryAsync(p => p.Code, p => p.Id, ct);
        var existing = await db.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId }).ToListAsync(ct);

        foreach (var role in roles)
        {
            var codes = PermissionsForRole(role.Code);
            foreach (var code in codes)
            {
                if (!permissions.TryGetValue(code, out var permId)) continue;
                if (existing.Any(e => e.RoleId == role.Id && e.PermissionId == permId)) continue;
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permId });
            }
        }
    }

    private static async Task SeedDemoDataAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(ct)) return;

        // Platform Super Admin (no tenant).
        db.Users.Add(new User
        {
            FullName = "Super Admin",
            Mobile = "9999999999",
            Email = "superadmin@business-one.local",
            PasswordHash = hasher.Hash("Admin@123"),
            IsSuperAdmin = true,
            TenantId = null
        });

        // Demo tenant + owner + one Transport business.
        var tenant = new Tenant { Name = "Demo Group" };
        var owner = new User
        {
            FullName = "Demo Owner",
            Mobile = "9000000001",
            Email = "owner@business-one.local",
            PasswordHash = hasher.Hash("Owner@123"),
            TenantId = tenant.Id
        };

        var transportType = await db.BusinessTypes.FirstAsync(bt => bt.Code == BusinessTypeCodes.Transport, ct);
        var business = new Business
        {
            TenantId = tenant.Id,
            BusinessTypeId = transportType.Id,
            Name = "Sri Transport"
        };
        var ownerRole = await db.Roles.FirstAsync(r => r.Code == RoleCodes.Owner, ct);

        db.Tenants.Add(tenant);
        db.Users.Add(owner);
        db.Businesses.Add(business);
        db.UserBusinesses.Add(new UserBusiness
        {
            UserId = owner.Id,
            BusinessId = business.Id,
            RoleId = ownerRole.Id
        });

        // Tenant<->User is a circular FK (tenant.owner_user_id -> user, user.tenant_id ->
        // tenant). Insert all rows first with owner_user_id left NULL, then set it in a
        // second save; otherwise EF's insert ordering throws a circular-dependency error.
        await db.SaveChangesAsync(ct);
        tenant.OwnerUserId = owner.Id;
        await db.SaveChangesAsync(ct);
    }

    // ---- RBAC source of truth (mirrors docs/10) ----

    private static readonly string[] AllPermissions =
    {
        Permissions.DashboardView, Permissions.ReportGenerate, Permissions.AccountingView,
        Permissions.Platform.ReadAll,
        Permissions.Business.Manage, Permissions.Business.MembersManage,
        Permissions.Users.Manage,
        Permissions.Employee.Manage, Permissions.Employee.AttendanceMark,
        Permissions.Expense.Manage,
        Permissions.Customer.Manage, Permissions.Customer.CollectionRecord,
        Permissions.Transport.VehicleManage, Permissions.Transport.DriverManage,
        Permissions.Transport.LoadCreate, Permissions.Transport.LoadView, Permissions.Transport.CreditManage,
        Permissions.Cctv.ItemManage, Permissions.Cctv.PoCreate, Permissions.Cctv.PoApprove,
        Permissions.Cctv.SaleCreate, Permissions.Cctv.ServiceManage,
        Permissions.Farm.BatchManage, Permissions.Farm.FeedRecord, Permissions.Farm.MedicalRecord,
        Permissions.Farm.WalletManage,
        Permissions.Coconut.BatchManage, Permissions.Coconut.ChargeRecord,
    };

    private static IEnumerable<string> PermissionsForRole(string roleCode) => roleCode switch
    {
        RoleCodes.SuperAdmin => AllPermissions,

        RoleCodes.Owner => AllPermissions.Where(p => p != Permissions.Platform.ReadAll),

        RoleCodes.Manager => AllPermissions
            .Where(p => p is not (Permissions.Platform.ReadAll
                                  or Permissions.Business.Manage
                                  or Permissions.Users.Manage)),

        RoleCodes.Employee => new[]
        {
            Permissions.DashboardView, Permissions.ReportGenerate,
            Permissions.Employee.AttendanceMark, Permissions.Expense.Manage,
            Permissions.Customer.Manage, Permissions.Customer.CollectionRecord,
            Permissions.Transport.LoadCreate, Permissions.Transport.LoadView,
            Permissions.Cctv.SaleCreate, Permissions.Cctv.ServiceManage,
            Permissions.Farm.FeedRecord, Permissions.Farm.MedicalRecord,
            Permissions.Coconut.ChargeRecord,
        },

        RoleCodes.Driver => new[]
        {
            Permissions.DashboardView, Permissions.Transport.LoadView, Permissions.Transport.LoadCreate,
        },

        RoleCodes.Labour => new[]
        {
            Permissions.Farm.FeedRecord, Permissions.Coconut.ChargeRecord,
        },

        _ => Array.Empty<string>()
    };
}

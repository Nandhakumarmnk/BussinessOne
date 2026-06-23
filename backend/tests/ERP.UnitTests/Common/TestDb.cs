using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.UnitTests.Common;

internal sealed class FakeTenant : ITenantContext
{
    public Guid? UserId { get; init; }
    public Guid? BusinessId { get; init; }
    public bool IsSuperAdmin { get; init; }
}

internal static class TestDb
{
    /// <summary>An isolated in-memory AppDbContext scoped to <paramref name="businessId"/>.</summary>
    public static AppDbContext Create(Guid businessId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"erp-tests-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, new FakeTenant { BusinessId = businessId });
    }
}

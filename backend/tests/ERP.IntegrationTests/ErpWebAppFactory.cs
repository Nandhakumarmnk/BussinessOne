using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// Boots the real API pipeline (auth, filters, middleware, MediatR, EF) but swaps PostgreSQL for
/// EF InMemory so the suite runs without Docker. It exercises everything except Postgres-specific
/// SQL (jsonb/check constraints) — those are covered by migration generation + the unit tests.
/// </summary>
public class ErpWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = $"erp-it-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-32-characters-min");

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration")))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>((sp, o) =>
                o.UseInMemoryDatabase(_dbName)
                 .AddInterceptors(
                     sp.GetRequiredService<AuditableEntityInterceptor>(),
                     sp.GetRequiredService<AuditTrailInterceptor>())
                 .ConfigureWarnings(w => w.Ignore(
                     InMemoryEventId.TransactionIgnoredWarning,
                     CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await DbSeeder.SeedAsync(db, hasher);
    }

    public new Task DisposeAsync() => Task.CompletedTask;
}

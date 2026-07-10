using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Reporting;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Reporting;
using ERP.Infrastructure.Storage;
using ERP.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString =
            config.GetConnectionString("Default")
            ?? Environment.GetEnvironmentVariable("ERP_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=erp;Username=postgres;Password=postgres";

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditTrailInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options
                .UseNpgsql(connectionString, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention()
                // Stamp interceptor first (it rewrites hard deletes to soft deletes), then the
                // trail interceptor records the change.
                .AddInterceptors(
                    sp.GetRequiredService<AuditableEntityInterceptor>(),
                    sp.GetRequiredService<AuditTrailInterceptor>())
                .ConfigureWarnings(w =>
                    w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IIdentityQueries, IdentityQueries>();
        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.AddScoped<IJwtService, JwtService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IDateTime, SystemDateTime>();

        // File storage: local disk for dev/tests (default), Firebase Storage (GCS) in production.
        // Selection is lazy, so the 'Local' default keeps tests credential-free.
        if (string.Equals(config["Storage:Provider"], "Firebase", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IFileStorage, GcsFileStorage>();
        else
            services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddSingleton<IReportExporter, ReportExporter>();

        return services;
    }
}

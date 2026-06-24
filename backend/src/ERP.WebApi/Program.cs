using System.Text;
using System.Threading.RateLimiting;
using ERP.Application;
using ERP.Application.Common.Interfaces;
using ERP.Infrastructure;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Seed;
using ERP.WebApi.Identity;
using ERP.WebApi.Middleware;
using ERP.WebApi.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- Application + Infrastructure ----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Request context (ICurrentUser + ITenantContext) ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpCurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<HttpCurrentUser>());
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

// ---- MVC ----
builder.Services.AddScoped<ERP.WebApi.Filters.IdempotencyFilter>();
builder.Services.AddControllers(o => o.Filters.AddService<ERP.WebApi.Filters.IdempotencyFilter>());

// ---- Auth (JWT bearer) ----
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep "sub"/"sa"/"tenant" as-is
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };
    });
builder.Services.AddAuthorization();

// ---- CORS (Web SPA + Expo dev) ----
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(o => o.AddPolicy("spa", p =>
    p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

// ---- Rate limiting (skipped under integration tests) ----
var rateLimitingEnabled = !builder.Environment.IsEnvironment("Testing");
if (rateLimitingEnabled)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Global: per-user (or per-IP) fixed window.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                ctx.User.FindFirst("sub")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));

        // Stricter window for the auth endpoints (brute-force defense).
        options.AddFixedWindowLimiter("auth", o =>
        {
            o.PermitLimit = 20;
            o.Window = TimeSpan.FromMinutes(1);
        });
    });
}

// ---- Swagger / OpenAPI ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Multi-Business ERP API",
        Version = "v1",
        Description = "Multi-tenant ERP for Transport, CCTV, Farm and Coconut businesses."
    });

    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT access token."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    o.OperationFilter<BusinessIdHeaderOperationFilter>();

    var xml = Path.Combine(AppContext.BaseDirectory, "ERP.WebApi.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

// ---- Health checks ----
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// ---- Auto migrate + seed (toggle via Database:AutoMigrate) ----
if (app.Configuration.GetValue("Database:AutoMigrate", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DbSeeder.SeedAsync(db, hasher);
}

// ---- Pipeline ----
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (rateLimitingEnabled) app.UseRateLimiter();

// Swagger UI is on outside Production; in Production enable it explicitly via
// Swagger:Enabled=true (env Swagger__Enabled=true).
if (!app.Environment.IsProduction() || app.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP API v1");
        o.DisplayRequestDuration();
    });
}

app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>).
public partial class Program { }

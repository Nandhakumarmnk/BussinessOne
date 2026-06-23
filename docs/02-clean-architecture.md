# 02 · Clean Architecture, CQRS, Repository/UoW, SOLID

## 1. Dependency rule

```
        ┌─────────────────────────────────────────────┐
        │                 Presentation                 │  WebApi (controllers, middleware, DI)
        │   depends inward only ──────────────┐        │
        ├─────────────────────────────────────┼────────┤
        │              Infrastructure          │        │  EF Core, repos, storage, auth, email
        │   implements Application interfaces  ▼        │
        ├──────────────────────────────────────────────┤
        │                Application                    │  CQRS handlers, DTOs, validators, interfaces
        ├──────────────────────────────────────────────┤
        │                  Domain                       │  Entities, value objects, enums, domain rules
        └──────────────────────────────────────────────┘
              Inner layers know NOTHING of outer layers.
```

- **Domain** has zero dependencies (no EF, no ASP.NET). Pure C#.
- **Application** depends on Domain only; defines *interfaces* (`IRepository`, `IUnitOfWork`,
  `IFileStorage`, `ICurrentUser`, `IDateTime`) that Infrastructure implements.
- **Infrastructure** depends on Application + Domain; contains EF Core, repos, JWT, storage.
- **WebApi** depends on Application (+ Infrastructure only for DI wiring at composition root).

This keeps business rules testable without a database or web server.

## 2. Solution layout (.NET)

```
backend/
├─ src/
│  ├─ ERP.Domain/              ← entities, enums, value objects, domain events, exceptions
│  ├─ ERP.Application/         ← CQRS (commands/queries), DTOs, validators, interfaces, behaviors
│  ├─ ERP.Infrastructure/      ← EF Core DbContext, repositories, UoW, JWT, storage, email, FCM
│  └─ ERP.WebApi/              ← controllers, middleware, filters, Program.cs, Swagger, DI
└─ tests/
   ├─ ERP.Domain.UnitTests/
   ├─ ERP.Application.UnitTests/
   └─ ERP.IntegrationTests/    ← Testcontainers (real Postgres) + WebApplicationFactory
```

Full folder tree is in [03-project-structure.md](03-project-structure.md).

## 3. CQRS with MediatR

Every use case is a **Command** (mutation) or **Query** (read), each with one handler.

```csharp
// Application/Features/Transport/Loads/Commands/CreateLoad
public record CreateLoadCommand(
    Guid BusinessId, Guid CustomerId, Guid VehicleId, Guid DriverId,
    string Source, string Destination, decimal LoadAmount,
    decimal LoadmanCharges, decimal FuelExpense, decimal MaintenanceExpense,
    decimal OtherExpense, DateOnly LoadDate, decimal DriverCharges
) : IRequest<Result<LoadDto>>;

public class CreateLoadHandler : IRequestHandler<CreateLoadCommand, Result<LoadDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;

    public CreateLoadHandler(IUnitOfWork uow, ICurrentUser user) { _uow = uow; _user = user; }

    public async Task<Result<LoadDto>> Handle(CreateLoadCommand c, CancellationToken ct)
    {
        var load = Load.Create(c.BusinessId, c.CustomerId, c.VehicleId, c.DriverId,
            c.Source, c.Destination, c.LoadAmount, c.LoadmanCharges, c.FuelExpense,
            c.MaintenanceExpense, c.OtherExpense, c.DriverCharges, c.LoadDate);
        // load.Profit computed inside the entity (single source of truth)
        await _uow.Loads.AddAsync(load, ct);
        await _uow.SaveChangesAsync(ct);     // UoW commits the transaction
        return Result.Ok(load.ToDto());
    }
}
```

### Pipeline behaviors (cross-cutting, run for every request)
1. `RequestLoggingBehavior` — log request name + correlation id + timing.
2. `ValidationBehavior` — run FluentValidation validators; short-circuit on failure.
3. `AuthorizationBehavior` — check the caller has the required permission for this request.
4. `UnitOfWorkBehavior` — wrap commands in a DB transaction; commit on success, rollback on error.
5. `PerformanceBehavior` — warn on slow handlers.

## 4. Repository + Unit of Work

```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    IQueryable<T> Query();                       // for read-side projections
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);                        // soft delete
}

public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<Load> Loads { get; }
    IRepository<Vehicle> Vehicles { get; }
    IRepository<Customer> Customers { get; }
    // ... per aggregate
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
```

- One `DbContext` per request (scoped). UoW wraps it; repositories share the same context so a
  single `SaveChangesAsync` commits the whole use case atomically.
- **Read side** uses `Query()` + EF `Select` projections to DTOs (no tracking) for performance.
- **Write side** loads aggregates, applies domain methods, and persists via UoW.

> CQRS note: we use a *single* PostgreSQL database (no separate read store) for the pilot. The
> read/write *separation* is logical — queries project directly to DTOs; commands go through
> aggregates. This gives CQRS benefits without operational overhead.

## 5. Domain layer essentials

```csharp
public abstract class BaseEntity {
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public void SoftDelete() { IsDeleted = true; DeletedAt = DateTime.UtcNow; }
}

public interface IBusinessScoped { Guid BusinessId { get; } }  // multi-tenant marker

public class Load : BaseEntity, IBusinessScoped
{
    public Guid BusinessId { get; private set; }
    public decimal LoadAmount { get; private set; }
    public decimal FuelExpense { get; private set; }
    public decimal MaintenanceExpense { get; private set; }
    public decimal DriverCharges { get; private set; }
    public decimal LoadmanCharges { get; private set; }
    public decimal OtherExpense { get; private set; }

    // Profit is a derived rule that lives in the domain — never duplicated in handlers/UI.
    public decimal Profit =>
        LoadAmount - (FuelExpense + MaintenanceExpense + DriverCharges
                      + LoadmanCharges + OtherExpense);

    public static Load Create(/* ... */) { /* invariants validated here */ }
}
```

Domain rules captured as entity methods / value objects:
- **Load.Profit**, **Batch P&L**, **Coconut Profit** — single source of truth, shared to client
  via `packages/domain` mirror.
- **Money** value object guards non-negative amounts and 2-dp rounding.
- **CreditBalance** invariant: `paid_amount <= load_amount`.

## 6. SOLID applied

| Principle | How it shows up here |
|-----------|----------------------|
| **S**ingle responsibility | One handler per use case; thin controllers; repos only persist |
| **O**pen/closed | New business vertical = new feature folder + tables; existing code untouched. Reports use a strategy per `BusinessType` |
| **L**iskov | All repos honor `IRepository<T>`; behaviors honor `IPipelineBehavior` |
| **I**nterface segregation | Small focused interfaces (`IFileStorage`, `ICurrentUser`, `IDateTime`) not one fat service |
| **D**ependency inversion | Application depends on abstractions; Infrastructure provides them via DI at the composition root |

## 7. Validation & error handling

- **FluentValidation** validator per command/query, executed in `ValidationBehavior`.
- Domain invariant breaches throw `DomainException`; mapped to HTTP 422.
- A global **exception-handling middleware** converts exceptions to the standard error envelope
  (see [07-swagger-and-conventions.md](07-swagger-and-conventions.md)).
- `Result<T>` pattern avoids exceptions for expected business failures (e.g. "credit limit
  exceeded") — returned as 400/409 with a machine-readable code.

## 8. Testing strategy

| Layer | Test type | Tooling |
|-------|-----------|---------|
| Domain | Pure unit (profit/P&L math, invariants) | xUnit, FluentAssertions |
| Application | Handler unit tests with mocked UoW | xUnit, NSubstitute |
| Infrastructure/API | Integration against real Postgres | Testcontainers, WebApplicationFactory |
| Contract | OpenAPI snapshot test (spec doesn't break clients) | Swagger CLI diff |
| Web/Mobile | Component + e2e | Vitest/RTL, Playwright (web), Detox (mobile) |

Target: **≥ 80%** coverage on Domain + Application (the money-critical layers).

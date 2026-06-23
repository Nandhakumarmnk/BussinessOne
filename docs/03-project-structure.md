# 03 · Project Structure (single codebase / monorepo)

## 1. Repository root

```
business-one/
├─ apps/
│  ├─ web/                      React 18 + Vite + TS (responsive web app)
│  └─ mobile/                   React Native + Expo + TS (Android app)
├─ packages/
│  ├─ types/                    Shared DTOs/enums generated from OpenAPI
│  ├─ api-client/               Typed HTTP client (auth, retry, tenant header)
│  ├─ domain/                   Shared pure calc (load profit, batch P&L)
│  └─ ui/                       Optional shared design tokens / primitives
├─ backend/                     ASP.NET Core 8 solution (see §3)
├─ infra/
│  ├─ docker/                   Dockerfiles + docker-compose (api + postgres)
│  ├─ gcp/                      VM provisioning, Caddy config, backup cron
│  └─ github-actions/           CI/CD workflows
├─ docs/                        This design package
├─ package.json                 workspaces: apps/*, packages/*
├─ pnpm-workspace.yaml
└─ README.md
```

Frontend uses **pnpm workspaces**; backend is a standard .NET solution. They live together so
the OpenAPI contract regenerates `packages/types` for both clients in one place.

## 2. Backend — Clean Architecture tree

```
backend/
├─ ERP.sln
├─ src/
│  ├─ ERP.Domain/
│  │  ├─ Common/                BaseEntity, IBusinessScoped, IAuditable, ValueObject, Money
│  │  ├─ Enums/                 BusinessTypeCode, RoleCode, LoadStatus, ServiceStatus, ...
│  │  ├─ Exceptions/            DomainException, BusinessRuleException
│  │  ├─ Identity/              User, Role, Permission, Tenant, Business, UserBusiness
│  │  ├─ Common/                Employee, SalaryHistory, Attendance, Expense, Customer,
│  │  │                          CustomerLedger, Collection
│  │  ├─ Transport/             Vehicle, Driver, Load, Credit
│  │  ├─ Cctv/                  Item, Supplier, PurchaseOrder, PurchaseOrderLine,
│  │  │                          Sale, SaleLine, ServiceComplaint
│  │  ├─ Farm/                  FarmBatch, Feed, FeedEntry, MedicalRecord,
│  │  │                          BatchExpense, BatchSale, Wallet, WalletTransaction
│  │  ├─ Coconut/               Product, CoconutBatch, LabourCharge, TransportCharge,
│  │  │                          CoconutBatchSale
│  │  └─ Accounting/            Account, LedgerEntry, JournalTransaction
│  │
│  ├─ ERP.Application/
│  │  ├─ Common/
│  │  │  ├─ Behaviors/          Validation, Authorization, UnitOfWork, Logging, Performance
│  │  │  ├─ Interfaces/         IUnitOfWork, IRepository<T>, ICurrentUser, IDateTime,
│  │  │  │                       IFileStorage, IJwtService, IPdfReport, IExcelReport, IPushSender
│  │  │  ├─ Models/             Result<T>, PagedResult<T>, ApiError
│  │  │  ├─ Mappings/           Mapster/AutoMapper profiles
│  │  │  └─ Security/           Permissions constants, [HasPermission] attr
│  │  ├─ Features/
│  │  │  ├─ Auth/               Login, Refresh, ForgotPassword, ChangePassword (Commands)
│  │  │  ├─ Users/              CreateUser, AssignRole, ListUsers ...
│  │  │  ├─ Businesses/         CreateBusiness, ListMyBusinesses, SwitchBusiness
│  │  │  ├─ Dashboard/          GetDashboardSummary (Query)
│  │  │  ├─ Employees/          + Salary, Attendance
│  │  │  ├─ Expenses/
│  │  │  ├─ Customers/          + Ledger, Collections
│  │  │  ├─ Transport/          Vehicles, Drivers, Loads, Credits
│  │  │  ├─ Cctv/               Items, Suppliers, PurchaseOrders, Sales, Service
│  │  │  ├─ Farm/               Batches, Feed, Medical, Sales, Wallet
│  │  │  ├─ Coconut/            Products, Batches, Labour, Transport, Sales
│  │  │  ├─ Accounting/         CashBook, Ledger, ProfitAndLoss
│  │  │  └─ Reporting/          Generate (PDF/Excel) per report type
│  │  └─ DependencyInjection.cs
│  │
│  ├─ ERP.Infrastructure/
│  │  ├─ Persistence/
│  │  │  ├─ AppDbContext.cs     global query filters, audit interceptor, tenant stamping
│  │  │  ├─ Configurations/     IEntityTypeConfiguration<T> per entity
│  │  │  ├─ Repositories/       Repository<T>, UnitOfWork
│  │  │  ├─ Interceptors/       AuditSaveChangesInterceptor, TenantInterceptor
│  │  │  ├─ Migrations/         EF Core migrations
│  │  │  └─ Seed/               DbSeeder (roles, permissions, business types, demo data)
│  │  ├─ Identity/              JwtService, PasswordHasher, CurrentUser
│  │  ├─ Storage/               GcsFileStorage (+ LocalFileStorage for dev)
│  │  ├─ Reporting/             QuestPdfReport, ClosedXmlExcelReport
│  │  ├─ Notifications/         FcmPushSender, SmtpEmailSender
│  │  ├─ DateTime/              SystemDateTime
│  │  └─ DependencyInjection.cs
│  │
│  └─ ERP.WebApi/
│     ├─ Controllers/           Thin controllers → MediatR Send
│     │  ├─ AuthController.cs
│     │  ├─ BusinessesController.cs
│     │  ├─ DashboardController.cs
│     │  ├─ EmployeesController.cs, ExpensesController.cs, CustomersController.cs
│     │  ├─ Transport/ (Vehicles, Drivers, Loads, Credits)
│     │  ├─ Cctv/      (Items, PurchaseOrders, Sales, Service)
│     │  ├─ Farm/      (Batches, Feed, Medical, Sales, Wallet)
│     │  ├─ Coconut/   (Products, Batches, Charges, Sales)
│     │  ├─ AccountingController.cs
│     │  └─ ReportsController.cs
│     ├─ Middleware/            ExceptionHandling, TenantResolution, CorrelationId
│     ├─ Filters/               ApiExceptionFilter, ValidationProblem
│     ├─ Extensions/            AddSwagger, AddAuth, AddCors
│     ├─ appsettings*.json
│     └─ Program.cs             composition root (DI wiring of all layers)
│
└─ tests/
   ├─ ERP.Domain.UnitTests/
   ├─ ERP.Application.UnitTests/
   └─ ERP.IntegrationTests/
```

### Why feature folders (vertical slices) inside Application?
Each use case keeps its Command/Query + Validator + Handler + DTO together. Adding a feature
touches one folder. This pairs well with the Open/Closed principle and makes the 4 verticals
independently evolvable.

## 3. Web app tree (`apps/web`)

```
apps/web/
├─ src/
│  ├─ app/                routing, providers (QueryClient, Auth, Theme), layout shells
│  ├─ features/           mirrors backend features
│  │  ├─ auth/  dashboard/  employees/  expenses/  customers/
│  │  ├─ transport/  cctv/  farm/  coconut/
│  │  ├─ accounting/  reports/  admin/
│  ├─ components/         shared UI (DataTable, FormField, KpiCard, Charts, FileUpload)
│  ├─ hooks/              useAuth, useBusinessContext, usePermission
│  ├─ lib/                api client wiring, formatters (money/date), pdf/excel download
│  ├─ store/              Zustand (auth, active business, ui)
│  └─ types/              re-export from @erp/types
├─ index.html
├─ vite.config.ts
└─ package.json
```

State: **React Query** for server state (caching, refetch), **Zustand** for client/UI state
(active business, theme). Routing via React Router. Charts via Recharts. Tables via TanStack Table.

## 4. Mobile app tree (`apps/mobile`)

```
apps/mobile/
├─ src/
│  ├─ navigation/         stack + bottom tabs, role-aware routes
│  ├─ features/           same feature names as web (subset enabled per role)
│  ├─ components/         shared RN components (Card, Field, SyncBadge, CameraCapture)
│  ├─ offline/            WatermelonDB models, sync engine, outbox queue
│  ├─ hooks/  lib/  store/  types/
│  └─ services/           push registration (FCM), file capture/upload
├─ app.json               Expo config
└─ package.json
```

Offline: **WatermelonDB** local store + an **outbox** of pending mutations replayed on
reconnect (design in [09-mobile-screens.md](09-mobile-screens.md)).

## 5. Shared packages

```
packages/types/        // generated: enums + request/response DTOs (openapi-typescript)
packages/api-client/   // createClient({ baseUrl, getToken, businessId }) → typed methods
packages/domain/       // calcLoadProfit(), calcBatchPnl(), calcCoconutProfit(), Money helpers
```

These guarantee Web and Mobile speak the **exact same contract** as the API and compute
previews identically to the server.

## 6. Naming & conventions

| Item | Convention |
|------|-----------|
| DB tables / columns | `snake_case`, plural tables (`load_credits`) |
| C# | PascalCase types, camelCase locals, async suffix `Async` |
| API routes | kebab/lowercase plural (`/api/v1/transport/loads`) |
| TS files | `kebab-case.ts`, components `PascalCase.tsx` |
| Git | Conventional Commits; trunk-based with short-lived feature branches |
| Migrations | `yyyyMMddHHmmss_VerbNoun` (EF default timestamp) |

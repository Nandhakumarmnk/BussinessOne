# Getting Started — Phase 0 (Walking Skeleton)

This is the deployable Phase 0 increment: a Clean Architecture ASP.NET Core 8 API with JWT auth,
multi-tenant identity/RBAC, PostgreSQL via EF Core, Swagger, health checks, and a minimal React
web client that logs in and reads the secured `/me` endpoint.

> Design docs live in [`docs/`](docs/). Roadmap & phases: [docs/11](docs/11-development-roadmap.md).

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 8.0+ | builds/runs `net8.0` (SDK 9/10 also build it) |
| PostgreSQL | 15 | local install **or** Docker |
| Node.js | 20+ | for the web app |
| pnpm | 9+ | `corepack enable pnpm` |
| Docker | optional | to run the whole stack with one command |

## Option A — Run everything with Docker (recommended)

```bash
docker compose -f infra/docker/docker-compose.yml up --build
```

- API → http://localhost:8080  ·  Swagger → http://localhost:8080/swagger
- Health → http://localhost:8080/health/ready
- The API auto-migrates the database and seeds demo data on startup.

## Option B — Run the API from the .NET CLI

1. Start PostgreSQL (any way you like), e.g.:
   ```bash
   docker run --name erp-db -e POSTGRES_DB=erp -e POSTGRES_USER=postgres \
     -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:15-alpine
   ```
2. Run the API (Development env auto-migrates + seeds, and sets a dev JWT key):
   ```bash
   cd backend
   dotnet run --project src/ERP.WebApi
   ```
   API listens on the Kestrel dev port (see console) and serves Swagger at `/swagger`.

### Apply migrations manually (instead of auto-migrate)
```bash
cd backend
dotnet tool restore
dotnet ef database update \
  --project src/ERP.Infrastructure/ERP.Infrastructure.csproj \
  --startup-project src/ERP.WebApi/ERP.WebApi.csproj
```

## Run the Web app

```bash
pnpm install
pnpm dev:web         # → http://localhost:5173  (proxies /api to http://localhost:8080)
```

Open http://localhost:5173 and log in with a seeded account.

## Seeded demo accounts

| Role | Login | Password |
|------|-------|----------|
| Super Admin | `superadmin@business-one.local` (or mobile `9999999999`) | `Admin@123` |
| Business Owner | `owner@business-one.local` (or mobile `9000000001`) | `Owner@123` |

The Owner has one demo business **"Sri Transport"** (Transport type) with the full Owner permission set.

## Try the API directly

```bash
# Login
curl -s http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"mobileOrEmail":"owner@business-one.local","password":"Owner@123"}'

# Use the returned accessToken
curl -s http://localhost:8080/api/v1/me -H "Authorization: Bearer <accessToken>"
```

## What works in Phase 0

- ✅ Clean Architecture solution (Domain / Application / Infrastructure / WebApi) + unit tests
- ✅ CQRS (MediatR) with validation + logging + authorization pipeline behaviors
- ✅ Repository + Unit of Work over EF Core (PostgreSQL, snake_case)
- ✅ Multi-tenant identity: tenants, businesses, users, roles, permissions, per-business RBAC
- ✅ JWT access + rotating refresh tokens; PBKDF2 password hashing
- ✅ Soft-delete + audit interceptor; global query-filter scaffolding (tenant filter activates with vertical entities)
- ✅ `/auth/login`, secured `/me`, Swagger, health checks, standard error envelope
- ✅ Docker Compose (API + Postgres), GitHub Actions CI, React web login

## What Phase 1 adds (Identity, Tenancy & RBAC)

- ✅ **Auth round-out:** `POST /auth/register` (tenant + owner + optional first business),
  `POST /auth/refresh` (rotating), `POST /auth/logout`, `POST /auth/change-password`.
- ✅ **Reference:** `GET /business-types`, `GET /roles`, `GET /permissions`.
- ✅ **Businesses:** `GET/POST /businesses`, `GET/PUT/DELETE /businesses/{id}`.
- ✅ **Membership:** `GET/POST /businesses/{id}/members`, `DELETE /businesses/{id}/members/{userId}`.
- ✅ **Users (tenant-scoped):** `GET /users`, `POST /users` (create + optional membership), `GET /users/{id}`.
- ✅ **RBAC enforced end-to-end:** tenant-owner gate for tenant ops; `business.members.manage`
  (route-scoped) for membership; Super Admin bypass.
- ✅ **Append-only audit trail** (`audit_logs`) capturing create/update/delete with JSON diffs
  (passwords/token hashes excluded).
- ✅ **Web console:** business switcher (sets `X-Business-Id`), create-business, members list,
  invite-user-with-role.

### Try the new flows

```bash
# Register a brand-new tenant + owner + first business (returns tokens)
curl -s http://localhost:8080/api/v1/auth/register -H "Content-Type: application/json" -d '{
  "tenantName":"Acme Group","fullName":"Asha","mobile":"9111111111",
  "password":"Owner@123","firstBusinessName":"Acme CCTV","firstBusinessTypeCode":"CCTV"}'

# As the seeded owner: create a business (needs Authorization: Bearer <token>)
curl -s http://localhost:8080/api/v1/businesses -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"name":"Green Farm","businessTypeCode":"FARM"}'

# Invite a user into a business with a role
curl -s http://localhost:8080/api/v1/users -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Kumar","mobile":"9222222222","password":"Pass@1234",
       "businessId":"<businessId>","roleCode":"DRIVER"}'
```

## What Phase 2 adds (Common modules — business-scoped)

This is where the **multi-tenant query filter activates**: every entity below is `IBusinessScoped`,
so reads are auto-scoped to the active business (`X-Business-Id`) and writes are auto-stamped.

- ✅ **Employees:** CRUD + **salary history** (`POST/GET /employees/{id}/salary`, `GET /reports/salary`)
  + **attendance** (`POST/GET /employees/{id}/attendance`).
- ✅ **Expenses:** CRUD with date/type filters; **expense types**; **reports** (`GET /reports/expenses?period=daily|monthly|yearly`);
  **attachments** via `POST /files` (local storage in the pilot, Cloud Storage later).
- ✅ **Customers:** CRUD with optional opening balance; **ledger** (`GET /customers/{id}/ledger`);
  **collections** (`POST /customers/{id}/collections`, posts a ledger credit); **outstanding** (`GET /reports/outstanding`).
- ✅ **Dashboard:** `GET /dashboard/summary` — today/month income & expense, total profit, pending
  credits & collections — surfaced as KPI tiles in the web console.
- ✅ RBAC per module (`employee.manage`, `expense.manage`, `customer.manage`, `customer.collection.record`,
  `dashboard.view`); a shared **customer-ledger service** maintains running balances (reused by Phase 3 loads/sales).

### Try it

```bash
# (with Authorization: Bearer <token> and X-Business-Id: <businessId>)
curl -s http://localhost:8080/api/v1/dashboard/summary -H "Authorization: Bearer <t>" -H "X-Business-Id: <b>"
curl -s http://localhost:8080/api/v1/expenses -H "Authorization: Bearer <t>" -H "X-Business-Id: <b>" \
  -H "Content-Type: application/json" -d '{"expenseDate":"2026-06-23","amount":4200,"description":"Diesel"}'
```

## What Phase 3 adds (Business 1 — Goods Transport)

The first vertical, and the first time a module feeds the **shared customer ledger**.

- ✅ **Vehicle master** (`/transport/vehicles`) and **Driver master** (`/transport/drivers`) — CRUD,
  business-scoped, RBAC-gated (`transport.vehicle.manage`, `transport.driver.manage`).
- ✅ **Load entry** (`/transport/loads`) with **server-authoritative profit** computed in the domain
  (`Load Amount − (Loadman + Fuel + Maintenance + Driver + Other)`).
- ✅ **Credit integration:** billing a customer on a load creates a **LoadCredit** and **debits the
  customer ledger**; recording a payment (`PATCH /transport/credits/{id}/payment`) updates paid/status,
  **credits the ledger**, and books a collection — so dashboard income & outstanding reconcile.
- ✅ Edits/deletes keep the ledger consistent (amount-change adjustments; delete reverses an unpaid debit).
- ✅ **Reports:** vehicle-wise profit, driver-wise profit, profit by period (`daily|monthly|yearly`),
  and transport outstanding (`/transport/reports/*`).

### Try it

```bash
# (Authorization: Bearer <token>, X-Business-Id: <a TRANSPORT business>)
curl -s http://localhost:8080/api/v1/transport/loads -H "Authorization: Bearer <t>" -H "X-Business-Id: <b>" \
  -H "Content-Type: application/json" -d '{
    "loadNumber":"LD-0007","loadName":"Cement","customerId":"<c>","loadDate":"2026-06-23",
    "loadAmount":18000,"loadmanCharges":800,"fuelExpense":4200,"maintenanceExpense":600,
    "driverCharges":1500,"otherExpense":300 }'
# → profit = 10600, and the customer now owes 18000 (visible in /reports/outstanding)
```

## What Phase 4 adds (Business 2 — Electronics & CCTV)

- ✅ **Item master** (`/cctv/items`) with stock + reorder level, and **Suppliers** (`/cctv/suppliers`).
- ✅ **Purchase Orders** (`/cctv/purchase-orders`) with line items and a **state machine**:
  `draft → submit → approve → receive` (+ `cancel`). **Receiving stocks-in** (increments item stock);
  approval requires `cctv.po.approve` (segregated from `cctv.po.create`).
- ✅ **Sales & installation** (`/cctv/sales`) — line items + installation + labour + per-line tax →
  sub-total / tax / total; **decrements stock**; debits the customer ledger for the receivable and
  books any immediate payment as a collection.
- ✅ **Service complaints** (`/cctv/service-complaints`) — create, assign, and status transitions
  `open → in_progress → closed`.
- ✅ **Reports** (`/cctv/reports/*`): item-wise sales, revenue by period, service summary +
  employee performance, and credit outstanding.

### Try it

```bash
# (Authorization: Bearer <token>, X-Business-Id: <a CCTV business>)
# Create a sale: 4 cameras @ 3200 +18% tax, +2000 install +1500 labour  → total 18,604
curl -s http://localhost:8080/api/v1/cctv/sales -H "Authorization: Bearer <t>" -H "X-Business-Id: <b>" \
  -H "Content-Type: application/json" -d '{
    "invoiceNumber":"INV-0102","customerId":"<c>","saleDate":"2026-06-23",
    "installationCharges":2000,"labourCharges":1500,"paidAmount":0,"mode":"cash",
    "lines":[{"itemId":"<item>","quantity":4,"rate":3200,"taxPercentage":18}] }'
```

## What Phase 5 adds (Business 3 — Farm Management)

- ✅ **Batches** (`/farm/batches`) for goat/hen/cow with purchase qty + amount and status (active/sold/closed).
- ✅ **Feed master** (`/farm/feeds`) + **feed entries** per batch (qty × rate = amount).
- ✅ **Medical records** and **batch expenses** (labour/other) per batch.
- ✅ **Batch sale** (`/farm/batches/{id}/sales`) — marks the batch sold.
- ✅ **Wallet** (`/farm/wallet`) — per-business cash float with add/use transactions and a guarded
  balance (no overdraft).
- ✅ **Batch P&L** (`/farm/batches/{id}/pnl`) = `Σ sales − (purchase + feed + medical + labour + other)`,
  plus reports: batch-profit list, feed consumption, farm profit summary.

### Try it

```bash
# (Authorization: Bearer <token>, X-Business-Id: <a FARM business>)
curl -s http://localhost:8080/api/v1/farm/batches/<batchId>/pnl -H "Authorization: Bearer <t>" -H "X-Business-Id: <b>"
# → { purchase, feedCost, medicalCost, labourCost, totalSales, totalCost, profit }
```

> The P&L math is reconciled by a unit test against the wireframe example (₹2.2L sales − ₹1.818L cost = **₹38,200** profit).

## What Phase 6 adds (Business 4 — Coconut Business)

The fourth and final vertical — **all four business types are now operable.**

- ✅ **Product master** (`/coconut/products`) — Coconut, Copra, Coconut Powder, Coconut Oil, …
- ✅ **Batch purchase** (`/coconut/batches`) per product.
- ✅ **Labour & transport charges** per batch (`/coconut/batches/{id}/labour-charges`, `/transport-charges`).
- ✅ **Batch sales** (`/coconut/batches/{id}/sales`) — marks the batch sold.
- ✅ **Batch P&L** (`/coconut/batches/{id}/pnl`) = `Σ sale value − (purchase + labour + transport)`,
  plus reports: batch profit, **product-wise profit**, and cash-basis profit by period (daily/monthly/yearly).

> The P&L math is reconciled by a unit test against the wireframe example (₹84,000 sales − ₹69,400 cost = **₹14,600** profit).

## What Phase 7 adds (Accounting & Reporting)

- ✅ **Double-entry GL** — `accounts` / `journal_transactions` / `ledger_entries` with a
  **balance-enforced posting service** (`IJournalService`, debits must equal credits; a DB
  check constraint enforces debit-XOR-credit per line) and a lazily-seeded chart of accounts.
  Expense creation auto-posts (Dr Expenses / Cr Cash); a manual entry endpoint (`POST /accounting/journal`)
  lets accountants post adjusting entries.
- ✅ **Financial statements** (cash basis, derived from the unified collection/expense primitives so
  they tie out with the dashboard): `GET /accounting/cash-book`, `/profit-loss`, `/credit-tracking`,
  `/collection-tracking`, plus GL views `/accounting/accounts`, `/journal`, `/ledger`.
- ✅ **Reporting engine** — `POST /reports/export` renders **PDF (QuestPDF)** or **Excel (ClosedXML)**
  with date filters. Report keys: `expenses`, `collections`, `credit-outstanding`, `profit-loss`.

### Try it

```bash
# Download a PDF P&L (returns the file)
curl -s -X POST http://localhost:8080/api/v1/reports/export \
  -H "Authorization: Bearer <t>" -H "X-Business-Id: <b>" -H "Content-Type: application/json" \
  -d '{"reportKey":"profit-loss","format":"pdf","from":"2026-06-01","to":"2026-06-30"}' -o pl.pdf
```

> The exporters are unit-tested end-to-end (they actually generate PDF/XLSX bytes), and the journal
> service is tested to reject unbalanced entries.

## What Phase 8 adds (Mobile + sync contract)

**Backend (built + tested):**
- ✅ **Idempotent writes** — a POST carrying an `Idempotency-Key` header is deduped per business
  (`IdempotencyFilter` + `idempotency_records`); a replay returns the original response instead of
  re-applying. This is what makes the mobile offline outbox safe to resend after reconnect.
- ✅ **`GET /api/v1/sync/pull?since=<cursor>`** — returns picker masters (customers, vehicles,
  drivers, items, feeds, products, expense types) changed since the cursor, plus a new cursor, so
  the device can refresh offline caches.

**Mobile app (`apps/mobile`, authored scaffold — _not_ runtime-verified):**
- React Native/Expo app: secure-token auth, an **AsyncStorage outbox + sync engine** (replays queued
  writes idempotently, then pulls masters), Login/Home/Add-Expense screens, FCM push registration.
- See [`apps/mobile/README.md`](apps/mobile/README.md) for run steps and the documented divergences
  from [docs/09](docs/09-mobile-screens.md) (AsyncStorage scaffold vs. WatermelonDB production target).

> ⚠️ The mobile app was written without Expo tooling/a device available, so it is not build- or
> run-verified. The backend sync contract it depends on **is** covered by unit tests.

## What Phase 9 adds (Hardening & UAT)

- ✅ **End-to-end integration tests** (`ERP.IntegrationTests`, `WebApplicationFactory` + EF InMemory,
  no Docker needed): login & 401s, JWT-protected `/me`, expense create→list (auth + RBAC + GL
  posting), **idempotency dedupe**, **cross-tenant isolation**, and security headers — the live HTTP
  pipeline the unit tests didn't cover. **8 tests, all green.**
- ✅ **Rate limiting** — global 300/min per user/IP; `/auth/*` 20/min (brute-force defense).
- ✅ **Security headers** — `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, etc.
- ✅ **Security review + go-live runbook** — [docs/12](docs/12-hardening-and-runbook.md): auth/RBAC/
  isolation status, backup & restore drill, deploy/rollback runbook, NFR/load plan, go-live checklist.

### Run the full test suite

```bash
dotnet test backend/ERP.sln       # 44 unit + 8 integration = 52 tests
```

## Project status — all roadmap phases complete

| Phase | | Phase | |
|-------|---|-------|---|
| 0 Foundation | ✅ | 5 Farm | ✅ |
| 1 Identity/RBAC | ✅ | 6 Coconut | ✅ |
| 2 Common modules | ✅ | 7 Accounting & Reporting | ✅ |
| 3 Transport | ✅ | 8 Mobile (+ sync) | ✅ * |
| 4 CCTV | ✅ | 9 Hardening & UAT | ✅ |

\* Backend sync contract built + tested; the Expo app is an authored scaffold (not build/run-verified).

**The remaining step before production is a live run** (`docker compose -f infra/docker/docker-compose.yml up --build`)
and UAT — everything to date is verified by build + 52 automated tests + migration generation, but
not yet exercised against live PostgreSQL/HTTP in this environment.

## Configuration reference

| Setting | Where | Default (dev) |
|---------|-------|---------------|
| `ConnectionStrings:Default` | appsettings / env `ConnectionStrings__Default` | local Postgres |
| `Database:AutoMigrate` | appsettings / env | `true` in Development |
| `Jwt:SigningKey` | appsettings.Development.json / env `Jwt__SigningKey` | dev key (≥32 chars) |
| `Cors:Origins` | appsettings / env | `http://localhost:5173` |

> **Production:** never use the dev signing key. Provide `Jwt__SigningKey` (≥32 chars) and the
> connection string via environment / GCP Secret Manager. See [docs/01](docs/01-solution-architecture.md).

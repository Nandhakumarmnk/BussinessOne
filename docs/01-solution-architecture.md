# 01 · Solution Architecture

## 1. System context (C4 - Level 1)

```
            ┌──────────────┐      ┌──────────────┐      ┌──────────────┐
            │  Web Browser │      │ Android App  │      │  Super Admin │
            │  (React SPA) │      │(React Native)│      │   (Web)      │
            └──────┬───────┘      └──────┬───────┘      └──────┬───────┘
                   │ HTTPS/JSON          │ HTTPS/JSON          │
                   └─────────────┬───────┴─────────────────────┘
                                 ▼
                    ┌────────────────────────────┐
                    │   ASP.NET Core 8 Web API    │  ← JWT auth, RBAC, CQRS
                    │   (Clean Architecture)      │
                    └─────┬───────────────┬───────┘
                          │               │
                 ┌────────▼──────┐  ┌─────▼─────────┐  ┌──────────────┐
                 │ PostgreSQL 15 │  │ Cloud Storage │  │  FCM (push)  │
                 │  (Cloud SQL)  │  │ (attachments) │  │ notifications│
                 └───────────────┘  └───────────────┘  └──────────────┘
```

External integrations (v1): **Firebase Cloud Messaging** (push), **Cloud Storage** (files),
**SMTP/Email** (password reset). GST/e-invoice and SMS are deferred.

## 2. Container view (C4 - Level 2)

| Container | Tech | Responsibility |
|-----------|------|----------------|
| Web SPA | React + Vite | Owner/Manager desktop experience, dashboards, reports |
| Mobile App | React Native (Expo) | Field data entry, offline-first, camera upload |
| Shared packages | TypeScript | `@erp/types`, `@erp/api-client`, `@erp/domain` reused by Web + Mobile |
| API | ASP.NET Core 8 | Business logic, auth, validation, reporting, file orchestration |
| DB | PostgreSQL 15 | System of record (multi-tenant, row-scoped by `business_id`) |
| Object Storage | Cloud Storage | Attachments, generated PDFs/Excel exports |
| Cache (optional) | In-memory / Redis (later) | Token revocation list, hot dashboard aggregates |

## 3. Single codebase strategy

A **monorepo** holds Web, Mobile, shared TS packages, and the .NET solution.

```
/ (repo root)
├─ apps/
│  ├─ web/            React (Vite)
│  └─ mobile/         React Native (Expo)
├─ packages/
│  ├─ types/          DTOs & enums generated from the OpenAPI spec (shared)
│  ├─ api-client/     Typed fetch client (shared by web + mobile)
│  └─ domain/         Pure calc logic (load profit, batch P&L) shared on client
├─ backend/           ASP.NET Core solution (see 02 & 03)
└─ docs/
```

- **Type safety end-to-end:** the OpenAPI spec emitted by Swagger is the contract; `packages/types`
  is generated from it, so Web and Mobile cannot drift from the API.
- **Shared domain calculations** (e.g. Load Profit, Batch P&L) live in `packages/domain` so the
  client can show live previews identical to the server's authoritative computation.

## 4. Request lifecycle

```
Client → HTTPS → API Gateway/Ingress
   → Authentication middleware (validate JWT, load principal)
   → Tenant resolution middleware (extract business_id from header/route, authorize membership)
   → Routing → Controller (thin) → MediatR Send(Command/Query)
   → Validation behavior (FluentValidation) → Authorization behavior (permission check)
   → Handler → Repository/UoW → EF Core (+ global query filter business_id) → PostgreSQL
   → Result → DTO → JSON response (+ standard envelope)
```

Cross-cutting concerns are MediatR **pipeline behaviors**: logging, validation, authorization,
transaction/UoW, performance timing. See [02-clean-architecture.md](02-clean-architecture.md).

## 5. Multi-tenant isolation

- Every business-scoped entity implements `IBusinessScoped { Guid BusinessId }`.
- An EF Core **global query filter** appends `WHERE business_id = @current` automatically.
- The current `business_id` comes from the `X-Business-Id` header (or route), validated against
  the caller's memberships in the tenant-resolution middleware.
- **Super Admin** can bypass the filter via an explicit `IgnoreQueryFilters()` path guarded by
  the `platform.read.all` permission.
- Defense in depth: a **composite index/foreign-key** ties child rows to `business_id`, and an
  `EF SaveChanges` interceptor stamps `business_id` on insert so a handler cannot forget it.

## 6. Deployment on Google Cloud Free Tier

Two deployment options; **Option B** is the recommended free-tier-friendly default.

### Option A — Managed (simplest, may exceed free tier under load)
```
Cloud Run (API, scale-to-zero)  ──→  Cloud SQL for PostgreSQL (db-f1-micro)
Cloud Storage (files)           ──→  Firebase Hosting (Web SPA)
```

### Option B — e2-micro Compute Engine (stays in Always-Free) ✅ recommended for pilot
```
┌─────────────────────────────────────────────────────────────┐
│ GCE e2-micro (Always Free, us-central1/us-west1/us-east1)    │
│   • Docker: ASP.NET Core API container                       │
│   • Docker: PostgreSQL 15 container (data on persistent disk)│
│   • Caddy/Nginx reverse proxy + Let's Encrypt TLS            │
└─────────────────────────────────────────────────────────────┘
   Cloud Storage (5 GB free)  → attachments & exports
   Firebase Hosting (free)    → React Web SPA (static)
   Firebase Cloud Messaging   → push notifications (free)
```

| Concern | Free-tier approach |
|---------|--------------------|
| Compute | 1× e2-micro VM (Always Free) running API + Postgres in Docker Compose |
| DB | PostgreSQL in container; nightly `pg_dump` to Cloud Storage |
| Static web | Firebase Hosting (free SSL, CDN) |
| Files | Cloud Storage Standard, 5 GB free |
| Push | FCM (free) |
| TLS | Caddy auto-HTTPS / Let's Encrypt |
| CI/CD | GitHub Actions → build images → push → SSH deploy to VM |
| Backups | Cron `pg_dump` gzip → `gsutil cp` to bucket; 30-day lifecycle rule |

> **Migration path:** when the pilot outgrows the VM, lift the API to **Cloud Run** and the DB to
> **Cloud SQL** with no code change — connection string + storage driver are the only differences.

## 7. Environments

| Env | Purpose | Data | Hosting |
|-----|---------|------|---------|
| Local | Dev | Docker Postgres + seed | localhost |
| Staging | QA / UAT | Anonymized | GCE VM (small) |
| Production | Live | Real | GCE e2-micro (Option B) |

Configuration via `appsettings.{Environment}.json` + environment variables (secrets via GCP
Secret Manager or VM env file). **No secrets in source control.**

## 8. Non-functional targets (pilot)

| Attribute | Target |
|-----------|--------|
| API p95 latency | < 400 ms for CRUD; < 1.5 s for report generation |
| Availability | 99% (single VM pilot); revisit with managed services later |
| Mobile offline | Full data entry offline; sync ≤ 30 s after reconnect |
| Concurrent users | ~50 for pilot |
| RPO / RTO | RPO 24h (nightly dump) / RTO 2h (restore to fresh VM) |
| Security | TLS 1.2+, hashed passwords (PBKDF2/Argon2), JWT 15-min access tokens |

## 9. Observability

- **Structured logging** with Serilog → console + rolling file (shipped to Cloud Logging later).
- **Correlation IDs** per request, propagated to client and logs.
- **Health checks** `/health/live` and `/health/ready` (DB + storage probes).
- **Audit log** table for all create/update/delete on business data.
- Metrics endpoint (`/metrics`, Prometheus format) for later scrape.

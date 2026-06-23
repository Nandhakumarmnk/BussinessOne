# 00 · Overview

## 1. Vision

A small business owner often runs **several unrelated businesses** (a transport fleet, a CCTV
shop, a farm, a coconut trade). Each needs different operational screens, but they share the
same backbone: users, money in, money out, customers, credit, and reports.

This platform gives that owner **one login, one app, many businesses** — with each business
showing only the modules relevant to its type, while the owner gets a consolidated financial
picture across all of them.

## 2. Goals & non-goals

### Goals
- One **single codebase** serving Web and Mobile (shared TypeScript domain + API client).
- **Multi-tenant**: strict data isolation between businesses; an owner can hold many.
- **Role-based**: Super Admin, Business Owner, Manager, Employee, Driver, Labour.
- **Offline-first mobile** data entry with sync — field staff often have poor connectivity.
- Run within **Google Cloud Free Tier** budget for the pilot.
- **Extensible**: adding a 5th business vertical must not require touching the other four.

### Non-goals (v1)
- Full statutory GST e-invoicing / e-way bill integration (GST fields captured; filing later).
- Payroll statutory compliance (PF/ESI). We track salary, not statutory payroll.
- Multi-currency. Single currency (₹ INR) in v1.
- Real-time GPS vehicle tracking (load entry is manual in v1).

## 3. Actors / roles

| Role | Scope | Typical actions |
|------|-------|-----------------|
| **Super Admin** | Platform-wide | Manage tenants, see all data, platform config |
| **Business Owner** | All businesses they own | Create businesses, full access within them, consolidated dashboard |
| **Manager** | Assigned business(es) | Day-to-day operations, approve POs, reports |
| **Employee** | Assigned business(es) | Data entry, service jobs, attendance |
| **Driver** | Transport business | View assigned loads, mark trips |
| **Labour** | Farm / Coconut | Logged as cost; limited/no app access in v1 |

> Roles are **per business membership**, not global (except Super Admin). The same person can
> be Owner of Business A and Manager of Business B. See [10-security-rbac.md](10-security-rbac.md).

## 4. Tenancy model

```
Tenant (an owner's organization / account)
  └── Business (has exactly one BusinessType: Transport | CCTV | Farm | Coconut)
        └── all transactional data is scoped by business_id
```

- **Tenant** = the billing/ownership boundary (one Business Owner ≈ one Tenant).
- **Business** = the operational + reporting boundary. The `business_id` discriminator is on
  every business-scoped table and enforced by an EF Core **global query filter**.
- Shared masters that are genuinely platform-level (e.g. `business_types`, `roles`,
  `permissions`) are **not** tenant-scoped.

## 5. Module map

```
COMMON (every business)                 VERTICAL (only its business type)
├─ Dashboard                            ├─ [1] Goods Transport
├─ Employees (+ salary, attendance)     │     Vehicle, Driver, Load, Credit
├─ Expenses                             ├─ [2] Electronics & CCTV
├─ Customers (+ ledger, collection)     │     Item, Purchase Order, Sales, Service
├─ Accounting (cash book, ledger, P&L)  ├─ [3] Farm Management
└─ Reporting (PDF / Excel)              │     Batch, Feed, Medical, Sale, Wallet
                                        └─ [4] Coconut Business
                                              Product, Batch, Labour, Transport, Sale
```

## 6. Domain glossary

| Term | Meaning |
|------|---------|
| **Tenant** | Top-level account owned by a Business Owner; billing & isolation boundary |
| **Business** | An operational unit of a single business type under a tenant |
| **BusinessType** | One of Transport / CCTV / Farm / Coconut; drives which modules show |
| **Load** | A single transport job (source→destination) with revenue and trip costs |
| **Credit** | Amount a customer owes against a load/sale (receivable) |
| **Collection** | A payment received against an outstanding customer balance |
| **Batch** (Farm) | A cohort of animals (goat/hen/cow) tracked from purchase to sale |
| **Batch** (Coconut) | A lot of product purchased, processed and sold |
| **Wallet** | A per-business cash float used to fund farm operations |
| **Ledger** | Double-sided record of money movement for accounting |
| **UOM** | Unit of measure (kg, nos, litre, …) |
| **HSN** | Harmonized System Nomenclature code (tax classification) |

## 7. Key cross-cutting requirements

| Concern | Decision |
|---------|----------|
| Money | `numeric(14,2)`, integer-paisa-safe arithmetic; never `float` |
| Dates | Store UTC `timestamptz`; display in tenant timezone (default Asia/Kolkata) |
| Soft delete | `is_deleted` + `deleted_at`; nothing is hard-deleted in transactional tables |
| Audit | `created_by/at`, `updated_by/at` on all tables + append-only `audit_logs` |
| Attachments | Stored in Cloud Storage; DB keeps object key + metadata only |
| Offline | Mobile queues writes locally, syncs with conflict resolution (see [09](09-mobile-screens.md)) |
| Idempotency | All mobile-originated writes carry a client UUID for dedupe |

## 8. Deliverables in this design package

1. ER Diagram — [04-database-design.md](04-database-design.md)
2. Database Scripts — [05-database-scripts.sql](05-database-scripts.sql)
3. API Design — [06-api-design.md](06-api-design.md)
4. Swagger Documentation — [07-swagger-and-conventions.md](07-swagger-and-conventions.md)
5. Mobile Screens — [09-mobile-screens.md](09-mobile-screens.md)
6. Web Screens — [08-web-screens.md](08-web-screens.md)
7. Project Structure — [03-project-structure.md](03-project-structure.md)
8. Clean Architecture Folder Structure — [02-clean-architecture.md](02-clean-architecture.md)
9. Development Roadmap — [11-development-roadmap.md](11-development-roadmap.md)
10. Phase-wise Delivery Plan — [11-development-roadmap.md](11-development-roadmap.md)

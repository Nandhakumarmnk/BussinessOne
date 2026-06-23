# 11 · Development Roadmap & Phase-wise Delivery Plan

Strategy: build the **platform spine first** (auth, tenancy, common modules), then ship one
business vertical at a time so the owner gets usable value early. Each phase ends with a
deployable, demo-able increment on the GCP free-tier environment.

## 1. Guiding principles

- **Walking skeleton first:** end-to-end thin slice (login → create business → one CRUD →
  dashboard) deployed before breadth.
- **Vertical by vertical:** Transport → CCTV → Farm → Coconut. Each is independent (Open/Closed),
  so order can change by business priority without rework.
- **Contract-first:** Swagger spec drives generated TS clients each phase.
- **Always shippable:** trunk-based dev, CI on every PR, tagged release each phase.

## 2. Phase plan (indicative: ~16–20 weeks for v1)

| Phase | Theme | Duration | Outcome |
|-------|-------|----------|---------|
| **0** | Foundation & DevOps | 1–2 wk | Repo, CI/CD, DB, deploy pipeline, walking skeleton |
| **1** | Identity, Tenancy & RBAC | 2 wk | Login, users, businesses, roles/permissions, business switch |
| **2** | Common modules | 2–3 wk | Employees, Expenses, Customers, Dashboard, file upload |
| **3** | Business 1 — Transport | 2 wk | Vehicles, Drivers, Loads (profit), Credits, transport reports |
| **4** | Business 2 — CCTV | 2–3 wk | Items, PO + approval, Sales, Service, CCTV reports |
| **5** | Business 3 — Farm | 2 wk | Batches, Feed, Medical, Sales, Wallet, P&L |
| **6** | Business 4 — Coconut | 1–2 wk | Products, Batches, Labour/Transport charges, Sales, profit |
| **7** | Accounting & Reporting | 2 wk | Cash book, ledger, P&L, PDF/Excel export engine |
| **8** | Mobile offline + Push | 2–3 wk | WatermelonDB sync, outbox, FCM, camera, invoice PDF |
| **9** | Hardening & UAT | 1–2 wk | Security review, load test, bug-fix, docs, go-live |

> Durations assume a small team (≈2 backend, 1 web, 1 mobile, shared QA). Phases 3–6 can run
> partly in parallel once the common platform (Phase 2) is stable.

## 3. Phase detail & exit criteria

### Phase 0 — Foundation & DevOps
- Monorepo (pnpm workspaces) + .NET solution skeleton (Domain/Application/Infrastructure/WebApi).
- Docker Compose (API + Postgres); EF Core migrations + seed; Swagger live.
- GitHub Actions: build/test/lint, build images, deploy to GCE e2-micro; Caddy TLS.
- **Exit:** "Hello secured endpoint" deployed; `swagger.json` → generated TS types; health checks green.

### Phase 1 — Identity, Tenancy & RBAC
- Users, roles, permissions, refresh-token auth, forgot/change password.
- Tenant + Business creation; `user_businesses` membership; business switcher; `X-Business-Id`
  middleware; global query filter; audit logging.
- **Exit:** an Owner can register a tenant, create a business, invite a user with a role, and the
  RBAC matrix is enforced end-to-end (web + API).

### Phase 2 — Common modules
- Employees (+ salary history, attendance), Expenses (+ types, attachments), Customers (+ ledger,
  collections, outstanding), Dashboard summary, Cloud Storage file upload.
- **Exit:** dashboard KPIs populate from real common-module data for any business type.

### Phase 3 — Goods Transport
- Vehicle & Driver masters; Load entry with server-authoritative + live client profit; Credits
  with payments; transport reports (daily, vehicle/driver-wise, monthly/yearly, outstanding).
- **Exit:** a transport business is fully operable; profit and outstanding reconcile.

### Phase 4 — Electronics & CCTV
- Item master (+ stock), Suppliers, PO with state machine + approval + receive (stock in),
  Sales & installation (lines + charges + tax), Service complaints (Kanban), CCTV reports.
- **Exit:** PO→approve→receive updates stock; sale reduces stock; service lifecycle works.

### Phase 5 — Farm Management
- Batches, Feed master + entries, Medical, Batch expenses, Batch sale, Wallet, `v_farm_batch_pnl`.
- **Exit:** batch P&L matches manual calculation; wallet balance reconciles with transactions.

### Phase 6 — Coconut Business
- Products, Batch purchase, Labour & Transport charges, Batch sales, `v_coconut_batch_pnl`.
- **Exit:** product-wise and batch profit reports correct.

### Phase 7 — Accounting & Reporting
- Journal/ledger postings from each financial event; Cash Book, Ledger, P&L; reporting engine
  (QuestPDF for PDF, ClosedXML for Excel) with daily/weekly/monthly/yearly/custom filters; async
  export with signed-URL download.
- **Exit:** every money event posts a balanced journal entry; P&L ties out to module reports.

### Phase 8 — Mobile offline & push
- WatermelonDB local store, outbox + `/sync/push` + `/sync/pull`, idempotency, conflict policy,
  camera upload, FCM push, invoice/report PDF download on device.
- **Exit:** full offline data entry for loads/expenses/service; reliable sync after reconnect.

### Phase 9 — Hardening & UAT
- Security review (auth, RBAC, isolation), load/perf test to NFR targets, accessibility pass,
  backup/restore drill, UAT sign-off, production cutover + runbook.
- **Exit:** go-live checklist complete; rollback plan verified.

## 4. Cross-cutting workstreams (run every phase)

- **Testing:** Domain/Application unit tests (≥80% on money logic), integration tests with
  Testcontainers, web component + e2e, mobile e2e (Detox) from Phase 8.
- **Security:** dependency scanning, secret scanning, periodic RBAC/isolation review.
- **Docs:** keep this `docs/` package + Swagger current; changelog per release.
- **Observability:** structured logs, correlation IDs, health checks, audit log from Phase 1.

## 5. Milestones & demos

| Milestone | After phase | Demo |
|-----------|-------------|------|
| M1 — Platform live | 1 | Multi-business login + RBAC |
| M2 — Operations base | 2 | Dashboard + common modules |
| M3 — First vertical | 3 | Transport end-to-end |
| M4 — All verticals | 6 | All four businesses operable |
| M5 — Books & reports | 7 | Accounting + PDF/Excel exports |
| M6 — Mobile GA | 8 | Offline Android app |
| M7 — v1 GA | 9 | Production go-live |

## 6. Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Free-tier resource limits (e2-micro) | Perf under load | Keep Postgres lean; cache dashboards; clear path to Cloud Run + Cloud SQL |
| Offline sync conflicts | Data integrity | Idempotent clientUuid writes; LWW + server authority; block offline edits of settled money |
| Scope creep across 4 verticals | Schedule | Vertical isolation; ship one at a time; defer GST e-invoice/SMS |
| Money calculation drift | Trust | Single source of truth in Domain + generated columns/views; ≥80% test coverage |
| Multi-tenant leak | Severe | Defense-in-depth isolation (API + app + ORM + DB), reviewed in Phase 9 |

## 7. Definition of Done (per feature)

- Command/Query + validator + handler + tests; DTO in Swagger; TS types regenerated.
- Web screen (responsive) + mobile screen where applicable; permission-gated.
- Audit logging; soft delete; tenant-scoped; error codes documented.
- Green CI; deployed to staging; product owner accepted.

## 8. Immediate next step (post sign-off)

Scaffold **Phase 0**: create the monorepo skeleton, the four .NET projects with Clean
Architecture wiring, `docker-compose` (API + Postgres), the initial EF migration from
[05-database-scripts.sql](05-database-scripts.sql), and the CI/CD workflow — yielding the
walking skeleton (secured `/me` endpoint + login screen) deployed to the GCE free-tier VM.

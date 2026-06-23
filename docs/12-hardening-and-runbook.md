# 12 · Hardening, Security Review & Go-Live Runbook (Phase 9)

Status of the platform at the end of Phase 9, the operational runbook, and the go-live checklist.

## 1. Security review

| Area | Status | Notes |
|------|--------|-------|
| Password storage | ✅ | PBKDF2-HMAC-SHA256, 100k iters, per-user salt, fixed-time compare |
| Access tokens | ✅ | JWT HS256, 15-min expiry, validated issuer/audience/lifetime/signature |
| Refresh tokens | ✅ | Opaque, hashed at rest, 30-day rolling, **rotated** on use; revoked on password change |
| Signing key guard | ✅ | App refuses to start if `Jwt:SigningKey` < 32 chars |
| RBAC | ✅ | `[HasPermission]` on every command/query; resolved per-business from membership → role → permissions |
| Tenant isolation | ✅ | EF global query filter on `business_id`; insert-time stamping; **verified by integration test** (`Tenants_are_isolated`) |
| Idempotent writes | ✅ | `Idempotency-Key` dedupe (per business); **verified by integration test** |
| Rate limiting | ✅ | Global 300/min per user/IP; `/auth/*` 20/min (brute-force defense) |
| Security headers | ✅ | `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, cross-domain-policies; HSTS via TLS proxy |
| Audit trail | ✅ | Append-only `audit_logs` (create/update/delete, JSON diffs; secrets excluded) |
| Soft delete | ✅ | Financial/business rows never hard-deleted |
| Transport security | ⚙️ infra | TLS terminated at Caddy/Let's Encrypt (see docs/01); enforce HTTPS at the proxy |
| Secrets | ⚙️ ops | Prod `Jwt:SigningKey` + DB connection via env / GCP Secret Manager — never in source |
| CORS | ✅ | Allow-list of known SPA/Expo origins |

### Known gaps / follow-ups (documented, not blockers for pilot)
- **GL auto-posting coverage**: journals auto-post for expenses; extend to load/sale/batch revenue using the same `IJournalService` pattern. Statements are derived from primitives and tie out today.
- **Forgot/reset password** delivery (email/SMS) is scaffolded conceptually but not wired to a provider.
- **Mobile app** is an authored scaffold, not build/run-verified (see `apps/mobile/README.md`).
- **Attachment AV scanning** + signed-URL expiry tuning for Cloud Storage.
- **Field-level encryption** for PII is not applied (disk encryption only).

## 2. Test coverage at go-live

| Suite | What it proves | Count |
|-------|----------------|-------|
| `ERP.UnitTests` | Domain math (load profit, batch P&L, sale totals, PO state machine), money logic, validators, journal balancing, PDF/Excel export, idempotency store | 44 |
| `ERP.IntegrationTests` | Full HTTP pipeline on a real host (InMemory DB): login/401, JWT-protected `/me`, expense create→list (auth + RBAC + GL), idempotency dedupe, **tenant isolation**, security headers | 8 |

> Integration tests use `WebApplicationFactory` + EF InMemory (no Docker needed). Before production,
> add a **Testcontainers (real PostgreSQL)** pass to also cover Postgres-specific SQL
> (jsonb, check constraints, generated/precision columns).

## 3. NFR / load-test plan

Targets (docs/01 §8): API p95 < 400 ms CRUD / < 1.5 s reports; ~50 concurrent users (pilot).

```bash
# Example with k6 (or bombardier). Authenticate, then hammer a read + a write.
k6 run load/dashboard.js          # GET /dashboard/summary at 50 VUs, 5 min
k6 run load/create-load.js        # POST /transport/loads with Idempotency-Key
```
Measure: p50/p95/p99 latency, error rate, DB CPU/connections. Add indexes already cover the
date-range report queries (see docs/04 §9). Tune Npgsql max pool size for the VM.

## 4. Backup & restore drill (Option B — GCE + Postgres in Docker)

**Nightly backup (cron on the VM):**
```bash
# /etc/cron.daily/erp-backup
TS=$(date +%F-%H%M)
docker exec erp-db pg_dump -U postgres -d erp | gzip > /tmp/erp-$TS.sql.gz
gsutil cp /tmp/erp-$TS.sql.gz gs://business-one-backups/
rm /tmp/erp-$TS.sql.gz
# bucket lifecycle rule: delete objects older than 30 days
```

**Restore drill (must be rehearsed before go-live):**
```bash
gsutil cp gs://business-one-backups/erp-<TS>.sql.gz .
gunzip -c erp-<TS>.sql.gz | docker exec -i erp-db psql -U postgres -d erp_restore
# verify row counts on key tables, then repoint the API or rename the DB
```
RPO 24h (nightly dump) · RTO ≤ 2h (provision fresh VM + restore).

## 5. Deploy & rollback runbook

**Deploy (CI/CD → GCE):**
1. CI builds + tests (`dotnet test`), builds the API image, tags `erp-api:<git-sha>`, pushes.
2. SSH to the VM; `docker compose pull api && docker compose up -d api`.
3. Migrations: `Database:AutoMigrate=true` applies pending EF migrations on startup (idempotent),
   or run `dotnet ef database update` out-of-band for control.
4. Smoke test: `curl https://<host>/health/ready` → 200; log in via Swagger; check a dashboard.

**Rollback:**
1. `docker compose up -d` with the previous image tag (`erp-api:<previous-sha>`).
2. If a migration is incompatible, restore the pre-deploy `pg_dump` (migrations are
   forward-only; keep each release's backup).
3. Confirm `/health/ready` + a read endpoint.

> Health: `/health/live` (process up) and `/health/ready` (DB reachable) back the load balancer
> and the deploy smoke test.

## 6. Accessibility (web)

- Semantic HTML via MUI components; keyboard-navigable forms; visible focus states.
- Color contrast on KPI cards/labels meets WCAG AA (verify with axe DevTools before go-live).
- This is a pilot checklist item, not yet automated.

## 7. Go-live checklist

- [ ] Production `Jwt:SigningKey` (≥32 chars, random) set via secret store; dev key not present.
- [ ] DB connection string via env/secret; strong DB password.
- [ ] TLS enforced at the proxy; HSTS on; HTTP→HTTPS redirect.
- [ ] `Database:AutoMigrate` strategy chosen; initial migration applied; seed reviewed (remove demo users for prod).
- [ ] Nightly backup cron live; **restore drill rehearsed**.
- [ ] Rate limits reviewed for expected load; CORS origins set to production hosts.
- [ ] `/health/ready` wired to monitoring/uptime alerts; log shipping to Cloud Logging.
- [ ] CI green (unit + integration); image tagged with git SHA; rollback tag known.
- [ ] UAT sign-off from the business owner on each vertical's core flow.
- [ ] Runbook (this doc) shared with whoever operates the VM.

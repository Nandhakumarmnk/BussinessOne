# 10 · Security, Authentication & RBAC

## 1. Authentication

- **Password storage:** Argon2id (or PBKDF2-HMAC-SHA256, ≥100k iters) with per-user salt. Never
  plaintext, never reversible.
- **JWT access token:** 15-minute lifetime, signed HS256 (pilot) → RS256 later for key rotation.
  Claims: `sub` (userId), `tenant` (tenantId), `sa` (isSuperAdmin), `jti`, `exp`, `iat`.
- **Refresh token:** opaque random 256-bit, stored **hashed** in `refresh_tokens`, 30-day rolling,
  **rotated** on each use; reuse of a rotated token revokes the whole token family (theft defense).
- **Forgot/reset:** time-boxed single-use token in `password_reset_tokens` (15-min TTL), delivered
  by email/SMS; reset invalidates all refresh tokens.
- **Change password:** requires current password; invalidates other sessions optionally.
- **Brute-force protection:** rate limit `/auth/*` (e.g. 10/min/IP) + progressive lockout after N
  failures; log to `audit_logs` (action `login`).

## 2. Permissions are resolved per request (not baked into the JWT)

Because a user's rights differ **per business**, the JWT only proves identity. For each request:

```
1. Validate JWT → userId, tenantId, isSuperAdmin
2. Read X-Business-Id → confirm a row in user_businesses(user, business) exists
   (else 403 tenant.not_a_member)
3. Resolve role → role_permissions → permission set (cached in memory ~60s per user+business)
4. AuthorizationBehavior checks the request's required permission against that set
```

Super Admin (`sa = true` + `platform.read.all`) bypasses the business-membership check and the
EF global query filter for read operations.

## 3. Role → permission matrix

Legend: ● full · ◐ own/limited · — none. Permissions use `module.action`.

| Permission (module.action)       | SuperAdmin | Owner | Manager | Employee | Driver | Labour |
|----------------------------------|:--:|:--:|:--:|:--:|:--:|:--:|
| platform.read.all                | ● | — | — | — | — | — |
| tenant/business.manage           | ● | ● | — | — | — | — |
| business.members.manage          | ● | ● | ◐ | — | — | — |
| dashboard.view                   | ● | ● | ● | ◐ | ◐ | — |
| employee.manage                  | ● | ● | ● | — | — | — |
| employee.attendance.mark         | ● | ● | ● | ◐ | — | — |
| expense.manage                   | ● | ● | ● | ◐ | — | — |
| customer.manage                  | ● | ● | ● | ◐ | — | — |
| customer.collection.record       | ● | ● | ● | ◐ | — | — |
| transport.vehicle.manage         | ● | ● | ● | — | — | — |
| transport.driver.manage          | ● | ● | ● | — | — | — |
| transport.load.create            | ● | ● | ● | ● | ◐ | — |
| transport.load.view              | ● | ● | ● | ● | ◐ | — |
| transport.credit.manage          | ● | ● | ● | — | — | — |
| cctv.item.manage                 | ● | ● | ● | — | — | — |
| cctv.po.create                   | ● | ● | ● | ◐ | — | — |
| cctv.po.approve                  | ● | ● | ● | — | — | — |
| cctv.sale.create                 | ● | ● | ● | ● | — | — |
| cctv.service.manage              | ● | ● | ● | ● | — | — |
| farm.batch.manage                | ● | ● | ● | ◐ | — | — |
| farm.feed.record                 | ● | ● | ● | ● | — | ◐ |
| farm.medical.record              | ● | ● | ● | ● | — | — |
| farm.wallet.manage               | ● | ● | ● | — | — | — |
| coconut.batch.manage             | ● | ● | ● | ◐ | — | — |
| coconut.charge.record            | ● | ● | ● | ● | — | ◐ |
| accounting.view                  | ● | ● | ● | — | — | — |
| report.generate                  | ● | ● | ● | ◐ | ◐ | — |
| user.manage                      | ● | ● | ◐ | — | — | — |

> ◐ for Driver/Labour means: only their own records (e.g. a driver sees only loads where
> `driver_id` = their linked driver, enforced by an extra row filter in the query handler).
> Labour is primarily a *cost record*; app access is optional in v1.

## 4. Multi-tenant data isolation (defense in depth)

| Layer | Control |
|-------|---------|
| API | `X-Business-Id` validated against `user_businesses` membership |
| App | `AuthorizationBehavior` + per-handler row filters (own-records for Driver/Labour) |
| ORM | EF Core **global query filter** `business_id = current` on all `IBusinessScoped` entities |
| ORM | `SaveChanges` interceptor **stamps** `business_id` on insert (handlers can't forget) |
| DB | FKs + tenant-scoped unique constraints (`unique(business_id, code)`) |
| DB (optional) | PostgreSQL Row-Level Security as a final backstop for direct DB access |

Cross-business reads are impossible through the API except for Super Admin via the explicit
`IgnoreQueryFilters()` path guarded by `platform.read.all`.

## 5. Authorization in code

```csharp
[HasPermission(Permissions.Transport.LoadCreate)]
public record CreateLoadCommand(...) : IRequest<Result<LoadDto>>;

// Pipeline behavior
public class AuthorizationBehavior<TReq,TRes> : IPipelineBehavior<TReq,TRes>
{
    public async Task<TRes> Handle(TReq req, RequestHandlerDelegate<TRes> next, CancellationToken ct)
    {
        var perm = typeof(TReq).GetCustomAttribute<HasPermissionAttribute>()?.Permission;
        if (perm is not null && !await _current.HasPermissionAsync(perm, ct))
            throw new ForbiddenException("auth.forbidden", perm);
        return await next();
    }
}
```

Controllers also carry `[Authorize]`; method-level permission checks live in the behavior so the
rule is enforced regardless of entry point.

## 6. Data protection & privacy

- **In transit:** TLS 1.2+ everywhere (Caddy/Let's Encrypt). HSTS enabled.
- **At rest:** disk encryption on the VM/Cloud SQL; DB backups encrypted in Cloud Storage.
- **Secrets:** GCP Secret Manager / VM env file; never in source. Rotated periodically.
- **PII:** mobiles/addresses minimized in logs; audit logs store value diffs, not secrets.
- **Attachments:** private bucket; access only via short-lived signed URLs.
- **Soft delete + audit:** financial records are never hard-deleted; `audit_logs` is append-only.

## 7. Threats considered (lightweight STRIDE)

| Threat | Mitigation |
|--------|-----------|
| Spoofing | JWT + refresh rotation; password hashing; rate-limited auth |
| Tampering | Server-authoritative calculations (profit/P&L); generated columns; checks |
| Repudiation | `audit_logs` with user, action, before/after, IP, timestamp |
| Info disclosure | Tenant query filter + membership checks + signed URLs; least-privilege RBAC |
| DoS | Rate limiting; request size limits; pagination caps |
| Elevation | Permission resolved server-side per business; Super Admin path explicitly gated |

## 8. Compliance posture (pilot)

- GST numbers captured for invoices; statutory filing/e-invoice out of scope v1.
- Right-to-be-forgotten handled via anonymization routine (keeps financial integrity).
- Backup retention 30 days; configurable per tenant later.

# 06 · API Design

REST over HTTPS, JSON, versioned under `/api/v1`. Auth via `Authorization: Bearer <jwt>`.
Tenant/business context via `X-Business-Id: <uuid>` header (required for all business-scoped
endpoints; ignored for auth and platform endpoints).

## 1. Conventions

| Aspect | Rule |
|--------|------|
| Base URL | `/api/v1` |
| Resource naming | plural nouns, kebab/lowercase (`/transport/loads`) |
| Verbs | `GET` list/read, `POST` create, `PUT` full update, `PATCH` partial, `DELETE` soft-delete |
| IDs | UUID in path: `/transport/loads/{id}` |
| Tenant scope | `X-Business-Id` header (validated against caller's memberships) |
| Pagination | `?page=1&pageSize=20&sort=-loadDate` → `PagedResult<T>` |
| Filtering | query params, e.g. `?from=2026-06-01&to=2026-06-30&customerId=…` |
| Idempotency | `Idempotency-Key` header (UUID) for mobile-originated writes |
| Errors | RFC-7807-style envelope (see [07](07-swagger-and-conventions.md)) |
| Dates | ISO-8601; date-only fields `yyyy-MM-dd` |

## 2. Standard envelopes

```jsonc
// Success (single)
{ "data": { ... }, "meta": { "correlationId": "..." } }

// Success (paged list)
{ "data": [ ... ],
  "meta": { "page": 1, "pageSize": 20, "total": 137, "totalPages": 7 } }

// Error
{ "error": { "code": "credit.limit_exceeded",
             "message": "Customer credit limit exceeded",
             "details": [ { "field": "loadAmount", "message": "..." } ],
             "correlationId": "..." } }
```

## 3. Authentication & account

| Method | Path | Body | Notes |
|--------|------|------|-------|
| POST | `/auth/login` | `{ mobileOrEmail, password }` | → access + refresh tokens, memberships |
| POST | `/auth/refresh` | `{ refreshToken }` | rotate refresh token |
| POST | `/auth/logout` | `{ refreshToken }` | revoke refresh token |
| POST | `/auth/forgot-password` | `{ mobileOrEmail }` | sends reset link/OTP |
| POST | `/auth/reset-password` | `{ token, newPassword }` | |
| POST | `/auth/change-password` | `{ currentPassword, newPassword }` | authed |
| GET | `/me` | — | profile + memberships + permissions |
| GET | `/me/businesses` | — | businesses the caller can access (for switcher) |

### Login response (shape)
```jsonc
{ "data": {
    "accessToken": "ey...", "expiresIn": 900,
    "refreshToken": "ey...",
    "user": { "id":"...", "fullName":"...", "isSuperAdmin": false },
    "memberships": [
      { "businessId":"...", "businessName":"Sri Transport",
        "businessTypeCode":"TRANSPORT", "role":"OWNER",
        "permissions":["dashboard.view","transport.load.create", ...] }
    ] } }
```

## 4. Platform / Super Admin

| Method | Path | Notes |
|--------|------|-------|
| GET/POST | `/admin/tenants` | list/create tenants |
| GET/POST | `/admin/users` | platform user management |
| GET | `/admin/business-types` | reference list |

## 5. Businesses & users (tenant-scoped)

| Method | Path | Notes |
|--------|------|-------|
| GET/POST | `/businesses` | list owned / create a business (choose type) |
| GET/PUT/DELETE | `/businesses/{id}` | |
| GET/POST | `/businesses/{id}/members` | assign user + role to business |
| DELETE | `/businesses/{id}/members/{userId}` | revoke membership |
| GET/POST | `/users` | create users within tenant |
| GET | `/roles`, `/permissions` | reference lists |

## 6. Common modules (require `X-Business-Id`)

### Dashboard
| GET | `/dashboard/summary?date=2026-06-23` | today/month income, expense, profit, pending credits & collections |

### Employees
| GET/POST | `/employees` · GET/PUT/DELETE | `/employees/{id}` |
| GET/POST | `/employees/{id}/salary` | salary history; POST records a month |
| GET | `/employees/{id}/salary/report?year=2026` | monthly salary report |
| GET/POST | `/employees/{id}/attendance` · `?month=2026-06` | attendance |
| GET | `/reports/salary?month=2026-06` | monthly salary report (all employees) |

### Expenses
| GET/POST | `/expenses` (filter `from,to,typeId`) · GET/PUT/DELETE `/expenses/{id}` |
| GET/POST | `/expense-types` |
| GET | `/reports/expenses?period=daily|monthly|yearly&from&to` |
| POST | `/expenses/{id}/attachment` | upload receipt → returns object key |

### Customers
| GET/POST | `/customers` · GET/PUT/DELETE `/customers/{id}` |
| GET | `/customers/{id}/ledger?from&to` | customer ledger |
| GET | `/customers/{id}/outstanding` | outstanding balance |
| GET/POST | `/customers/{id}/collections` | record/track collections |
| GET | `/reports/outstanding` | all customers outstanding |

## 7. Business 1 — Goods Transport

| Method | Path | Notes |
|--------|------|-------|
| GET/POST | `/transport/vehicles` · GET/PUT/DELETE `/{id}` | vehicle master |
| GET/POST | `/transport/drivers` · GET/PUT/DELETE `/{id}` | driver master |
| GET/POST | `/transport/loads` · GET/PUT/DELETE `/{id}` | load entry; response includes computed `profit` |
| GET | `/transport/loads/{id}/profit` | profit breakdown |
| GET/POST | `/transport/credits` · PATCH `/{id}/payment` | credit mgmt; payment reduces balance |
| GET | `/transport/reports/daily-income?date=` | |
| GET | `/transport/reports/daily-expense?date=` | |
| GET | `/transport/reports/vehicle-profit?from&to` | vehicle-wise profit |
| GET | `/transport/reports/driver-profit?from&to` | driver-wise profit |
| GET | `/transport/reports/profit?period=monthly|yearly` | |
| GET | `/transport/reports/outstanding` | |

### Create load (request)
```jsonc
POST /api/v1/transport/loads      (X-Business-Id, Idempotency-Key)
{ "loadNumber":"LD-0007","loadName":"Cement",
  "customerId":"...","vehicleId":"...","driverId":"...",
  "source":"Coimbatore","destination":"Salem","loadDate":"2026-06-23",
  "loadAmount":18000,"loadmanCharges":800,"fuelExpense":4200,
  "maintenanceExpense":600,"driverCharges":1500,"otherExpense":300 }
// → data.profit = 18000 - (800+4200+600+1500+300) = 10600
```

## 8. Business 2 — Electronics & CCTV

| GET/POST | `/cctv/items` · GET/PUT/DELETE `/{id}` | item master |
| GET/POST | `/cctv/suppliers` · `/{id}` | |
| GET/POST | `/cctv/purchase-orders` · `/{id}` | PO with lines |
| POST | `/cctv/purchase-orders/{id}/submit` | draft → pending |
| POST | `/cctv/purchase-orders/{id}/approve` | pending → approved (perm `cctv.po.approve`) |
| POST | `/cctv/purchase-orders/{id}/receive` | approved → received (stock +) |
| GET/POST | `/cctv/sales` · `/{id}` | sales & installation (lines + charges + tax) |
| GET/POST | `/cctv/service-complaints` · `/{id}` | |
| PATCH | `/cctv/service-complaints/{id}/status` | open→in_progress→closed |
| GET | `/cctv/reports/item-sales?from&to` | item-wise sales |
| GET | `/cctv/reports/service` | service report |
| GET | `/cctv/reports/employee-performance?from&to` | |
| GET | `/cctv/reports/revenue?period=monthly|yearly` | |
| GET | `/cctv/reports/credit-outstanding` | |

## 9. Business 3 — Farm Management

| GET/POST | `/farm/batches` · `/{id}` | batch management |
| GET | `/farm/batches/{id}/pnl` | profit/loss (from `v_farm_batch_pnl`) |
| GET/POST | `/farm/feeds` · `/{id}` | feed master |
| GET/POST | `/farm/batches/{id}/feed-entries` | feed consumption |
| GET/POST | `/farm/batches/{id}/medical` | medical records |
| GET/POST | `/farm/batches/{id}/expenses` | batch expense tracking |
| GET/POST | `/farm/batches/{id}/sales` | batch sale |
| GET | `/farm/wallet` · POST `/farm/wallet/transactions` | wallet add/use |
| GET | `/farm/reports/batch-profit?from&to` | |
| GET | `/farm/reports/feed-consumption?batchId` | total feed kg + expense |
| GET | `/farm/reports/medical-expense?from&to` | |
| GET | `/farm/reports/profit?period=monthly|yearly` | |

## 10. Business 4 — Coconut Business

| GET/POST | `/coconut/products` · `/{id}` | product master |
| GET/POST | `/coconut/batches` · `/{id}` | batch purchase |
| GET | `/coconut/batches/{id}/pnl` | profit (from `v_coconut_batch_pnl`) |
| GET/POST | `/coconut/batches/{id}/labour-charges` | |
| GET/POST | `/coconut/batches/{id}/transport-charges` | |
| GET/POST | `/coconut/batches/{id}/sales` | |
| GET | `/coconut/reports/profit?period=daily|monthly|yearly` | |
| GET | `/coconut/reports/product-profit?from&to` | product-wise profit |

## 11. Accounting (cross-business, scoped)

| GET | `/accounting/cash-book?from&to` | cash in/out timeline |
| GET | `/accounting/ledger?accountId&from&to` | ledger entries |
| GET | `/accounting/profit-loss?from&to` | P&L statement |
| GET | `/accounting/income?from&to` · `/accounting/expense?from&to` | |
| GET | `/accounting/credit-tracking` · `/accounting/collection-tracking` | |

## 12. Reporting (generation)

| POST | `/reports/export` | `{ reportKey, format: "pdf"|"excel", filters }` → returns download URL (Cloud Storage signed URL) |
| GET | `/reports/{jobId}` | poll status for large reports (async) |

`reportKey` examples: `transport.vehicle-profit`, `farm.batch-profit`, `accounting.profit-loss`,
`expenses.monthly`, `customer.outstanding`. Filters accept `period` + `from`/`to` + module ids.

## 13. Files & sync

| POST | `/files` (multipart) | upload; returns `{ objectKey }` |
| GET | `/files/{objectKey}/url` | signed download URL |
| POST | `/sync/push` | batch of offline mutations (idempotent by clientUuid) |
| GET | `/sync/pull?since=<cursor>&businessId=` | changed records since cursor |

## 14. HTTP status usage

| Status | When |
|--------|------|
| 200 | OK (read/update) |
| 201 | Created (with `Location` header) |
| 204 | Deleted / no content |
| 400 | Malformed request |
| 401 | Missing/invalid token |
| 403 | Authenticated but lacks permission / wrong business |
| 404 | Resource not found (or filtered by tenant) |
| 409 | Conflict (duplicate code, invalid state transition) |
| 422 | Domain rule violation (validation envelope) |
| 429 | Rate limited |
| 500 | Unexpected |

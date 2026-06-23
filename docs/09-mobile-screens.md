# 09 · Mobile Screens & Offline Sync (React Native / Android)

The mobile app targets **field use**: drivers logging loads, employees logging service visits,
farm staff recording feed/medical, with **offline-first** data entry and background sync. Built
with Expo + React Native, sharing `@erp/types`, `@erp/api-client`, `@erp/domain` with web.

## 1. Navigation (bottom tabs, role-aware)

```
┌──────────────────────────────┐
│  Sri Transport         🔄 ▲  │  ← active business + sync status (▲ pending, ✓ synced)
│                              │
│        < screen >            │
│                              │
├──────┬──────┬──────┬─────────┤
│ 🏠   │ ➕   │ 📋   │  ☰      │
│ Home │ Add  │ List │ More    │
└──────┴──────┴──────┴─────────┘
```

Tabs adapt to role/business type: a Driver sees Home + My Loads; an Owner sees full menu under
"More" (Reports, Accounting, Switch Business).

## 2. Login & business switch

```
┌──────────────────────┐     ┌──────────────────────┐
│   Business One       │     │  Switch Business     │
│  Mobile  [________]  │     │ ◉ Sri Transport      │
│  Password[________]  │     │ ○ CCTV Shop          │
│  [   Log in    ]     │     │ ○ Green Farm         │
│  Forgot password?    │     │ [    Continue    ]   │
└──────────────────────┘     └──────────────────────┘
Token + memberships cached securely (Expo SecureStore). Works offline after first login
until refresh token expires.
```

## 3. Home (dashboard, compact)

```
┌──────────────────────────────┐
│ Today                        │
│ ┌────────────┐ ┌───────────┐ │
│ │Income ₹18k │ │Exp  ₹7.4k │ │
│ └────────────┘ └───────────┘ │
│ Month Profit       ₹2.35L    │
│ Pending Credits    ₹96,500   │
│ ── Quick actions ──          │
│ [ + Load ] [ + Expense ]     │
│ [ + Collection ]             │
│ 🔄 3 items waiting to sync   │
└──────────────────────────────┘
```

## 4. Add Load (offline-capable, live profit)

```
┌──────────────────────────────┐
│ ← New Load            [Save]  │
│ Load No   [LD-0007]          │
│ Customer  [ Ramraj      ▼ ]  │  ← pickers backed by local cache
│ Vehicle   [ TN01AB1234  ▼ ]  │
│ Driver    [ Kumar       ▼ ]  │
│ Source    [ Coimbatore   ]   │
│ Dest      [ Salem        ]   │
│ Amount    [ 18000 ]          │
│ Fuel [4200] Maint [600]      │
│ Driver[1500] Loadman[800]    │
│ Other [300]                  │
│ ─────────────────────────    │
│ Profit:        ₹ 10,600      │  ← computed locally (same formula as server)
│ 📷 Attach LR/photo           │
│ Saved offline → will sync ▲  │
└──────────────────────────────┘
```

## 5. Camera upload

```
┌──────────────────────────────┐
│  Attach                      │
│  [ 📷 Take Photo ]           │
│  [ 🖼  Choose from Gallery ] │
│  thumbnails: [img][img] +    │
│  Stored locally; uploaded to │
│  Cloud Storage on sync; DB   │
│  keeps object key only.      │
└──────────────────────────────┘
```

## 6. Lists & detail (My Loads / Service jobs)

```
┌──────────────────────────────┐
│ My Loads     [Filter ▾] 🔄   │
│ ┌──────────────────────────┐ │
│ │ LD-0007  Salem   ₹10,600 │ │  ▲ pending sync badge per row
│ │ 23-Jun · Profit ✓        │ │
│ ├──────────────────────────┤ │
│ │ LD-0006  Erode   ₹ 8,200 │ │
│ │ 22-Jun · synced ✓        │ │
│ └──────────────────────────┘ │
└──────────────────────────────┘
```

## 7. Service job (CCTV employee)

```
┌──────────────────────────────┐
│ ← #C-12  Hotel Blue          │
│ Issue: Camera 3 no signal    │
│ Status [ In Progress ▼ ]     │
│ Notes  [____________]        │
│ 📷 before/after photos       │
│ [ Update ]                   │
└──────────────────────────────┘
```

## 8. Reports & invoice PDF

```
┌──────────────────────────────┐
│ Reports                      │
│ [ Daily Income        ] →    │
│ [ Vehicle Profit      ] →    │
│ [ Outstanding         ] →    │
│ Generated PDFs open in       │
│ system viewer / share sheet. │
│ Invoice → [ Download PDF ]   │
└──────────────────────────────┘
```

## 9. Offline-first architecture

```
        UI (forms)                        Sync engine (background)
           │ write                               │
           ▼                                      ▼
   ┌─────────────────┐   queue   ┌──────────────────────────┐    HTTPS
   │ WatermelonDB    │──────────▶│ Outbox (pending mutations│───────────▶  POST /sync/push
   │ (local SQLite)  │           │  with clientUuid + body) │              (Idempotency-Key)
   └─────────────────┘◀──────────└──────────────────────────┘◀───────────  GET  /sync/pull?since=
        ▲   read                         apply server changes
        │
   screens render from local store first (instant), reconcile after sync
```

### Write path (offline)
1. User saves → record written to **WatermelonDB** with a client-generated `clientUuid` and
   `_status = pending`. UI updates instantly (optimistic).
2. The mutation (entity, payload, clientUuid) is appended to the **outbox**.
3. When online, the sync engine POSTs the outbox to `/sync/push` with `Idempotency-Key`.
4. Server applies idempotently (dedupe via `sync_client_requests`), returns server ids.
5. Local records get their `serverId`, `_status = synced`; outbox entries cleared.

### Read path (pull)
- On reconnect / pull-to-refresh / periodic, `GET /sync/pull?since=<cursor>&businessId=` returns
  records changed since the last cursor; local store upserts them; cursor advances.

### Conflict resolution
- **Last-write-wins by `updated_at`** for master data, with the server as the authority.
- Transactional records (loads, sales) are **append-mostly** and keyed by `clientUuid`, so
  conflicts are rare; on the rare update conflict, server value wins and the client is notified
  with a non-blocking toast ("Load LD-0007 was updated on the server").
- Money-affecting edits to already-synced records require connectivity (no offline edit of a
  settled credit), preventing divergent financial state.

### What is cached locally (per active business)
- Pickers: customers, vehicles, drivers, items, feeds, products (small master sets).
- The user's own recent transactions (last N days) for offline lists.
- Pending outbox + attachments staged in local file system until uploaded.

## 10. Push notifications (FCM)

| Trigger | Audience |
|---------|----------|
| PO awaiting approval | Manager/Owner |
| Service complaint assigned | Assigned employee |
| Credit overdue / collection reminder | Owner/Manager |
| Insurance expiry (vehicle) | Owner |
| Sync completed / failed | Acting user (silent/in-app) |

Device tokens registered on login; topics per business + role. Tapping a notification deep-links
to the relevant screen.

## 11. Platform & build notes

- **Android** primary (per brief). Expo EAS build → APK/AAB. iOS possible later from same code.
- Min Android 8 (API 26). Permissions: camera, storage (scoped), notifications.
- Secure storage for tokens (Expo SecureStore / Keystore).
- Crash/analytics via Sentry (optional) — no PII in logs.

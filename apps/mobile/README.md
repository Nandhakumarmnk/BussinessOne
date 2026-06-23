# @erp/mobile — React Native (Expo) app

Offline-first Android app for field staff (drivers, employees). Shares the API contract with the
web app; the offline-sync design is in [docs/09](../../docs/09-mobile-screens.md).

> **Status: authored scaffold, not yet built/run.** This was written without an Expo toolchain or
> device/emulator available, so it is **not runtime-verified**. The backend sync contract it depends
> on (`Idempotency-Key` dedupe + `GET /api/v1/sync/pull`) *is* built and tested in the .NET solution.

## What's implemented

| Area | File | Notes |
|------|------|-------|
| API client | `src/api.ts` | token + `X-Business-Id` + `Idempotency-Key` headers; typed methods |
| Offline outbox + sync | `src/offline.ts` | durable queue (AsyncStorage); replays writes idempotently, then pulls masters |
| Session | `src/session.ts` | tokens + active business in Expo SecureStore |
| Push | `src/services.ts` | FCM device-token registration via expo-notifications |
| Screens | `src/screens.tsx` | Login, Home (KPIs + sync), Add Expense (offline) |
| App shell | `App.tsx`, `index.ts` | bootstrap + simple screen routing |

### How the offline loop works
1. **Add Expense** while offline → enqueued in the outbox with a generated `id` (used as the
   `Idempotency-Key`). The UI confirms "saved offline".
2. **Sync now** (or on reconnect) → `runSync()` replays each queued write to its normal endpoint
   (`POST /expenses`) carrying that key. The backend `IdempotencyFilter` dedupes, so a retried
   request returns the original response instead of double-posting.
3. Then it calls `GET /sync/pull?since=<cursor>` to refresh cached picker masters (customers,
   vehicles, items, …) for offline data entry, and advances the cursor.

## Divergences from docs/09 (deliberate, for the scaffold)
- Uses **AsyncStorage** for the outbox/cache instead of **WatermelonDB** (the documented production
  target) to stay Expo-managed and dependency-light. The outbox abstraction makes swapping the store
  straightforward later.
- Camera capture is a documented integration point in `src/services.ts` (capture → `POST /files` →
  store object key), not yet wired into a screen.

## Run it (once Expo tooling is available)

```bash
cd apps/mobile
npm install
npx expo start            # press 'a' for Android emulator, or scan in Expo Go
npm run typecheck         # tsc --noEmit
```

The API base URL defaults to `http://10.0.2.2:8080/api/v1` (Android emulator → host machine);
override via `app.json` → `expo.extra.apiBaseUrl`.

## Before production
- Swap AsyncStorage outbox → WatermelonDB; add conflict resolution per docs/09.
- Wire camera attachment upload; finish push deep-links; add refresh-token rotation on 401.
- Add Detox e2e tests; build via EAS.

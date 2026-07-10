# @erp/mobile — React Native (Expo) app

Offline-first Android app for field staff (drivers, employees). Shares the API contract with the
web app; the offline-sync design is in [docs/09](../../docs/09-mobile-screens.md).

> **Status: type-checked (`tsc --noEmit` clean), not yet run on a device.** Written without an
> emulator available, so it is **not runtime-verified** — run it on a device/emulator before a pilot
> (steps below). The backend contract it depends on (`Idempotency-Key` dedupe, `GET /api/v1/sync/pull`,
> `POST /api/v1/files`) *is* built and tested in the .NET solution.

## What's implemented

| Area | File | Notes |
|------|------|-------|
| API client | `src/api.ts` | token + `X-Business-Id` + `Idempotency-Key` headers; typed methods; multipart `uploadFile` |
| Offline outbox + sync | `src/offline.ts` | durable queue (AsyncStorage); replays writes idempotently, then pulls masters |
| Session | `src/session.ts` | tokens + active business in Expo SecureStore |
| Push | `src/services.ts` | FCM device-token registration via expo-notifications (not wired to a backend yet) |
| Screens | `src/screens.tsx` | Login, Home, Add Expense (**+ camera attachment → Firebase Storage**), Customers, New Load |
| App shell | `App.tsx`, `index.ts` | bootstrap + simple screen routing |

### Camera attachment (Firebase Storage)
**Add Expense → 📷 Attach photo** opens the camera (`expo-camera`), captures a receipt, and uploads it
to `POST /api/v1/files` (which stores it in the Firebase Storage bucket) — the returned object key is
attached to the expense. Uploads are **online-only**: offline, the expense still queues in the outbox
and is saved without the photo.

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
- Master pickers (customer/vehicle/driver on the New Load screen) are simple tap-to-select chips fed
  from the `sync/pull` cache, rather than searchable lists.

## Run it (once Expo tooling is available)

```bash
cd apps/mobile
npm install
npx expo start            # press 'a' for Android emulator, or scan in Expo Go
npm run typecheck         # tsc --noEmit
```

The API base URL is set in `app.json` → `expo.extra.apiBaseUrl`. It defaults to the **deployed API**
(`https://34-122-117-197.sslip.io/api/v1`) so a distributed APK works out of the box. For local
development against a machine-hosted backend, set it to `http://10.0.2.2:8080/api/v1` (Android
emulator → host) or your LAN IP.

## Get an APK from GitHub

CI can build the Android APK — no local Android toolchain or Expo account needed
(`.github/workflows/mobile-apk.yml`):

- **On demand:** GitHub → **Actions → Mobile APK → Run workflow**. Download it from the run's
  **Artifacts** (`business-one-android-apk`).
- **As a release:** push a tag like `mobile-v1.0.0` and the APK is attached to the GitHub Release.

The workflow runs `expo prebuild` + `gradle assembleRelease`; the APK is debug-signed by the Expo
template (installable for pilots). For Play Store, add a release keystore + signing config.
Set the production API URL in `app.json` (`expo.extra.apiBaseUrl`) before building.

## Before production
- Swap AsyncStorage outbox → WatermelonDB; add conflict resolution per docs/09.
- Wire push deep-links + a backend device-token registration endpoint; add refresh-token rotation on 401.
- Add Detox e2e tests; build via EAS. Run on a device to verify the camera/upload flow end-to-end.

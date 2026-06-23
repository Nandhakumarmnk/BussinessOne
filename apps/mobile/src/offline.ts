// Offline-first engine: a durable outbox of pending writes (replayed idempotently on reconnect)
// plus a pull cache of picker masters. Uses AsyncStorage; the production target per docs/09 is
// WatermelonDB, but AsyncStorage keeps the scaffold Expo-managed and dependency-light.

import AsyncStorage from "@react-native-async-storage/async-storage";
import { Api, ApiError, SyncPull, uuid } from "./api";

const OUTBOX_KEY = "erp.outbox";
const CURSOR_KEY = "erp.sync.cursor";
const CACHE_KEY = "erp.sync.cache";

export interface OutboxItem {
  id: string;          // also used as the Idempotency-Key
  method: string;      // "POST"
  path: string;        // e.g. "/expenses"
  body: unknown;
  label: string;       // human-friendly, for the sync screen
  createdAt: string;
}

async function readOutbox(): Promise<OutboxItem[]> {
  const raw = await AsyncStorage.getItem(OUTBOX_KEY);
  return raw ? (JSON.parse(raw) as OutboxItem[]) : [];
}
async function writeOutbox(items: OutboxItem[]): Promise<void> {
  await AsyncStorage.setItem(OUTBOX_KEY, JSON.stringify(items));
}

export const outbox = {
  list: readOutbox,
  async enqueue(method: string, path: string, body: unknown, label: string): Promise<OutboxItem> {
    const item: OutboxItem = { id: uuid(), method, path, body, label, createdAt: new Date().toISOString() };
    const items = await readOutbox();
    items.push(item);
    await writeOutbox(items);
    return item;
  },
  async count(): Promise<number> {
    return (await readOutbox()).length;
  },
};

export interface SyncResult {
  pushed: number;
  failed: number;
  pulled: boolean;
  remaining: number;
}

/**
 * Replays queued writes (each carries its id as the Idempotency-Key, so the server dedupes a
 * retried request), then pulls master changes since the last cursor.
 */
export async function runSync(api: Api): Promise<SyncResult> {
  let pushed = 0;
  let failed = 0;
  const items = await readOutbox();
  const survivors: OutboxItem[] = [];

  for (const item of items) {
    try {
      await api.request(item.method, item.path, item.body, item.id);
      pushed++;
    } catch (err) {
      if (err instanceof ApiError) {
        // A definite HTTP rejection (validation/permission) won't succeed on retry — drop it
        // so the queue can drain; a real app would surface this to the user / a dead-letter list.
        failed++;
      } else {
        // Network error — stop and keep this and the rest for the next attempt.
        survivors.push(item, ...items.slice(items.indexOf(item) + 1));
        break;
      }
    }
  }
  await writeOutbox(survivors);

  let pulled = false;
  try {
    const since = await AsyncStorage.getItem(CURSOR_KEY);
    const data: SyncPull = await api.syncPull(since);
    await AsyncStorage.setItem(CACHE_KEY, JSON.stringify(data));
    await AsyncStorage.setItem(CURSOR_KEY, data.cursor);
    pulled = true;
  } catch {
    // offline — keep the previous cache/cursor
  }

  return { pushed, failed, pulled, remaining: survivors.length };
}

export async function cachedMasters(): Promise<SyncPull | null> {
  const raw = await AsyncStorage.getItem(CACHE_KEY);
  return raw ? (JSON.parse(raw) as SyncPull) : null;
}

// Shared API client factory for Web + Mobile.
//
// Phase 0 scaffold: apps/web ships a local client (apps/web/src/api.ts) for its first run.
// Phase 1 promotes that into this factory so Web and Mobile share one implementation that
// injects the Authorization + X-Business-Id headers and handles token refresh/retry.

export interface ApiClientOptions {
  baseUrl: string;
  getToken: () => string | null;
  getBusinessId: () => string | null;
}

export interface ApiEnvelope<T> {
  data: T;
}

export function createClient(options: ApiClientOptions) {
  async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers);
    headers.set("Content-Type", "application/json");

    const token = options.getToken();
    if (token) headers.set("Authorization", `Bearer ${token}`);

    const businessId = options.getBusinessId();
    if (businessId) headers.set("X-Business-Id", businessId);

    const res = await fetch(`${options.baseUrl}${path}`, { ...init, headers });
    const body = await res.json().catch(() => null);
    if (!res.ok) {
      throw new Error(body?.error?.message ?? `Request failed (${res.status})`);
    }
    return (body as ApiEnvelope<T>).data;
  }

  return { request };
}

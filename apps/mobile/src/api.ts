// Typed API client for the mobile app. Mirrors apps/web/src/api.ts; in a later pass both move to
// the shared @erp/api-client package generated from the backend OpenAPI spec.

export function uuid(): string {
  // RFC-4122 v4 (Math.random based — fine for client-side idempotency keys, not for secrets).
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export interface Membership {
  businessId: string;
  businessName: string;
  businessTypeCode: string;
  role: string;
  permissions: string[];
}
export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: { id: string; fullName: string; isSuperAdmin: boolean };
  memberships: Membership[];
}
export interface DashboardSummary {
  todayIncome: number; todayExpense: number; monthIncome: number; monthExpense: number;
  totalProfit: number; pendingCredits: number; pendingCollections: number;
}
export interface SyncItem { id: string; name: string; extra: string | null }
export interface SyncPull {
  cursor: string;
  customers: SyncItem[]; vehicles: SyncItem[]; drivers: SyncItem[]; items: SyncItem[];
  feeds: SyncItem[]; products: SyncItem[]; expenseTypes: SyncItem[];
}

/* ---- Module read models (mirror the backend DTOs used by the mobile screens) ---- */
export interface BusinessLite {
  id: string; name: string; businessTypeCode: string; businessTypeName: string;
}
export interface ItemLite {
  id: string; itemCode: string; itemName: string; uom: string;
  rate: number; stockQuantity: number; reorderLevel: number;
}
export interface ServiceComplaintLite {
  id: string; complaintNumber: string; customerName: string | null;
  issueDescription: string | null; status: string;
}
export interface FarmBatchLite {
  id: string; batchNumber: string; animalType: string;
  quantityPurchased: number; purchaseAmount: number; status: string;
}
export interface CoconutBatchLite {
  id: string; batchNumber: string; productName: string | null;
  quantity: number; purchaseAmount: number; status: string;
}
export interface ProfitLossLite { totalIncome: number; totalExpense: number; netProfit: number; }

export interface ApiOptions {
  baseUrl: string;
  getToken: () => string | null;
  getBusinessId: () => string | null;
}

/** Thrown for HTTP (4xx/5xx) responses; network failures throw a plain Error. */
export class ApiError extends Error {
  constructor(public status: number, public code: string, message: string) {
    super(message);
  }
}

export function createApi(opts: ApiOptions) {
  async function request<T>(method: string, path: string, body?: unknown, idempotencyKey?: string): Promise<T> {
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    const token = opts.getToken();
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const businessId = opts.getBusinessId();
    if (businessId) headers["X-Business-Id"] = businessId;
    if (idempotencyKey) headers["Idempotency-Key"] = idempotencyKey;

    const res = await fetch(`${opts.baseUrl}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });

    if (res.status === 204) return undefined as T;
    const json = await res.json().catch(() => null);
    if (!res.ok) {
      throw new ApiError(res.status, json?.error?.code ?? "error", json?.error?.message ?? `Request failed (${res.status})`);
    }
    return json?.data as T;
  }

  /**
   * Uploads a captured/selected file (multipart) to Firebase Storage via the API and returns its
   * object key. Online-only — the offline outbox never queues binary bodies; a photo is uploaded
   * when connected and its key is attached to the (possibly queued) record.
   */
  async function uploadFile(
    uri: string, name: string, type: string, folder = "expenses"): Promise<{ objectKey: string }> {
    const form = new FormData();
    // React Native's FormData accepts a { uri, name, type } file part.
    form.append("file", { uri, name, type } as unknown as Blob);

    const headers: Record<string, string> = {};   // no Content-Type — RN sets the multipart boundary
    const token = opts.getToken();
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const businessId = opts.getBusinessId();
    if (businessId) headers["X-Business-Id"] = businessId;

    const res = await fetch(`${opts.baseUrl}/files?folder=${encodeURIComponent(folder)}`, {
      method: "POST", headers, body: form,
    });
    const json = await res.json().catch(() => null);
    if (!res.ok) {
      throw new ApiError(res.status, json?.error?.code ?? "error",
        json?.error?.message ?? `Upload failed (${res.status})`);
    }
    return json?.data as { objectKey: string };
  }

  return {
    request,
    uploadFile,
    login: (mobileOrEmail: string, password: string) =>
      request<LoginResponse>("POST", "/auth/login", { mobileOrEmail, password }),
    dashboard: () => request<DashboardSummary>("GET", "/dashboard/summary"),
    syncPull: (since?: string | null) =>
      request<SyncPull>("GET", `/sync/pull${since ? `?since=${encodeURIComponent(since)}` : ""}`),

    // ---- Module reads (online) ----
    businesses: () => request<BusinessLite[]>("GET", "/businesses"),
    items: () => request<ItemLite[]>("GET", "/cctv/items"),
    serviceComplaints: () => request<ServiceComplaintLite[]>("GET", "/cctv/service-complaints"),
    farmBatches: () => request<FarmBatchLite[]>("GET", "/farm/batches"),
    coconutBatches: () => request<CoconutBatchLite[]>("GET", "/coconut/batches"),
    profitLoss: () => request<ProfitLossLite>("GET", "/accounting/profit-loss"),
  };
}

export type Api = ReturnType<typeof createApi>;

// Minimal typed API client for the Business One web console.
// Phase later replaces this with the shared @erp/api-client generated from OpenAPI.

import type {
  ApiEnvelope, BusinessDto, CreditDto, CustomerDto, DashboardSummary, DriverDto, ExpenseDto,
  LedgerEntryDto, LoadDto, LoginResponse, MeResponse, MemberDto, RefItem, VehicleDto,
} from "./types";
import { demoApi } from "./demo";

/** When built with VITE_DEMO=true (e.g. the GitHub Pages demo) the app runs entirely
 *  on in-memory sample data with no backend. */
export const IS_DEMO = import.meta.env.VITE_DEMO === "true";

const BASE = "/api/v1";

let accessToken: string | null = sessionStorage.getItem("accessToken");
let refreshToken: string | null = sessionStorage.getItem("refreshToken");
let activeBusinessId: string | null = sessionStorage.getItem("businessId");

export function setAccessToken(token: string | null) {
  accessToken = token;
  if (token) sessionStorage.setItem("accessToken", token);
  else sessionStorage.removeItem("accessToken");
}

export function setRefreshToken(token: string | null) {
  refreshToken = token;
  if (token) sessionStorage.setItem("refreshToken", token);
  else sessionStorage.removeItem("refreshToken");
}

export function setActiveBusiness(businessId: string | null) {
  activeBusinessId = businessId;
  if (businessId) sessionStorage.setItem("businessId", businessId);
  else sessionStorage.removeItem("businessId");
}

export function getActiveBusiness() {
  return activeBusinessId;
}

/** Auth + active-business headers shared by JSON requests, file uploads and downloads. */
function authHeaders(): Record<string, string> {
  const h: Record<string, string> = {};
  if (accessToken) h["Authorization"] = `Bearer ${accessToken}`;
  if (activeBusinessId) h["X-Business-Id"] = activeBusinessId;
  return h;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Content-Type", "application/json");
  for (const [k, v] of Object.entries(authHeaders())) headers.set(k, v);

  const res = await fetch(`${BASE}${path}`, { ...init, headers });
  if (res.status === 204) return undefined as T;

  const body = await res.json().catch(() => null);
  if (!res.ok) {
    throw new Error(body?.error?.message ?? `Request failed (${res.status})`);
  }
  return (body as ApiEnvelope<T>).data;
}

const qs = (params: Record<string, string | number | undefined | null>) => {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== "") q.set(k, String(v));
  const s = q.toString();
  return s ? `?${s}` : "";
};

export interface CreateBusinessInput {
  name: string;
  businessTypeCode: string;
  gstNumber?: string | null;
  address?: string | null;
}

export interface InviteUserInput {
  fullName: string;
  mobile: string;
  email?: string | null;
  password: string;
  businessId: string;
  roleCode: string;
}

export interface CreateExpenseInput {
  expenseDate: string;
  amount: number;
  description?: string | null;
  expenseTypeId?: string | null;
  attachmentKey?: string | null;
}

export interface CreateCustomerInput {
  name: string;
  mobile?: string | null;
  address?: string | null;
  gstNumber?: string | null;
  creditLimit: number;
  openingBalance: number;
}

export interface RecordCollectionInput {
  collectionDate: string;
  amount: number;
  mode: string;
  reference?: string | null;
}

export interface CreateVehicleInput {
  vehicleNumber: string;
  vehicleType?: string | null;
  model?: string | null;
  fuelType?: string | null;
  insuranceExpiry?: string | null;
}

export interface CreateDriverInput {
  name: string;
  mobile?: string | null;
  driverType: string;
  salary: number;
}

export interface CreateLoadInput {
  loadNumber: string;
  loadName?: string | null;
  customerId?: string | null;
  vehicleId?: string | null;
  driverId?: string | null;
  source?: string | null;
  destination?: string | null;
  loadDate: string;
  loadAmount: number;
  loadmanCharges: number;
  fuelExpense: number;
  maintenanceExpense: number;
  driverCharges: number;
  otherExpense: number;
}

const liveApi = {
  // ---- Auth ----
  login: (mobileOrEmail: string, password: string) =>
    request<LoginResponse>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ mobileOrEmail, password }),
    }),

  logout: () =>
    refreshToken
      ? request<void>("/auth/logout", { method: "POST", body: JSON.stringify({ refreshToken }) }).catch(() => {})
      : Promise.resolve(),

  me: () => request<MeResponse>("/me"),

  // ---- Businesses / members / reference ----
  businesses: () => request<BusinessDto[]>("/businesses"),
  createBusiness: (input: CreateBusinessInput) =>
    request<BusinessDto>("/businesses", { method: "POST", body: JSON.stringify(input) }),
  businessTypes: () => request<RefItem[]>("/business-types"),
  roles: () => request<RefItem[]>("/roles"),
  members: (businessId: string) => request<MemberDto[]>(`/businesses/${businessId}/members`),
  inviteUser: (input: InviteUserInput) =>
    request<unknown>("/users", { method: "POST", body: JSON.stringify(input) }),

  dashboard: () => request<DashboardSummary>("/dashboard/summary"),

  // ---- Expenses ----
  expenses: (from?: string, to?: string, typeId?: string) =>
    request<ExpenseDto[]>(`/expenses${qs({ from, to, typeId })}`),
  createExpense: (input: CreateExpenseInput) =>
    request<ExpenseDto>("/expenses", { method: "POST", body: JSON.stringify(input) }),
  deleteExpense: (id: string) => request<void>(`/expenses/${id}`, { method: "DELETE" }),
  expenseTypes: () => request<RefItem[]>("/expense-types"),
  expenseAttachment: (id: string) => request<{ url: string }>(`/expenses/${id}/attachment`),

  // ---- Customers ----
  customers: () => request<CustomerDto[]>("/customers"),
  createCustomer: (input: CreateCustomerInput) =>
    request<CustomerDto>("/customers", { method: "POST", body: JSON.stringify(input) }),
  customerLedger: (id: string) => request<LedgerEntryDto[]>(`/customers/${id}/ledger`),
  recordCollection: (id: string, input: RecordCollectionInput) =>
    request<unknown>(`/customers/${id}/collections`, { method: "POST", body: JSON.stringify(input) }),

  // ---- Transport ----
  vehicles: () => request<VehicleDto[]>("/transport/vehicles"),
  createVehicle: (input: CreateVehicleInput) =>
    request<VehicleDto>("/transport/vehicles", { method: "POST", body: JSON.stringify(input) }),
  drivers: () => request<DriverDto[]>("/transport/drivers"),
  createDriver: (input: CreateDriverInput) =>
    request<DriverDto>("/transport/drivers", { method: "POST", body: JSON.stringify(input) }),
  loads: (from?: string, to?: string) => request<LoadDto[]>(`/transport/loads${qs({ from, to })}`),
  createLoad: (input: CreateLoadInput) =>
    request<LoadDto>("/transport/loads", { method: "POST", body: JSON.stringify(input) }),
  credits: (status?: string) => request<CreditDto[]>(`/transport/credits${qs({ status })}`),
  recordCreditPayment: (id: string, amount: number, mode: string, paymentDate?: string) =>
    request<CreditDto>(`/transport/credits/${id}/payment`, {
      method: "PATCH",
      body: JSON.stringify({ amount, mode, paymentDate }),
    }),

  // ---- Files ----
  /** Uploads a file as multipart/form-data and returns the storage object key. */
  async uploadFile(file: File, folder = "expenses"): Promise<{ objectKey: string }> {
    const form = new FormData();
    form.append("file", file);
    // NOTE: do NOT set Content-Type — the browser adds the multipart boundary itself.
    const res = await fetch(`${BASE}/files${qs({ folder })}`, {
      method: "POST",
      headers: authHeaders(),
      body: form,
    });
    const body = await res.json().catch(() => null);
    if (!res.ok) throw new Error(body?.error?.message ?? `Upload failed (${res.status})`);
    return (body as ApiEnvelope<{ objectKey: string }>).data;
  },

  /**
   * Resolves a download URL into something openable. Cloud Storage returns an absolute signed URL
   * (self-authenticating), so it is returned as-is. The local dev provider returns a relative,
   * auth-protected path, which we fetch with the bearer token and expose as a blob object URL.
   */
  async resolveFileUrl(url: string): Promise<string> {
    if (/^https?:\/\//i.test(url)) return url;
    const res = await fetch(url, { headers: authHeaders() });
    if (!res.ok) throw new Error(`Download failed (${res.status})`);
    return URL.createObjectURL(await res.blob());
  },
};

/** The active client: the in-memory demo backend when VITE_DEMO=true, else the real HTTP client. */
export const api: typeof liveApi = IS_DEMO ? (demoApi as unknown as typeof liveApi) : liveApi;

// Minimal typed API client for the Business One web console.
// Phase later replaces this with the shared @erp/api-client generated from OpenAPI.

import type {
  AccountDto, ApiEnvelope, BusinessDto, CashBookRowDto, CoconutBatchDto, CoconutBatchPnlDto,
  CoconutProductDto, CreditDto, CustomerDto, DashboardSummary, DriverDto, EmployeeDto, ExpenseDto,
  FarmBatchDto, FarmBatchPnlDto, FarmBatchSaleDto, FeedDto, ItemDto, JournalTxnDto, LedgerEntryDto,
  LedgerLineDto, LoadDto, LoginResponse, MeResponse, MemberDto, ProfitLossDto, PurchaseOrderDto,
  RefItem, SalaryRecordDto, SaleDto, ServiceComplaintDto, SupplierDto, VehicleDto, WalletDto,
  WalletTransactionDto,
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

/** Saves a Blob to the user's device under the given filename. */
export function triggerDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

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

export interface CreateItemInput {
  itemCode: string;
  itemName: string;
  uom: string;
  hsnCode?: string | null;
  rate: number;
  taxPercentage: number;
  reorderLevel: number;
}

export interface CreateSupplierInput {
  name: string;
  mobile?: string | null;
  gstNumber?: string | null;
  address?: string | null;
}

export interface CreateServiceComplaintInput {
  complaintNumber: string;
  customerId: string;
  issueDescription?: string | null;
  assignedEmployeeId?: string | null;
}

export interface CreateFarmBatchInput {
  batchNumber: string;
  batchName?: string | null;
  animalType: string;
  startDate: string;
  quantityPurchased: number;
  purchaseAmount: number;
}

export interface CreateFeedInput {
  feedName: string;
  feedType?: string | null;
  uom: string;
  rate: number;
}

export interface CreateCoconutBatchInput {
  productId: string;
  batchNumber: string;
  purchaseDate: string;
  quantity: number;
  purchaseAmount: number;
}

export interface CreateProductInput {
  name: string;
  category?: string | null;
  uom: string;
}

export interface PoLineInput {
  itemId: string;
  quantity: number;
  rate: number;
  taxPercentage: number;
}

export interface CreatePurchaseOrderInput {
  poNumber: string;
  supplierId: string;
  poDate: string;
  note?: string | null;
  lines: PoLineInput[];
}

export interface AddFeedEntryInput { feedId: string; entryDate: string; quantity: number; rate: number; }
export interface AddBatchSaleInput {
  saleDate: string; saleQuantity: number; totalWeight?: number | null; saleAmount: number; customerId?: string | null;
}
export interface AddLabourChargeInput { labourName?: string | null; amount: number; chargeDate: string; }
export interface AddTransportChargeInput { vehicle?: string | null; amount: number; chargeDate: string; }
export interface AddCoconutSaleInput { saleDate: string; saleQuantity: number; saleValue: number; customerId?: string | null; }

export interface CreateEmployeeInput {
  name: string;
  mobile?: string | null;
  address?: string | null;
  joiningDate?: string | null;
  salary: number;
  status?: string | null;
}

export interface RecordSalaryInput {
  periodMonth: string;
  amount: number;
  paidAmount: number;
  paidOn?: string | null;
  note?: string | null;
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

  // ---- CCTV / Electronics ----
  items: () => request<ItemDto[]>("/cctv/items"),
  createItem: (input: CreateItemInput) =>
    request<ItemDto>("/cctv/items", { method: "POST", body: JSON.stringify(input) }),
  suppliers: () => request<SupplierDto[]>("/cctv/suppliers"),
  createSupplier: (input: CreateSupplierInput) =>
    request<SupplierDto>("/cctv/suppliers", { method: "POST", body: JSON.stringify(input) }),
  purchaseOrders: (status?: string) => request<PurchaseOrderDto[]>(`/cctv/purchase-orders${qs({ status })}`),
  createPurchaseOrder: (input: CreatePurchaseOrderInput) =>
    request<PurchaseOrderDto>("/cctv/purchase-orders", { method: "POST", body: JSON.stringify(input) }),
  poSubmit: (id: string) => request<PurchaseOrderDto>(`/cctv/purchase-orders/${id}/submit`, { method: "POST" }),
  poApprove: (id: string) => request<PurchaseOrderDto>(`/cctv/purchase-orders/${id}/approve`, { method: "POST" }),
  poReceive: (id: string) => request<PurchaseOrderDto>(`/cctv/purchase-orders/${id}/receive`, { method: "POST" }),
  cctvSales: (from?: string, to?: string) => request<SaleDto[]>(`/cctv/sales${qs({ from, to })}`),
  serviceComplaints: (status?: string) => request<ServiceComplaintDto[]>(`/cctv/service-complaints${qs({ status })}`),
  createServiceComplaint: (input: CreateServiceComplaintInput) =>
    request<ServiceComplaintDto>("/cctv/service-complaints", { method: "POST", body: JSON.stringify(input) }),
  updateServiceStatus: (id: string, status: string) =>
    request<ServiceComplaintDto>(`/cctv/service-complaints/${id}/status`, { method: "PATCH", body: JSON.stringify({ status }) }),

  // ---- Farm ----
  farmBatches: (status?: string) => request<FarmBatchDto[]>(`/farm/batches${qs({ status })}`),
  createFarmBatch: (input: CreateFarmBatchInput) =>
    request<FarmBatchDto>("/farm/batches", { method: "POST", body: JSON.stringify(input) }),
  farmBatchPnl: (id: string) => request<FarmBatchPnlDto>(`/farm/batches/${id}/pnl`),
  farmBatchSales: (id: string) => request<FarmBatchSaleDto[]>(`/farm/batches/${id}/sales`),
  addFeedEntry: (batchId: string, input: AddFeedEntryInput) =>
    request<unknown>(`/farm/batches/${batchId}/feed-entries`, { method: "POST", body: JSON.stringify(input) }),
  addBatchSale: (batchId: string, input: AddBatchSaleInput) =>
    request<FarmBatchSaleDto>(`/farm/batches/${batchId}/sales`, { method: "POST", body: JSON.stringify(input) }),
  feeds: () => request<FeedDto[]>("/farm/feeds"),
  createFeed: (input: CreateFeedInput) =>
    request<FeedDto>("/farm/feeds", { method: "POST", body: JSON.stringify(input) }),
  wallet: () => request<WalletDto>("/farm/wallet"),
  walletTransactions: () => request<WalletTransactionDto[]>("/farm/wallet/transactions"),

  // ---- Coconut ----
  coconutBatches: (status?: string) => request<CoconutBatchDto[]>(`/coconut/batches${qs({ status })}`),
  createCoconutBatch: (input: CreateCoconutBatchInput) =>
    request<CoconutBatchDto>("/coconut/batches", { method: "POST", body: JSON.stringify(input) }),
  coconutBatchPnl: (id: string) => request<CoconutBatchPnlDto>(`/coconut/batches/${id}/pnl`),
  addLabourCharge: (batchId: string, input: AddLabourChargeInput) =>
    request<unknown>(`/coconut/batches/${batchId}/labour-charges`, { method: "POST", body: JSON.stringify(input) }),
  addTransportCharge: (batchId: string, input: AddTransportChargeInput) =>
    request<unknown>(`/coconut/batches/${batchId}/transport-charges`, { method: "POST", body: JSON.stringify(input) }),
  addCoconutSale: (batchId: string, input: AddCoconutSaleInput) =>
    request<unknown>(`/coconut/batches/${batchId}/sales`, { method: "POST", body: JSON.stringify(input) }),
  products: () => request<CoconutProductDto[]>("/coconut/products"),
  createProduct: (input: CreateProductInput) =>
    request<CoconutProductDto>("/coconut/products", { method: "POST", body: JSON.stringify(input) }),

  // ---- Employees ----
  employees: () => request<EmployeeDto[]>("/employees"),
  createEmployee: (input: CreateEmployeeInput) =>
    request<EmployeeDto>("/employees", { method: "POST", body: JSON.stringify(input) }),
  salaryHistory: (id: string) => request<SalaryRecordDto[]>(`/employees/${id}/salary`),
  recordSalary: (id: string, input: RecordSalaryInput) =>
    request<SalaryRecordDto>(`/employees/${id}/salary`, { method: "POST", body: JSON.stringify(input) }),

  // ---- Accounting ----
  profitLoss: (from?: string, to?: string) => request<ProfitLossDto>(`/accounting/profit-loss${qs({ from, to })}`),
  cashBook: (from?: string, to?: string) => request<CashBookRowDto[]>(`/accounting/cash-book${qs({ from, to })}`),
  accounts: () => request<AccountDto[]>("/accounting/accounts"),
  journal: (from?: string, to?: string) => request<JournalTxnDto[]>(`/accounting/journal${qs({ from, to })}`),
  ledger: (accountId?: string, from?: string, to?: string) =>
    request<LedgerLineDto[]>(`/accounting/ledger${qs({ accountId, from, to })}`),

  // ---- Reporting ----
  /** Generates a report (reportKey: expenses|collections|credit-outstanding|profit-loss) as
   *  pdf|excel and triggers a browser download. */
  async exportReport(reportKey: string, format: "pdf" | "excel", from?: string, to?: string): Promise<void> {
    const res = await fetch(`${BASE}/reports/export`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...authHeaders() },
      body: JSON.stringify({ reportKey, format, from: from ?? null, to: to ?? null }),
    });
    if (!res.ok) throw new Error(`Export failed (${res.status})`);
    triggerDownload(await res.blob(), `${reportKey}.${format === "excel" ? "xlsx" : "pdf"}`);
  },

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

// Phase 0/1: hand-written to match the API contract. Phase later generates these from the
// backend OpenAPI spec into packages/types and imports them in both web and mobile (docs/07).

export interface Membership {
  businessId: string;
  businessName: string;
  businessTypeCode: "TRANSPORT" | "CCTV" | "FARM" | "COCONUT";
  role: string;
  permissions: string[];
}

export interface UserSummary {
  id: string;
  fullName: string;
  mobile: string;
  email: string | null;
  isSuperAdmin: boolean;
}

export interface LoginResponse {
  accessToken: string;
  expiresIn: number;
  refreshToken: string;
  user: UserSummary;
  memberships: Membership[];
}

export interface MeResponse {
  user: UserSummary;
  memberships: Membership[];
}

export interface BusinessDto {
  id: string;
  name: string;
  businessTypeCode: string;
  businessTypeName: string;
  gstNumber: string | null;
  address: string | null;
  isActive: boolean;
  role: string | null;
}

export interface MemberDto {
  userId: string;
  fullName: string;
  mobile: string;
  roleCode: string;
  roleName: string;
}

export interface RefItem {
  id: string;
  code: string;
  name: string;
}

export interface TrendPoint {
  label: string;
  income: number;
  expense: number;
}

export interface CategorySlice {
  label: string;
  value: number;
}

export interface DashboardSummary {
  todayIncome: number;
  todayExpense: number;
  monthIncome: number;
  monthExpense: number;
  totalProfit: number;
  pendingCredits: number;
  pendingCollections: number;
  /** Optional analytics used by the dashboard charts (populated by the demo backend). */
  trend?: TrendPoint[];
  expenseBreakdown?: CategorySlice[];
}

export interface ApiEnvelope<T> {
  data: T;
}

/* ---- Expenses ---- */
export interface ExpenseDto {
  id: string;
  expenseTypeId: string | null;
  expenseTypeName: string | null;
  expenseDate: string;
  amount: number;
  description: string | null;
  attachmentKey: string | null;
}

/* ---- Customers ---- */
export interface CustomerDto {
  id: string;
  name: string;
  mobile: string | null;
  address: string | null;
  gstNumber: string | null;
  creditLimit: number;
  outstanding: number;
}

export interface LedgerEntryDto {
  id: string;
  entryDate: string;
  refType: string;
  refId: string | null;
  debit: number;
  credit: number;
  runningBalance: number;
}

/* ---- Transport ---- */
export interface VehicleDto {
  id: string;
  vehicleNumber: string;
  vehicleType: string | null;
  model: string | null;
  fuelType: string | null;
  rcDetails: string | null;
  insuranceDetails: string | null;
  insuranceExpiry: string | null;
  isActive: boolean;
}

export interface DriverDto {
  id: string;
  name: string;
  mobile: string | null;
  driverType: string;
  salary: number;
  isActive: boolean;
}

export interface LoadDto {
  id: string;
  loadNumber: string;
  loadName: string | null;
  customerId: string | null;
  vehicleId: string | null;
  driverId: string | null;
  source: string | null;
  destination: string | null;
  loadAmount: number;
  loadmanCharges: number;
  fuelExpense: number;
  maintenanceExpense: number;
  driverCharges: number;
  otherExpense: number;
  profit: number;
  loadDate: string;
  status: string;
}

export interface CreditDto {
  id: string;
  loadId: string;
  loadNumber: string | null;
  customerId: string;
  customerName: string | null;
  loadAmount: number;
  paidAmount: number;
  balanceAmount: number;
  status: string;
}

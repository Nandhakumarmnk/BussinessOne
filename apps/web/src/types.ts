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

/* ---- CCTV (Electronics) ---- */
export interface ItemDto {
  id: string;
  itemCode: string;
  itemName: string;
  uom: string;
  hsnCode: string | null;
  rate: number;
  taxPercentage: number;
  stockQuantity: number;
  reorderLevel: number;
  isActive: boolean;
}

export interface SupplierDto {
  id: string;
  name: string;
  mobile: string | null;
  gstNumber: string | null;
  address: string | null;
}

export interface PoLineDto {
  id: string;
  itemId: string;
  quantity: number;
  rate: number;
  taxPercentage: number;
  lineTotal: number;
}

export interface PurchaseOrderDto {
  id: string;
  poNumber: string;
  supplierId: string;
  supplierName: string | null;
  poDate: string;
  totalAmount: number;
  status: string;
  lines: PoLineDto[];
}

export interface SaleLineDto {
  id: string;
  itemId: string;
  quantity: number;
  rate: number;
  taxPercentage: number;
  lineTotal: number;
}

export interface SaleDto {
  id: string;
  invoiceNumber: string;
  customerId: string | null;
  customerName: string | null;
  saleDate: string;
  installationCharges: number;
  labourCharges: number;
  subTotal: number;
  taxAmount: number;
  totalAmount: number;
  paidAmount: number;
  balance: number;
  status: string;
  lines: SaleLineDto[];
}

export interface ServiceComplaintDto {
  id: string;
  complaintNumber: string;
  customerId: string;
  customerName: string | null;
  issueDescription: string | null;
  assignedEmployeeId: string | null;
  assignedEmployeeName: string | null;
  status: string;
  openedAt: string;
  closedAt: string | null;
}

/* ---- Farm ---- */
export interface FarmBatchDto {
  id: string;
  batchNumber: string;
  batchName: string | null;
  animalType: string;
  startDate: string;
  quantityPurchased: number;
  purchaseAmount: number;
  status: string;
}

export interface FarmBatchSaleDto {
  id: string;
  batchId: string;
  saleDate: string;
  saleQuantity: number;
  totalWeight: number | null;
  saleAmount: number;
  customerId: string | null;
}

export interface FeedDto {
  id: string;
  feedName: string;
  feedType: string | null;
  uom: string;
  rate: number;
  isActive: boolean;
}

export interface WalletDto {
  balance: number;
}

export interface WalletTransactionDto {
  id: string;
  txnDate: string;
  direction: string;
  amount: number;
  reason: string | null;
}

export interface FarmBatchPnlDto {
  batchId: string;
  batchNumber: string;
  batchName: string | null;
  purchase: number;
  feedCost: number;
  medicalCost: number;
  labourCost: number;
  otherCost: number;
  totalSales: number;
  totalCost?: number;
  profit?: number;
}

/* ---- Coconut ---- */
export interface CoconutProductDto {
  id: string;
  name: string;
  category: string | null;
  uom: string;
  isActive: boolean;
}

export interface CoconutBatchDto {
  id: string;
  productId: string;
  productName: string | null;
  batchNumber: string;
  purchaseDate: string;
  quantity: number;
  purchaseAmount: number;
  status: string;
}

export interface CoconutSaleDto {
  id: string;
  batchId: string;
  saleDate: string;
  saleQuantity: number;
  saleValue: number;
  customerId: string | null;
}

export interface CoconutBatchPnlDto {
  batchId: string;
  batchNumber: string;
  productId: string;
  productName: string | null;
  purchase: number;
  labourCost: number;
  transportCost: number;
  totalSales: number;
  totalCost?: number;
  profit?: number;
}

/* ---- Accounting ---- */
export interface ProfitLossDto {
  totalIncome: number;
  totalExpense: number;
  netProfit: number;
}

export interface CashBookRowDto {
  date: string;
  description: string;
  in: number;
  out: number;
  balance: number;
}

export interface AccountDto {
  id: string;
  code: string;
  name: string;
  type: string;
  isActive: boolean;
}

export interface JournalLineViewDto {
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
}

export interface JournalTxnDto {
  id: string;
  txnDate: string;
  sourceModule: string;
  narration: string | null;
  lines: JournalLineViewDto[];
}

export interface LedgerLineDto {
  date: string;
  accountCode: string;
  accountName: string;
  narration: string | null;
  debit: number;
  credit: number;
  balance: number;
}

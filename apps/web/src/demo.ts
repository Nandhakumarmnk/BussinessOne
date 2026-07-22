/* ==========================================================================
   Demo backend — an in-memory implementation of the `api` surface.

   Used when the app is built with VITE_DEMO=true (e.g. the GitHub Pages demo),
   so the whole console is fully explorable with realistic, mutable sample data
   and NO server. Every write mutates the in-memory store so the UI reacts just
   like it would against the real API. Data resets on page reload.
   ========================================================================== */

import type {
  AddBatchSaleInput, AddCoconutSaleInput, AddFeedEntryInput, AddLabourChargeInput,
  AddTransportChargeInput, CreateBusinessInput, CreateCoconutBatchInput, CreateCustomerInput,
  CreateDriverInput, CreateEmployeeInput, CreateExpenseInput, CreateFarmBatchInput, CreateFeedInput,
  CreateItemInput, CreateLoadInput, CreateProductInput, CreatePurchaseOrderInput,
  CreateServiceComplaintInput, CreateSupplierInput, CreateVehicleInput, InviteUserInput,
  RecordCollectionInput, RecordSalaryInput,
} from "./api";
import type {
  AccountDto, BusinessDto, CashBookRowDto, CoconutBatchDto, CoconutBatchPnlDto, CoconutProductDto,
  CreditDto, CustomerDto, DashboardSummary, DriverDto, EmployeeDto, ExpenseDto, FarmBatchDto,
  FarmBatchPnlDto, FarmBatchSaleDto, FeedDto, ItemDto, JournalTxnDto, LedgerEntryDto, LedgerLineDto,
  LoadDto, LoginResponse, MeResponse, MemberDto, ProfitLossDto, PurchaseOrderDto, RefItem,
  SalaryRecordDto, SaleDto, ServiceComplaintDto, SupplierDto, VehicleDto, WalletDto,
  WalletTransactionDto,
} from "./types";

/* -- tiny helpers ---------------------------------------------------------- */
let seq = 1000;
const uid = (p: string) => `${p}-${++seq}`;
const wait = <T>(value: T): Promise<T> =>
  new Promise((resolve) => setTimeout(() => resolve(value), 180)); // mimic latency
const clone = <T>(v: T): T => JSON.parse(JSON.stringify(v));

/* -- reference data -------------------------------------------------------- */
const businessTypes: RefItem[] = [
  { id: "t-transport", code: "TRANSPORT", name: "Goods Transport" },
  { id: "t-cctv", code: "CCTV", name: "Electronics & CCTV" },
  { id: "t-farm", code: "FARM", name: "Farm Management" },
  { id: "t-coconut", code: "COCONUT", name: "Coconut Business" },
];

const roles: RefItem[] = [
  { id: "r-owner", code: "OWNER", name: "Owner" },
  { id: "r-manager", code: "MANAGER", name: "Manager" },
  { id: "r-accountant", code: "ACCOUNTANT", name: "Accountant" },
  { id: "r-employee", code: "EMPLOYEE", name: "Employee" },
];

const expenseTypes: RefItem[] = [
  { id: "et-fuel", code: "FUEL", name: "Fuel" },
  { id: "et-salary", code: "SALARY", name: "Salaries" },
  { id: "et-maint", code: "MAINT", name: "Maintenance" },
  { id: "et-rent", code: "RENT", name: "Rent" },
  { id: "et-util", code: "UTIL", name: "Utilities" },
  { id: "et-misc", code: "MISC", name: "Miscellaneous" },
];

/* -- user + businesses ----------------------------------------------------- */
const user = {
  id: "u-owner",
  fullName: "Nandhakumar Murugesan",
  mobile: "9000012345",
  email: "owner@business-one.local",
  isSuperAdmin: true,
};

const businesses: BusinessDto[] = [
  { id: "b-transport", name: "Sri Balaji Goods Transport", businessTypeCode: "TRANSPORT",
    businessTypeName: "Goods Transport", gstNumber: "33ABCDE1234F1Z5", address: "Erode, Tamil Nadu",
    isActive: true, role: "OWNER" },
  { id: "b-cctv", name: "VisionGuard CCTV & Security", businessTypeCode: "CCTV",
    businessTypeName: "Electronics & CCTV", gstNumber: "33XYZAB5678K1Z2", address: "Coimbatore, Tamil Nadu",
    isActive: true, role: "OWNER" },
  { id: "b-farm", name: "Green Valley Poultry Farm", businessTypeCode: "FARM",
    businessTypeName: "Farm Management", gstNumber: null, address: "Namakkal, Tamil Nadu",
    isActive: true, role: "OWNER" },
  { id: "b-coconut", name: "Kongu Coconut Traders", businessTypeCode: "COCONUT",
    businessTypeName: "Coconut Business", gstNumber: null, address: "Pollachi, Tamil Nadu",
    isActive: false, role: "MANAGER" },
];

const members: MemberDto[] = [
  { userId: "u-owner", fullName: "Nandhakumar Murugesan", mobile: "9000012345", roleCode: "OWNER", roleName: "Owner" },
  { userId: "u-priya", fullName: "Priya Kumar", mobile: "9000023456", roleCode: "ACCOUNTANT", roleName: "Accountant" },
  { userId: "u-arun", fullName: "Arun Selvam", mobile: "9000034567", roleCode: "MANAGER", roleName: "Manager" },
  { userId: "u-divya", fullName: "Divya Ramesh", mobile: "9000045678", roleCode: "EMPLOYEE", roleName: "Employee" },
];

/* -- dashboards (per business) --------------------------------------------- */
const MONTHS = ["Dec", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul"];
const trend = (base: number, spread: number, margin: number) =>
  MONTHS.map((label, i) => {
    const income = Math.round(base + spread * Math.sin(i / 1.6) + i * spread * 0.06);
    const expense = Math.round(income * (1 - margin) + spread * 0.12 * Math.cos(i / 2));
    return { label, income, expense };
  });

const dashboards: Record<string, DashboardSummary> = {
  "b-transport": {
    todayIncome: 84500, todayExpense: 51200, monthIncome: 1862000, monthExpense: 1188400,
    totalProfit: 673600, pendingCredits: 214500, pendingCollections: 96300,
    trend: trend(240000, 90000, 0.36),
    expenseBreakdown: [
      { label: "Fuel", value: 486000 }, { label: "Driver charges", value: 268000 },
      { label: "Maintenance", value: 172000 }, { label: "Loadman", value: 138400 },
      { label: "Other", value: 124000 },
    ],
  },
  "b-cctv": {
    todayIncome: 62400, todayExpense: 28900, monthIncome: 1345000, monthExpense: 792000,
    totalProfit: 553000, pendingCredits: 158200, pendingCollections: 71000,
    trend: trend(175000, 70000, 0.41),
    expenseBreakdown: [
      { label: "Stock purchase", value: 402000 }, { label: "Salaries", value: 196000 },
      { label: "Installation", value: 104000 }, { label: "Rent", value: 56000 },
      { label: "Utilities", value: 34000 },
    ],
  },
  "b-farm": {
    todayIncome: 41200, todayExpense: 33600, monthIncome: 968000, monthExpense: 731000,
    totalProfit: 237000, pendingCredits: 62000, pendingCollections: 44800,
    trend: trend(128000, 52000, 0.24),
    expenseBreakdown: [
      { label: "Feed", value: 384000 }, { label: "Medical", value: 118000 },
      { label: "Labour", value: 132000 }, { label: "Utilities", value: 58000 },
      { label: "Other", value: 39000 },
    ],
  },
  "b-coconut": {
    todayIncome: 28900, todayExpense: 19400, monthIncome: 612000, monthExpense: 437000,
    totalProfit: 175000, pendingCredits: 38400, pendingCollections: 21600,
    trend: trend(84000, 34000, 0.28),
    expenseBreakdown: [
      { label: "Purchase", value: 246000 }, { label: "Labour", value: 92000 },
      { label: "Transport", value: 61000 }, { label: "Other", value: 38000 },
    ],
  },
};

const activeBiz = () => sessionStorage.getItem("businessId") ?? "b-transport";

/* -- operational data (expenses / customers / transport) ------------------- */
const expenses: ExpenseDto[] = [
  { id: uid("exp"), expenseTypeId: "et-fuel", expenseTypeName: "Fuel", expenseDate: "2026-07-18", amount: 18600, description: "Diesel — TN-38 fleet", attachmentKey: "demo/fuel.png" },
  { id: uid("exp"), expenseTypeId: "et-salary", expenseTypeName: "Salaries", expenseDate: "2026-07-15", amount: 96000, description: "Driver & staff salaries", attachmentKey: null },
  { id: uid("exp"), expenseTypeId: "et-maint", expenseTypeName: "Maintenance", expenseDate: "2026-07-12", amount: 24300, description: "Tyre replacement", attachmentKey: "demo/tyre.png" },
  { id: uid("exp"), expenseTypeId: "et-rent", expenseTypeName: "Rent", expenseDate: "2026-07-05", amount: 35000, description: "Godown rent", attachmentKey: null },
  { id: uid("exp"), expenseTypeId: "et-util", expenseTypeName: "Utilities", expenseDate: "2026-07-03", amount: 8400, description: "Electricity + internet", attachmentKey: null },
  { id: uid("exp"), expenseTypeId: "et-misc", expenseTypeName: "Miscellaneous", expenseDate: "2026-07-01", amount: 5200, description: "Office supplies", attachmentKey: null },
];

const customers: CustomerDto[] = [
  { id: "c-1", name: "Ramco Cements Ltd", mobile: "9812300011", address: "Ariyalur", gstNumber: "33RAMCO0001Z1", creditLimit: 500000, outstanding: 128500 },
  { id: "c-2", name: "Sakthi Sugars", mobile: "9812300022", address: "Sakthinagar", gstNumber: null, creditLimit: 300000, outstanding: 54000 },
  { id: "c-3", name: "KG Textiles", mobile: "9812300033", address: "Tiruppur", gstNumber: "33KGTEX0002Z2", creditLimit: 250000, outstanding: 0 },
  { id: "c-4", name: "Annapoorna Foods", mobile: "9812300044", address: "Coimbatore", gstNumber: null, creditLimit: 150000, outstanding: 32000 },
  { id: "c-5", name: "Sri Venkateswara Traders", mobile: "9812300055", address: "Salem", gstNumber: null, creditLimit: 200000, outstanding: 0 },
];

const ledgers: Record<string, LedgerEntryDto[]> = {
  "c-1": [
    { id: uid("le"), entryDate: "2026-07-02", refType: "Load #TR-2041", refId: null, debit: 96000, credit: 0, runningBalance: 96000 },
    { id: uid("le"), entryDate: "2026-07-08", refType: "Collection (UPI)", refId: null, debit: 0, credit: 60000, runningBalance: 36000 },
    { id: uid("le"), entryDate: "2026-07-14", refType: "Load #TR-2068", refId: null, debit: 92500, credit: 0, runningBalance: 128500 },
  ],
  "c-2": [
    { id: uid("le"), entryDate: "2026-07-04", refType: "Load #TR-2049", refId: null, debit: 54000, credit: 0, runningBalance: 54000 },
  ],
  "c-4": [
    { id: uid("le"), entryDate: "2026-07-06", refType: "Load #TR-2053", refId: null, debit: 48000, credit: 0, runningBalance: 48000 },
    { id: uid("le"), entryDate: "2026-07-16", refType: "Collection (Cash)", refId: null, debit: 0, credit: 16000, runningBalance: 32000 },
  ],
};

const vehicles: VehicleDto[] = [
  { id: "v-1", vehicleNumber: "TN-38-AB-1234", vehicleType: "Truck", model: "Tata LPT 1613", fuelType: "Diesel", rcDetails: null, insuranceDetails: null, insuranceExpiry: "2027-01-31", isActive: true },
  { id: "v-2", vehicleNumber: "TN-38-CD-5678", vehicleType: "Truck", model: "Ashok Leyland 2820", fuelType: "Diesel", rcDetails: null, insuranceDetails: null, insuranceExpiry: "2026-11-15", isActive: true },
  { id: "v-3", vehicleNumber: "TN-38-EF-9012", vehicleType: "Tempo", model: "Mahindra Bolero Pik-Up", fuelType: "Diesel", rcDetails: null, insuranceDetails: null, insuranceExpiry: "2027-03-20", isActive: true },
  { id: "v-4", vehicleNumber: "TN-38-GH-3456", vehicleType: "Container", model: "BharatBenz 1917", fuelType: "Diesel", rcDetails: null, insuranceDetails: null, insuranceExpiry: "2026-09-01", isActive: false },
];

const drivers: DriverDto[] = [
  { id: "d-1", name: "Murugan S", mobile: "9500011111", driverType: "salaried", salary: 22000, isActive: true },
  { id: "d-2", name: "Karthik R", mobile: "9500022222", driverType: "salaried", salary: 21000, isActive: true },
  { id: "d-3", name: "Vignesh P", mobile: "9500033333", driverType: "self", salary: 0, isActive: true },
  { id: "d-4", name: "Saravanan M", mobile: "9500044444", driverType: "salaried", salary: 20000, isActive: true },
];

function mkLoad(n: number, name: string, custId: string | null, vId: string, dId: string,
               date: string, amount: number, loadman: number, fuel: number, maint: number,
               driverC: number, other: number): LoadDto {
  const profit = amount - loadman - fuel - maint - driverC - other;
  return {
    id: uid("load"), loadNumber: `TR-${n}`, loadName: name, customerId: custId,
    vehicleId: vId, driverId: dId, source: "Erode", destination: "Chennai",
    loadAmount: amount, loadmanCharges: loadman, fuelExpense: fuel, maintenanceExpense: maint,
    driverCharges: driverC, otherExpense: other, profit, loadDate: date, status: "COMPLETED",
  };
}

const loads: LoadDto[] = [
  mkLoad(2068, "Cement bags", "c-1", "v-1", "d-1", "2026-07-14", 92500, 6000, 21000, 3500, 8000, 2000),
  mkLoad(2061, "Cotton bales", "c-3", "v-2", "d-2", "2026-07-11", 78000, 5000, 18500, 0, 7000, 1500),
  mkLoad(2053, "Rice sacks", "c-4", "v-3", "d-3", "2026-07-06", 48000, 3500, 9200, 0, 0, 1200),
  mkLoad(2049, "Sugar", "c-2", "v-1", "d-4", "2026-07-04", 54000, 4000, 12800, 2200, 6000, 1000),
  mkLoad(2041, "Cement bags", "c-1", "v-2", "d-1", "2026-07-02", 96000, 6500, 22400, 0, 8000, 2500),
  mkLoad(2034, "Machinery", null, "v-1", "d-2", "2026-06-28", 118000, 8000, 26000, 5400, 9000, 3000),
  mkLoad(2027, "Textiles", "c-3", "v-3", "d-3", "2026-06-24", 42000, 3000, 8600, 0, 0, 900),
];

const credits: CreditDto[] = [
  { id: "cr-1", loadId: loads[0].id, loadNumber: "TR-2068", customerId: "c-1", customerName: "Ramco Cements Ltd", loadAmount: 92500, paidAmount: 0, balanceAmount: 92500, status: "PENDING" },
  { id: "cr-2", loadId: loads[3].id, loadNumber: "TR-2049", customerId: "c-2", customerName: "Sakthi Sugars", loadAmount: 54000, paidAmount: 0, balanceAmount: 54000, status: "PENDING" },
  { id: "cr-3", loadId: loads[4].id, loadNumber: "TR-2041", customerId: "c-1", customerName: "Ramco Cements Ltd", loadAmount: 96000, paidAmount: 60000, balanceAmount: 36000, status: "PARTIAL" },
  { id: "cr-4", loadId: loads[2].id, loadNumber: "TR-2053", customerId: "c-4", customerName: "Annapoorna Foods", loadAmount: 48000, paidAmount: 16000, balanceAmount: 32000, status: "PARTIAL" },
  { id: "cr-5", loadId: loads[1].id, loadNumber: "TR-2061", customerId: "c-3", customerName: "KG Textiles", loadAmount: 78000, paidAmount: 78000, balanceAmount: 0, status: "CLEARED" },
];

/* -- CCTV vertical --------------------------------------------------------- */
const items: ItemDto[] = [
  { id: "it-1", itemCode: "CAM-DOME-2MP", itemName: "2MP Dome Camera", uom: "pcs", hsnCode: "8525", rate: 1650, taxPercentage: 18, stockQuantity: 42, reorderLevel: 10, isActive: true },
  { id: "it-2", itemCode: "CAM-BULLET-4MP", itemName: "4MP Bullet Camera", uom: "pcs", hsnCode: "8525", rate: 2450, taxPercentage: 18, stockQuantity: 8, reorderLevel: 10, isActive: true },
  { id: "it-3", itemCode: "DVR-8CH", itemName: "8-Channel DVR", uom: "pcs", hsnCode: "8521", rate: 5200, taxPercentage: 18, stockQuantity: 15, reorderLevel: 5, isActive: true },
  { id: "it-4", itemCode: "HDD-2TB", itemName: "2TB Surveillance HDD", uom: "pcs", hsnCode: "8471", rate: 4800, taxPercentage: 18, stockQuantity: 23, reorderLevel: 8, isActive: true },
  { id: "it-5", itemCode: "CABLE-3+1", itemName: "3+1 CCTV Cable (90m)", uom: "roll", hsnCode: "8544", rate: 1350, taxPercentage: 18, stockQuantity: 6, reorderLevel: 12, isActive: true },
];

const suppliers: SupplierDto[] = [
  { id: "sup-1", name: "Hikvision Distributors", mobile: "9840011111", gstNumber: "33HIKV0001Z1", address: "Chennai" },
  { id: "sup-2", name: "CP Plus Wholesale", mobile: "9840022222", gstNumber: null, address: "Coimbatore" },
];

const purchaseOrders: PurchaseOrderDto[] = [
  { id: "po-1", poNumber: "PO-1042", supplierId: "sup-1", supplierName: "Hikvision Distributors", poDate: "2026-07-15", totalAmount: 98500, status: "APPROVED", lines: [] },
  { id: "po-2", poNumber: "PO-1043", supplierId: "sup-2", supplierName: "CP Plus Wholesale", poDate: "2026-07-18", totalAmount: 64200, status: "DRAFT", lines: [] },
  { id: "po-3", poNumber: "PO-1041", supplierId: "sup-1", supplierName: "Hikvision Distributors", poDate: "2026-07-10", totalAmount: 145000, status: "RECEIVED", lines: [] },
];

const cctvSales: SaleDto[] = [
  { id: "sl-1", invoiceNumber: "INV-2087", customerId: "c-1", customerName: "Ramco Cements Ltd", saleDate: "2026-07-16", installationCharges: 3500, labourCharges: 1500, subTotal: 42000, taxAmount: 7560, totalAmount: 54560, paidAmount: 54560, balance: 0, status: "PAID", lines: [] },
  { id: "sl-2", invoiceNumber: "INV-2088", customerId: "c-4", customerName: "Annapoorna Foods", saleDate: "2026-07-19", installationCharges: 2000, labourCharges: 1000, subTotal: 28000, taxAmount: 5040, totalAmount: 36040, paidAmount: 16040, balance: 20000, status: "PARTIAL", lines: [] },
];

const serviceComplaints: ServiceComplaintDto[] = [
  { id: "sc-1", complaintNumber: "SVC-501", customerId: "c-1", customerName: "Ramco Cements Ltd", issueDescription: "Camera 3 offline since morning", assignedEmployeeId: null, assignedEmployeeName: "Arun Selvam", status: "OPEN", openedAt: "2026-07-20T09:00:00Z", closedAt: null },
  { id: "sc-2", complaintNumber: "SVC-502", customerId: "c-4", customerName: "Annapoorna Foods", issueDescription: "DVR not recording overnight", assignedEmployeeId: null, assignedEmployeeName: "Divya Ramesh", status: "IN_PROGRESS", openedAt: "2026-07-19T11:30:00Z", closedAt: null },
  { id: "sc-3", complaintNumber: "SVC-499", customerId: "c-2", customerName: "Sakthi Sugars", issueDescription: "Night vision blurry on gate cam", assignedEmployeeId: null, assignedEmployeeName: "Arun Selvam", status: "RESOLVED", openedAt: "2026-07-15T10:00:00Z", closedAt: "2026-07-17T16:00:00Z" },
];

/* -- Farm vertical --------------------------------------------------------- */
const farmBatches: FarmBatchDto[] = [
  { id: "fb-1", batchNumber: "BATCH-24", batchName: "Broiler July-A", animalType: "Broiler", startDate: "2026-06-20", quantityPurchased: 2000, purchaseAmount: 120000, status: "ACTIVE" },
  { id: "fb-2", batchNumber: "BATCH-23", batchName: "Layer June", animalType: "Layer", startDate: "2026-05-15", quantityPurchased: 1500, purchaseAmount: 135000, status: "ACTIVE" },
  { id: "fb-3", batchNumber: "BATCH-22", batchName: "Broiler June-B", animalType: "Broiler", startDate: "2026-05-01", quantityPurchased: 1800, purchaseAmount: 99000, status: "CLOSED" },
];

const feeds: FeedDto[] = [
  { id: "fd-1", feedName: "Starter Crumbs", feedType: "Starter", uom: "kg", rate: 38, isActive: true },
  { id: "fd-2", feedName: "Grower Mash", feedType: "Grower", uom: "kg", rate: 34, isActive: true },
  { id: "fd-3", feedName: "Finisher Pellets", feedType: "Finisher", uom: "kg", rate: 36, isActive: true },
];

const walletTxns: WalletTransactionDto[] = [
  { id: "wt-1", txnDate: "2026-07-18", direction: "IN", amount: 60000, reason: "Batch-22 sale settlement" },
  { id: "wt-2", txnDate: "2026-07-16", direction: "OUT", amount: 32000, reason: "Feed purchase" },
  { id: "wt-3", txnDate: "2026-07-12", direction: "IN", amount: 45000, reason: "Egg sales" },
];
let walletBalance = 84500;

// Extras accumulated from in-app writes (sales/feed/charges) so the demo P&L reacts live.
const farmExtra: Record<string, { sales: number; feed: number }> = {};
const coconutExtra: Record<string, { sales: number; labour: number; transport: number }> = {};

function farmPnl(b: FarmBatchDto): FarmBatchPnlDto {
  const ex = farmExtra[b.id] ?? { sales: 0, feed: 0 };
  const feedCost = Math.round(b.purchaseAmount * 0.9) + ex.feed;
  const medicalCost = Math.round(b.purchaseAmount * 0.08);
  const labourCost = Math.round(b.purchaseAmount * 0.12);
  const otherCost = Math.round(b.purchaseAmount * 0.05);
  const totalSales = Math.round(b.purchaseAmount * (b.status === "CLOSED" ? 2.05 : 1.15)) + ex.sales;
  const totalCost = b.purchaseAmount + feedCost + medicalCost + labourCost + otherCost;
  return {
    batchId: b.id, batchNumber: b.batchNumber, batchName: b.batchName, purchase: b.purchaseAmount,
    feedCost, medicalCost, labourCost, otherCost, totalSales, totalCost, profit: totalSales - totalCost,
  };
}

/* -- Coconut vertical ------------------------------------------------------ */
const products: CoconutProductDto[] = [
  { id: "pr-1", name: "Semi-husked Coconut", category: "Raw", uom: "pcs", isActive: true },
  { id: "pr-2", name: "Copra", category: "Processed", uom: "kg", isActive: true },
  { id: "pr-3", name: "Tender Coconut", category: "Raw", uom: "pcs", isActive: true },
];

const coconutBatches: CoconutBatchDto[] = [
  { id: "cb-1", productId: "pr-1", productName: "Semi-husked Coconut", batchNumber: "CB-31", purchaseDate: "2026-07-14", quantity: 12000, purchaseAmount: 186000, status: "ACTIVE" },
  { id: "cb-2", productId: "pr-2", productName: "Copra", batchNumber: "CB-30", purchaseDate: "2026-07-05", quantity: 3200, purchaseAmount: 224000, status: "SOLD" },
];

function coconutPnl(b: CoconutBatchDto): CoconutBatchPnlDto {
  const ex = coconutExtra[b.id] ?? { sales: 0, labour: 0, transport: 0 };
  const labourCost = Math.round(b.purchaseAmount * 0.14) + ex.labour;
  const transportCost = Math.round(b.purchaseAmount * 0.08) + ex.transport;
  const totalSales = Math.round(b.purchaseAmount * (b.status === "SOLD" ? 1.32 : 0.4)) + ex.sales;
  const totalCost = b.purchaseAmount + labourCost + transportCost;
  return {
    batchId: b.id, batchNumber: b.batchNumber, productId: b.productId, productName: b.productName,
    purchase: b.purchaseAmount, labourCost, transportCost, totalSales, totalCost, profit: totalSales - totalCost,
  };
}

/* -- Accounting ------------------------------------------------------------ */
const accounts: AccountDto[] = [
  { id: "ac-cash", code: "1000", name: "Cash", type: "Asset", isActive: true },
  { id: "ac-bank", code: "1010", name: "Bank", type: "Asset", isActive: true },
  { id: "ac-ar", code: "1100", name: "Accounts Receivable", type: "Asset", isActive: true },
  { id: "ac-sales", code: "4000", name: "Sales", type: "Income", isActive: true },
  { id: "ac-exp", code: "5000", name: "Operating Expenses", type: "Expense", isActive: true },
];

const journal: JournalTxnDto[] = [
  { id: "jt-1", txnDate: "2026-07-16", sourceModule: "Transport", narration: "Load TR-2068 billed to Ramco", lines: [
    { accountCode: "1100", accountName: "Accounts Receivable", debit: 92500, credit: 0 },
    { accountCode: "4000", accountName: "Sales", debit: 0, credit: 92500 }] },
  { id: "jt-2", txnDate: "2026-07-15", sourceModule: "Expenses", narration: "Staff & driver salaries", lines: [
    { accountCode: "5000", accountName: "Operating Expenses", debit: 96000, credit: 0 },
    { accountCode: "1000", accountName: "Cash", debit: 0, credit: 96000 }] },
  { id: "jt-3", txnDate: "2026-07-08", sourceModule: "Collections", narration: "Collection from Ramco (UPI)", lines: [
    { accountCode: "1010", accountName: "Bank", debit: 60000, credit: 0 },
    { accountCode: "1100", accountName: "Accounts Receivable", debit: 0, credit: 60000 }] },
  { id: "jt-4", txnDate: "2026-07-05", sourceModule: "Expenses", narration: "Godown rent", lines: [
    { accountCode: "5000", accountName: "Operating Expenses", debit: 35000, credit: 0 },
    { accountCode: "1000", accountName: "Cash", debit: 0, credit: 35000 }] },
];

/* -- Employees ------------------------------------------------------------- */
const employees: EmployeeDto[] = [
  { id: "em-1", name: "Priya Kumar", mobile: "9000023456", address: "Erode", joiningDate: "2024-04-01", salary: 28000, status: "ACTIVE" },
  { id: "em-2", name: "Arun Selvam", mobile: "9000034567", address: "Coimbatore", joiningDate: "2023-11-15", salary: 32000, status: "ACTIVE" },
  { id: "em-3", name: "Divya Ramesh", mobile: "9000045678", address: "Salem", joiningDate: "2025-02-10", salary: 24000, status: "ACTIVE" },
  { id: "em-4", name: "Suresh Babu", mobile: "9000056789", address: "Tiruppur", joiningDate: "2022-08-01", salary: 21000, status: "INACTIVE" },
];
const salaryByEmployee: Record<string, SalaryRecordDto[]> = {
  "em-1": [
    { id: uid("sal"), employeeId: "em-1", periodMonth: "2026-06-01", amount: 28000, paidAmount: 28000, paidOn: "2026-07-02", note: "June salary", balance: 0 },
    { id: uid("sal"), employeeId: "em-1", periodMonth: "2026-07-01", amount: 28000, paidAmount: 15000, paidOn: "2026-07-18", note: "Advance", balance: 13000 },
  ],
  "em-2": [
    { id: uid("sal"), employeeId: "em-2", periodMonth: "2026-06-01", amount: 32000, paidAmount: 32000, paidOn: "2026-07-02", note: null, balance: 0 },
  ],
};

function setPoStatus(id: string, status: string): Promise<PurchaseOrderDto> {
  const po = purchaseOrders.find((p) => p.id === id)!;
  po.status = status;
  return wait(clone(po));
}

/* A 1×1 transparent PNG stand-in so "view attachment" opens something in demo mode. */
const RECEIPT_STUB =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='420' height='560'%3E%3Crect width='420' height='560' fill='%23f1f5f9'/%3E%3Crect x='40' y='40' width='340' height='480' rx='10' fill='white' stroke='%23cbd5e1'/%3E%3Ctext x='210' y='120' font-family='Arial' font-size='22' font-weight='700' fill='%230b1220' text-anchor='middle'%3EDEMO RECEIPT%3C/text%3E%3Ctext x='210' y='160' font-family='Arial' font-size='13' fill='%2364748b' text-anchor='middle'%3ESample attachment (no backend)%3C/text%3E%3C/svg%3E";

/* -- the demo api ---------------------------------------------------------- */
export const demoApi = {
  // Auth — any credentials succeed in demo mode.
  login: (_mobileOrEmail: string, _password: string): Promise<LoginResponse> =>
    wait({
      accessToken: "demo-access-token", expiresIn: 3600, refreshToken: "demo-refresh-token",
      user: clone(user),
      memberships: businesses.map((b) => ({
        businessId: b.id, businessName: b.name, businessTypeCode: b.businessTypeCode as any,
        role: b.role ?? "OWNER", permissions: ["*"],
      })),
    }),
  logout: (): Promise<void> => Promise.resolve(),
  me: (): Promise<MeResponse> =>
    wait({
      user: clone(user),
      memberships: businesses.map((b) => ({
        businessId: b.id, businessName: b.name, businessTypeCode: b.businessTypeCode as any,
        role: b.role ?? "OWNER", permissions: ["*"],
      })),
    }),

  // Businesses / members / reference
  businesses: (): Promise<BusinessDto[]> => wait(clone(businesses)),
  createBusiness: (input: CreateBusinessInput): Promise<BusinessDto> => {
    const type = businessTypes.find((t) => t.code === input.businessTypeCode);
    const b: BusinessDto = {
      id: uid("b"), name: input.name, businessTypeCode: input.businessTypeCode,
      businessTypeName: type?.name ?? input.businessTypeCode, gstNumber: input.gstNumber ?? null,
      address: input.address ?? null, isActive: true, role: "OWNER",
    };
    businesses.push(b);
    dashboards[b.id] = {
      todayIncome: 0, todayExpense: 0, monthIncome: 0, monthExpense: 0, totalProfit: 0,
      pendingCredits: 0, pendingCollections: 0, trend: trend(40000, 15000, 0.3), expenseBreakdown: [],
    };
    return wait(clone(b));
  },
  businessTypes: (): Promise<RefItem[]> => wait(clone(businessTypes)),
  roles: (): Promise<RefItem[]> => wait(clone(roles)),
  members: (_businessId: string): Promise<MemberDto[]> => wait(clone(members)),
  inviteUser: (input: InviteUserInput): Promise<unknown> => {
    const role = roles.find((r) => r.code === input.roleCode);
    members.push({
      userId: uid("u"), fullName: input.fullName, mobile: input.mobile,
      roleCode: input.roleCode, roleName: role?.name ?? input.roleCode,
    });
    return wait({ ok: true });
  },

  dashboard: (): Promise<DashboardSummary> =>
    wait(clone(dashboards[activeBiz()] ?? dashboards["b-transport"])),

  // Expenses
  expenses: (_from?: string, _to?: string, _typeId?: string): Promise<ExpenseDto[]> => wait(clone(expenses)),
  createExpense: (input: CreateExpenseInput): Promise<ExpenseDto> => {
    const type = expenseTypes.find((t) => t.id === input.expenseTypeId);
    const row: ExpenseDto = {
      id: uid("exp"), expenseTypeId: input.expenseTypeId ?? null, expenseTypeName: type?.name ?? null,
      expenseDate: input.expenseDate, amount: input.amount, description: input.description ?? null,
      attachmentKey: input.attachmentKey ?? null,
    };
    expenses.unshift(row);
    return wait(clone(row));
  },
  deleteExpense: (id: string): Promise<void> => {
    const i = expenses.findIndex((x) => x.id === id);
    if (i >= 0) expenses.splice(i, 1);
    return wait<void>(undefined);
  },
  expenseTypes: (): Promise<RefItem[]> => wait(clone(expenseTypes)),
  expenseAttachment: (_id: string): Promise<{ url: string }> => wait({ url: RECEIPT_STUB }),

  // Customers
  customers: (): Promise<CustomerDto[]> => wait(clone(customers)),
  createCustomer: (input: CreateCustomerInput): Promise<CustomerDto> => {
    const c: CustomerDto = {
      id: uid("c"), name: input.name, mobile: input.mobile ?? null, address: input.address ?? null,
      gstNumber: input.gstNumber ?? null, creditLimit: input.creditLimit, outstanding: input.openingBalance,
    };
    customers.push(c);
    if (input.openingBalance > 0)
      ledgers[c.id] = [{ id: uid("le"), entryDate: "2026-07-21",
        refType: "Opening balance", refId: null, debit: input.openingBalance, credit: 0,
        runningBalance: input.openingBalance }];
    return wait(clone(c));
  },
  customerLedger: (id: string): Promise<LedgerEntryDto[]> => wait(clone(ledgers[id] ?? [])),
  recordCollection: (id: string, input: RecordCollectionInput): Promise<unknown> => {
    const c = customers.find((x) => x.id === id);
    if (c) c.outstanding = Math.max(0, c.outstanding - input.amount);
    const list = (ledgers[id] ??= []);
    const prev = list.length ? list[list.length - 1].runningBalance : 0;
    list.push({ id: uid("le"), entryDate: input.collectionDate, refType: `Collection (${input.mode})`,
      refId: null, debit: 0, credit: input.amount, runningBalance: Math.max(0, prev - input.amount) });
    return wait({ ok: true });
  },

  // Transport
  vehicles: (): Promise<VehicleDto[]> => wait(clone(vehicles)),
  createVehicle: (input: CreateVehicleInput): Promise<VehicleDto> => {
    const v: VehicleDto = {
      id: uid("v"), vehicleNumber: input.vehicleNumber, vehicleType: input.vehicleType ?? null,
      model: input.model ?? null, fuelType: input.fuelType ?? null, rcDetails: null,
      insuranceDetails: null, insuranceExpiry: input.insuranceExpiry ?? null, isActive: true,
    };
    vehicles.push(v);
    return wait(clone(v));
  },
  drivers: (): Promise<DriverDto[]> => wait(clone(drivers)),
  createDriver: (input: CreateDriverInput): Promise<DriverDto> => {
    const d: DriverDto = {
      id: uid("d"), name: input.name, mobile: input.mobile ?? null, driverType: input.driverType,
      salary: input.salary, isActive: true,
    };
    drivers.push(d);
    return wait(clone(d));
  },
  loads: (_from?: string, _to?: string): Promise<LoadDto[]> => wait(clone(loads)),
  createLoad: (input: CreateLoadInput): Promise<LoadDto> => {
    const profit = input.loadAmount - input.loadmanCharges - input.fuelExpense -
      input.maintenanceExpense - input.driverCharges - input.otherExpense;
    const l: LoadDto = {
      id: uid("load"), loadNumber: input.loadNumber, loadName: input.loadName ?? null,
      customerId: input.customerId ?? null, vehicleId: input.vehicleId ?? null,
      driverId: input.driverId ?? null, source: input.source ?? null, destination: input.destination ?? null,
      loadAmount: input.loadAmount, loadmanCharges: input.loadmanCharges, fuelExpense: input.fuelExpense,
      maintenanceExpense: input.maintenanceExpense, driverCharges: input.driverCharges,
      otherExpense: input.otherExpense, profit, loadDate: input.loadDate, status: "COMPLETED",
    };
    loads.unshift(l);
    if (input.customerId) {
      const cust = customers.find((c) => c.id === input.customerId);
      credits.unshift({
        id: uid("cr"), loadId: l.id, loadNumber: l.loadNumber, customerId: input.customerId,
        customerName: cust?.name ?? null, loadAmount: input.loadAmount, paidAmount: 0,
        balanceAmount: input.loadAmount, status: "PENDING",
      });
      if (cust) cust.outstanding += input.loadAmount;
    }
    return wait(clone(l));
  },
  credits: (_status?: string): Promise<CreditDto[]> => wait(clone(credits)),
  recordCreditPayment: (id: string, amount: number, _mode: string, _paymentDate?: string): Promise<CreditDto> => {
    const cr = credits.find((c) => c.id === id)!;
    cr.paidAmount += amount;
    cr.balanceAmount = Math.max(0, cr.loadAmount - cr.paidAmount);
    cr.status = cr.balanceAmount === 0 ? "CLEARED" : "PARTIAL";
    const cust = customers.find((c) => c.id === cr.customerId);
    if (cust) cust.outstanding = Math.max(0, cust.outstanding - amount);
    return wait(clone(cr));
  },

  // CCTV / Electronics
  items: (): Promise<ItemDto[]> => wait(clone(items)),
  createItem: (input: CreateItemInput): Promise<ItemDto> => {
    const it: ItemDto = {
      id: uid("it"), itemCode: input.itemCode, itemName: input.itemName, uom: input.uom,
      hsnCode: input.hsnCode ?? null, rate: input.rate, taxPercentage: input.taxPercentage,
      stockQuantity: 0, reorderLevel: input.reorderLevel, isActive: true,
    };
    items.unshift(it);
    return wait(clone(it));
  },
  suppliers: (): Promise<SupplierDto[]> => wait(clone(suppliers)),
  createSupplier: (input: CreateSupplierInput): Promise<SupplierDto> => {
    const s: SupplierDto = {
      id: uid("sup"), name: input.name, mobile: input.mobile ?? null,
      gstNumber: input.gstNumber ?? null, address: input.address ?? null,
    };
    suppliers.push(s);
    return wait(clone(s));
  },
  purchaseOrders: (_status?: string): Promise<PurchaseOrderDto[]> => wait(clone(purchaseOrders)),
  createPurchaseOrder: (input: CreatePurchaseOrderInput): Promise<PurchaseOrderDto> => {
    const sup = suppliers.find((s) => s.id === input.supplierId);
    const lines = input.lines.map((l) => ({
      id: uid("pol"), itemId: l.itemId, quantity: l.quantity, rate: l.rate,
      taxPercentage: l.taxPercentage, lineTotal: Math.round(l.quantity * l.rate * (1 + l.taxPercentage / 100)),
    }));
    const po: PurchaseOrderDto = {
      id: uid("po"), poNumber: input.poNumber, supplierId: input.supplierId, supplierName: sup?.name ?? null,
      poDate: input.poDate, totalAmount: lines.reduce((s, l) => s + l.lineTotal, 0), status: "DRAFT", lines,
    };
    purchaseOrders.unshift(po);
    return wait(clone(po));
  },
  poSubmit: (id: string): Promise<PurchaseOrderDto> => setPoStatus(id, "SUBMITTED"),
  poApprove: (id: string): Promise<PurchaseOrderDto> => setPoStatus(id, "APPROVED"),
  poReceive: (id: string): Promise<PurchaseOrderDto> => setPoStatus(id, "RECEIVED"),
  cctvSales: (_from?: string, _to?: string): Promise<SaleDto[]> => wait(clone(cctvSales)),
  serviceComplaints: (_status?: string): Promise<ServiceComplaintDto[]> => wait(clone(serviceComplaints)),
  createServiceComplaint: (input: CreateServiceComplaintInput): Promise<ServiceComplaintDto> => {
    const cust = customers.find((c) => c.id === input.customerId);
    const s: ServiceComplaintDto = {
      id: uid("sc"), complaintNumber: input.complaintNumber, customerId: input.customerId,
      customerName: cust?.name ?? null, issueDescription: input.issueDescription ?? null,
      assignedEmployeeId: input.assignedEmployeeId ?? null, assignedEmployeeName: null,
      status: "OPEN", openedAt: "2026-07-21T09:00:00Z", closedAt: null,
    };
    serviceComplaints.unshift(s);
    return wait(clone(s));
  },
  updateServiceStatus: (id: string, status: string): Promise<ServiceComplaintDto> => {
    const s = serviceComplaints.find((x) => x.id === id)!;
    s.status = status;
    s.closedAt = status === "RESOLVED" ? "2026-07-21T15:00:00Z" : null;
    return wait(clone(s));
  },

  // Farm
  farmBatches: (_status?: string): Promise<FarmBatchDto[]> => wait(clone(farmBatches)),
  createFarmBatch: (input: CreateFarmBatchInput): Promise<FarmBatchDto> => {
    const b: FarmBatchDto = {
      id: uid("fb"), batchNumber: input.batchNumber, batchName: input.batchName ?? null,
      animalType: input.animalType, startDate: input.startDate,
      quantityPurchased: input.quantityPurchased, purchaseAmount: input.purchaseAmount, status: "ACTIVE",
    };
    farmBatches.unshift(b);
    return wait(clone(b));
  },
  farmBatchPnl: (id: string): Promise<FarmBatchPnlDto> => {
    const b = farmBatches.find((x) => x.id === id) ?? farmBatches[0];
    return wait(farmPnl(b));
  },
  farmBatchSales: (id: string): Promise<FarmBatchSaleDto[]> =>
    wait([
      { id: uid("bs"), batchId: id, saleDate: "2026-07-18", saleQuantity: 800, totalWeight: 1600, saleAmount: 152000, customerId: null },
      { id: uid("bs"), batchId: id, saleDate: "2026-07-10", saleQuantity: 600, totalWeight: 1140, saleAmount: 108000, customerId: null },
    ]),
  addFeedEntry: (batchId: string, input: AddFeedEntryInput): Promise<unknown> => {
    const e = (farmExtra[batchId] ??= { sales: 0, feed: 0 });
    e.feed += input.quantity * input.rate;
    return wait({ ok: true });
  },
  addBatchSale: (batchId: string, input: AddBatchSaleInput): Promise<FarmBatchSaleDto> => {
    const e = (farmExtra[batchId] ??= { sales: 0, feed: 0 });
    e.sales += input.saleAmount;
    return wait({
      id: uid("bs"), batchId, saleDate: input.saleDate, saleQuantity: input.saleQuantity,
      totalWeight: input.totalWeight ?? null, saleAmount: input.saleAmount, customerId: input.customerId ?? null,
    });
  },
  feeds: (): Promise<FeedDto[]> => wait(clone(feeds)),
  createFeed: (input: CreateFeedInput): Promise<FeedDto> => {
    const f: FeedDto = {
      id: uid("fd"), feedName: input.feedName, feedType: input.feedType ?? null,
      uom: input.uom, rate: input.rate, isActive: true,
    };
    feeds.push(f);
    return wait(clone(f));
  },
  wallet: (): Promise<WalletDto> => wait({ balance: walletBalance }),
  walletTransactions: (): Promise<WalletTransactionDto[]> => wait(clone(walletTxns)),

  // Coconut
  coconutBatches: (_status?: string): Promise<CoconutBatchDto[]> => wait(clone(coconutBatches)),
  createCoconutBatch: (input: CreateCoconutBatchInput): Promise<CoconutBatchDto> => {
    const prod = products.find((p) => p.id === input.productId);
    const b: CoconutBatchDto = {
      id: uid("cb"), productId: input.productId, productName: prod?.name ?? null,
      batchNumber: input.batchNumber, purchaseDate: input.purchaseDate,
      quantity: input.quantity, purchaseAmount: input.purchaseAmount, status: "ACTIVE",
    };
    coconutBatches.unshift(b);
    return wait(clone(b));
  },
  coconutBatchPnl: (id: string): Promise<CoconutBatchPnlDto> => {
    const b = coconutBatches.find((x) => x.id === id) ?? coconutBatches[0];
    return wait(coconutPnl(b));
  },
  addLabourCharge: (batchId: string, input: AddLabourChargeInput): Promise<unknown> => {
    const e = (coconutExtra[batchId] ??= { sales: 0, labour: 0, transport: 0 });
    e.labour += input.amount;
    return wait({ ok: true });
  },
  addTransportCharge: (batchId: string, input: AddTransportChargeInput): Promise<unknown> => {
    const e = (coconutExtra[batchId] ??= { sales: 0, labour: 0, transport: 0 });
    e.transport += input.amount;
    return wait({ ok: true });
  },
  addCoconutSale: (batchId: string, input: AddCoconutSaleInput): Promise<unknown> => {
    const e = (coconutExtra[batchId] ??= { sales: 0, labour: 0, transport: 0 });
    e.sales += input.saleValue;
    return wait({ ok: true });
  },
  products: (): Promise<CoconutProductDto[]> => wait(clone(products)),
  createProduct: (input: CreateProductInput): Promise<CoconutProductDto> => {
    const p: CoconutProductDto = {
      id: uid("pr"), name: input.name, category: input.category ?? null, uom: input.uom, isActive: true,
    };
    products.push(p);
    return wait(clone(p));
  },

  // Employees
  employees: (): Promise<EmployeeDto[]> => wait(clone(employees)),
  createEmployee: (input: CreateEmployeeInput): Promise<EmployeeDto> => {
    const em: EmployeeDto = {
      id: uid("em"), name: input.name, mobile: input.mobile ?? null, address: input.address ?? null,
      joiningDate: input.joiningDate ?? null, salary: input.salary, status: input.status ?? "ACTIVE",
    };
    employees.unshift(em);
    return wait(clone(em));
  },
  salaryHistory: (id: string): Promise<SalaryRecordDto[]> => wait(clone(salaryByEmployee[id] ?? [])),
  recordSalary: (id: string, input: RecordSalaryInput): Promise<SalaryRecordDto> => {
    const rec: SalaryRecordDto = {
      id: uid("sal"), employeeId: id, periodMonth: input.periodMonth, amount: input.amount,
      paidAmount: input.paidAmount, paidOn: input.paidOn ?? null, note: input.note ?? null,
      balance: input.amount - input.paidAmount,
    };
    (salaryByEmployee[id] ??= []).push(rec);
    return wait(clone(rec));
  },

  // Accounting
  profitLoss: (_from?: string, _to?: string): Promise<ProfitLossDto> => {
    const d = dashboards[activeBiz()] ?? dashboards["b-transport"];
    const tr = d.trend ?? [];
    const totalIncome = tr.reduce((s, t) => s + t.income, 0);
    const totalExpense = tr.reduce((s, t) => s + t.expense, 0);
    return wait({ totalIncome, totalExpense, netProfit: totalIncome - totalExpense });
  },
  cashBook: (_from?: string, _to?: string): Promise<CashBookRowDto[]> => {
    const d = dashboards[activeBiz()] ?? dashboards["b-transport"];
    let bal = 0;
    const rows: CashBookRowDto[] = (d.trend ?? []).map((t, i) => {
      bal += t.income - t.expense;
      return { date: `2026-${String((i % 12) + 1).padStart(2, "0")}-01`, description: `${t.label} — net settlement`, in: t.income, out: t.expense, balance: bal };
    });
    return wait(rows);
  },
  accounts: (): Promise<AccountDto[]> => wait(clone(accounts)),
  journal: (_from?: string, _to?: string): Promise<JournalTxnDto[]> => wait(clone(journal)),
  ledger: (accountId?: string, _from?: string, _to?: string): Promise<LedgerLineDto[]> => {
    const acc = accounts.find((a) => a.id === accountId) ?? accounts[0];
    let bal = 0;
    const lines: LedgerLineDto[] = journal.flatMap((t) =>
      t.lines.filter((l) => l.accountCode === acc.code).map((l) => {
        bal += l.debit - l.credit;
        return { date: t.txnDate, accountCode: acc.code, accountName: acc.name, narration: t.narration, debit: l.debit, credit: l.credit, balance: bal };
      }));
    return wait(lines);
  },
  async exportReport(reportKey: string, _format: "pdf" | "excel", _from?: string, _to?: string): Promise<void> {
    // No server in demo — synthesize a small CSV so the download still works.
    const d = dashboards[activeBiz()] ?? dashboards["b-transport"];
    const rows = (d.trend ?? []).map((t) => `${t.label},${t.income},${t.expense},${t.income - t.expense}`).join("\n");
    const blob = new Blob([`Report: ${reportKey}\n\nMonth,Income,Expense,Net\n${rows}\n`], { type: "text/csv" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url; a.download = `${reportKey}-demo.csv`;
    document.body.appendChild(a); a.click(); a.remove();
    URL.revokeObjectURL(url);
  },

  // Files — no storage in demo; return stubs.
  async uploadFile(file: File, folder = "expenses"): Promise<{ objectKey: string }> {
    return wait({ objectKey: `demo/${folder}/${file.name}` });
  },
  async resolveFileUrl(url: string): Promise<string> {
    return url;
  },
};

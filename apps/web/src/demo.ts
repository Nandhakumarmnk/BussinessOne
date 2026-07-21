/* ==========================================================================
   Demo backend — an in-memory implementation of the `api` surface.

   Used when the app is built with VITE_DEMO=true (e.g. the GitHub Pages demo),
   so the whole console is fully explorable with realistic, mutable sample data
   and NO server. Every write mutates the in-memory store so the UI reacts just
   like it would against the real API. Data resets on page reload.
   ========================================================================== */

import type {
  CreateBusinessInput, CreateCustomerInput, CreateDriverInput, CreateExpenseInput,
  CreateLoadInput, CreateVehicleInput, InviteUserInput, RecordCollectionInput,
} from "./api";
import type {
  BusinessDto, CreditDto, CustomerDto, DashboardSummary, DriverDto, ExpenseDto,
  LedgerEntryDto, LoadDto, LoginResponse, MeResponse, MemberDto, RefItem, VehicleDto,
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

  // Files — no storage in demo; return stubs.
  async uploadFile(file: File, folder = "expenses"): Promise<{ objectKey: string }> {
    return wait({ objectKey: `demo/${folder}/${file.name}` });
  },
  async resolveFileUrl(url: string): Promise<string> {
    return url;
  },
};

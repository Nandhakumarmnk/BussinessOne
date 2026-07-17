/**
 * BusinessOne ERP — realistic data seeder (idempotent).
 *
 * Drives the running API over HTTP to populate all four business verticals plus the
 * shared modules (customers, employees, expenses, collections). Safe to re-run: masters
 * are matched by their natural key (name / number / code) and only created if missing;
 * keyless child records (collections, salary, attendance, feed/medical entries, wallet
 * txns, batch sales) are only added when the parent has none yet.
 *
 * Usage:
 *   node infra/seed/seed.mjs
 *
 * Config (env):
 *   ERP_BASE   API base URL         (default http://localhost:5153)
 *   ERP_USER   login mobile/email   (default owner@business-one.local)
 *   ERP_PASS   password             (default Owner@123)
 *   NODE_EXTRA_CA_CERTS  point at a CA bundle when hitting an HTTPS endpoint behind a proxy
 */

const BASE = (process.env.ERP_BASE ?? 'http://localhost:5153').replace(/\/$/, '');
const USER = process.env.ERP_USER ?? 'owner@business-one.local';
const PASS = process.env.ERP_PASS ?? 'Owner@123';

let TOKEN = null;
const idOf = (x) => x?.id ?? x?.Id ?? null;
const log = (...a) => console.log(...a);

async function api(method, path, { body, bid } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (TOKEN) headers.Authorization = `Bearer ${TOKEN}`;
  if (bid) headers['X-Business-Id'] = bid;
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json = null;
  if (text) { try { json = JSON.parse(text); } catch { json = text; } }
  if (!res.ok) {
    const detail = json?.error ? `${json.error.code}: ${json.error.message}` : (json ?? '');
    throw new Error(`${method} ${path} -> ${res.status} ${typeof detail === 'string' ? detail : JSON.stringify(detail)}`);
  }
  return json && Object.prototype.hasOwnProperty.call(json, 'data') ? json.data : json; // {data} envelope, or 204 -> null
}

const getList = async (path, bid) => {
  const d = await api('GET', path, { bid });
  return Array.isArray(d) ? d : (d?.items ?? []);
};

/** create-or-get by natural key. Returns { entity, created }. */
async function ensure({ label, listPath, match, createPath, body, bid }) {
  const existing = (await getList(listPath, bid)).find(match);
  if (existing) { log(`   = ${label}`); return { entity: existing, created: false }; }
  const entity = await api('POST', createPath, { body, bid });
  log(`   + ${label}`);
  return { entity, created: true };
}

async function login() {
  const d = await api('POST', '/api/v1/auth/login', { body: { mobileOrEmail: USER, password: PASS } });
  TOKEN = d.accessToken ?? d.AccessToken;
  if (!TOKEN) throw new Error('login returned no access token');
  log(`Logged in as ${d.user?.fullName ?? USER}`);
}

async function ensureBusiness(name, typeCode) {
  return ensure({
    label: `business ${name} (${typeCode})`,
    listPath: '/api/v1/businesses',
    match: (b) => b.name === name,
    createPath: '/api/v1/businesses',
    body: { name, businessTypeCode: typeCode },
  });
}

// ---- shared module seeders (business-scoped) ----

async function seedExpenses(bid, types, rows) {
  const typeMap = {};
  for (const t of types) {
    const { entity } = await ensure({
      label: `expense-type ${t}`, bid,
      listPath: '/api/v1/expense-types', match: (x) => x.name === t,
      createPath: '/api/v1/expense-types', body: { name: t },
    });
    typeMap[t] = idOf(entity);
  }
  if ((await getList('/api/v1/expenses', bid)).length === 0) {
    for (const r of rows) {
      await api('POST', '/api/v1/expenses', {
        bid, body: { expenseTypeId: typeMap[r.type] ?? null, expenseDate: r.date, amount: r.amount, description: r.desc },
      });
    }
    log(`   + ${rows.length} expenses`);
  } else log('   = expenses');
  return typeMap;
}

async function seedEmployee(bid, e) {
  const { entity, created } = await ensure({
    label: `employee ${e.name}`, bid,
    listPath: '/api/v1/employees', match: (x) => x.name === e.name,
    createPath: '/api/v1/employees',
    body: { name: e.name, mobile: e.mobile, address: e.address ?? null, joiningDate: e.joiningDate, salary: e.salary, status: 'active' },
  });
  const id = idOf(entity);
  if ((await getList(`/api/v1/employees/${id}/salary`, bid)).length === 0 && e.salaryRun) {
    await api('POST', `/api/v1/employees/${id}/salary`, {
      bid, body: { periodMonth: e.salaryRun.month, amount: e.salary, paidAmount: e.salaryRun.paid, paidOn: e.salaryRun.paidOn, note: 'Monthly salary' },
    });
    log(`     + salary for ${e.name}`);
  }
  if (e.attendance && (await getList(`/api/v1/employees/${id}/attendance?year=${e.attendance.year}&month=${e.attendance.month}`, bid)).length === 0) {
    for (const a of e.attendance.days) {
      await api('POST', `/api/v1/employees/${id}/attendance`, { bid, body: { attendanceDate: a.date, status: a.status } });
    }
    log(`     + attendance for ${e.name}`);
  }
  return id;
}

async function seedCustomer(bid, c) {
  const { entity } = await ensure({
    label: `customer ${c.name}`, bid,
    listPath: '/api/v1/customers', match: (x) => x.name === c.name,
    createPath: '/api/v1/customers',
    body: { name: c.name, mobile: c.mobile, address: c.address ?? null, gstNumber: c.gst ?? null, creditLimit: c.creditLimit ?? 0, openingBalance: c.openingBalance ?? 0 },
  });
  const id = idOf(entity);
  if (c.collection && (await getList(`/api/v1/customers/${id}/collections`, bid)).length === 0) {
    await api('POST', `/api/v1/customers/${id}/collections`, {
      bid, body: { collectionDate: c.collection.date, amount: c.collection.amount, mode: c.collection.mode ?? 'cash', reference: c.collection.ref ?? null },
    });
    log(`     + collection from ${c.name}`);
  }
  return id;
}

// ---- vertical seeders ----

async function seedTransport(bid) {
  log('\n[Transport] Sri Transport');
  await seedExpenses(bid, ['Fuel', 'Maintenance', 'Toll & Permit', 'Office'], [
    { type: 'Fuel', date: '2026-06-04', amount: 8200, desc: 'Diesel — fleet' },
    { type: 'Maintenance', date: '2026-06-11', amount: 3400, desc: 'Tyre replacement' },
    { type: 'Toll & Permit', date: '2026-06-18', amount: 1500, desc: 'Interstate permit' },
    { type: 'Office', date: '2026-07-02', amount: 2100, desc: 'Stationery & printing' },
  ]);
  await seedEmployee(bid, { name: 'Manikandan R', mobile: '9843012001', joiningDate: '2024-03-01', salary: 22000,
    salaryRun: { month: '2026-06-01', paid: 22000, paidOn: '2026-07-01' },
    attendance: { year: 2026, month: 7, days: [{ date: '2026-07-01', status: 'present' }, { date: '2026-07-02', status: 'present' }, { date: '2026-07-03', status: 'half' }] } });
  await seedEmployee(bid, { name: 'Selvam K', mobile: '9843012002', joiningDate: '2023-08-15', salary: 18000,
    salaryRun: { month: '2026-06-01', paid: 15000, paidOn: '2026-07-01' } });

  const custA = await seedCustomer(bid, { name: 'Anand Traders', mobile: '9840011111', address: 'Coimbatore', creditLimit: 200000, openingBalance: 15000,
    collection: { date: '2026-06-20', amount: 15000, mode: 'upi', ref: 'Opening cleared' } });
  const custB = await seedCustomer(bid, { name: 'Sri Balaji Steels', mobile: '9840022222', address: 'Salem', creditLimit: 300000, openingBalance: 0 });
  const custC = await seedCustomer(bid, { name: 'Kovai Cement Depot', mobile: '9840033333', address: 'Coimbatore', creditLimit: 250000, openingBalance: 0 });

  const veh1 = idOf((await ensure({ label: 'vehicle TN37 BX 1234', bid, listPath: '/api/v1/transport/vehicles', match: (v) => v.vehicleNumber === 'TN37 BX 1234', createPath: '/api/v1/transport/vehicles', body: { vehicleNumber: 'TN37 BX 1234', vehicleType: 'Lorry', model: 'Ashok Leyland 3118', fuelType: 'Diesel', rcDetails: 'RC-1234', insuranceDetails: 'ICICI Lombard', insuranceExpiry: '2027-01-31' } })).entity);
  const veh2 = idOf((await ensure({ label: 'vehicle TN37 CY 5678', bid, listPath: '/api/v1/transport/vehicles', match: (v) => v.vehicleNumber === 'TN37 CY 5678', createPath: '/api/v1/transport/vehicles', body: { vehicleNumber: 'TN37 CY 5678', vehicleType: 'Truck', model: 'Tata LPT 1618', fuelType: 'Diesel', rcDetails: 'RC-5678', insuranceDetails: 'New India', insuranceExpiry: '2026-11-30' } })).entity);
  const veh3 = idOf((await ensure({ label: 'vehicle TN37 DZ 9012', bid, listPath: '/api/v1/transport/vehicles', match: (v) => v.vehicleNumber === 'TN37 DZ 9012', createPath: '/api/v1/transport/vehicles', body: { vehicleNumber: 'TN37 DZ 9012', vehicleType: 'Mini Truck', model: 'Mahindra Bolero', fuelType: 'Diesel', rcDetails: 'RC-9012', insuranceDetails: 'HDFC Ergo', insuranceExpiry: '2027-03-15' } })).entity);

  const drv1 = idOf((await ensure({ label: 'driver Ravi', bid, listPath: '/api/v1/transport/drivers', match: (d) => d.name === 'Ravi Kumar', createPath: '/api/v1/transport/drivers', body: { name: 'Ravi Kumar', mobile: '9843022001', driverType: 'salaried', salary: 20000 } })).entity);
  const drv2 = idOf((await ensure({ label: 'driver Murugan', bid, listPath: '/api/v1/transport/drivers', match: (d) => d.name === 'Murugan S', createPath: '/api/v1/transport/drivers', body: { name: 'Murugan S', mobile: '9843022002', driverType: 'self', salary: 0 } })).entity);

  const loads = [
    { loadNumber: 'LD-2026-001', loadName: 'Cement bags', customerId: custA, vehicleId: veh1, driverId: drv1, source: 'Coimbatore', destination: 'Salem', loadDate: '2026-06-06', loadAmount: 18000, loadmanCharges: 800, fuelExpense: 4200, maintenanceExpense: 600, driverCharges: 1500, otherExpense: 300 },
    { loadNumber: 'LD-2026-002', loadName: 'Steel rods', customerId: custB, vehicleId: veh2, driverId: drv2, source: 'Salem', destination: 'Erode', loadDate: '2026-06-12', loadAmount: 24000, loadmanCharges: 1200, fuelExpense: 5600, maintenanceExpense: 900, driverCharges: 2000, otherExpense: 500 },
    { loadNumber: 'LD-2026-003', loadName: 'Textiles', customerId: custC, vehicleId: veh3, driverId: drv1, source: 'Tirupur', destination: 'Chennai', loadDate: '2026-06-19', loadAmount: 32000, loadmanCharges: 1500, fuelExpense: 8200, maintenanceExpense: 1200, driverCharges: 3000, otherExpense: 700 },
    { loadNumber: 'LD-2026-004', loadName: 'Machinery', customerId: custA, vehicleId: veh1, driverId: drv2, source: 'Coimbatore', destination: 'Bengaluru', loadDate: '2026-07-03', loadAmount: 45000, loadmanCharges: 2000, fuelExpense: 12000, maintenanceExpense: 1800, driverCharges: 4000, otherExpense: 1000 },
    { loadNumber: 'LD-2026-005', loadName: 'FMCG goods', customerId: custB, vehicleId: veh2, driverId: drv1, source: 'Erode', destination: 'Madurai', loadDate: '2026-07-10', loadAmount: 21000, loadmanCharges: 1000, fuelExpense: 5200, maintenanceExpense: 700, driverCharges: 1800, otherExpense: 400 },
  ];
  let anyLoadCreated = false;
  for (const l of loads) {
    const r = await ensure({ label: `load ${l.loadNumber}`, bid, listPath: '/api/v1/transport/loads', match: (x) => x.loadNumber === l.loadNumber, createPath: '/api/v1/transport/loads', body: l });
    anyLoadCreated = anyLoadCreated || r.created;
  }
  // Record partial payments on a couple of the credits — only on the run that created the loads (keeps re-runs idempotent).
  if (anyLoadCreated) {
    const credits = (await getList('/api/v1/transport/credits', bid)).filter((c) => (c.paidAmount ?? 0) === 0 && (c.balanceAmount ?? 0) > 0);
    for (const c of credits.slice(0, 2)) {
      await api('PATCH', `/api/v1/transport/credits/${idOf(c)}/payment`, { bid, body: { amount: Math.round((c.balanceAmount) * 0.5), mode: 'bank', paymentDate: '2026-07-12' } });
      log(`     + partial payment on credit ${c.loadNumber ?? idOf(c)}`);
    }
  }
}

async function seedCctv(bid) {
  log('\n[CCTV] Bright Vision Systems');
  await seedExpenses(bid, ['Rent', 'Salaries', 'Travel', 'Utilities'], [
    { type: 'Rent', date: '2026-06-01', amount: 15000, desc: 'Showroom rent' },
    { type: 'Travel', date: '2026-06-14', amount: 2600, desc: 'Site survey travel' },
    { type: 'Utilities', date: '2026-07-01', amount: 3200, desc: 'Electricity + internet' },
  ]);
  const emp1 = await seedEmployee(bid, { name: 'Vignesh P', mobile: '9843033001', joiningDate: '2024-01-10', salary: 24000, salaryRun: { month: '2026-06-01', paid: 24000, paidOn: '2026-07-01' } });
  await seedEmployee(bid, { name: 'Dinesh M', mobile: '9843033002', joiningDate: '2025-05-20', salary: 19000 });

  const custX = await seedCustomer(bid, { name: 'Green Park Apartments', mobile: '9841044001', address: 'Coimbatore', creditLimit: 150000 });
  const custY = await seedCustomer(bid, { name: 'SKV Supermarket', mobile: '9841044002', address: 'Tirupur', creditLimit: 100000 });

  const sup1 = idOf((await ensure({ label: 'supplier Hikvision Dist.', bid, listPath: '/api/v1/cctv/suppliers', match: (s) => s.name === 'Hikvision Distributors', createPath: '/api/v1/cctv/suppliers', body: { name: 'Hikvision Distributors', mobile: '9812000001', gstNumber: '33ABCDE1234F1Z5', address: 'Chennai' } })).entity);
  await ensure({ label: 'supplier CP Plus Agency', bid, listPath: '/api/v1/cctv/suppliers', match: (s) => s.name === 'CP Plus Agency', createPath: '/api/v1/cctv/suppliers', body: { name: 'CP Plus Agency', mobile: '9812000002', gstNumber: '33ZYXWV9876K1Z2', address: 'Coimbatore' } });

  const items = [
    { itemCode: 'CAM-DOME-2MP', itemName: 'Dome Camera 2MP', uom: 'pcs', hsnCode: '85258900', rate: 1800, taxPercentage: 18, reorderLevel: 5, poQty: 25 },
    { itemCode: 'CAM-BULLET-4MP', itemName: 'Bullet Camera 4MP', uom: 'pcs', hsnCode: '85258900', rate: 2600, taxPercentage: 18, reorderLevel: 5, poQty: 15 },
    { itemCode: 'DVR-8CH', itemName: '8 Channel DVR', uom: 'pcs', hsnCode: '85219090', rate: 4500, taxPercentage: 18, reorderLevel: 2, poQty: 6 },
    { itemCode: 'HDD-1TB', itemName: 'Surveillance HDD 1TB', uom: 'pcs', hsnCode: '84717020', rate: 3800, taxPercentage: 18, reorderLevel: 3, poQty: 8 },
    { itemCode: 'CABLE-90M', itemName: 'CCTV Cable 3+1 (90m)', uom: 'roll', hsnCode: '85444999', rate: 1200, taxPercentage: 18, reorderLevel: 10, poQty: 20 },
  ];
  const itemIds = {};
  for (const it of items) {
    const { entity } = await ensure({ label: `item ${it.itemCode}`, bid, listPath: '/api/v1/cctv/items', match: (x) => x.itemCode === it.itemCode, createPath: '/api/v1/cctv/items', body: { itemCode: it.itemCode, itemName: it.itemName, uom: it.uom, hsnCode: it.hsnCode, rate: it.rate, taxPercentage: it.taxPercentage, reorderLevel: it.reorderLevel } });
    itemIds[it.itemCode] = idOf(entity);
  }

  // Purchase order → stock-in via the draft→submit→approve→receive state machine.
  const poNumber = 'PO-2026-001';
  const po = (await ensure({ label: `PO ${poNumber}`, bid, listPath: '/api/v1/cctv/purchase-orders', match: (p) => p.poNumber === poNumber, createPath: '/api/v1/cctv/purchase-orders', body: { poNumber, supplierId: sup1, poDate: '2026-06-02', note: 'Opening stock', lines: items.map((it) => ({ itemId: itemIds[it.itemCode], quantity: it.poQty, rate: it.rate, taxPercentage: it.taxPercentage })) } })).entity;
  await drivePoToReceived(bid, idOf(po));

  // Sales (decrement stock). Partial payment on the first, full on the second.
  await ensureSale(bid, { invoiceNumber: 'INV-2026-101', customerId: custX, saleDate: '2026-06-22', installationCharges: 2000, labourCharges: 1500, paidAmount: 8000, mode: 'cash', lines: [ { itemId: itemIds['CAM-DOME-2MP'], quantity: 4, rate: 1800, taxPercentage: 18 }, { itemId: itemIds['DVR-8CH'], quantity: 1, rate: 4500, taxPercentage: 18 }, { itemId: itemIds['HDD-1TB'], quantity: 1, rate: 3800, taxPercentage: 18 } ] });
  await ensureSale(bid, { invoiceNumber: 'INV-2026-102', customerId: custY, saleDate: '2026-07-05', installationCharges: 3000, labourCharges: 2500, paidAmount: 0, mode: 'cash', lines: [ { itemId: itemIds['CAM-BULLET-4MP'], quantity: 6, rate: 2600, taxPercentage: 18 }, { itemId: itemIds['CABLE-90M'], quantity: 2, rate: 1200, taxPercentage: 18 } ] });

  // Service complaints
  const sc1 = await ensureComplaint(bid, { complaintNumber: 'SC-2026-001', customerId: custX, issueDescription: 'Camera 3 offline at gate' });
  if (sc1.created) {
    await api('PATCH', `/api/v1/cctv/service-complaints/${idOf(sc1.entity)}/assign`, { bid, body: { employeeId: emp1 } });
    await api('PATCH', `/api/v1/cctv/service-complaints/${idOf(sc1.entity)}/status`, { bid, body: { status: 'in_progress' } });
    log('     ~ SC-2026-001 assigned + in_progress');
  }
  await ensureComplaint(bid, { complaintNumber: 'SC-2026-002', customerId: custY, issueDescription: 'DVR not recording at night' });
}

async function drivePoToReceived(bid, poId) {
  for (let i = 0; i < 6; i++) {
    const po = await api('GET', `/api/v1/cctv/purchase-orders/${poId}`, { bid });
    const status = po.status ?? po.Status;
    if (status === 'received' || status === 'cancelled') return;
    const next = status === 'draft' ? 'submit' : status === 'pending' ? 'approve' : status === 'approved' ? 'receive' : null;
    if (!next) return;
    await api('POST', `/api/v1/cctv/purchase-orders/${poId}/${next}`, { bid });
    log(`     ~ PO ${next} (${status} -> ...)`);
  }
}

async function ensureSale(bid, sale) {
  const existing = (await getList('/api/v1/cctv/sales', bid)).find((s) => s.invoiceNumber === sale.invoiceNumber);
  if (existing) { log(`   = sale ${sale.invoiceNumber}`); return existing; }
  const created = await api('POST', '/api/v1/cctv/sales', { bid, body: sale });
  log(`   + sale ${sale.invoiceNumber}`);
  return created;
}

async function ensureComplaint(bid, c) {
  return ensure({ label: `complaint ${c.complaintNumber}`, bid, listPath: '/api/v1/cctv/service-complaints', match: (x) => x.complaintNumber === c.complaintNumber, createPath: '/api/v1/cctv/service-complaints', body: c });
}

async function seedFarm(bid) {
  log('\n[Farm] Green Valley Farm');
  await seedExpenses(bid, ['Feed', 'Labour', 'Utilities', 'Veterinary'], [
    { type: 'Labour', date: '2026-06-05', amount: 6000, desc: 'Shed cleaning' },
    { type: 'Utilities', date: '2026-06-20', amount: 2400, desc: 'Water & power' },
  ]);
  await seedEmployee(bid, { name: 'Palanisamy G', mobile: '9843044001', joiningDate: '2024-06-01', salary: 16000, salaryRun: { month: '2026-06-01', paid: 16000, paidOn: '2026-07-01' } });
  await seedCustomer(bid, { name: 'Farm Fresh Retailers', mobile: '9842055001', address: 'Coimbatore', creditLimit: 80000 });

  const feeds = {
    layer: idOf((await ensure({ label: 'feed Layer Mash', bid, listPath: '/api/v1/farm/feeds', match: (f) => f.feedName === 'Layer Mash', createPath: '/api/v1/farm/feeds', body: { feedName: 'Layer Mash', feedType: 'poultry', uom: 'kg', rate: 34 } })).entity),
    goat: idOf((await ensure({ label: 'feed Goat Feed', bid, listPath: '/api/v1/farm/feeds', match: (f) => f.feedName === 'Goat Feed', createPath: '/api/v1/farm/feeds', body: { feedName: 'Goat Feed', feedType: 'ruminant', uom: 'kg', rate: 40 } })).entity),
  };

  // Wallet — cash float (add before use; balance is guarded server-side).
  if ((await getList('/api/v1/farm/wallet/transactions', bid)).length === 0) {
    await api('POST', '/api/v1/farm/wallet/add', { bid, body: { amount: 200000, reason: 'Owner capital injection', date: '2026-06-01' } });
    await api('POST', '/api/v1/farm/wallet/use', { bid, body: { amount: 45000, reason: 'Feed purchase', date: '2026-06-15' } });
    log('   + wallet add/use');
  } else log('   = wallet');

  const goatBatch = idOf((await ensure({ label: 'batch GB-2026-01 (goat)', bid, listPath: '/api/v1/farm/batches', match: (b) => b.batchNumber === 'GB-2026-01', createPath: '/api/v1/farm/batches', body: { batchNumber: 'GB-2026-01', batchName: 'Goat batch — Jun', animalType: 'goat', startDate: '2026-06-02', quantityPurchased: 30, purchaseAmount: 180000 } })).entity);
  const henBatch = idOf((await ensure({ label: 'batch HB-2026-01 (hen)', bid, listPath: '/api/v1/farm/batches', match: (b) => b.batchNumber === 'HB-2026-01', createPath: '/api/v1/farm/batches', body: { batchNumber: 'HB-2026-01', batchName: 'Layer batch — Jun', animalType: 'hen', startDate: '2026-06-03', quantityPurchased: 500, purchaseAmount: 60000 } })).entity);

  await seedBatchChildren(bid, goatBatch, {
    feed: { feedId: feeds.goat, entryDate: '2026-06-18', quantity: 400, rate: 40 },
    medical: { medicineName: 'Deworming + vaccination', amount: 3200, doctorCharges: 1500, recordDate: '2026-06-25' },
    expense: { expenseKind: 'labour', amount: 5000, expenseDate: '2026-06-28', description: 'Herding labour' },
  });
  await seedBatchChildren(bid, henBatch, {
    feed: { feedId: feeds.layer, entryDate: '2026-06-20', quantity: 900, rate: 34 },
    medical: { medicineName: 'Poultry vitamins', amount: 1800, doctorCharges: 800, recordDate: '2026-06-27' },
    expense: { expenseKind: 'other', amount: 2200, expenseDate: '2026-07-01', description: 'Sawdust bedding' },
  });

  // Sell the hen batch (marks it sold) — only if it has no sales yet.
  if ((await getList(`/api/v1/farm/batches/${henBatch}/sales`, bid)).length === 0) {
    await api('POST', `/api/v1/farm/batches/${henBatch}/sales`, { bid, body: { saleDate: '2026-07-08', saleQuantity: 480, totalWeight: 960, saleAmount: 132000, customerId: null } });
    log('   + hen batch sale (marked sold)');
  } else log('   = hen batch sale');
}

async function seedBatchChildren(bid, batchId, { feed, medical, expense }) {
  if ((await getList(`/api/v1/farm/batches/${batchId}/feed-entries`, bid)).length === 0) {
    await api('POST', `/api/v1/farm/batches/${batchId}/feed-entries`, { bid, body: feed });
  }
  if ((await getList(`/api/v1/farm/batches/${batchId}/medical`, bid)).length === 0) {
    await api('POST', `/api/v1/farm/batches/${batchId}/medical`, { bid, body: medical });
  }
  if ((await getList(`/api/v1/farm/batches/${batchId}/expenses`, bid)).length === 0) {
    await api('POST', `/api/v1/farm/batches/${batchId}/expenses`, { bid, body: expense });
  }
  log(`     ~ batch children ensured`);
}

async function seedCoconut(bid) {
  log('\n[Coconut] Kerala Coconut Traders');
  await seedExpenses(bid, ['Loading', 'Transport', 'Commission'], [
    { type: 'Loading', date: '2026-06-08', amount: 3500, desc: 'Loading wages' },
    { type: 'Commission', date: '2026-06-22', amount: 2800, desc: 'Broker commission' },
  ]);
  await seedCustomer(bid, { name: 'Coco Exports Pvt Ltd', mobile: '9842066001', address: 'Pollachi', creditLimit: 400000 });

  const products = {
    coconut: idOf((await ensure({ label: 'product Coconut', bid, listPath: '/api/v1/coconut/products', match: (p) => p.name === 'Coconut', createPath: '/api/v1/coconut/products', body: { name: 'Coconut', category: 'Raw', uom: 'nos' } })).entity),
    copra: idOf((await ensure({ label: 'product Copra', bid, listPath: '/api/v1/coconut/products', match: (p) => p.name === 'Copra', createPath: '/api/v1/coconut/products', body: { name: 'Copra', category: 'Processed', uom: 'kg' } })).entity),
    oil: idOf((await ensure({ label: 'product Coconut Oil', bid, listPath: '/api/v1/coconut/products', match: (p) => p.name === 'Coconut Oil', createPath: '/api/v1/coconut/products', body: { name: 'Coconut Oil', category: 'Processed', uom: 'litre' } })).entity),
    powder: idOf((await ensure({ label: 'product Coconut Powder', bid, listPath: '/api/v1/coconut/products', match: (p) => p.name === 'Coconut Powder', createPath: '/api/v1/coconut/products', body: { name: 'Coconut Powder', category: 'Processed', uom: 'kg' } })).entity),
  };

  const b1 = idOf((await ensure({ label: 'coconut batch CB-2026-01', bid, listPath: '/api/v1/coconut/batches', match: (b) => b.batchNumber === 'CB-2026-01', createPath: '/api/v1/coconut/batches', body: { productId: products.coconut, batchNumber: 'CB-2026-01', purchaseDate: '2026-06-09', quantity: 5000, purchaseAmount: 60000 } })).entity);
  const b2 = idOf((await ensure({ label: 'coconut batch CB-2026-02', bid, listPath: '/api/v1/coconut/batches', match: (b) => b.batchNumber === 'CB-2026-02', createPath: '/api/v1/coconut/batches', body: { productId: products.copra, batchNumber: 'CB-2026-02', purchaseDate: '2026-06-24', quantity: 800, purchaseAmount: 48000 } })).entity);

  await seedCoconutBatchChildren(bid, b1, {
    labour: { labourName: 'Dehusking team', amount: 4000, chargeDate: '2026-06-11' },
    transport: { vehicle: 'TN38 AA 1010', amount: 3000, chargeDate: '2026-06-12' },
    sale: { saleDate: '2026-06-16', saleQuantity: 5000, saleValue: 84000 },
  });
  await seedCoconutBatchChildren(bid, b2, {
    labour: { labourName: 'Drying yard', amount: 2500, chargeDate: '2026-06-26' },
    transport: { vehicle: 'TN38 AA 1010', amount: 2000, chargeDate: '2026-06-27' },
    sale: { saleDate: '2026-07-04', saleQuantity: 800, saleValue: 66000 },
  });
}

async function seedCoconutBatchChildren(bid, batchId, { labour, transport, sale }) {
  if ((await getList(`/api/v1/coconut/batches/${batchId}/labour-charges`, bid)).length === 0) {
    await api('POST', `/api/v1/coconut/batches/${batchId}/labour-charges`, { bid, body: labour });
  }
  if ((await getList(`/api/v1/coconut/batches/${batchId}/transport-charges`, bid)).length === 0) {
    await api('POST', `/api/v1/coconut/batches/${batchId}/transport-charges`, { bid, body: transport });
  }
  if ((await getList(`/api/v1/coconut/batches/${batchId}/sales`, bid)).length === 0) {
    await api('POST', `/api/v1/coconut/batches/${batchId}/sales`, { bid, body: sale });
    log('     + coconut batch sale (marked sold)');
  }
  log('     ~ coconut batch children ensured');
}

async function verify(businesses) {
  log('\n===== Verification =====');
  for (const b of businesses) {
    const bid = idOf(b);
    try {
      const s = await api('GET', '/api/v1/dashboard/summary', { bid });
      log(`\n${b.name} (${b.businessTypeCode})`);
      log('  dashboard:', JSON.stringify(s));
    } catch (e) {
      log(`\n${b.name}: dashboard error — ${e.message}`);
    }
  }
}

async function main() {
  log(`Seeding ${BASE} ...`);
  await login();

  const transport = (await ensureBusiness('Sri Transport', 'TRANSPORT')).entity;
  const cctv = (await ensureBusiness('Bright Vision Systems', 'CCTV')).entity;
  const farm = (await ensureBusiness('Green Valley Farm', 'FARM')).entity;
  const coconut = (await ensureBusiness('Kerala Coconut Traders', 'COCONUT')).entity;

  await seedTransport(idOf(transport));
  await seedCctv(idOf(cctv));
  await seedFarm(idOf(farm));
  await seedCoconut(idOf(coconut));

  await verify([transport, cctv, farm, coconut]);
  log('\nSeed complete.');
}

main().catch((e) => { console.error('\nSEED FAILED:', e.message); process.exit(1); });

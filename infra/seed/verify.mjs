// Spot-check that seeded vertical data is coherent (stock, credits/outstanding, batch P&L).
// Usage: node infra/seed/verify.mjs
const BASE = (process.env.ERP_BASE ?? 'http://localhost:5153').replace(/\/$/, '');
const USER = process.env.ERP_USER ?? 'owner@business-one.local';
const PASS = process.env.ERP_PASS ?? 'Owner@123';
let TOKEN = null;

async function api(method, path, bid) {
  const h = { 'Content-Type': 'application/json' };
  if (TOKEN) h.Authorization = `Bearer ${TOKEN}`;
  if (bid) h['X-Business-Id'] = bid;
  const res = await fetch(`${BASE}${path}`, { method, headers: h });
  const t = await res.text();
  const j = t ? JSON.parse(t) : null;
  if (!res.ok) throw new Error(`${path} -> ${res.status} ${t}`);
  return j && 'data' in j ? j.data : j;
}
const idOf = (x) => x.id ?? x.Id;

{ // login
  const res = await fetch(`${BASE}/api/v1/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ mobileOrEmail: USER, password: PASS }) });
  TOKEN = (await res.json()).data.accessToken;
}

const businesses = await api('GET', '/api/v1/businesses');
const byType = Object.fromEntries(businesses.map((b) => [b.businessTypeCode, b]));

console.log('\n== TRANSPORT ==');
const tId = idOf(byType.TRANSPORT);
console.log('credits:', JSON.stringify(await api('GET', '/api/v1/transport/credits', tId)));
console.log('outstanding:', JSON.stringify(await api('GET', '/api/v1/reports/outstanding', tId)));

console.log('\n== CCTV (item stock after PO receive + 2 sales) ==');
const cId = idOf(byType.CCTV);
for (const it of await api('GET', '/api/v1/cctv/items', cId)) console.log(`  ${it.itemCode}: stock=${it.stock ?? it.stockQuantity ?? '?'}`);

console.log('\n== FARM batch P&L ==');
const fId = idOf(byType.FARM);
for (const b of await api('GET', '/api/v1/farm/batches', fId)) console.log(`  ${b.batchNumber} (${b.status}):`, JSON.stringify(await api('GET', `/api/v1/farm/batches/${idOf(b)}/pnl`, fId)));

console.log('\n== COCONUT batch P&L ==');
const kId = idOf(byType.COCONUT);
for (const b of await api('GET', '/api/v1/coconut/batches', kId)) console.log(`  ${b.batchNumber} (${b.status}):`, JSON.stringify(await api('GET', `/api/v1/coconut/batches/${idOf(b)}/pnl`, kId)));
console.log('  product-profit:', JSON.stringify(await api('GET', '/api/v1/coconut/reports/product-profit', kId)));

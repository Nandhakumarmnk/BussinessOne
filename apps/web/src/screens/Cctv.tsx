import { useEffect, useState } from "react";
import { api } from "../api";
import type { CustomerDto, ItemDto, PurchaseOrderDto, SaleDto, ServiceComplaintDto } from "../types";
import { Icon, inr, ListSkeleton, prettyStatus, statusBadgeClass } from "../ui";

type Tab = "items" | "orders" | "sales" | "service";

export function CctvScreen({ setError }: { setError: (e: string | null) => void }) {
  const [tab, setTab] = useState<Tab>("items");
  const [items, setItems] = useState<ItemDto[]>([]);
  const [orders, setOrders] = useState<PurchaseOrderDto[]>([]);
  const [sales, setSales] = useState<SaleDto[]>([]);
  const [service, setService] = useState<ServiceComplaintDto[]>([]);
  const [customers, setCustomers] = useState<CustomerDto[]>([]);
  const [loading, setLoading] = useState(true);

  async function loadAll() {
    try {
      const [i, o, s, sv, c] = await Promise.all([
        api.items(), api.purchaseOrders(), api.cctvSales(), api.serviceComplaints(), api.customers(),
      ]);
      setItems(i); setOrders(o); setSales(s); setService(sv); setCustomers(c);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load CCTV data");
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => { loadAll(); /* eslint-disable-next-line */ }, []);

  const tabs: { id: Tab; label: string; icon: string; count: number }[] = [
    { id: "items", label: "Inventory", icon: "receipt", count: items.length },
    { id: "orders", label: "Purchase Orders", icon: "card", count: orders.length },
    { id: "sales", label: "Sales", icon: "wallet", count: sales.length },
    { id: "service", label: "Service", icon: "shield", count: service.length },
  ];

  return (
    <section id="cctv">
      <div className="section__head">
        <div className="section__title"><Icon name="camera" />Electronics &amp; CCTV</div>
        <div className="section__sub">Inventory with stock, purchase orders, sales &amp; installation, and the service desk.</div>
      </div>

      <div className="tabs">
        {tabs.map((t) => (
          <button key={t.id} className={`tab${tab === t.id ? " is-active" : ""}`} onClick={() => setTab(t.id)}>
            <Icon name={t.icon} />{t.label}<span className="count">{t.count}</span>
          </button>
        ))}
      </div>

      {loading ? <ListSkeleton /> : (
        <>
          {tab === "items" && <ItemsTab items={items} setError={setError} reload={loadAll} />}
          {tab === "orders" && <OrdersTab orders={orders} setError={setError} reload={loadAll} />}
          {tab === "sales" && <SalesTab sales={sales} />}
          {tab === "service" && <ServiceTab service={service} customers={customers} setError={setError} reload={loadAll} />}
        </>
      )}
    </section>
  );
}

/* -------------------------------------------------------------------------- */
function ItemsTab({ items, setError, reload }: {
  items: ItemDto[]; setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  const [f, setF] = useState({ itemCode: "", itemName: "", uom: "pcs", rate: "", taxPercentage: "18", reorderLevel: "5" });
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value });

  const lowStock = items.filter((i) => i.stockQuantity <= i.reorderLevel).length;
  const stockValue = items.reduce((s, i) => s + i.stockQuantity * i.rate, 0);

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.createItem({
        itemCode: f.itemCode, itemName: f.itemName, uom: f.uom, rate: Number(f.rate || 0),
        taxPercentage: Number(f.taxPercentage || 0), reorderLevel: Number(f.reorderLevel || 0),
      });
      setF({ ...f, itemCode: "", itemName: "", rate: "" });
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create item");
    } finally { setBusy(false); }
  }

  return (
    <>
      <div className="kpis" style={{ marginBottom: 22 }}>
        <div className="kpi t-info"><div className="kpi__top"><div className="kpi__icon"><Icon name="receipt" /></div></div>
          <div className="kpi__label">Items</div><div className="kpi__value">{items.length}</div></div>
        <div className="kpi t-warn"><div className="kpi__top"><div className="kpi__icon"><Icon name="down" /></div></div>
          <div className="kpi__label">Low stock</div><div className="kpi__value">{lowStock}</div></div>
        <div className="kpi t-pos"><div className="kpi__top"><div className="kpi__icon"><Icon name="wallet" /></div></div>
          <div className="kpi__label">Stock value</div><div className="kpi__value">{inr(stockValue)}</div></div>
      </div>

      <div className="cols">
        <div className="card">
          <div className="card__head"><Icon name="receipt" /><span className="card__title">Inventory</span>
            <span className="count">{items.length}</span></div>
          <div className="card__body"><div className="rows">
            {items.map((i) => {
              const low = i.stockQuantity <= i.reorderLevel;
              return (
                <div key={i.id} className="row">
                  <div className="row__main">
                    <div className="row__title">{i.itemName}</div>
                    <div className="row__sub">{i.itemCode}<span className="dot">·</span>{inr(i.rate)}<span className="dot">·</span>GST {i.taxPercentage}%</div>
                  </div>
                  <span className={low ? "badge badge--owner" : "badge badge--ok"}
                        style={low ? { background: "var(--warn-50)", color: "var(--warn-700)" } : undefined}>
                    {i.stockQuantity} {i.uom}{low ? " · low" : ""}
                  </span>
                </div>
              );
            })}
            {items.length === 0 && <div className="empty">No items yet.</div>}
          </div></div>
        </div>

        <div className="card">
          <div className="card__head"><Icon name="plus" /><span className="card__title">Add item</span></div>
          <div className="card__body">
            <form className="form" onSubmit={create}>
              <div className="form--inline">
                <div className="field"><label>Item code</label>
                  <input className="input" value={f.itemCode} onChange={set("itemCode")} required /></div>
                <div className="field"><label>UOM</label>
                  <input className="input" value={f.uom} onChange={set("uom")} /></div>
              </div>
              <div className="field"><label>Item name</label>
                <input className="input" value={f.itemName} onChange={set("itemName")} required /></div>
              <div className="form--inline">
                <div className="field"><label>Rate (₹)</label>
                  <input className="input" type="number" min="0" value={f.rate} onChange={set("rate")} required /></div>
                <div className="field"><label>GST %</label>
                  <input className="input" type="number" min="0" max="100" value={f.taxPercentage} onChange={set("taxPercentage")} /></div>
                <div className="field"><label>Reorder</label>
                  <input className="input" type="number" min="0" value={f.reorderLevel} onChange={set("reorderLevel")} /></div>
              </div>
              <button className="btn btn--block" disabled={busy || !f.itemCode || !f.itemName}>
                <Icon name="plus" />{busy ? "Saving…" : "Add item"}
              </button>
            </form>
          </div>
        </div>
      </div>
    </>
  );
}

/* -------------------------------------------------------------------------- */
function OrdersTab({ orders, setError, reload }: {
  orders: PurchaseOrderDto[]; setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  async function act(id: string, action: "submit" | "approve" | "receive") {
    setError(null);
    try {
      if (action === "submit") await api.poSubmit(id);
      else if (action === "approve") await api.poApprove(id);
      else await api.poReceive(id);
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Action failed");
    }
  }
  const next: Record<string, "submit" | "approve" | "receive" | undefined> = {
    DRAFT: "submit", SUBMITTED: "approve", APPROVED: "receive",
  };

  return (
    <div className="card">
      <div className="card__head"><Icon name="card" /><span className="card__title">Purchase orders</span>
        <span className="count">{orders.length}</span></div>
      <div className="card__body"><div className="rows">
        {orders.map((o) => {
          const action = next[o.status.toUpperCase()];
          return (
            <div key={o.id} className="row">
              <div className="row__main">
                <div className="row__title">{o.poNumber}<span className="dot">·</span>{o.supplierName ?? "Supplier"}</div>
                <div className="row__sub">{o.poDate}<span className="dot">·</span>{inr(o.totalAmount)}</div>
              </div>
              <span className={statusBadgeClass(o.status)}>{prettyStatus(o.status)}</span>
              {action && (
                <button className="btn btn--ghost" onClick={() => act(o.id, action)}>
                  {action === "submit" ? "Submit" : action === "approve" ? "Approve" : "Receive"}
                </button>
              )}
            </div>
          );
        })}
        {orders.length === 0 && <div className="empty">No purchase orders yet.</div>}
      </div></div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
function SalesTab({ sales }: { sales: SaleDto[] }) {
  return (
    <div className="card">
      <div className="card__head"><Icon name="wallet" /><span className="card__title">Sales &amp; installation</span>
        <span className="count">{sales.length}</span></div>
      <div className="card__body"><div className="rows">
        {sales.map((s) => (
          <div key={s.id} className="row">
            <div className="row__main">
              <div className="row__title">{s.invoiceNumber}<span className="dot">·</span>{s.customerName ?? "Walk-in"}</div>
              <div className="row__sub">{s.saleDate}<span className="dot">·</span>Total {inr(s.totalAmount)}
                {s.balance > 0 ? <><span className="dot">·</span>Bal {inr(s.balance)}</> : null}</div>
            </div>
            <span className={statusBadgeClass(s.status)}>{prettyStatus(s.status)}</span>
          </div>
        ))}
        {sales.length === 0 && <div className="empty">No sales yet.</div>}
      </div></div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
const SERVICE_COLUMNS: { key: string; label: string; tone: string }[] = [
  { key: "OPEN", label: "Open", tone: "open" },
  { key: "IN_PROGRESS", label: "In progress", tone: "progress" },
  { key: "RESOLVED", label: "Resolved", tone: "resolved" },
];

function ServiceTab({ service, customers, setError, reload }: {
  service: ServiceComplaintDto[]; customers: CustomerDto[];
  setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  const [f, setF] = useState({ complaintNumber: "", customerId: "", issueDescription: "" });
  const [busy, setBusy] = useState(false);

  async function advance(id: string, status: string) {
    setError(null);
    try { await api.updateServiceStatus(id, status); await reload(); }
    catch (err) { setError(err instanceof Error ? err.message : "Failed to update"); }
  }
  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.createServiceComplaint({
        complaintNumber: f.complaintNumber, customerId: f.customerId,
        issueDescription: f.issueDescription || null,
      });
      setF({ complaintNumber: "", customerId: "", issueDescription: "" });
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to log complaint");
    } finally { setBusy(false); }
  }
  const nextStatus: Record<string, string | undefined> = { OPEN: "IN_PROGRESS", IN_PROGRESS: "RESOLVED" };

  return (
    <>
      <div className="kanban">
        {SERVICE_COLUMNS.map((col) => {
          const cards = service.filter((s) => s.status.toUpperCase() === col.key);
          return (
            <div key={col.key} className={`kanban__col kanban__col--${col.tone}`}>
              <div className="kanban__head">{col.label}<span className="count">{cards.length}</span></div>
              {cards.map((s) => {
                const nxt = nextStatus[s.status.toUpperCase()];
                return (
                  <div key={s.id} className="ticket">
                    <div className="ticket__no">{s.complaintNumber}</div>
                    <div className="ticket__cust">{s.customerName ?? "Customer"}</div>
                    <div className="ticket__issue">{s.issueDescription ?? "—"}</div>
                    <div className="ticket__foot">
                      <span className="ticket__assignee">{s.assignedEmployeeName ?? "Unassigned"}</span>
                      {nxt && (
                        <button className="btn btn--ghost btn--xs" onClick={() => advance(s.id, nxt)}>
                          {nxt === "IN_PROGRESS" ? "Start" : "Resolve"}
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
              {cards.length === 0 && <div className="kanban__empty">Nothing here</div>}
            </div>
          );
        })}
      </div>

      <div className="card" style={{ marginTop: 22 }}>
        <div className="card__head"><Icon name="plus" /><span className="card__title">Log a complaint</span></div>
        <div className="card__body">
          <form className="form form--inline" onSubmit={create}>
            <div className="field"><label>Complaint #</label>
              <input className="input" value={f.complaintNumber} onChange={(e) => setF({ ...f, complaintNumber: e.target.value })} required /></div>
            <div className="field"><label>Customer</label>
              <select className="select" value={f.customerId} onChange={(e) => setF({ ...f, customerId: e.target.value })} required>
                <option value="">— Select —</option>
                {customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select></div>
            <div className="field" style={{ minWidth: 220 }}><label>Issue</label>
              <input className="input" value={f.issueDescription} onChange={(e) => setF({ ...f, issueDescription: e.target.value })} /></div>
            <button className="btn" disabled={busy || !f.complaintNumber || !f.customerId}><Icon name="plus" />Log</button>
          </form>
        </div>
      </div>
    </>
  );
}

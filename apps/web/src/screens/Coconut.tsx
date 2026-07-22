import { useEffect, useState } from "react";
import { api } from "../api";
import type { CoconutBatchDto, CoconutBatchPnlDto, CoconutProductDto } from "../types";
import { Icon, inr, ListSkeleton, prettyStatus, statusBadgeClass, today } from "../ui";
import { PnlBreakdown } from "./Farm";

type Tab = "batches" | "products";

export function CoconutScreen({ setError }: { setError: (e: string | null) => void }) {
  const [tab, setTab] = useState<Tab>("batches");
  const [batches, setBatches] = useState<CoconutBatchDto[]>([]);
  const [products, setProducts] = useState<CoconutProductDto[]>([]);
  const [loading, setLoading] = useState(true);

  async function loadAll() {
    try {
      const [b, p] = await Promise.all([api.coconutBatches(), api.products()]);
      setBatches(b); setProducts(p);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load coconut data");
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => { loadAll(); /* eslint-disable-next-line */ }, []);

  const tabs: { id: Tab; label: string; icon: string; count: number }[] = [
    { id: "batches", label: "Batches", icon: "package", count: batches.length },
    { id: "products", label: "Products", icon: "receipt", count: products.length },
  ];

  return (
    <section id="coconut">
      <div className="section__head">
        <div className="section__title"><Icon name="package" />Coconut Business</div>
        <div className="section__sub">Product master and purchase batches with labour, transport and live profit.</div>
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
          {tab === "batches" && <BatchesTab batches={batches} products={products} setError={setError} reload={loadAll} />}
          {tab === "products" && <ProductsTab products={products} setError={setError} reload={loadAll} />}
        </>
      )}
    </section>
  );
}

/* -------------------------------------------------------------------------- */
function BatchesTab({ batches, products, setError, reload }: {
  batches: CoconutBatchDto[]; products: CoconutProductDto[];
  setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  const [selected, setSelected] = useState<CoconutBatchDto | null>(null);
  const [pnl, setPnl] = useState<CoconutBatchPnlDto | null>(null);
  const [f, setF] = useState({ productId: "", batchNumber: "", purchaseDate: today(), quantity: "", purchaseAmount: "" });
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setF({ ...f, [k]: e.target.value });

  useEffect(() => { if (!f.productId && products[0]) setF((s) => ({ ...s, productId: products[0].id })); }, [products, f.productId]);

  async function openPnl(b: CoconutBatchDto) {
    setSelected(b); setPnl(null); setError(null);
    try { setPnl(await api.coconutBatchPnl(b.id)); }
    catch (err) { setError(err instanceof Error ? err.message : "Failed to load P&L"); }
  }
  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.createCoconutBatch({
        productId: f.productId, batchNumber: f.batchNumber, purchaseDate: f.purchaseDate,
        quantity: Number(f.quantity || 0), purchaseAmount: Number(f.purchaseAmount || 0),
      });
      setF({ ...f, batchNumber: "", quantity: "", purchaseAmount: "" });
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create batch");
    } finally { setBusy(false); }
  }

  return (
    <div className="cols">
      <div className="card">
        <div className="card__head"><Icon name="package" /><span className="card__title">Purchase batches</span>
          <span className="count">{batches.length}</span></div>
        <div className="card__body">
          <div className="rows">
            {batches.map((b) => (
              <div key={b.id} className="row" style={{ cursor: "pointer" }} onClick={() => openPnl(b)}>
                <div className="row__main">
                  <div className="row__title">{b.batchNumber}<span className="dot">·</span>{b.productName ?? "Product"}</div>
                  <div className="row__sub">{b.purchaseDate}<span className="dot">·</span>{b.quantity} units<span className="dot">·</span>{inr(b.purchaseAmount)}</div>
                </div>
                <span className={statusBadgeClass(b.status)}>{prettyStatus(b.status)}</span>
              </div>
            ))}
            {batches.length === 0 && <div className="empty">No batches yet.</div>}
          </div>

          <div className="form__divider">New purchase batch</div>
          <form className="form" onSubmit={create}>
            <div className="form--inline">
              <div className="field"><label>Batch #</label>
                <input className="input" value={f.batchNumber} onChange={set("batchNumber")} required /></div>
              <div className="field"><label>Product</label>
                <select className="select" value={f.productId} onChange={set("productId")}>
                  {products.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select></div>
              <div className="field"><label>Date</label>
                <input className="input" type="date" value={f.purchaseDate} onChange={set("purchaseDate")} /></div>
            </div>
            <div className="form--inline">
              <div className="field"><label>Quantity</label>
                <input className="input" type="number" min="0" value={f.quantity} onChange={set("quantity")} required /></div>
              <div className="field"><label>Purchase (₹)</label>
                <input className="input" type="number" min="0" value={f.purchaseAmount} onChange={set("purchaseAmount")} required /></div>
            </div>
            <button className="btn btn--block" disabled={busy || !f.batchNumber || !f.productId}><Icon name="plus" />Create batch</button>
          </form>
        </div>
      </div>

      <div className="card">
        <div className="card__head"><Icon name="wallet" />
          <span className="card__title">{selected ? `P&L · ${selected.batchNumber}` : "Batch P&L"}</span></div>
        <div className="card__body">
          {!selected && <div className="empty">Select a batch to view its profit &amp; loss.</div>}
          {selected && !pnl && <div className="empty">Loading…</div>}
          {pnl && <PnlBreakdown rows={[
            { label: "Sales", value: pnl.totalSales, kind: "in" },
            { label: "Purchase", value: pnl.purchase, kind: "out" },
            { label: "Labour", value: pnl.labourCost, kind: "out" },
            { label: "Transport", value: pnl.transportCost, kind: "out" },
          ]} profit={pnl.profit ?? pnl.totalSales - (pnl.totalCost ?? 0)} />}
        </div>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
function ProductsTab({ products, setError, reload }: {
  products: CoconutProductDto[]; setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  const [f, setF] = useState({ name: "", category: "Raw", uom: "pcs" });
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setF({ ...f, [k]: e.target.value });

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.createProduct({ name: f.name, category: f.category, uom: f.uom });
      setF({ ...f, name: "" });
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add product");
    } finally { setBusy(false); }
  }

  return (
    <div className="cols">
      <div className="card">
        <div className="card__head"><Icon name="receipt" /><span className="card__title">Products</span>
          <span className="count">{products.length}</span></div>
        <div className="card__body"><div className="rows">
          {products.map((p) => (
            <div key={p.id} className="row">
              <div className="row__main">
                <div className="row__title">{p.name}</div>
                <div className="row__sub">{p.category ?? "—"}<span className="dot">·</span>per {p.uom}</div>
              </div>
              <span className={p.isActive ? "badge badge--ok" : "badge badge--off"}>{p.isActive ? "Active" : "Inactive"}</span>
            </div>
          ))}
          {products.length === 0 && <div className="empty">No products yet.</div>}
        </div></div>
      </div>
      <div className="card">
        <div className="card__head"><Icon name="plus" /><span className="card__title">Add product</span></div>
        <div className="card__body">
          <form className="form" onSubmit={create}>
            <div className="field"><label>Name</label>
              <input className="input" value={f.name} onChange={set("name")} required /></div>
            <div className="form--inline">
              <div className="field"><label>Category</label>
                <select className="select" value={f.category} onChange={set("category")}>
                  <option>Raw</option><option>Processed</option>
                </select></div>
              <div className="field"><label>UOM</label>
                <input className="input" value={f.uom} onChange={set("uom")} /></div>
            </div>
            <button className="btn btn--block" disabled={busy || !f.name}><Icon name="plus" />Add product</button>
          </form>
        </div>
      </div>
    </div>
  );
}

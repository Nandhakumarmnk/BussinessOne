import { useEffect, useState } from "react";
import { api } from "../api";
import type { FarmBatchDto, FarmBatchPnlDto, FeedDto, WalletDto, WalletTransactionDto } from "../types";
import { Icon, inr, prettyStatus, statusBadgeClass, today } from "../ui";

type Tab = "batches" | "feeds" | "wallet";

export function FarmScreen({ setError }: { setError: (e: string | null) => void }) {
  const [tab, setTab] = useState<Tab>("batches");
  const [batches, setBatches] = useState<FarmBatchDto[]>([]);
  const [feeds, setFeeds] = useState<FeedDto[]>([]);
  const [wallet, setWallet] = useState<WalletDto | null>(null);
  const [txns, setTxns] = useState<WalletTransactionDto[]>([]);

  async function loadAll() {
    try {
      const [b, f, w, t] = await Promise.all([
        api.farmBatches(), api.feeds(), api.wallet(), api.walletTransactions(),
      ]);
      setBatches(b); setFeeds(f); setWallet(w); setTxns(t);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load farm data");
    }
  }
  useEffect(() => { loadAll(); /* eslint-disable-next-line */ }, []);

  const tabs: { id: Tab; label: string; icon: string; count: number }[] = [
    { id: "batches", label: "Batches", icon: "leaf", count: batches.length },
    { id: "feeds", label: "Feed master", icon: "receipt", count: feeds.length },
    { id: "wallet", label: "Wallet", icon: "wallet", count: txns.length },
  ];

  return (
    <section id="farm">
      <div className="section__head">
        <div className="section__title"><Icon name="leaf" />Farm Management</div>
        <div className="section__sub">Batches with live P&amp;L, feed master, and the farm wallet.</div>
      </div>

      <div className="tabs">
        {tabs.map((t) => (
          <button key={t.id} className={`tab${tab === t.id ? " is-active" : ""}`} onClick={() => setTab(t.id)}>
            <Icon name={t.icon} />{t.label}<span className="count">{t.count}</span>
          </button>
        ))}
      </div>

      {tab === "batches" && <BatchesTab batches={batches} setError={setError} reload={loadAll} />}
      {tab === "feeds" && <FeedsTab feeds={feeds} setError={setError} reload={loadAll} />}
      {tab === "wallet" && <WalletTab wallet={wallet} txns={txns} />}
    </section>
  );
}

/* -------------------------------------------------------------------------- */
function BatchesTab({ batches, setError, reload }: {
  batches: FarmBatchDto[]; setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  const [selected, setSelected] = useState<FarmBatchDto | null>(null);
  const [pnl, setPnl] = useState<FarmBatchPnlDto | null>(null);
  const [f, setF] = useState({ batchNumber: "", animalType: "Broiler", startDate: today(), quantityPurchased: "", purchaseAmount: "" });
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setF({ ...f, [k]: e.target.value });

  async function openPnl(b: FarmBatchDto) {
    setSelected(b); setPnl(null); setError(null);
    try { setPnl(await api.farmBatchPnl(b.id)); }
    catch (err) { setError(err instanceof Error ? err.message : "Failed to load P&L"); }
  }
  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.createFarmBatch({
        batchNumber: f.batchNumber, animalType: f.animalType, startDate: f.startDate,
        quantityPurchased: Number(f.quantityPurchased || 0), purchaseAmount: Number(f.purchaseAmount || 0),
      });
      setF({ ...f, batchNumber: "", quantityPurchased: "", purchaseAmount: "" });
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create batch");
    } finally { setBusy(false); }
  }

  return (
    <div className="cols">
      <div className="card">
        <div className="card__head"><Icon name="leaf" /><span className="card__title">Batches</span>
          <span className="count">{batches.length}</span></div>
        <div className="card__body">
          <div className="rows">
            {batches.map((b) => (
              <div key={b.id} className="row" style={{ cursor: "pointer" }} onClick={() => openPnl(b)}>
                <div className="row__main">
                  <div className="row__title">{b.batchNumber}{b.batchName ? ` · ${b.batchName}` : ""}</div>
                  <div className="row__sub">{b.animalType}<span className="dot">·</span>{b.quantityPurchased} birds<span className="dot">·</span>{inr(b.purchaseAmount)}</div>
                </div>
                <span className={statusBadgeClass(b.status)}>{prettyStatus(b.status)}</span>
              </div>
            ))}
            {batches.length === 0 && <div className="empty">No batches yet.</div>}
          </div>

          <div className="form__divider">Start a batch</div>
          <form className="form" onSubmit={create}>
            <div className="form--inline">
              <div className="field"><label>Batch #</label>
                <input className="input" value={f.batchNumber} onChange={set("batchNumber")} required /></div>
              <div className="field"><label>Type</label>
                <select className="select" value={f.animalType} onChange={set("animalType")}>
                  <option>Broiler</option><option>Layer</option><option>Country</option>
                </select></div>
              <div className="field"><label>Start date</label>
                <input className="input" type="date" value={f.startDate} onChange={set("startDate")} /></div>
            </div>
            <div className="form--inline">
              <div className="field"><label>Quantity</label>
                <input className="input" type="number" min="0" value={f.quantityPurchased} onChange={set("quantityPurchased")} required /></div>
              <div className="field"><label>Purchase (₹)</label>
                <input className="input" type="number" min="0" value={f.purchaseAmount} onChange={set("purchaseAmount")} required /></div>
            </div>
            <button className="btn btn--block" disabled={busy || !f.batchNumber}><Icon name="plus" />Create batch</button>
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
            { label: "Feed", value: pnl.feedCost, kind: "out" },
            { label: "Medical", value: pnl.medicalCost, kind: "out" },
            { label: "Labour", value: pnl.labourCost, kind: "out" },
            { label: "Other", value: pnl.otherCost, kind: "out" },
          ]} profit={pnl.profit ?? pnl.totalSales - (pnl.totalCost ?? 0)} />}
        </div>
      </div>
    </div>
  );
}

export function PnlBreakdown({ rows, profit }: {
  rows: { label: string; value: number; kind: "in" | "out" }[]; profit: number;
}) {
  return (
    <>
      <div className="rows">
        {rows.map((r) => (
          <div key={r.label} className="row">
            <div className="row__main"><div className="row__title">{r.label}</div></div>
            <div className="row__title" style={{ color: r.kind === "in" ? "var(--pos-700)" : "var(--neg-700)" }}>
              {r.kind === "in" ? "+" : "−"}{inr(r.value)}
            </div>
          </div>
        ))}
      </div>
      <div className={`pnl-total ${profit >= 0 ? "is-pos" : "is-neg"}`}>
        <span>Net profit</span><span>{inr(profit)}</span>
      </div>
    </>
  );
}

/* -------------------------------------------------------------------------- */
function FeedsTab({ feeds, setError, reload }: {
  feeds: FeedDto[]; setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  const [f, setF] = useState({ feedName: "", feedType: "Starter", uom: "kg", rate: "" });
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setF({ ...f, [k]: e.target.value });

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.createFeed({ feedName: f.feedName, feedType: f.feedType, uom: f.uom, rate: Number(f.rate || 0) });
      setF({ ...f, feedName: "", rate: "" });
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add feed");
    } finally { setBusy(false); }
  }

  return (
    <div className="cols">
      <div className="card">
        <div className="card__head"><Icon name="receipt" /><span className="card__title">Feed master</span>
          <span className="count">{feeds.length}</span></div>
        <div className="card__body"><div className="rows">
          {feeds.map((x) => (
            <div key={x.id} className="row">
              <div className="row__main">
                <div className="row__title">{x.feedName}</div>
                <div className="row__sub">{x.feedType ?? "—"}<span className="dot">·</span>per {x.uom}</div>
              </div>
              <div className="row__title">{inr(x.rate)}</div>
            </div>
          ))}
          {feeds.length === 0 && <div className="empty">No feeds yet.</div>}
        </div></div>
      </div>
      <div className="card">
        <div className="card__head"><Icon name="plus" /><span className="card__title">Add feed</span></div>
        <div className="card__body">
          <form className="form" onSubmit={create}>
            <div className="field"><label>Feed name</label>
              <input className="input" value={f.feedName} onChange={set("feedName")} required /></div>
            <div className="form--inline">
              <div className="field"><label>Type</label>
                <select className="select" value={f.feedType} onChange={set("feedType")}>
                  <option>Starter</option><option>Grower</option><option>Finisher</option>
                </select></div>
              <div className="field"><label>UOM</label>
                <input className="input" value={f.uom} onChange={set("uom")} /></div>
              <div className="field"><label>Rate (₹)</label>
                <input className="input" type="number" min="0" value={f.rate} onChange={set("rate")} required /></div>
            </div>
            <button className="btn btn--block" disabled={busy || !f.feedName}><Icon name="plus" />Add feed</button>
          </form>
        </div>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
function WalletTab({ wallet, txns }: { wallet: WalletDto | null; txns: WalletTransactionDto[] }) {
  return (
    <div className="cols">
      <div className="card">
        <div className="card__head"><Icon name="wallet" /><span className="card__title">Wallet balance</span></div>
        <div className="card__body">
          <div className="kpi is-featured" style={{ boxShadow: "none" }}>
            <div className="kpi__top"><div className="kpi__icon"><Icon name="wallet" /></div><span className="kpi__delta">Available</span></div>
            <div className="kpi__label">Current balance</div>
            <div className="kpi__value">{inr(wallet?.balance ?? 0)}</div>
          </div>
        </div>
      </div>
      <div className="card">
        <div className="card__head"><Icon name="clock" /><span className="card__title">Transactions</span>
          <span className="count">{txns.length}</span></div>
        <div className="card__body"><div className="rows">
          {txns.map((t) => (
            <div key={t.id} className="row">
              <div className="row__main">
                <div className="row__title">{t.reason ?? t.direction}</div>
                <div className="row__sub">{t.txnDate}</div>
              </div>
              <div className="row__title" style={{ color: t.direction === "IN" ? "var(--pos-700)" : "var(--neg-700)" }}>
                {t.direction === "IN" ? "+" : "−"}{inr(t.amount)}
              </div>
            </div>
          ))}
          {txns.length === 0 && <div className="empty">No transactions yet.</div>}
        </div></div>
      </div>
    </div>
  );
}

import { useEffect, useState } from "react";
import { api } from "../api";
import type { AccountDto, CashBookRowDto, JournalTxnDto, LedgerLineDto, ProfitLossDto } from "../types";
import { Icon, inr } from "../ui";
import { DonutChart } from "../charts";

type Tab = "pnl" | "cashbook" | "journal" | "ledger";

export function AccountingScreen({ setError }: { setError: (e: string | null) => void }) {
  const [tab, setTab] = useState<Tab>("pnl");

  const tabs: { id: Tab; label: string; icon: string }[] = [
    { id: "pnl", label: "Profit & Loss", icon: "up" },
    { id: "cashbook", label: "Cash Book", icon: "wallet" },
    { id: "journal", label: "Journal", icon: "book" },
    { id: "ledger", label: "Ledger", icon: "receipt" },
  ];

  return (
    <section id="accounting">
      <div className="section__head">
        <div className="section__title"><Icon name="book" />Accounting &amp; Reports</div>
        <div className="section__sub">Profit &amp; loss, cash book, journal and ledger — with PDF / Excel export.</div>
      </div>

      <div className="tabs">
        {tabs.map((t) => (
          <button key={t.id} className={`tab${tab === t.id ? " is-active" : ""}`} onClick={() => setTab(t.id)}>
            <Icon name={t.icon} />{t.label}
          </button>
        ))}
      </div>

      {tab === "pnl" && <PnlTab setError={setError} />}
      {tab === "cashbook" && <CashBookTab setError={setError} />}
      {tab === "journal" && <JournalTab setError={setError} />}
      {tab === "ledger" && <LedgerTab setError={setError} />}
    </section>
  );
}

/* -------------------------------------------------------------------------- */
function PnlTab({ setError }: { setError: (e: string | null) => void }) {
  const [pl, setPl] = useState<ProfitLossDto | null>(null);
  const [busy, setBusy] = useState<"pdf" | "excel" | null>(null);

  useEffect(() => {
    api.profitLoss()
      .then(setPl)
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load P&L"));
  }, [setError]);

  async function exportAs(format: "pdf" | "excel") {
    setBusy(format); setError(null);
    try { await api.exportReport("profit-loss", format); }
    catch (err) { setError(err instanceof Error ? err.message : "Export failed"); }
    finally { setBusy(null); }
  }

  return (
    <>
      <div className="kpis" style={{ marginBottom: 22 }}>
        <div className="kpi t-pos"><div className="kpi__top"><div className="kpi__icon"><Icon name="up" /></div></div>
          <div className="kpi__label">Total income</div><div className="kpi__value">{inr(pl?.totalIncome ?? 0)}</div></div>
        <div className="kpi t-neg"><div className="kpi__top"><div className="kpi__icon"><Icon name="down" /></div></div>
          <div className="kpi__label">Total expense</div><div className="kpi__value">{inr(pl?.totalExpense ?? 0)}</div></div>
        <div className="kpi is-featured"><div className="kpi__top"><div className="kpi__icon"><Icon name="wallet" /></div><span className="kpi__delta">Net</span></div>
          <div className="kpi__label">Net profit</div><div className="kpi__value">{inr(pl?.netProfit ?? 0)}</div></div>
      </div>

      <div className="analytics">
        <div className="card">
          <div className="card__head"><Icon name="wallet" /><span className="card__title">Income vs Expense</span></div>
          <div className="card__body">
            {pl
              ? <DonutChart data={[{ label: "Income", value: pl.totalIncome }, { label: "Expense", value: pl.totalExpense }]} centerLabel="Turnover" />
              : <div className="empty">Loading…</div>}
          </div>
        </div>
        <div className="card">
          <div className="card__head"><Icon name="download" /><span className="card__title">Export report</span></div>
          <div className="card__body">
            <p className="hint" style={{ marginBottom: 14 }}>Download the profit &amp; loss statement for the current business.</p>
            <div className="form--inline">
              <button className="btn" disabled={busy !== null} onClick={() => exportAs("pdf")}>
                <Icon name="download" />{busy === "pdf" ? "Preparing…" : "PDF"}
              </button>
              <button className="btn btn--ghost" disabled={busy !== null} onClick={() => exportAs("excel")}>
                <Icon name="download" />{busy === "excel" ? "Preparing…" : "Excel"}
              </button>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

/* -------------------------------------------------------------------------- */
function CashBookTab({ setError }: { setError: (e: string | null) => void }) {
  const [rows, setRows] = useState<CashBookRowDto[]>([]);
  useEffect(() => {
    api.cashBook()
      .then(setRows)
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load cash book"));
  }, [setError]);

  return (
    <div className="card">
      <div className="card__head"><Icon name="wallet" /><span className="card__title">Cash book</span>
        <span className="count">{rows.length}</span></div>
      <div className="card__body"><div className="rows">
        {rows.map((r, i) => (
          <div key={i} className="row">
            <div className="row__main">
              <div className="row__title">{r.description}</div>
              <div className="row__sub">{r.date}</div>
            </div>
            {r.in > 0 && <div className="row__sub" style={{ color: "var(--pos-700)" }}>+{inr(r.in)}</div>}
            {r.out > 0 && <div className="row__sub" style={{ color: "var(--neg-700)" }}>−{inr(r.out)}</div>}
            <div className="row__title">{inr(r.balance)}</div>
          </div>
        ))}
        {rows.length === 0 && <div className="empty">No entries.</div>}
      </div></div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
function JournalTab({ setError }: { setError: (e: string | null) => void }) {
  const [txns, setTxns] = useState<JournalTxnDto[]>([]);
  useEffect(() => {
    api.journal()
      .then(setTxns)
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load journal"));
  }, [setError]);

  return (
    <div className="card">
      <div className="card__head"><Icon name="book" /><span className="card__title">Journal</span>
        <span className="count">{txns.length}</span></div>
      <div className="card__body">
        {txns.map((t) => (
          <div key={t.id} className="journal">
            <div className="journal__head">
              <span className="journal__narration">{t.narration ?? "Journal entry"}</span>
              <span className="badge">{t.sourceModule}</span>
              <span className="journal__date">{t.txnDate}</span>
            </div>
            {t.lines.map((l, i) => (
              <div key={i} className="journal__line">
                <span className="journal__acct">{l.accountCode} · {l.accountName}</span>
                <span className="journal__dr">{l.debit > 0 ? inr(l.debit) : ""}</span>
                <span className="journal__cr">{l.credit > 0 ? inr(l.credit) : ""}</span>
              </div>
            ))}
          </div>
        ))}
        {txns.length === 0 && <div className="empty">No journal entries.</div>}
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
function LedgerTab({ setError }: { setError: (e: string | null) => void }) {
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [accountId, setAccountId] = useState("");
  const [lines, setLines] = useState<LedgerLineDto[]>([]);

  useEffect(() => {
    api.accounts()
      .then((a) => { setAccounts(a); if (a[0]) setAccountId(a[0].id); })
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load accounts"));
  }, [setError]);

  useEffect(() => {
    if (!accountId) return;
    api.ledger(accountId)
      .then(setLines)
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load ledger"));
  }, [accountId, setError]);

  return (
    <div className="card">
      <div className="card__head"><Icon name="receipt" /><span className="card__title">Ledger</span>
        <label className="switcher" style={{ marginLeft: "auto" }}>
          <select value={accountId} onChange={(e) => setAccountId(e.target.value)}>
            {accounts.map((a) => <option key={a.id} value={a.id}>{a.code} · {a.name}</option>)}
          </select>
        </label>
      </div>
      <div className="card__body"><div className="rows">
        {lines.map((l, i) => (
          <div key={i} className="row">
            <div className="row__main">
              <div className="row__title">{l.narration ?? l.accountName}</div>
              <div className="row__sub">{l.date}</div>
            </div>
            {l.debit > 0 && <div className="row__sub" style={{ color: "var(--pos-700)" }}>Dr {inr(l.debit)}</div>}
            {l.credit > 0 && <div className="row__sub" style={{ color: "var(--neg-700)" }}>Cr {inr(l.credit)}</div>}
            <div className="row__title">{inr(l.balance)}</div>
          </div>
        ))}
        {lines.length === 0 && <div className="empty">No ledger entries.</div>}
      </div></div>
    </div>
  );
}

import { useEffect, useState } from "react";
import { api } from "../api";
import type { CustomerDto, LedgerEntryDto } from "../types";
import { Icon, initials, inr, today } from "../ui";

export function CustomersScreen({ setError }: { setError: (e: string | null) => void }) {
  const [customers, setCustomers] = useState<CustomerDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<CustomerDto | null>(null);
  const [ledger, setLedger] = useState<LedgerEntryDto[]>([]);

  // New customer
  const [name, setName] = useState("");
  const [mobile, setMobile] = useState("");
  const [opening, setOpening] = useState("");
  const [busy, setBusy] = useState(false);

  // Collection
  const [collAmount, setCollAmount] = useState("");
  const [collMode, setCollMode] = useState("cash");

  async function load() {
    setLoading(true);
    try {
      setCustomers(await api.customers());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load customers");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); /* eslint-disable-next-line */ }, []);

  async function openLedger(c: CustomerDto) {
    setSelected(c);
    setError(null);
    try {
      setLedger(await api.customerLedger(c.id));
    } catch (err) {
      setLedger([]);
      setError(err instanceof Error ? err.message : "Failed to load ledger");
    }
  }

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await api.createCustomer({
        name, mobile: mobile || null, creditLimit: 0, openingBalance: Number(opening || 0),
      });
      setName(""); setMobile(""); setOpening("");
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create customer");
    } finally {
      setBusy(false);
    }
  }

  async function recordCollection(e: React.FormEvent) {
    e.preventDefault();
    if (!selected) return;
    setError(null);
    try {
      await api.recordCollection(selected.id, {
        collectionDate: today(), amount: Number(collAmount), mode: collMode,
      });
      setCollAmount("");
      await Promise.all([openLedger(selected), load()]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to record collection");
    }
  }

  return (
    <section id="customers">
      <div className="section__head">
        <div className="section__title"><Icon name="contact" />Customers</div>
        <div className="section__sub">Customer master, outstanding balances, ledger and collections.</div>
      </div>

      <div className="cols">
        <div className="card">
          <div className="card__head">
            <Icon name="contact" /><span className="card__title">Customers</span>
            <span className="count">{customers.length}</span>
          </div>
          <div className="card__body">
            <div className="rows">
              {loading && Array.from({ length: 4 }).map((_, i) => (
                <div key={`sk-${i}`} className="row">
                  <div className="skeleton skeleton--avatar" />
                  <div className="row__main">
                    <div className="skeleton skeleton--line" />
                    <div className="skeleton skeleton--line skeleton--sm" />
                  </div>
                  <div className="skeleton skeleton--pill" />
                </div>
              ))}
              {!loading && customers.map((c) => (
                <div key={c.id} className="row" style={{ cursor: "pointer" }} onClick={() => openLedger(c)}>
                  <div className="avatar--sq">{initials(c.name)}</div>
                  <div className="row__main">
                    <div className="row__title">{c.name}</div>
                    <div className="row__sub">{c.mobile || "—"}</div>
                  </div>
                  <span className={`badge ${c.outstanding > 0 ? "badge--owner" : "badge--ok"}`}>
                    {c.outstanding > 0 ? `Due ${inr(c.outstanding)}` : "Settled"}
                  </span>
                </div>
              ))}
              {!loading && customers.length === 0 && <div className="empty">No customers yet.</div>}
            </div>

            <div className="form__divider">Add a customer</div>
            <form className="form" onSubmit={create}>
              <div className="form--inline">
                <div className="field" style={{ minWidth: 160 }}>
                  <input className="input" placeholder="Name" value={name} onChange={(e) => setName(e.target.value)} required />
                </div>
                <div className="field">
                  <input className="input" placeholder="Mobile" value={mobile} onChange={(e) => setMobile(e.target.value)} />
                </div>
                <div className="field">
                  <input className="input" type="number" min="0" step="0.01" placeholder="Opening balance"
                         value={opening} onChange={(e) => setOpening(e.target.value)} />
                </div>
              </div>
              <button className="btn" disabled={busy || !name}><Icon name="plus" />Create</button>
            </form>
          </div>
        </div>

        <div className="card">
          <div className="card__head">
            <Icon name="wallet" /><span className="card__title">{selected ? `Ledger · ${selected.name}` : "Ledger"}</span>
          </div>
          <div className="card__body">
            {!selected && <div className="empty">Select a customer to view their ledger.</div>}
            {selected && (
              <>
                <div className="rows">
                  {ledger.map((l) => (
                    <div key={l.id} className="row">
                      <div className="row__main">
                        <div className="row__title">{l.refType}</div>
                        <div className="row__sub">{l.entryDate}</div>
                      </div>
                      <div className="row__sub">
                        {l.debit > 0 ? <span style={{ color: "var(--neg-700)" }}>+{inr(l.debit)}</span>
                                     : <span style={{ color: "var(--pos-700)" }}>−{inr(l.credit)}</span>}
                      </div>
                      <div className="row__title">{inr(l.runningBalance)}</div>
                    </div>
                  ))}
                  {ledger.length === 0 && <div className="empty">No ledger entries.</div>}
                </div>

                <div className="form__divider">Record a collection</div>
                <form className="form form--inline" onSubmit={recordCollection}>
                  <div className="field">
                    <input className="input" type="number" min="0" step="0.01" placeholder="Amount"
                           value={collAmount} onChange={(e) => setCollAmount(e.target.value)} required />
                  </div>
                  <div className="field" style={{ flex: "0 0 auto", minWidth: 120 }}>
                    <select className="select" value={collMode} onChange={(e) => setCollMode(e.target.value)}>
                      <option value="cash">Cash</option>
                      <option value="upi">UPI</option>
                      <option value="bank">Bank</option>
                      <option value="cheque">Cheque</option>
                    </select>
                  </div>
                  <button className="btn" disabled={!collAmount}><Icon name="tick" />Collect</button>
                </form>
              </>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}

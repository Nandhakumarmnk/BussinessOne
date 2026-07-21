import { useEffect, useRef, useState } from "react";
import { api } from "../api";
import type { ExpenseDto, RefItem } from "../types";
import { Icon, inr, today } from "../ui";

export function ExpensesScreen({ setError }: { setError: (e: string | null) => void }) {
  const [expenses, setExpenses] = useState<ExpenseDto[]>([]);
  const [types, setTypes] = useState<RefItem[]>([]);
  const [loading, setLoading] = useState(true);

  const [date, setDate] = useState(today());
  const [amount, setAmount] = useState("");
  const [typeId, setTypeId] = useState("");
  const [description, setDescription] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  async function load() {
    setLoading(true);
    try {
      const [rows, t] = await Promise.all([api.expenses(), api.expenseTypes()]);
      setExpenses(rows);
      setTypes(t);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load expenses");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); /* eslint-disable-next-line */ }, []);

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      // Attachment first (so a failed upload doesn't create a dangling expense), then the record.
      let attachmentKey: string | null = null;
      if (file) attachmentKey = (await api.uploadFile(file, "expenses")).objectKey;

      await api.createExpense({
        expenseDate: date,
        amount: Number(amount),
        description: description || null,
        expenseTypeId: typeId || null,
        attachmentKey,
      });
      setAmount(""); setDescription(""); setFile(null);
      if (fileInput.current) fileInput.current.value = "";
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save expense");
    } finally {
      setBusy(false);
    }
  }

  async function openAttachment(id: string) {
    setError(null);
    try {
      const { url } = await api.expenseAttachment(id);
      const openable = await api.resolveFileUrl(url);
      window.open(openable, "_blank", "noopener");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not open attachment");
    }
  }

  async function remove(id: string) {
    setError(null);
    try {
      await api.deleteExpense(id);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete expense");
    }
  }

  return (
    <section id="expenses">
      <div className="section__head">
        <div className="section__title"><Icon name="receipt" />Expenses</div>
        <div className="section__sub">Record spend and attach a bill or receipt (stored in Firebase Storage).</div>
      </div>

      <div className="cols">
        <div className="card">
          <div className="card__head">
            <Icon name="receipt" /><span className="card__title">Recent expenses</span>
            <span className="count">{expenses.length}</span>
          </div>
          <div className="card__body">
            <div className="rows">
              {loading && Array.from({ length: 4 }).map((_, i) => (
                <div key={`sk-${i}`} className="row">
                  <div className="row__main">
                    <div className="skeleton skeleton--line" />
                    <div className="skeleton skeleton--line skeleton--sm" />
                  </div>
                  <div className="skeleton skeleton--pill" />
                </div>
              ))}
              {!loading && expenses.map((x) => (
                <div key={x.id} className="row">
                  <div className="row__main">
                    <div className="row__title">{x.description || x.expenseTypeName || "Expense"}</div>
                    <div className="row__sub">
                      {x.expenseDate}{x.expenseTypeName ? <><span className="dot">·</span>{x.expenseTypeName}</> : null}
                    </div>
                  </div>
                  <div className="row__title">{inr(x.amount)}</div>
                  {x.attachmentKey && (
                    <button className="iconbtn" title="View attachment" onClick={() => openAttachment(x.id)}>
                      <Icon name="paperclip" />
                    </button>
                  )}
                  <button className="iconbtn" title="Delete" onClick={() => remove(x.id)}><Icon name="trash" /></button>
                </div>
              ))}
              {!loading && expenses.length === 0 && <div className="empty">No expenses yet.</div>}
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card__head"><Icon name="plus" /><span className="card__title">Add expense</span></div>
          <div className="card__body">
            <form className="form" onSubmit={create}>
              <div className="form--inline">
                <div className="field">
                  <label>Date</label>
                  <input className="input" type="date" value={date} onChange={(e) => setDate(e.target.value)} required />
                </div>
                <div className="field">
                  <label>Amount (₹)</label>
                  <input className="input" type="number" min="0" step="0.01" placeholder="0"
                         value={amount} onChange={(e) => setAmount(e.target.value)} required />
                </div>
              </div>
              <div className="field">
                <label>Type</label>
                <select className="select" value={typeId} onChange={(e) => setTypeId(e.target.value)}>
                  <option value="">— None —</option>
                  {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                </select>
              </div>
              <div className="field">
                <label>Description</label>
                <input className="input" placeholder="e.g. Diesel" value={description}
                       onChange={(e) => setDescription(e.target.value)} />
              </div>
              <div className="field">
                <label>Attachment (bill / receipt)</label>
                <input ref={fileInput} className="input" type="file"
                       accept="image/*,application/pdf"
                       onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
                {file && <span className="hint">{file.name} · {(file.size / 1024).toFixed(0)} KB</span>}
              </div>
              <button className="btn btn--block" disabled={busy || !amount}>
                <Icon name="plus" />{busy ? "Saving…" : "Save expense"}
              </button>
            </form>
          </div>
        </div>
      </div>
    </section>
  );
}

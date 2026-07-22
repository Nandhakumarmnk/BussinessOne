import { useEffect, useState } from "react";
import { api } from "../api";
import type { EmployeeDto, SalaryRecordDto } from "../types";
import { Icon, initials, inr, today } from "../ui";

export function EmployeesScreen({ setError }: { setError: (e: string | null) => void }) {
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<EmployeeDto | null>(null);
  const [history, setHistory] = useState<SalaryRecordDto[]>([]);

  const [f, setF] = useState({ name: "", mobile: "", salary: "" });
  const [busy, setBusy] = useState(false);
  const [sal, setSal] = useState({ month: today().slice(0, 7), amount: "", paidAmount: "" });

  const due = (s: SalaryRecordDto) => s.balance ?? s.amount - s.paidAmount;

  async function load() {
    setLoading(true);
    try { setEmployees(await api.employees()); }
    catch (err) { setError(err instanceof Error ? err.message : "Failed to load employees"); }
    finally { setLoading(false); }
  }
  useEffect(() => { load(); /* eslint-disable-next-line */ }, []);

  async function openHistory(e: EmployeeDto) {
    setSelected(e); setError(null);
    try { setHistory(await api.salaryHistory(e.id)); }
    catch (err) { setHistory([]); setError(err instanceof Error ? err.message : "Failed to load salary"); }
  }

  async function create(ev: React.FormEvent) {
    ev.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.createEmployee({ name: f.name, mobile: f.mobile || null, salary: Number(f.salary || 0) });
      setF({ name: "", mobile: "", salary: "" });
      await load();
    } catch (err) { setError(err instanceof Error ? err.message : "Failed to create employee"); }
    finally { setBusy(false); }
  }

  async function record(ev: React.FormEvent) {
    ev.preventDefault();
    if (!selected) return;
    setError(null);
    try {
      await api.recordSalary(selected.id, {
        periodMonth: `${sal.month}-01`, amount: Number(sal.amount || 0), paidAmount: Number(sal.paidAmount || 0),
      });
      setSal({ ...sal, amount: "", paidAmount: "" });
      await Promise.all([openHistory(selected), load()]);
    } catch (err) { setError(err instanceof Error ? err.message : "Failed to record salary"); }
  }

  return (
    <section id="employees">
      <div className="section__head">
        <div className="section__title"><Icon name="users" />Employees</div>
        <div className="section__sub">Staff master, salary history and payments.</div>
      </div>

      <div className="cols">
        <div className="card">
          <div className="card__head"><Icon name="users" /><span className="card__title">Employees</span>
            <span className="count">{employees.length}</span></div>
          <div className="card__body">
            <div className="rows">
              {loading && Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="row">
                  <div className="skeleton skeleton--avatar" />
                  <div className="row__main"><div className="skeleton skeleton--line" /><div className="skeleton skeleton--line skeleton--sm" /></div>
                  <div className="skeleton skeleton--pill" />
                </div>
              ))}
              {!loading && employees.map((e) => (
                <div key={e.id} className="row" style={{ cursor: "pointer" }} onClick={() => openHistory(e)}>
                  <div className="avatar--sq">{initials(e.name)}</div>
                  <div className="row__main">
                    <div className="row__title">{e.name}</div>
                    <div className="row__sub">{e.mobile ?? "—"}<span className="dot">·</span>{inr(e.salary)}/mo</div>
                  </div>
                  <span className={e.status === "ACTIVE" ? "badge badge--ok" : "badge badge--off"}>
                    {e.status === "ACTIVE" ? "Active" : "Inactive"}
                  </span>
                </div>
              ))}
              {!loading && employees.length === 0 && <div className="empty">No employees yet.</div>}
            </div>

            <div className="form__divider">Add an employee</div>
            <form className="form form--inline" onSubmit={create}>
              <div className="field" style={{ minWidth: 150 }}>
                <input className="input" placeholder="Name" value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} required /></div>
              <div className="field">
                <input className="input" placeholder="Mobile" value={f.mobile} onChange={(e) => setF({ ...f, mobile: e.target.value })} /></div>
              <div className="field">
                <input className="input" type="number" min="0" placeholder="Salary/mo" value={f.salary} onChange={(e) => setF({ ...f, salary: e.target.value })} required /></div>
              <button className="btn" disabled={busy || !f.name}><Icon name="plus" />Add</button>
            </form>
          </div>
        </div>

        <div className="card">
          <div className="card__head"><Icon name="wallet" />
            <span className="card__title">{selected ? `Salary · ${selected.name}` : "Salary"}</span></div>
          <div className="card__body">
            {!selected && <div className="empty">Select an employee to view salary history.</div>}
            {selected && (
              <>
                <div className="rows">
                  {history.map((s) => (
                    <div key={s.id} className="row">
                      <div className="row__main">
                        <div className="row__title">{s.periodMonth.slice(0, 7)}</div>
                        <div className="row__sub">Paid {inr(s.paidAmount)} of {inr(s.amount)}{s.note ? ` · ${s.note}` : ""}</div>
                      </div>
                      <span className={due(s) > 0 ? "badge badge--owner" : "badge badge--ok"}>
                        {due(s) > 0 ? `Due ${inr(due(s))}` : "Cleared"}
                      </span>
                    </div>
                  ))}
                  {history.length === 0 && <div className="empty">No salary records.</div>}
                </div>

                <div className="form__divider">Record salary</div>
                <form className="form form--inline" onSubmit={record}>
                  <div className="field"><label>Month</label>
                    <input className="input" type="month" value={sal.month} onChange={(e) => setSal({ ...sal, month: e.target.value })} /></div>
                  <div className="field"><label>Amount</label>
                    <input className="input" type="number" min="0" value={sal.amount} onChange={(e) => setSal({ ...sal, amount: e.target.value })} required /></div>
                  <div className="field"><label>Paid</label>
                    <input className="input" type="number" min="0" value={sal.paidAmount} onChange={(e) => setSal({ ...sal, paidAmount: e.target.value })} required /></div>
                  <button className="btn" disabled={!sal.amount}><Icon name="tick" />Record</button>
                </form>
              </>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}

import { useEffect, useState } from "react";
import { api } from "../api";
import type { CreditDto, CustomerDto, DriverDto, LoadDto, VehicleDto } from "../types";
import { Icon, inr, today } from "../ui";

type Tab = "loads" | "vehicles" | "drivers" | "credits";

export function TransportScreen({ setError }: { setError: (e: string | null) => void }) {
  const [tab, setTab] = useState<Tab>("loads");
  const [loads, setLoads] = useState<LoadDto[]>([]);
  const [vehicles, setVehicles] = useState<VehicleDto[]>([]);
  const [drivers, setDrivers] = useState<DriverDto[]>([]);
  const [customers, setCustomers] = useState<CustomerDto[]>([]);
  const [credits, setCredits] = useState<CreditDto[]>([]);

  async function loadAll() {
    try {
      const [l, v, d, c, cr] = await Promise.all([
        api.loads(), api.vehicles(), api.drivers(), api.customers(), api.credits(),
      ]);
      setLoads(l); setVehicles(v); setDrivers(d); setCustomers(c); setCredits(cr);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load transport data");
    }
  }
  useEffect(() => { loadAll(); /* eslint-disable-next-line */ }, []);

  const tabs: { id: Tab; label: string; icon: string; count: number }[] = [
    { id: "loads", label: "Loads", icon: "truck", count: loads.length },
    { id: "credits", label: "Credits", icon: "card", count: credits.length },
    { id: "vehicles", label: "Vehicles", icon: "truck", count: vehicles.length },
    { id: "drivers", label: "Drivers", icon: "users", count: drivers.length },
  ];

  return (
    <section id="transport">
      <div className="section__head">
        <div className="section__title"><Icon name="truck" />Goods Transport</div>
        <div className="section__sub">Loads with server-computed profit, vehicles, drivers and credit collection.</div>
      </div>

      <div className="tabs">
        {tabs.map((t) => (
          <button key={t.id} className={`tab${tab === t.id ? " is-active" : ""}`} onClick={() => setTab(t.id)}>
            <Icon name={t.icon} />{t.label}<span className="count">{t.count}</span>
          </button>
        ))}
      </div>

      {tab === "loads" && (
        <LoadsTab loads={loads} vehicles={vehicles} drivers={drivers} customers={customers}
                  setError={setError} reload={loadAll} />
      )}
      {tab === "credits" && <CreditsTab credits={credits} setError={setError} reload={loadAll} />}
      {tab === "vehicles" && <VehiclesTab vehicles={vehicles} setError={setError} reload={loadAll} />}
      {tab === "drivers" && <DriversTab drivers={drivers} setError={setError} reload={loadAll} />}
    </section>
  );
}

/* -------------------------------------------------------------------------- */
function LoadsTab({ loads, vehicles, drivers, customers, setError, reload }: {
  loads: LoadDto[]; vehicles: VehicleDto[]; drivers: DriverDto[]; customers: CustomerDto[];
  setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  const [f, setF] = useState({
    loadNumber: "", loadName: "", customerId: "", vehicleId: "", driverId: "", loadDate: today(),
    loadAmount: "", loadmanCharges: "", fuelExpense: "", maintenanceExpense: "", driverCharges: "", otherExpense: "",
  });
  const [busy, setBusy] = useState(false);
  const num = (v: string) => Number(v || 0);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setF({ ...f, [k]: e.target.value });

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await api.createLoad({
        loadNumber: f.loadNumber, loadName: f.loadName || null,
        customerId: f.customerId || null, vehicleId: f.vehicleId || null, driverId: f.driverId || null,
        loadDate: f.loadDate, loadAmount: num(f.loadAmount), loadmanCharges: num(f.loadmanCharges),
        fuelExpense: num(f.fuelExpense), maintenanceExpense: num(f.maintenanceExpense),
        driverCharges: num(f.driverCharges), otherExpense: num(f.otherExpense),
      });
      setF({ ...f, loadNumber: "", loadName: "", loadAmount: "", loadmanCharges: "", fuelExpense: "",
             maintenanceExpense: "", driverCharges: "", otherExpense: "" });
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create load");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="cols">
      <div className="card">
        <div className="card__head"><Icon name="truck" /><span className="card__title">Loads</span>
          <span className="count">{loads.length}</span></div>
        <div className="card__body">
          <div className="rows">
            {loads.map((l) => (
              <div key={l.id} className="row">
                <div className="row__main">
                  <div className="row__title">{l.loadNumber}{l.loadName ? ` · ${l.loadName}` : ""}</div>
                  <div className="row__sub">{l.loadDate}<span className="dot">·</span>{inr(l.loadAmount)}</div>
                </div>
                <span className={`badge ${l.profit >= 0 ? "badge--ok" : ""}`}
                      style={l.profit < 0 ? { background: "var(--neg-50)", color: "var(--neg-700)" } : undefined}>
                  Profit {inr(l.profit)}
                </span>
              </div>
            ))}
            {loads.length === 0 && <div className="empty">No loads yet.</div>}
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card__head"><Icon name="plus" /><span className="card__title">New load</span></div>
        <div className="card__body">
          <form className="form" onSubmit={create}>
            <div className="form--inline">
              <div className="field"><label>Load #</label>
                <input className="input" value={f.loadNumber} onChange={set("loadNumber")} required /></div>
              <div className="field"><label>Name / goods</label>
                <input className="input" value={f.loadName} onChange={set("loadName")} /></div>
              <div className="field"><label>Date</label>
                <input className="input" type="date" value={f.loadDate} onChange={set("loadDate")} /></div>
            </div>
            <div className="field"><label>Customer (bills to ledger)</label>
              <select className="select" value={f.customerId} onChange={set("customerId")}>
                <option value="">— None —</option>
                {customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
            <div className="form--inline">
              <div className="field"><label>Vehicle</label>
                <select className="select" value={f.vehicleId} onChange={set("vehicleId")}>
                  <option value="">— None —</option>
                  {vehicles.map((v) => <option key={v.id} value={v.id}>{v.vehicleNumber}</option>)}
                </select>
              </div>
              <div className="field"><label>Driver</label>
                <select className="select" value={f.driverId} onChange={set("driverId")}>
                  <option value="">— None —</option>
                  {drivers.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                </select>
              </div>
            </div>
            <div className="form--inline">
              <div className="field"><label>Load amount</label>
                <input className="input" type="number" min="0" value={f.loadAmount} onChange={set("loadAmount")} required /></div>
              <div className="field"><label>Loadman</label>
                <input className="input" type="number" min="0" value={f.loadmanCharges} onChange={set("loadmanCharges")} /></div>
              <div className="field"><label>Fuel</label>
                <input className="input" type="number" min="0" value={f.fuelExpense} onChange={set("fuelExpense")} /></div>
            </div>
            <div className="form--inline">
              <div className="field"><label>Maintenance</label>
                <input className="input" type="number" min="0" value={f.maintenanceExpense} onChange={set("maintenanceExpense")} /></div>
              <div className="field"><label>Driver charges</label>
                <input className="input" type="number" min="0" value={f.driverCharges} onChange={set("driverCharges")} /></div>
              <div className="field"><label>Other</label>
                <input className="input" type="number" min="0" value={f.otherExpense} onChange={set("otherExpense")} /></div>
            </div>
            <button className="btn btn--block" disabled={busy || !f.loadNumber || !f.loadAmount}>
              <Icon name="plus" />{busy ? "Saving…" : "Create load"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
function CreditsTab({ credits, setError, reload }: {
  credits: CreditDto[]; setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  const [payingId, setPayingId] = useState<string | null>(null);
  const [amount, setAmount] = useState("");
  const [mode, setMode] = useState("cash");

  async function pay(id: string) {
    setError(null);
    try {
      await api.recordCreditPayment(id, Number(amount), mode);
      setPayingId(null); setAmount("");
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to record payment");
    }
  }

  return (
    <div className="card">
      <div className="card__head"><Icon name="card" /><span className="card__title">Load credits</span>
        <span className="count">{credits.length}</span></div>
      <div className="card__body">
        <div className="rows">
          {credits.map((c) => (
            <div key={c.id} className="row">
              <div className="row__main">
                <div className="row__title">{c.customerName || "Customer"}<span className="dot">·</span>{c.loadNumber}</div>
                <div className="row__sub">Billed {inr(c.loadAmount)}<span className="dot">·</span>Paid {inr(c.paidAmount)}</div>
              </div>
              <span className={`badge ${c.balanceAmount > 0 ? "badge--owner" : "badge--ok"}`}>
                {c.balanceAmount > 0 ? `Balance ${inr(c.balanceAmount)}` : "Cleared"}
              </span>
              {c.balanceAmount > 0 && payingId !== c.id && (
                <button className="btn btn--ghost" onClick={() => { setPayingId(c.id); setAmount(String(c.balanceAmount)); }}>
                  Pay
                </button>
              )}
              {payingId === c.id && (
                <>
                  <input className="input" style={{ maxWidth: 110 }} type="number" min="0" value={amount}
                         onChange={(e) => setAmount(e.target.value)} />
                  <select className="select" style={{ maxWidth: 110 }} value={mode} onChange={(e) => setMode(e.target.value)}>
                    <option value="cash">Cash</option><option value="upi">UPI</option>
                    <option value="bank">Bank</option><option value="cheque">Cheque</option>
                  </select>
                  <button className="btn" onClick={() => pay(c.id)} disabled={!amount}><Icon name="tick" /></button>
                </>
              )}
            </div>
          ))}
          {credits.length === 0 && <div className="empty">No outstanding credits.</div>}
        </div>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
function VehiclesTab({ vehicles, setError, reload }: {
  vehicles: VehicleDto[]; setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  const [f, setF] = useState({ vehicleNumber: "", vehicleType: "", model: "", fuelType: "" });
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value });

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.createVehicle({
        vehicleNumber: f.vehicleNumber, vehicleType: f.vehicleType || null,
        model: f.model || null, fuelType: f.fuelType || null,
      });
      setF({ vehicleNumber: "", vehicleType: "", model: "", fuelType: "" });
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create vehicle");
    } finally { setBusy(false); }
  }

  return (
    <div className="cols">
      <div className="card">
        <div className="card__head"><Icon name="truck" /><span className="card__title">Vehicles</span>
          <span className="count">{vehicles.length}</span></div>
        <div className="card__body"><div className="rows">
          {vehicles.map((v) => (
            <div key={v.id} className="row">
              <div className="row__main">
                <div className="row__title">{v.vehicleNumber}</div>
                <div className="row__sub">{[v.vehicleType, v.model, v.fuelType].filter(Boolean).join(" · ") || "—"}</div>
              </div>
              <span className={`badge ${v.isActive ? "badge--ok" : "badge--off"}`}>{v.isActive ? "Active" : "Inactive"}</span>
            </div>
          ))}
          {vehicles.length === 0 && <div className="empty">No vehicles yet.</div>}
        </div></div>
      </div>
      <div className="card">
        <div className="card__head"><Icon name="plus" /><span className="card__title">Add vehicle</span></div>
        <div className="card__body">
          <form className="form" onSubmit={create}>
            <div className="field"><label>Vehicle number</label>
              <input className="input" value={f.vehicleNumber} onChange={set("vehicleNumber")} required /></div>
            <div className="form--inline">
              <div className="field"><label>Type</label>
                <input className="input" placeholder="Truck / Tempo" value={f.vehicleType} onChange={set("vehicleType")} /></div>
              <div className="field"><label>Model</label>
                <input className="input" value={f.model} onChange={set("model")} /></div>
              <div className="field"><label>Fuel</label>
                <input className="input" placeholder="Diesel" value={f.fuelType} onChange={set("fuelType")} /></div>
            </div>
            <button className="btn btn--block" disabled={busy || !f.vehicleNumber}><Icon name="plus" />Add vehicle</button>
          </form>
        </div>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
function DriversTab({ drivers, setError, reload }: {
  drivers: DriverDto[]; setError: (e: string | null) => void; reload: () => Promise<void>;
}) {
  const [f, setF] = useState({ name: "", mobile: "", driverType: "salaried", salary: "" });
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setF({ ...f, [k]: e.target.value });

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.createDriver({
        name: f.name, mobile: f.mobile || null, driverType: f.driverType, salary: Number(f.salary || 0),
      });
      setF({ name: "", mobile: "", driverType: "salaried", salary: "" });
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create driver");
    } finally { setBusy(false); }
  }

  return (
    <div className="cols">
      <div className="card">
        <div className="card__head"><Icon name="users" /><span className="card__title">Drivers</span>
          <span className="count">{drivers.length}</span></div>
        <div className="card__body"><div className="rows">
          {drivers.map((d) => (
            <div key={d.id} className="row">
              <div className="row__main">
                <div className="row__title">{d.name}</div>
                <div className="row__sub">{d.mobile || "—"}<span className="dot">·</span>{d.driverType}</div>
              </div>
              <div className="row__sub">{inr(d.salary)}</div>
            </div>
          ))}
          {drivers.length === 0 && <div className="empty">No drivers yet.</div>}
        </div></div>
      </div>
      <div className="card">
        <div className="card__head"><Icon name="plus" /><span className="card__title">Add driver</span></div>
        <div className="card__body">
          <form className="form" onSubmit={create}>
            <div className="field"><label>Name</label>
              <input className="input" value={f.name} onChange={set("name")} required /></div>
            <div className="form--inline">
              <div className="field"><label>Mobile</label>
                <input className="input" value={f.mobile} onChange={set("mobile")} /></div>
              <div className="field"><label>Type</label>
                <select className="select" value={f.driverType} onChange={set("driverType")}>
                  <option value="salaried">Salaried</option>
                  <option value="self">Self</option>
                </select></div>
              <div className="field"><label>Salary</label>
                <input className="input" type="number" min="0" value={f.salary} onChange={set("salary")} /></div>
            </div>
            <button className="btn btn--block" disabled={busy || !f.name}><Icon name="plus" />Add driver</button>
          </form>
        </div>
      </div>
    </div>
  );
}

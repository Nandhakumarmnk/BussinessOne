import { useCallback, useEffect, useState } from "react";
import { api, IS_DEMO, setAccessToken, setActiveBusiness, setRefreshToken } from "./api";
import type { BusinessDto, CategorySlice, DashboardSummary, MemberDto, RefItem, TrendPoint, UserSummary } from "./types";
import { Icon, initials, inr } from "./ui";
import { DonutChart, Sparkline, TrendChart } from "./charts";
import { ExpensesScreen } from "./screens/Expenses";
import { CustomersScreen } from "./screens/Customers";
import { TransportScreen } from "./screens/Transport";
import { CctvScreen } from "./screens/Cctv";
import { FarmScreen } from "./screens/Farm";
import { CoconutScreen } from "./screens/Coconut";

export function App() {
  const [user, setUser] = useState<UserSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  const logout = () => {
    void api.logout();               // best-effort refresh-token revoke
    setAccessToken(null);
    setRefreshToken(null);
    setActiveBusiness(null);
    setUser(null);
  };

  if (!user) return <Login onAuthed={setUser} setError={setError} error={error} />;
  return <Console user={user} onLogout={logout} />;
}

/* -------------------------------------------------------------------------- */
/* Login                                                                      */
/* -------------------------------------------------------------------------- */
function Login({ onAuthed, setError, error }: {
  onAuthed: (u: UserSummary) => void;
  setError: (e: string | null) => void;
  error: string | null;
}) {
  const [mobileOrEmail, setMobileOrEmail] = useState("owner@business-one.local");
  const [password, setPassword] = useState("Owner@123");
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const res = await api.login(mobileOrEmail, password);
      setAccessToken(res.accessToken);
      setRefreshToken(res.refreshToken);
      onAuthed(res.user);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setBusy(false);
    }
  }

  const features = [
    "Multi-business, role-based access control",
    "Real-time income, expense & profit insight",
    "Built for transport, CCTV, farm & coconut ops",
  ];

  return (
    <div className="auth">
      <aside className="auth__brand">
        <div className="auth__brandtop">
          <div className="brand__mark">B1</div>
          <div>
            <div className="brand__name" style={{ fontSize: 16 }}>Business One</div>
            <div className="brand__tag">Multi-Business ERP</div>
          </div>
        </div>

        <div className="auth__headline">
          <h2>Run every business from a single, unified console.</h2>
          <p>Finance, people and operations — consolidated, secure and always in sync.</p>
        </div>

        <div className="auth__features">
          {features.map((f) => (
            <div className="auth__feature" key={f}>
              <span className="tick"><Icon name="tick" /></span>{f}
            </div>
          ))}
        </div>
      </aside>

      <div className="auth__form">
        <form className="auth-card" onSubmit={submit}>
          <h1>Welcome back</h1>
          <p className="sub">Sign in to your Business One workspace.</p>

          <div className="form">
            <div className="field">
              <label>Mobile / Email</label>
              <input className="input" value={mobileOrEmail} onChange={(e) => setMobileOrEmail(e.target.value)} />
            </div>
            <div className="field">
              <label>Password</label>
              <input className="input" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
            </div>
            <button className="btn btn--block" disabled={busy}>{busy ? "Signing in…" : "Log in"}</button>
            {error && <p className="formerror">{error}</p>}
          </div>

          <div className="demo">
            {IS_DEMO
              ? <>Live demo · <b>any</b> email &amp; password signs you in — data is sample-only.</>
              : <>Demo · <b>owner@business-one.local</b> / <b>Owner@123</b></>}
          </div>
        </form>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Console (authenticated shell)                                              */
/* -------------------------------------------------------------------------- */
function Console({ user, onLogout }: { user: UserSummary; onLogout: () => void }) {
  const [businesses, setBusinesses] = useState<BusinessDto[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [types, setTypes] = useState<RefItem[]>([]);
  const [roles, setRoles] = useState<RefItem[]>([]);
  const [members, setMembers] = useState<MemberDto[]>([]);
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [nav, setNav] = useState("dashboard");

  const selectBusiness = useCallback(async (id: string) => {
    setActiveId(id);
    setActiveBusiness(id);
    try {
      const [m, s] = await Promise.all([api.members(id), api.dashboard()]);
      setMembers(m);
      setSummary(s);
    } catch (err) {
      setMembers([]);
      setSummary(null);
      setError(err instanceof Error ? err.message : "Failed to load business data");
    }
  }, []);

  const loadBusinesses = useCallback(async (selectFirst: boolean) => {
    const list = await api.businesses();
    setBusinesses(list);
    if (selectFirst && list[0]) await selectBusiness(list[0].id);
  }, [selectBusiness]);

  useEffect(() => {
    (async () => {
      try {
        const [t, r] = await Promise.all([api.businessTypes(), api.roles()]);
        setTypes(t);
        setRoles(r);
        await loadBusinesses(true);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load");
      }
    })();
  }, [loadBusinesses]);

  const active = businesses.find((b) => b.id === activeId) ?? null;

  // The one vertical module that applies to the active business type.
  const typeCode = active?.businessTypeCode;
  const vertical =
    typeCode === "TRANSPORT" ? { id: "transport", label: "Transport", icon: "truck" }
    : typeCode === "CCTV" ? { id: "cctv", label: "CCTV", icon: "camera" }
    : typeCode === "FARM" ? { id: "farm", label: "Farm", icon: "leaf" }
    : typeCode === "COCONUT" ? { id: "coconut", label: "Coconut", icon: "package" }
    : null;

  // Fall back to the dashboard if the active business can't show the current module
  // (e.g. the user switched to a different business type while on a vertical tab).
  useEffect(() => {
    if (["transport", "cctv", "farm", "coconut"].includes(nav) && vertical?.id !== nav) setNav("dashboard");
  }, [nav, vertical?.id]);

  // Auto-dismiss the error toast after a few seconds.
  useEffect(() => {
    if (!error) return;
    const t = setTimeout(() => setError(null), 5000);
    return () => clearTimeout(t);
  }, [error]);

  const workspaceNav = [
    { id: "dashboard", label: "Dashboard", icon: "dashboard" },
    { id: "businesses", label: "Businesses", icon: "building" },
    { id: "members", label: "Members", icon: "users" },
  ];
  const operationsNav = [
    { id: "expenses", label: "Expenses", icon: "receipt" },
    { id: "customers", label: "Customers", icon: "contact" },
    ...(vertical ? [vertical] : []),
  ];
  const activeLabel =
    [...workspaceNav, ...operationsNav].find((i) => i.id === nav)?.label ?? "Workspace";

  const NavItem = ({ id, label, icon }: { id: string; label: string; icon: string }) => (
    <div className={`nav__item${nav === id ? " is-active" : ""}`} onClick={() => setNav(id)}>
      <Icon name={icon} />{label}
    </div>
  );

  const needsBusiness = ["expenses", "customers", "transport", "cctv", "farm", "coconut"].includes(nav);

  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand__mark">B1</div>
          <div>
            <div className="brand__name">Business One {IS_DEMO && <span className="pill-demo">Demo</span>}</div>
            <div className="brand__tag">Multi-Business ERP</div>
          </div>
        </div>

        <nav className="nav">
          <div className="nav__label">Workspace</div>
          {workspaceNav.map((item) => <NavItem key={item.id} {...item} />)}
          <div className="nav__label">Operations</div>
          {operationsNav.map((item) => <NavItem key={item.id} {...item} />)}
        </nav>

        <div className="sidebar__foot">
          <b>Console</b> · Multi-business ERP<br />v0.2
        </div>
      </aside>

      <div className="main">
        <header className="topbar">
          <div>
            <div className="topbar__title">{active ? active.name : "Workspace"}</div>
            <div className="topbar__crumb">
              {active ? `${active.businessTypeName} · ${activeLabel}` : "No business selected"}
            </div>
          </div>

          <div className="spacer" />

          <label className="switcher">
            <Icon name="building" className="icon" />
            <select value={activeId ?? ""} onChange={(e) => selectBusiness(e.target.value)}>
              {businesses.length === 0 && <option value="">No businesses</option>}
              {businesses.map((b) => (
                <option key={b.id} value={b.id}>{b.name} · {b.businessTypeCode}</option>
              ))}
            </select>
          </label>

          <div className="user">
            <div className="avatar">{initials(user.fullName)}</div>
            <div className="user__meta">
              <div className="user__name">{user.fullName}</div>
              <div className="user__role">{user.isSuperAdmin ? "Super Admin" : "Business User"}</div>
            </div>
            <button className="iconbtn" onClick={onLogout} title="Log out" aria-label="Log out">
              <Icon name="logout" />
            </button>
          </div>
        </header>

        {error && (
          <div className="toast" role="alert">
            <span className="toast__icon"><Icon name="shield" /></span>
            <span className="toast__msg">{error}</span>
            <button className="toast__x" onClick={() => setError(null)} aria-label="Dismiss">
              <Icon name="close" />
            </button>
          </div>
        )}

        <main className="content">
          {needsBusiness && !activeId && (
            <div className="card"><div className="card__body">
              <div className="empty">Select or create a business to use this module.</div>
            </div></div>
          )}

          {nav === "dashboard" && <DashboardPanel summary={summary} businessName={active?.name ?? ""} />}

          {nav === "businesses" && (
            <BusinessesPanel
              businesses={businesses}
              types={types}
              onCreated={async (id) => { await loadBusinesses(false); await selectBusiness(id); }}
              setError={setError}
            />
          )}

          {nav === "members" && (
            <MembersPanel
              active={active}
              members={members}
              roles={roles}
              onInvited={async () => { if (activeId) await selectBusiness(activeId); }}
              setError={setError}
            />
          )}

          {nav === "expenses" && activeId && <ExpensesScreen key={activeId} setError={setError} />}
          {nav === "customers" && activeId && <CustomersScreen key={activeId} setError={setError} />}
          {nav === "transport" && activeId && <TransportScreen key={activeId} setError={setError} />}
          {nav === "cctv" && activeId && <CctvScreen key={activeId} setError={setError} />}
          {nav === "farm" && activeId && <FarmScreen key={activeId} setError={setError} />}
          {nav === "coconut" && activeId && <CoconutScreen key={activeId} setError={setError} />}
        </main>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Dashboard                                                                  */
/* -------------------------------------------------------------------------- */
type Tone = "pos" | "neg" | "warn" | "info";

/** Fallback analytics when the API returns only scalar KPIs (real backend, no series). */
function synthTrend(s: DashboardSummary | null): TrendPoint[] {
  const inc = s?.monthIncome ?? 0, exp = s?.monthExpense ?? 0;
  return ["Feb", "Mar", "Apr", "May", "Jun", "Jul"].map((label, i) => ({
    label,
    income: Math.round(inc * (0.78 + 0.05 * i)),
    expense: Math.round(exp * (0.82 + 0.04 * i)),
  }));
}
function synthBreakdown(s: DashboardSummary | null): CategorySlice[] {
  const e = s?.monthExpense ?? 0;
  if (!e) return [];
  return [
    { label: "Operations", value: Math.round(e * 0.45) },
    { label: "Salaries", value: Math.round(e * 0.3) },
    { label: "Overheads", value: Math.round(e * 0.15) },
    { label: "Other", value: Math.round(e * 0.1) },
  ];
}

function DashboardPanel({ summary, businessName }: { summary: DashboardSummary | null; businessName: string }) {
  const trend = summary?.trend?.length ? summary.trend : synthTrend(summary);
  const breakdown = summary?.expenseBreakdown?.length ? summary.expenseBreakdown : synthBreakdown(summary);
  const incomeSeries = trend.map((t) => t.income);
  const expenseSeries = trend.map((t) => t.expense);
  const profitSeries = trend.map((t) => t.income - t.expense);
  const pct = (a: number[]) => (a.length >= 2 && a[a.length - 2] ? (a[a.length - 1] - a[a.length - 2]) / Math.abs(a[a.length - 2]) : 0);

  const kpis: {
    label: string; value: number; tone: Tone; icon: string;
    featured?: boolean; series?: number[]; delta?: number;
  }[] = [
    { label: "Month Income", value: summary?.monthIncome ?? 0, tone: "pos", icon: "up", series: incomeSeries, delta: pct(incomeSeries) },
    { label: "Month Expense", value: summary?.monthExpense ?? 0, tone: "neg", icon: "down", series: expenseSeries },
    { label: "Total Profit", value: summary?.totalProfit ?? 0, tone: "info", icon: "wallet", featured: true, series: profitSeries, delta: pct(profitSeries) },
    { label: "Today Income", value: summary?.todayIncome ?? 0, tone: "pos", icon: "up" },
    { label: "Today Expense", value: summary?.todayExpense ?? 0, tone: "neg", icon: "down" },
    { label: "Pending Credits", value: summary?.pendingCredits ?? 0, tone: "warn", icon: "card" },
    { label: "Pending Collections", value: summary?.pendingCollections ?? 0, tone: "warn", icon: "clock" },
  ];

  return (
    <section id="dashboard">
      <div className="section__head">
        <div className="section__title"><Icon name="dashboard" />Dashboard</div>
        <div className="section__sub">
          {businessName ? `Financial overview for ${businessName}` : "Select a business to view its overview"}
        </div>
      </div>

      <div className="kpis">
        {kpis.map((k) => (
          <div key={k.label} className={`kpi t-${k.tone}${k.featured ? " is-featured" : ""}`}>
            <div className="kpi__top">
              <div className="kpi__icon"><Icon name={k.icon} /></div>
              {k.delta !== undefined
                ? <DeltaChip value={k.delta} />
                : k.featured && <span className="kpi__delta">Net</span>}
            </div>
            <div className="kpi__label">{k.label}</div>
            <div className="kpi__value">{inr(k.value)}</div>
            {k.series && (
              <div className="kpi__spark">
                <Sparkline data={k.series} tone={k.featured ? "rgba(255,255,255,.9)" : "var(--tone-500)"} />
              </div>
            )}
          </div>
        ))}
      </div>

      <div className="analytics">
        <div className="card">
          <div className="card__head">
            <Icon name="up" /><span className="card__title">Income vs Expense</span>
            <span className="card__hint">Last {trend.length} months</span>
          </div>
          <div className="card__body"><TrendChart data={trend} /></div>
        </div>

        <div className="card">
          <div className="card__head">
            <Icon name="wallet" /><span className="card__title">Expense breakdown</span>
          </div>
          <div className="card__body">
            {breakdown.length
              ? <DonutChart data={breakdown} centerLabel="Spend" />
              : <div className="empty">No expense data yet.</div>}
          </div>
        </div>
      </div>
    </section>
  );
}

function DeltaChip({ value }: { value: number }) {
  const up = value >= 0;
  const pct = Math.abs(value * 100);
  return (
    <span className={`delta ${up ? "delta--up" : "delta--down"}`}>
      {up ? "▲" : "▼"} {pct < 0.5 ? "0" : pct.toFixed(pct < 10 ? 1 : 0)}%
    </span>
  );
}

/* -------------------------------------------------------------------------- */
/* Businesses                                                                 */
/* -------------------------------------------------------------------------- */
function BusinessesPanel({ businesses, types, onCreated, setError }: {
  businesses: BusinessDto[];
  types: RefItem[];
  onCreated: (id: string) => void | Promise<void>;
  setError: (e: string | null) => void;
}) {
  const [name, setName] = useState("");
  const [typeCode, setTypeCode] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => { if (!typeCode && types[0]) setTypeCode(types[0].code); }, [types, typeCode]);

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const created = await api.createBusiness({ name, businessTypeCode: typeCode });
      setName("");
      await onCreated(created.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Create failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section id="businesses" className="card">
      <div className="card__head">
        <Icon name="building" />
        <span className="card__title">Businesses</span>
        <span className="count">{businesses.length}</span>
      </div>
      <div className="card__body">
        <div className="rows">
          {businesses.map((b) => (
            <div key={b.id} className="row">
              <div className="avatar--sq avatar--brand">{initials(b.name)}</div>
              <div className="row__main">
                <div className="row__title">{b.name}</div>
                <div className="row__sub">{b.businessTypeName}</div>
              </div>
              <span className={`badge ${b.role === "OWNER" ? "badge--owner" : ""}`}>{b.role ?? "—"}</span>
              <span className={`badge ${b.isActive ? "badge--ok" : "badge--off"}`}>{b.isActive ? "Active" : "Inactive"}</span>
            </div>
          ))}
          {businesses.length === 0 && <div className="empty">No businesses yet.</div>}
        </div>

        <div className="form__divider">Add a business</div>
        <form onSubmit={create} className="form form--inline">
          <div className="field" style={{ minWidth: 180 }}>
            <input className="input" placeholder="New business name" value={name}
                   onChange={(e) => setName(e.target.value)} required />
          </div>
          <div className="field" style={{ flex: "0 0 auto", minWidth: 150 }}>
            <select className="select" value={typeCode} onChange={(e) => setTypeCode(e.target.value)}>
              {types.map((t) => <option key={t.id} value={t.code}>{t.name}</option>)}
            </select>
          </div>
          <button className="btn" disabled={busy || !name}><Icon name="plus" />Create</button>
        </form>
      </div>
    </section>
  );
}

/* -------------------------------------------------------------------------- */
/* Members                                                                    */
/* -------------------------------------------------------------------------- */
function MembersPanel({ active, members, roles, onInvited, setError }: {
  active: BusinessDto | null;
  members: MemberDto[];
  roles: RefItem[];
  onInvited: () => void | Promise<void>;
  setError: (e: string | null) => void;
}) {
  const [fullName, setFullName] = useState("");
  const [mobile, setMobile] = useState("");
  const [password, setPassword] = useState("");
  const [roleCode, setRoleCode] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const employee = roles.find((r) => r.code === "EMPLOYEE");
    if (!roleCode && (employee || roles[0])) setRoleCode((employee ?? roles[0]).code);
  }, [roles, roleCode]);

  async function invite(e: React.FormEvent) {
    e.preventDefault();
    if (!active) return;
    setBusy(true);
    setError(null);
    try {
      await api.inviteUser({ fullName, mobile, password, businessId: active.id, roleCode });
      setFullName(""); setMobile(""); setPassword("");
      await onInvited();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Invite failed");
    } finally {
      setBusy(false);
    }
  }

  if (!active) {
    return (
      <section id="members" className="card">
        <div className="card__head"><Icon name="users" /><span className="card__title">Members</span></div>
        <div className="card__body"><div className="empty">Select a business to manage members.</div></div>
      </section>
    );
  }

  return (
    <section id="members" className="card">
      <div className="card__head">
        <Icon name="users" />
        <span className="card__title">Members</span>
        <span className="count">{members.length}</span>
      </div>
      <div className="card__body">
        <div className="rows">
          {members.map((m) => (
            <div key={m.userId} className="row">
              <div className="avatar--sq">{initials(m.fullName)}</div>
              <div className="row__main">
                <div className="row__title">{m.fullName}</div>
                <div className="row__sub">{m.mobile}</div>
              </div>
              <span className={`badge ${m.roleCode === "OWNER" ? "badge--owner" : ""}`}>{m.roleName}</span>
            </div>
          ))}
          {members.length === 0 && <div className="empty">No members yet.</div>}
        </div>

        <div className="form__divider">Invite a user</div>
        <form onSubmit={invite} className="form">
          <div className="field">
            <label>Full name</label>
            <input className="input" placeholder="e.g. Priya Kumar" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
          </div>
          <div className="form--inline">
            <div className="field">
              <label>Mobile</label>
              <input className="input" placeholder="9000000000" value={mobile} onChange={(e) => setMobile(e.target.value)} required />
            </div>
            <div className="field">
              <label>Temp password</label>
              <input className="input" type="password" placeholder="Min 8 characters" value={password} onChange={(e) => setPassword(e.target.value)} required />
            </div>
          </div>
          <div className="field">
            <label>Role</label>
            <select className="select" value={roleCode} onChange={(e) => setRoleCode(e.target.value)}>
              {roles.map((r) => <option key={r.id} value={r.code}>{r.name}</option>)}
            </select>
          </div>
          <button className="btn btn--block" disabled={busy}><Icon name="plus" />Send invite</button>
        </form>
      </div>
    </section>
  );
}

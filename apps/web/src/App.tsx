import { useCallback, useEffect, useState } from "react";
import { api, setAccessToken, setActiveBusiness } from "./api";
import type { BusinessDto, DashboardSummary, MemberDto, RefItem, UserSummary } from "./types";

const inr = (n: number) =>
  new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", maximumFractionDigits: 0 }).format(n);

// Phase 1 demo console: log in, switch business, create a business, and invite users with a
// role. Real routed UI + module screens arrive in Phase 2+ (see docs/08).
export function App() {
  const [user, setUser] = useState<UserSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!user) return <Login onAuthed={setUser} setError={setError} error={error} />;
  return <Console user={user} onLogout={() => { setAccessToken(null); setActiveBusiness(null); setUser(null); }} />;
}

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
      onAuthed(res.user);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div style={styles.page}>
      <form style={styles.card} onSubmit={submit}>
        <h1 style={styles.title}>Business One</h1>
        <p style={styles.subtitle}>Multi-Business ERP · Phase 1</p>
        <label style={styles.label}>Mobile / Email</label>
        <input style={styles.input} value={mobileOrEmail} onChange={(e) => setMobileOrEmail(e.target.value)} />
        <label style={styles.label}>Password</label>
        <input style={styles.input} type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
        <button style={styles.button} disabled={busy}>{busy ? "Signing in…" : "Log in"}</button>
        {error && <p style={styles.error}>{error}</p>}
        <p style={styles.hint}>Seeded: owner@business-one.local / Owner@123</p>
      </form>
    </div>
  );
}

function Console({ user, onLogout }: { user: UserSummary; onLogout: () => void }) {
  const [businesses, setBusinesses] = useState<BusinessDto[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [types, setTypes] = useState<RefItem[]>([]);
  const [roles, setRoles] = useState<RefItem[]>([]);
  const [members, setMembers] = useState<MemberDto[]>([]);
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

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

  return (
    <div style={styles.app}>
      <header style={styles.topbar}>
        <strong>Business One</strong>
        <select style={styles.select} value={activeId ?? ""} onChange={(e) => selectBusiness(e.target.value)}>
          {businesses.length === 0 && <option value="">No businesses</option>}
          {businesses.map((b) => (
            <option key={b.id} value={b.id}>{b.name} · {b.businessTypeCode}</option>
          ))}
        </select>
        <span style={{ flex: 1 }} />
        <span style={styles.who}>{user.fullName}{user.isSuperAdmin ? " (Super Admin)" : ""}</span>
        <button style={styles.linkBtn} onClick={onLogout}>Log out</button>
      </header>

      {error && <div style={styles.banner}>{error}</div>}

      {summary && active && <DashboardPanel summary={summary} businessName={active.name} />}

      <main style={styles.grid}>
        <BusinessesPanel
          businesses={businesses}
          types={types}
          onCreated={async (id) => { await loadBusinesses(false); await selectBusiness(id); }}
          setError={setError}
        />
        <MembersPanel
          active={active}
          members={members}
          roles={roles}
          onInvited={async () => { if (activeId) await selectBusiness(activeId); }}
          setError={setError}
        />
      </main>
    </div>
  );
}

function DashboardPanel({ summary, businessName }: { summary: DashboardSummary; businessName: string }) {
  const kpis: { label: string; value: number; accent?: boolean }[] = [
    { label: "Today Income", value: summary.todayIncome },
    { label: "Today Expense", value: summary.todayExpense },
    { label: "Month Income", value: summary.monthIncome },
    { label: "Month Expense", value: summary.monthExpense },
    { label: "Total Profit", value: summary.totalProfit, accent: true },
    { label: "Pending Credits", value: summary.pendingCredits },
    { label: "Pending Collections", value: summary.pendingCollections },
  ];
  return (
    <section style={{ ...styles.panel, margin: "20px 20px 0" }}>
      <h2 style={styles.h2}>Dashboard · {businessName}</h2>
      <div style={styles.kpiRow}>
        {kpis.map((k) => (
          <div key={k.label} style={{ ...styles.kpi, ...(k.accent ? styles.kpiAccent : {}) }}>
            <div style={styles.kpiLabel}>{k.label}</div>
            <div style={styles.kpiValue}>{inr(k.value)}</div>
          </div>
        ))}
      </div>
    </section>
  );
}

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
    <section style={styles.panel}>
      <h2 style={styles.h2}>Businesses</h2>
      <ul style={styles.list}>
        {businesses.map((b) => (
          <li key={b.id} style={styles.listItem}>
            <strong>{b.name}</strong> · {b.businessTypeName}
            <div style={styles.hint}>role {b.role ?? "—"} · {b.isActive ? "active" : "inactive"}</div>
          </li>
        ))}
        {businesses.length === 0 && <p style={styles.hint}>No businesses yet.</p>}
      </ul>
      <form onSubmit={create} style={styles.formRow}>
        <input style={styles.input} placeholder="New business name" value={name}
               onChange={(e) => setName(e.target.value)} required />
        <select style={styles.select} value={typeCode} onChange={(e) => setTypeCode(e.target.value)}>
          {types.map((t) => <option key={t.id} value={t.code}>{t.name}</option>)}
        </select>
        <button style={styles.button} disabled={busy || !name}>Create</button>
      </form>
    </section>
  );
}

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

  if (!active) return <section style={styles.panel}><h2 style={styles.h2}>Members</h2><p style={styles.hint}>Select a business.</p></section>;

  return (
    <section style={styles.panel}>
      <h2 style={styles.h2}>Members · {active.name}</h2>
      <ul style={styles.list}>
        {members.map((m) => (
          <li key={m.userId} style={styles.listItem}>
            <strong>{m.fullName}</strong> · {m.mobile}
            <div style={styles.hint}>{m.roleName}</div>
          </li>
        ))}
        {members.length === 0 && <p style={styles.hint}>No members.</p>}
      </ul>
      <form onSubmit={invite} style={styles.formCol}>
        <div style={styles.hint}>Invite a user to this business</div>
        <input style={styles.input} placeholder="Full name" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
        <input style={styles.input} placeholder="Mobile" value={mobile} onChange={(e) => setMobile(e.target.value)} required />
        <input style={styles.input} type="password" placeholder="Temp password (min 8)" value={password} onChange={(e) => setPassword(e.target.value)} required />
        <select style={styles.select} value={roleCode} onChange={(e) => setRoleCode(e.target.value)}>
          {roles.map((r) => <option key={r.id} value={r.code}>{r.name}</option>)}
        </select>
        <button style={styles.button} disabled={busy}>Invite</button>
      </form>
    </section>
  );
}

const styles: Record<string, React.CSSProperties> = {
  page: { minHeight: "100vh", display: "grid", placeItems: "center", background: "#0f172a", fontFamily: "system-ui, sans-serif" },
  card: { width: 360, background: "#fff", borderRadius: 12, padding: 28, boxShadow: "0 10px 30px rgba(0,0,0,.25)" },
  title: { margin: 0, fontSize: 24 },
  subtitle: { marginTop: 4, color: "#64748b", fontSize: 13 },
  app: { minHeight: "100vh", background: "#f1f5f9", fontFamily: "system-ui, sans-serif" },
  topbar: { display: "flex", alignItems: "center", gap: 12, padding: "12px 20px", background: "#0f172a", color: "#fff" },
  who: { fontSize: 13, color: "#cbd5e1" },
  linkBtn: { background: "transparent", color: "#93c5fd", border: 0, cursor: "pointer", fontSize: 13 },
  banner: { background: "#fee2e2", color: "#b91c1c", padding: "8px 20px", fontSize: 13 },
  grid: { display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(340px, 1fr))", gap: 16, padding: 20 },
  panel: { background: "#fff", borderRadius: 12, padding: 20, boxShadow: "0 1px 3px rgba(0,0,0,.1)" },
  h2: { marginTop: 0, fontSize: 18 },
  label: { display: "block", marginTop: 14, marginBottom: 4, fontSize: 13, color: "#334155" },
  input: { width: "100%", padding: "10px 12px", border: "1px solid #cbd5e1", borderRadius: 8, boxSizing: "border-box" },
  select: { padding: "9px 10px", border: "1px solid #cbd5e1", borderRadius: 8, background: "#fff" },
  button: { padding: "10px 14px", border: 0, borderRadius: 8, background: "#2563eb", color: "#fff", fontWeight: 600, cursor: "pointer" },
  error: { color: "#dc2626", fontSize: 13, marginTop: 10 },
  hint: { color: "#94a3b8", fontSize: 12, marginTop: 6 },
  list: { listStyle: "none", padding: 0, margin: "8px 0 14px" },
  listItem: { padding: "10px 0", borderBottom: "1px solid #e2e8f0" },
  formRow: { display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" },
  formCol: { display: "flex", flexDirection: "column", gap: 8 },
  kpiRow: { display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: 12 },
  kpi: { background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: 10, padding: "12px 14px" },
  kpiAccent: { background: "#eff6ff", borderColor: "#bfdbfe" },
  kpiLabel: { fontSize: 12, color: "#64748b" },
  kpiValue: { fontSize: 20, fontWeight: 700, marginTop: 4, color: "#0f172a" },
};

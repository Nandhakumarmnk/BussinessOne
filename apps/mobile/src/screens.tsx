import { useEffect, useState } from "react";
import {
  ActivityIndicator, Alert, Pressable, ScrollView, StyleSheet, Text, TextInput, View,
} from "react-native";
import { Api, DashboardSummary } from "./api";
import { cachedMasters, outbox, runSync } from "./offline";
import { session } from "./session";

const inr = (n: number) => `₹ ${Math.round(n).toLocaleString("en-IN")}`;

export function LoginScreen({ api, onLoggedIn }: { api: Api; onLoggedIn: () => void }) {
  const [mobileOrEmail, setMobileOrEmail] = useState("owner@business-one.local");
  const [password, setPassword] = useState("Owner@123");
  const [busy, setBusy] = useState(false);

  async function submit() {
    setBusy(true);
    try {
      const res = await api.login(mobileOrEmail, password);
      await session.start(res);
      onLoggedIn();
    } catch (e: any) {
      Alert.alert("Login failed", e?.message ?? "Try again");
    } finally {
      setBusy(false);
    }
  }

  return (
    <View style={styles.center}>
      <Text style={styles.title}>Business One</Text>
      <Text style={styles.muted}>Multi-Business ERP</Text>
      <TextInput style={styles.input} autoCapitalize="none" value={mobileOrEmail} onChangeText={setMobileOrEmail} placeholder="Mobile / Email" />
      <TextInput style={styles.input} secureTextEntry value={password} onChangeText={setPassword} placeholder="Password" />
      <Pressable style={styles.btn} onPress={submit} disabled={busy}>
        <Text style={styles.btnText}>{busy ? "Signing in…" : "Log in"}</Text>
      </Pressable>
    </View>
  );
}

export function HomeScreen({ api, onLogout, onAddExpense }: { api: Api; onLogout: () => void; onAddExpense: () => void }) {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [pending, setPending] = useState(0);
  const [syncing, setSyncing] = useState(false);

  async function refresh() {
    setPending(await outbox.count());
    try { setSummary(await api.dashboard()); } catch { /* offline — keep last */ }
  }
  useEffect(() => { refresh(); }, []);

  async function sync() {
    setSyncing(true);
    try {
      const r = await runSync(api);
      Alert.alert("Sync", `Pushed ${r.pushed}, failed ${r.failed}, ${r.pulled ? "pulled masters" : "no pull"}; ${r.remaining} queued.`);
      await refresh();
    } finally {
      setSyncing(false);
    }
  }

  const kpis: [string, number][] = summary
    ? [["Today Income", summary.todayIncome], ["Today Expense", summary.todayExpense],
       ["Month Profit", summary.totalProfit], ["Pending Credits", summary.pendingCredits]]
    : [];

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <View style={styles.row}>
        <Text style={styles.title}>Dashboard</Text>
        <Pressable onPress={onLogout}><Text style={styles.link}>Log out</Text></Pressable>
      </View>

      {pending > 0 && <Text style={styles.badge}>▲ {pending} change(s) waiting to sync</Text>}

      <View style={styles.kpis}>
        {kpis.map(([label, value]) => (
          <View key={label} style={styles.kpi}>
            <Text style={styles.muted}>{label}</Text>
            <Text style={styles.kpiValue}>{inr(value)}</Text>
          </View>
        ))}
        {!summary && <Text style={styles.muted}>Offline — dashboard unavailable.</Text>}
      </View>

      <Pressable style={styles.btn} onPress={onAddExpense}><Text style={styles.btnText}>+ Add Expense (works offline)</Text></Pressable>
      <Pressable style={[styles.btn, styles.btnAlt]} onPress={sync} disabled={syncing}>
        <Text style={styles.btnText}>{syncing ? "Syncing…" : "Sync now"}</Text>
      </Pressable>
    </ScrollView>
  );
}

export function AddExpenseScreen({ onDone }: { onDone: () => void }) {
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const today = new Date().toISOString().slice(0, 10);

  async function save() {
    const value = Number(amount);
    if (!value || value <= 0) { Alert.alert("Enter a valid amount"); return; }
    // Queued locally; replayed to POST /expenses with its id as the Idempotency-Key on next sync.
    await outbox.enqueue("POST", "/expenses",
      { expenseDate: today, amount: value, description }, `Expense ${inr(value)}`);
    Alert.alert("Saved offline", "Will sync when online.");
    onDone();
  }

  return (
    <View style={styles.page}>
      <Text style={styles.title}>New Expense</Text>
      <TextInput style={styles.input} keyboardType="numeric" value={amount} onChangeText={setAmount} placeholder="Amount" />
      <TextInput style={styles.input} value={description} onChangeText={setDescription} placeholder="Description" />
      <Text style={styles.muted}>Date: {today}</Text>
      <Pressable style={styles.btn} onPress={save}><Text style={styles.btnText}>Save</Text></Pressable>
      <Pressable onPress={onDone}><Text style={[styles.link, { marginTop: 14 }]}>Cancel</Text></Pressable>
    </View>
  );
}

export function Splash() {
  return <View style={styles.center}><ActivityIndicator size="large" /></View>;
}

const styles = StyleSheet.create({
  page: { padding: 20, gap: 12 },
  center: { flex: 1, justifyContent: "center", padding: 24, gap: 12 },
  row: { flexDirection: "row", justifyContent: "space-between", alignItems: "center" },
  title: { fontSize: 22, fontWeight: "700" },
  muted: { color: "#64748b" },
  link: { color: "#2563eb", fontWeight: "600" },
  badge: { backgroundColor: "#fef3c7", color: "#92400e", padding: 8, borderRadius: 8 },
  input: { borderWidth: 1, borderColor: "#cbd5e1", borderRadius: 8, padding: 12 },
  btn: { backgroundColor: "#2563eb", borderRadius: 8, padding: 14, alignItems: "center" },
  btnAlt: { backgroundColor: "#0f172a" },
  btnText: { color: "#fff", fontWeight: "700" },
  kpis: { gap: 10 },
  kpi: { backgroundColor: "#f1f5f9", borderRadius: 10, padding: 14 },
  kpiValue: { fontSize: 20, fontWeight: "700", marginTop: 2 },
});

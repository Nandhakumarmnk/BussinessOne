import { useEffect, useRef, useState } from "react";
import {
  ActivityIndicator, Alert, Image, Pressable, ScrollView, StyleSheet, Text, TextInput, View,
} from "react-native";
import { CameraView, useCameraPermissions } from "expo-camera";
import { Api, DashboardSummary, SyncItem, SyncPull } from "./api";
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

export function HomeScreen({ api, onLogout, onAddExpense, onCustomers, onNewLoad }: {
  api: Api; onLogout: () => void; onAddExpense: () => void; onCustomers: () => void; onNewLoad: () => void;
}) {
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
      <View style={styles.btnRow}>
        <Pressable style={[styles.btn, styles.btnGhost, styles.grow]} onPress={onCustomers}>
          <Text style={styles.btnGhostText}>Customers</Text>
        </Pressable>
        <Pressable style={[styles.btn, styles.btnGhost, styles.grow]} onPress={onNewLoad}>
          <Text style={styles.btnGhostText}>+ New Load</Text>
        </Pressable>
      </View>
      <Pressable style={[styles.btn, styles.btnAlt]} onPress={sync} disabled={syncing}>
        <Text style={styles.btnText}>{syncing ? "Syncing…" : "Sync now"}</Text>
      </Pressable>
    </ScrollView>
  );
}

/** Full-screen camera used to capture a bill/receipt photo. Returns the local file URI. */
function CameraCapture({ onCapture, onCancel }: { onCapture: (uri: string) => void; onCancel: () => void }) {
  const [permission, requestPermission] = useCameraPermissions();
  const ref = useRef<CameraView>(null);

  if (!permission) return <View style={styles.center}><ActivityIndicator size="large" /></View>;
  if (!permission.granted) {
    return (
      <View style={styles.center}>
        <Text style={styles.muted}>Camera access is needed to attach a photo.</Text>
        <Pressable style={styles.btn} onPress={requestPermission}><Text style={styles.btnText}>Grant permission</Text></Pressable>
        <Pressable onPress={onCancel}><Text style={styles.link}>Cancel</Text></Pressable>
      </View>
    );
  }

  async function shoot() {
    const photo = await ref.current?.takePictureAsync({ quality: 0.6 });
    if (photo?.uri) onCapture(photo.uri);
    else Alert.alert("Capture failed", "Please try again.");
  }

  return (
    <View style={{ flex: 1, backgroundColor: "#000" }}>
      <CameraView ref={ref} style={{ flex: 1 }} facing="back" />
      <View style={styles.camBar}>
        <Pressable onPress={onCancel}><Text style={[styles.link, { color: "#fff" }]}>Cancel</Text></Pressable>
        <Pressable style={styles.shutter} onPress={shoot} accessibilityLabel="Take photo" />
        <View style={{ width: 60 }} />
      </View>
    </View>
  );
}

export function AddExpenseScreen({ api, onDone }: { api: Api; onDone: () => void }) {
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [photoUri, setPhotoUri] = useState<string | null>(null);
  const [showCamera, setShowCamera] = useState(false);
  const [busy, setBusy] = useState(false);
  const today = new Date().toISOString().slice(0, 10);

  if (showCamera) {
    return <CameraCapture onCapture={(uri) => { setPhotoUri(uri); setShowCamera(false); }}
                          onCancel={() => setShowCamera(false)} />;
  }

  async function save() {
    const value = Number(amount);
    if (!value || value <= 0) { Alert.alert("Enter a valid amount"); return; }
    setBusy(true);
    try {
      // Attachments are online-only: upload the photo now (if any) and attach its key. If offline,
      // the expense still queues in the outbox without an attachment.
      let attachmentKey: string | null = null;
      if (photoUri) {
        try {
          attachmentKey = (await api.uploadFile(photoUri, `receipt-${Date.now()}.jpg`, "image/jpeg")).objectKey;
        } catch {
          Alert.alert("Attachment skipped", "The photo needs internet to upload; saving the expense without it.");
        }
      }
      // Queued locally; replayed to POST /expenses with its id as the Idempotency-Key on next sync.
      await outbox.enqueue("POST", "/expenses",
        { expenseDate: today, amount: value, description, attachmentKey }, `Expense ${inr(value)}`);
      Alert.alert("Saved", attachmentKey ? "Attachment uploaded; expense will sync when online."
                                         : "Will sync when online.");
      onDone();
    } finally {
      setBusy(false);
    }
  }

  return (
    <View style={styles.page}>
      <Text style={styles.title}>New Expense</Text>
      <TextInput style={styles.input} keyboardType="numeric" value={amount} onChangeText={setAmount} placeholder="Amount" />
      <TextInput style={styles.input} value={description} onChangeText={setDescription} placeholder="Description" />
      <Text style={styles.muted}>Date: {today}</Text>

      {photoUri
        ? <Image source={{ uri: photoUri }} style={styles.thumb} />
        : null}
      <Pressable style={[styles.btn, styles.btnGhost]} onPress={() => setShowCamera(true)}>
        <Text style={styles.btnGhostText}>{photoUri ? "Retake photo" : "📷 Attach photo"}</Text>
      </Pressable>

      <Pressable style={styles.btn} onPress={save} disabled={busy}>
        <Text style={styles.btnText}>{busy ? "Saving…" : "Save"}</Text>
      </Pressable>
      <Pressable onPress={onDone}><Text style={[styles.link, { marginTop: 14 }]}>Cancel</Text></Pressable>
    </View>
  );
}

/** A simple tap-to-select chip list, fed from the offline `sync/pull` master cache. */
function ChipPicker({ label, items, value, onSelect }: {
  label: string; items: SyncItem[]; value: string | null; onSelect: (id: string | null) => void;
}) {
  return (
    <View style={{ gap: 6 }}>
      <Text style={styles.muted}>{label}</Text>
      <View style={styles.chips}>
        {items.length === 0 && <Text style={styles.muted}>None cached — Sync on Home first.</Text>}
        {items.map((it) => {
          const on = value === it.id;
          return (
            <Pressable key={it.id} onPress={() => onSelect(on ? null : it.id)}
                       style={[styles.chip, on && styles.chipOn]}>
              <Text style={on ? styles.chipTextOn : styles.chipText}>{it.name}</Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

export function CustomersScreen({ api, onBack }: { api: Api; onBack: () => void }) {
  const [customers, setCustomers] = useState<SyncItem[]>([]);
  const [name, setName] = useState("");
  const [mobile, setMobile] = useState("");
  const [opening, setOpening] = useState("");
  const [syncing, setSyncing] = useState(false);

  async function load() { const m = await cachedMasters(); setCustomers(m?.customers ?? []); }
  useEffect(() => { load(); }, []);

  async function refresh() {
    setSyncing(true);
    try { await runSync(api); await load(); } finally { setSyncing(false); }
  }

  async function save() {
    if (!name.trim()) { Alert.alert("Enter a name"); return; }
    await outbox.enqueue("POST", "/customers",
      { name: name.trim(), mobile: mobile || null, creditLimit: 0, openingBalance: Number(opening || 0) },
      `Customer ${name.trim()}`);
    setName(""); setMobile(""); setOpening("");
    Alert.alert("Saved offline", "The customer will appear after the next sync.");
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <View style={styles.row}>
        <Text style={styles.title}>Customers</Text>
        <Pressable onPress={onBack}><Text style={styles.link}>Back</Text></Pressable>
      </View>
      <Pressable style={[styles.btn, styles.btnGhost]} onPress={refresh} disabled={syncing}>
        <Text style={styles.btnGhostText}>{syncing ? "Syncing…" : "Refresh from server"}</Text>
      </Pressable>
      <View style={styles.kpis}>
        {customers.map((c) => (
          <View key={c.id} style={styles.kpi}>
            <Text style={{ fontWeight: "600" }}>{c.name}</Text>
            {c.extra ? <Text style={styles.muted}>{c.extra}</Text> : null}
          </View>
        ))}
        {customers.length === 0 && <Text style={styles.muted}>No cached customers. Refresh to load.</Text>}
      </View>
      <Text style={styles.title}>Add customer</Text>
      <TextInput style={styles.input} value={name} onChangeText={setName} placeholder="Name" />
      <TextInput style={styles.input} value={mobile} onChangeText={setMobile} placeholder="Mobile" keyboardType="phone-pad" />
      <TextInput style={styles.input} value={opening} onChangeText={setOpening} placeholder="Opening balance" keyboardType="numeric" />
      <Pressable style={styles.btn} onPress={save}><Text style={styles.btnText}>Save (offline)</Text></Pressable>
    </ScrollView>
  );
}

export function NewLoadScreen({ onBack }: { onBack: () => void }) {
  const [masters, setMasters] = useState<SyncPull | null>(null);
  const [f, setF] = useState({
    loadNumber: "", loadName: "", loadAmount: "", loadmanCharges: "",
    fuelExpense: "", maintenanceExpense: "", driverCharges: "", otherExpense: "",
  });
  const [customerId, setCustomerId] = useState<string | null>(null);
  const [vehicleId, setVehicleId] = useState<string | null>(null);
  const [driverId, setDriverId] = useState<string | null>(null);
  const today = new Date().toISOString().slice(0, 10);

  useEffect(() => { cachedMasters().then(setMasters); }, []);
  const set = (k: keyof typeof f) => (v: string) => setF((prev) => ({ ...prev, [k]: v }));
  const num = (v: string) => Number(v || 0);

  async function save() {
    if (!f.loadNumber.trim() || !num(f.loadAmount)) { Alert.alert("Load # and amount are required"); return; }
    await outbox.enqueue("POST", "/transport/loads", {
      loadNumber: f.loadNumber.trim(), loadName: f.loadName || null,
      customerId, vehicleId, driverId, loadDate: today,
      loadAmount: num(f.loadAmount), loadmanCharges: num(f.loadmanCharges), fuelExpense: num(f.fuelExpense),
      maintenanceExpense: num(f.maintenanceExpense), driverCharges: num(f.driverCharges), otherExpense: num(f.otherExpense),
    }, `Load ${f.loadNumber.trim()}`);
    Alert.alert("Saved offline", "Will sync when online.");
    onBack();
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <View style={styles.row}>
        <Text style={styles.title}>New Load</Text>
        <Pressable onPress={onBack}><Text style={styles.link}>Back</Text></Pressable>
      </View>
      <TextInput style={styles.input} value={f.loadNumber} onChangeText={set("loadNumber")} placeholder="Load #" />
      <TextInput style={styles.input} value={f.loadName} onChangeText={set("loadName")} placeholder="Goods / name" />
      <ChipPicker label="Customer (bills to ledger)" items={masters?.customers ?? []} value={customerId} onSelect={setCustomerId} />
      <ChipPicker label="Vehicle" items={masters?.vehicles ?? []} value={vehicleId} onSelect={setVehicleId} />
      <ChipPicker label="Driver" items={masters?.drivers ?? []} value={driverId} onSelect={setDriverId} />
      <TextInput style={styles.input} value={f.loadAmount} onChangeText={set("loadAmount")} placeholder="Load amount" keyboardType="numeric" />
      <TextInput style={styles.input} value={f.loadmanCharges} onChangeText={set("loadmanCharges")} placeholder="Loadman charges" keyboardType="numeric" />
      <TextInput style={styles.input} value={f.fuelExpense} onChangeText={set("fuelExpense")} placeholder="Fuel" keyboardType="numeric" />
      <TextInput style={styles.input} value={f.maintenanceExpense} onChangeText={set("maintenanceExpense")} placeholder="Maintenance" keyboardType="numeric" />
      <TextInput style={styles.input} value={f.driverCharges} onChangeText={set("driverCharges")} placeholder="Driver charges" keyboardType="numeric" />
      <TextInput style={styles.input} value={f.otherExpense} onChangeText={set("otherExpense")} placeholder="Other" keyboardType="numeric" />
      <Text style={styles.muted}>Date: {today} · Profit is computed on the server.</Text>
      <Pressable style={styles.btn} onPress={save}><Text style={styles.btnText}>Save (offline)</Text></Pressable>
    </ScrollView>
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
  btnGhost: { backgroundColor: "#eef4ff", borderWidth: 1, borderColor: "#bfd3ff" },
  btnGhostText: { color: "#1d4ed8", fontWeight: "700" },
  btnRow: { flexDirection: "row", gap: 10 },
  grow: { flex: 1 },
  kpis: { gap: 10 },
  kpi: { backgroundColor: "#f1f5f9", borderRadius: 10, padding: 14 },
  kpiValue: { fontSize: 20, fontWeight: "700", marginTop: 2 },
  thumb: { width: "100%", height: 180, borderRadius: 10, backgroundColor: "#e2e8f0" },
  camBar: {
    flexDirection: "row", alignItems: "center", justifyContent: "space-between",
    paddingHorizontal: 24, paddingVertical: 18, backgroundColor: "#000",
  },
  shutter: { width: 66, height: 66, borderRadius: 33, backgroundColor: "#fff", borderWidth: 4, borderColor: "#94a3b8" },
  chips: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
  chip: { borderWidth: 1, borderColor: "#cbd5e1", borderRadius: 999, paddingVertical: 6, paddingHorizontal: 12 },
  chipOn: { backgroundColor: "#2563eb", borderColor: "#2563eb" },
  chipText: { color: "#1e293b", fontSize: 13 },
  chipTextOn: { color: "#fff", fontSize: 13, fontWeight: "600" },
});

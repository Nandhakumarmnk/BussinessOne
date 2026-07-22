import Constants from "expo-constants";
import { useEffect, useState } from "react";
import { SafeAreaView, StatusBar } from "react-native";
import { createApi } from "./src/api";
import {
  AccountingScreen, AddExpenseScreen, CctvScreen, CoconutScreen, CustomersScreen, FarmScreen,
  HomeScreen, LoginScreen, NewLoadScreen, Splash,
} from "./src/screens";
import { session } from "./src/session";

const baseUrl =
  (Constants.expoConfig?.extra as { apiBaseUrl?: string } | undefined)?.apiBaseUrl ??
  "http://10.0.2.2:8080/api/v1";

// Single API client bound to the session getters (token + active business).
const api = createApi({
  baseUrl,
  getToken: () => session.getToken(),
  getBusinessId: () => session.getBusinessId(),
});

type Screen =
  | "loading" | "login" | "home" | "addExpense" | "customers" | "newLoad"
  | "cctv" | "farm" | "coconut" | "accounting";

export default function App() {
  const [screen, setScreen] = useState<Screen>("loading");

  useEffect(() => {
    session.bootstrap().then((authed) => setScreen(authed ? "home" : "login"));
  }, []);

  async function logout() {
    await session.clear();
    setScreen("login");
  }

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: "#fff" }}>
      <StatusBar barStyle="dark-content" />
      {screen === "loading" && <Splash />}
      {screen === "login" && <LoginScreen api={api} onLoggedIn={() => setScreen("home")} />}
      {screen === "home" && (
        <HomeScreen
          api={api}
          onLogout={logout}
          onAddExpense={() => setScreen("addExpense")}
          onCustomers={() => setScreen("customers")}
          onNewLoad={() => setScreen("newLoad")}
          onOpenModule={(m) => setScreen(m)}
        />
      )}
      {screen === "addExpense" && <AddExpenseScreen api={api} onDone={() => setScreen("home")} />}
      {screen === "customers" && <CustomersScreen api={api} onBack={() => setScreen("home")} />}
      {screen === "newLoad" && <NewLoadScreen onBack={() => setScreen("home")} />}
      {screen === "cctv" && <CctvScreen api={api} onBack={() => setScreen("home")} />}
      {screen === "farm" && <FarmScreen api={api} onBack={() => setScreen("home")} />}
      {screen === "coconut" && <CoconutScreen api={api} onBack={() => setScreen("home")} />}
      {screen === "accounting" && <AccountingScreen api={api} onBack={() => setScreen("home")} />}
    </SafeAreaView>
  );
}

// Auth + active-business context, persisted in the device keystore (Expo SecureStore).

import * as SecureStore from "expo-secure-store";
import { LoginResponse, Membership } from "./api";

const ACCESS = "erp.accessToken";
const REFRESH = "erp.refreshToken";
const BUSINESS = "erp.businessId";

let accessToken: string | null = null;
let businessId: string | null = null;
let memberships: Membership[] = [];

export const session = {
  getToken: () => accessToken,
  getBusinessId: () => businessId,
  getMemberships: () => memberships,

  async bootstrap(): Promise<boolean> {
    accessToken = await SecureStore.getItemAsync(ACCESS);
    businessId = await SecureStore.getItemAsync(BUSINESS);
    return accessToken != null;
  },

  async start(login: LoginResponse): Promise<void> {
    accessToken = login.accessToken;
    memberships = login.memberships;
    businessId = login.memberships[0]?.businessId ?? null;
    await SecureStore.setItemAsync(ACCESS, login.accessToken);
    await SecureStore.setItemAsync(REFRESH, login.refreshToken);
    if (businessId) await SecureStore.setItemAsync(BUSINESS, businessId);
  },

  async setBusiness(id: string): Promise<void> {
    businessId = id;
    await SecureStore.setItemAsync(BUSINESS, id);
  },

  async clear(): Promise<void> {
    accessToken = null;
    businessId = null;
    memberships = [];
    await SecureStore.deleteItemAsync(ACCESS);
    await SecureStore.deleteItemAsync(REFRESH);
    await SecureStore.deleteItemAsync(BUSINESS);
  },
};

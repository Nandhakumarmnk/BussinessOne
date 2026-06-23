// Minimal typed API client for the Phase 0/1 walking skeleton.
// Phase later replaces this with the shared @erp/api-client generated from OpenAPI.

import type {
  ApiEnvelope, BusinessDto, DashboardSummary, LoginResponse, MeResponse, MemberDto, RefItem,
} from "./types";

const BASE = "/api/v1";

let accessToken: string | null = sessionStorage.getItem("accessToken");
let activeBusinessId: string | null = sessionStorage.getItem("businessId");

export function setAccessToken(token: string | null) {
  accessToken = token;
  if (token) sessionStorage.setItem("accessToken", token);
  else sessionStorage.removeItem("accessToken");
}

export function setActiveBusiness(businessId: string | null) {
  activeBusinessId = businessId;
  if (businessId) sessionStorage.setItem("businessId", businessId);
  else sessionStorage.removeItem("businessId");
}

export function getActiveBusiness() {
  return activeBusinessId;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Content-Type", "application/json");
  if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);
  if (activeBusinessId) headers.set("X-Business-Id", activeBusinessId);

  const res = await fetch(`${BASE}${path}`, { ...init, headers });
  if (res.status === 204) return undefined as T;

  const body = await res.json().catch(() => null);
  if (!res.ok) {
    throw new Error(body?.error?.message ?? `Request failed (${res.status})`);
  }
  return (body as ApiEnvelope<T>).data;
}

export interface CreateBusinessInput {
  name: string;
  businessTypeCode: string;
  gstNumber?: string | null;
  address?: string | null;
}

export interface InviteUserInput {
  fullName: string;
  mobile: string;
  email?: string | null;
  password: string;
  businessId: string;
  roleCode: string;
}

export const api = {
  login: (mobileOrEmail: string, password: string) =>
    request<LoginResponse>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ mobileOrEmail, password }),
    }),

  me: () => request<MeResponse>("/me"),

  businesses: () => request<BusinessDto[]>("/businesses"),
  createBusiness: (input: CreateBusinessInput) =>
    request<BusinessDto>("/businesses", { method: "POST", body: JSON.stringify(input) }),

  businessTypes: () => request<RefItem[]>("/business-types"),
  roles: () => request<RefItem[]>("/roles"),

  members: (businessId: string) => request<MemberDto[]>(`/businesses/${businessId}/members`),

  dashboard: () => request<DashboardSummary>("/dashboard/summary"),

  inviteUser: (input: InviteUserInput) =>
    request<unknown>("/users", { method: "POST", body: JSON.stringify(input) }),
};

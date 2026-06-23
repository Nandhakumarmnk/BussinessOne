// Phase 0/1: hand-written to match the API contract. Phase later generates these from the
// backend OpenAPI spec into packages/types and imports them in both web and mobile (docs/07).

export interface Membership {
  businessId: string;
  businessName: string;
  businessTypeCode: "TRANSPORT" | "CCTV" | "FARM" | "COCONUT";
  role: string;
  permissions: string[];
}

export interface UserSummary {
  id: string;
  fullName: string;
  mobile: string;
  email: string | null;
  isSuperAdmin: boolean;
}

export interface LoginResponse {
  accessToken: string;
  expiresIn: number;
  refreshToken: string;
  user: UserSummary;
  memberships: Membership[];
}

export interface MeResponse {
  user: UserSummary;
  memberships: Membership[];
}

export interface BusinessDto {
  id: string;
  name: string;
  businessTypeCode: string;
  businessTypeName: string;
  gstNumber: string | null;
  address: string | null;
  isActive: boolean;
  role: string | null;
}

export interface MemberDto {
  userId: string;
  fullName: string;
  mobile: string;
  roleCode: string;
  roleName: string;
}

export interface RefItem {
  id: string;
  code: string;
  name: string;
}

export interface DashboardSummary {
  todayIncome: number;
  todayExpense: number;
  monthIncome: number;
  monthExpense: number;
  totalProfit: number;
  pendingCredits: number;
  pendingCollections: number;
}

export interface ApiEnvelope<T> {
  data: T;
}

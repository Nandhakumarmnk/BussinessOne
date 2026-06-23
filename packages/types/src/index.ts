// Shared contract types for Web + Mobile.
//
// Phase 0: this package is the agreed home for shared types; apps/web currently keeps a local
// copy for a zero-build first run. Phase 1 generates `api.ts` here from the backend OpenAPI spec
// (see docs/07) and both clients import from `@erp/types`.

export type BusinessTypeCode = "TRANSPORT" | "CCTV" | "FARM" | "COCONUT";

export interface Membership {
  businessId: string;
  businessName: string;
  businessTypeCode: BusinessTypeCode;
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

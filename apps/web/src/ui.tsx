// Shared UI primitives + formatters used by the shell (App.tsx) and the module screens.

export const inr = (n: number) =>
  new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", maximumFractionDigits: 0 }).format(n);

export const initials = (name: string) =>
  name.trim().split(/\s+/).slice(0, 2).map((p) => p[0]?.toUpperCase() ?? "").join("") || "?";

export const today = () => new Date().toISOString().slice(0, 10);

/** Human label for an UPPER_SNAKE status code, e.g. IN_PROGRESS → "In progress". */
export const prettyStatus = (s: string) =>
  s.replace(/_/g, " ").toLowerCase().replace(/^\w/, (c) => c.toUpperCase());

/** Maps a status code to a badge className (green = done, blue = in-flight, grey = new). */
export function statusBadgeClass(status: string): string {
  const s = status.toUpperCase();
  if (["PAID", "RECEIVED", "RESOLVED", "CLEARED", "APPROVED", "ACTIVE", "SOLD", "CLOSED"].includes(s))
    return "badge badge--ok";
  if (["SUBMITTED", "PARTIAL", "IN_PROGRESS", "PENDING"].includes(s)) return "badge badge--owner";
  return "badge";
}

/* -------------------------------------------------------------------------- */
/* Icons (Lucide-style, inline so there is no runtime dependency)             */
/* -------------------------------------------------------------------------- */
const PATHS: Record<string, string> = {
  dashboard: "M3 3h7v7H3zM14 3h7v5h-7zM14 12h7v9h-7zM3 14h7v7H3z",
  building: "M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18ZM9 6h.01M9 10h.01M9 14h.01M14 6h.01M14 10h.01M14 14h.01M9 22v-4h6v4",
  users: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75",
  up: "M16 7h6v6M22 7l-8.5 8.5-5-5L2 17",
  down: "M16 17h6v-6M22 17l-8.5-8.5-5 5L2 7",
  wallet: "M19 7V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-2M16 12h.01M21 9v6h-5a3 3 0 0 1 0-6Z",
  clock: "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20ZM12 6v6l4 2",
  card: "M2 7a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2zM2 10h20",
  logout: "M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9",
  plus: "M12 5v14M5 12h14",
  tick: "M20 6 9 17l-5-5",
  shield: "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z",
  bolt: "M13 2 3 14h7l-1 8 10-12h-7l1-8Z",
  receipt: "M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1ZM8 7h8M8 11h8M8 15h5",
  truck: "M14 18V6H2v12h2m10 0h4m-4 0H8m10 0h2a1 1 0 0 0 1-1v-4l-3-4h-4v9M8 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Zm10 0a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z",
  paperclip: "M21 8.5 12.5 17a4 4 0 0 1-6-6l8.5-8.5a2.5 2.5 0 0 1 3.5 3.5L10 14.5a1 1 0 0 1-1.5-1.5l7-7",
  trash: "M3 6h18M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2m2 0v14a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V6",
  contact: "M16 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M20 8v6M23 11h-6",
  close: "M18 6 6 18M6 6l12 12",
  camera: "M23 7l-7 5 7 5V7zM14 5H3a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2Z",
  leaf: "M11 20A7 7 0 0 1 9.8 6.1C15.5 5 17 4.48 19 2c1 2 2 4.18 2 8 0 5.5-4.78 10-10 10ZM2 21c0-3 1.85-5.36 5.08-6",
  package: "M12 2 2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5",
};

export function Icon({ name, className = "icon" }: { name: string; className?: string }) {
  return (
    <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor"
         strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={PATHS[name]} />
    </svg>
  );
}

# 08 · Web Screens (responsive)

Layout: persistent left nav, top bar with **business switcher** + user menu. The nav menu is
**role- and business-type-aware** — a Farm business hides Transport/CCTV items, an Employee
sees fewer items than an Owner. Built with React + MUI; charts via Recharts; tables via TanStack.

## 1. Navigation shell

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ☰  Business One     [ Sri Transport  ▼ ]            🔔   Enes ▼  (Owner)     │  top bar
├──────────────┬────────────────────────────────────────────────────────────┤
│ ▸ Dashboard  │                                                              │
│ ▸ Employees  │                  < page content >                            │
│ ▸ Expenses   │                                                              │
│ ▸ Customers  │   (left nav items vary by business type + role)              │
│ ──Transport──│                                                              │
│ ▸ Vehicles   │                                                              │
│ ▸ Drivers    │                                                              │
│ ▸ Loads      │                                                              │
│ ▸ Credits    │                                                              │
│ ──General────│                                                              │
│ ▸ Accounting │                                                              │
│ ▸ Reports    │                                                              │
│ ▸ Admin*     │  (* Super Admin / Owner only)                                │
└──────────────┴────────────────────────────────────────────────────────────┘
```

Business switcher in the top bar sets `X-Business-Id` for all subsequent calls and re-renders
the nav for that business's type.

## 2. Login / auth

```
┌───────────────────────────────┐
│           Business One        │
│   ┌───────────────────────┐   │
│   │ Mobile / Email        │   │
│   ├───────────────────────┤   │
│   │ Password         👁    │   │
│   └───────────────────────┘   │
│   [ Forgot password? ]        │
│        [   Log in    ]        │
└───────────────────────────────┘
Forgot → enter mobile/email → OTP/reset link → set new password.
```

## 3. Dashboard (consolidated KPIs)

```
┌──────────────────────────────────────────────────────────────────────────┐
│  Dashboard — Sri Transport                         [ Today ▼ ] [Export ▾]  │
├───────────────┬───────────────┬───────────────┬──────────────────────────┤
│ Today Income  │ Today Expense │ Month Income  │ Month Expense             │
│  ₹ 18,000     │  ₹ 7,400      │  ₹ 4,20,000   │  ₹ 1,85,000               │
├───────────────┴───────────────┼───────────────┴──────────────────────────┤
│ Total Profit  ₹ 2,35,000       │ Pending Credits  ₹ 96,500   (12 customers)│
│                                │ Pending Collections ₹ 41,000              │
├────────────────────────────────┴──────────────────────────────────────────┤
│  Income vs Expense (last 6 months)        [ line/bar chart ]               │
│  Top customers by outstanding             [ horizontal bars ]              │
└────────────────────────────────────────────────────────────────────────────┘
```

KPI cards map directly to `/dashboard/summary`. Owner viewing "All Businesses" gets a
consolidated roll-up across owned businesses.

## 4. Generic list + form pattern (used by all masters)

```
List view                                    Create/Edit drawer
┌───────────────────────────────────────┐   ┌──────────────────────────────┐
│ Vehicles            [+ New] [Export ▾] │   │  New Vehicle             [x] │
│ ┌─Search──────┐  [Type ▾] [Status ▾]   │   │ Vehicle Number  [________]   │
│ ├──────────────────────────────────┤   │   │ Type            [Lorry  ▼]   │
│ │ No.   Type   Model   Insurance ⋮ │   │   │ Model           [________]   │
│ │ TN01  Lorry  3118    12-Aug ⋮    │   │   │ Fuel Type       [Diesel ▼]   │
│ │ TN09  Mini   Dost    03-Mar ⋮    │   │   │ RC Details      [________]   │
│ └──────────────────────────────────┤   │   │ Insurance       [________]   │
│  1–20 of 34      ‹ 1 2 ›               │   │ Expiry          [date    ]   │
└───────────────────────────────────────┘   │   [ Cancel ]  [  Save  ]      │
                                             └──────────────────────────────┘
Row ⋮ = Edit / Delete (soft) / View. Validation inline; errors from API envelope.
```

## 5. Transport — Load Entry (with live profit)

```
┌──────────────────────────────────────────────────────────────────────────┐
│  New Load                                                            [x]   │
│  Load No [LD-0007]   Load Name [Cement]      Date [2026-06-23]             │
│  Customer [Ramraj ▼]  Vehicle [TN01AB1234 ▼]  Driver [Kumar ▼]            │
│  Source [Coimbatore]            Destination [Salem]                        │
│  ┌── Amounts ─────────────────────────────────────────────────────────┐   │
│  │ Load Amount       [ 18000 ]   Loadman Charges  [   800 ]            │   │
│  │ Fuel Expense      [  4200 ]   Maintenance      [   600 ]            │   │
│  │ Driver Charges    [  1500 ]   Other Expense    [   300 ]            │   │
│  └────────────────────────────────────────────────────────────────────┘   │
│  ▶ Computed Profit:  ₹ 10,600   (live, from @erp/domain)                   │
│  [ ] Create as credit (unpaid)        [ Cancel ]   [  Save Load  ]         │
└──────────────────────────────────────────────────────────────────────────┘
```

## 6. Transport — Credits

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Credits                              [Status: Open ▼]  Outstanding ₹96,500 │
│ Customer   Load     Amount   Paid    Balance   Status      Action          │
│ Ramraj     LD-0007  18,000   8,000   10,000    Partial    [Collect]        │
│ Senthil    LD-0006  12,000      0    12,000    Open       [Collect]        │
│  → Collect opens payment dialog: amount, mode (cash/upi/bank), reference    │
└──────────────────────────────────────────────────────────────────────────┘
```

## 7. CCTV — Purchase Order (with approval) & Sales

```
Purchase Order                                   Sales & Installation
┌──────────────────────────────┐                 ┌──────────────────────────────┐
│ PO-0021   Supplier [Acme ▼]  │                 │ INV-0102  Customer [Hotel ▼] │
│ Date [2026-06-20]            │                 │ ┌Lines──────────────────────┐│
│ ┌Lines──────────────────────┐│                 │ │ Item   Qty Rate  Tax  Tot ││
│ │ Item     Qty Rate Tax  Tot ││                 │ │ Camera  4  3200  18  15.1k││
│ │ Dome Cam 10  3000 18  35.4k││                 │ └──────────────────────────┘│
│ └──────────────────────────┘│                 │ Installation [ 2000 ]        │
│ Total ₹ 35,400               │                 │ Labour       [ 1500 ]        │
│ Status: Pending              │                 │ Tax ₹ 2,718  Total ₹ 21,318  │
│ [Submit] [Approve] [Receive] │                 │ Paid [ 21,318 ]              │
└──────────────────────────────┘                 │ [ Save Invoice ] [PDF]       │
Approve requires cctv.po.approve.                └──────────────────────────────┘
```

```
Service Management (Kanban)
┌── Open ────────┐ ┌── In Progress ──┐ ┌── Closed ───────┐
│ #C-12 Hotel    │ │ #C-09 Mr.Raj    │ │ #C-04 School    │
│ No signal      │ │ HDD replace     │ │ Reinstall  ✓    │
│ → Kumar        │ │ → Suresh        │ │                 │
└────────────────┘ └─────────────────┘ └─────────────────┘
Drag card to change status (PATCH /status). Filter by assignee.
```

## 8. Farm — Batch detail (tabs) & Wallet

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Batch GT-03  (Goat · started 12-May · qty 40)               P/L ₹ 38,200   │
│ [ Overview ] [ Feed ] [ Medical ] [ Expenses ] [ Sales ]                   │
│ ── Overview ───────────────────────────────────────────────────────────── │
│  Purchase ₹ 1,20,000   Feed ₹ 46,800   Medical ₹ 7,000  Labour ₹ 8,000    │
│  Sales ₹ 2,20,000      →  Profit ₹ 38,200                                  │
│  [ feed-cost trend chart ]            [ + Add Feed Entry ]                  │
└──────────────────────────────────────────────────────────────────────────┘

Wallet  Balance ₹ 52,000   [ + Add Money ] [ − Record Use ]
  21-Jun  credit  +20,000  Owner top-up
  20-Jun  debit    -6,800  Feed purchase (Batch GT-03)
```

## 9. Coconut — Batch & Profit

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Coconut Batch CB-08  (Copra · 12-Jun · 1,200 kg)            Profit ₹ 14,600│
│ Purchase ₹ 60,000   Labour ₹ 5,400   Transport ₹ 4,000                     │
│ [ Labour ] [ Transport ] [ Sales ]                                         │
│ Sales: 14-Jun  900kg ₹ 70,000 · 18-Jun 300kg ₹ 14,000  →  Total ₹ 84,000  │
└──────────────────────────────────────────────────────────────────────────┘
```

## 10. Accounting & Reports

```
Accounting                                       Reports
┌──────────────────────────────┐                 ┌──────────────────────────────┐
│ [Cash Book][Ledger][P & L]   │                 │ Report  [ Vehicle Profit ▼ ] │
│ From [01-Jun] To [30-Jun]    │                 │ Period  [ Monthly ▼ ]        │
│ Date     In      Out   Bal   │                 │ From [..] To [..]            │
│ 01-Jun  18,000  7,400 10.6k  │                 │ Business [ Sri Transport ▼ ] │
│ ...                          │                 │ Format  ( • PDF  ◦ Excel )   │
│ Totals 4.2L   1.85L  2.35L   │                 │ [ Generate & Download ]      │
└──────────────────────────────┘                 └──────────────────────────────┘
```

## 11. Admin (Super Admin / Owner)

```
Users & Roles
┌──────────────────────────────────────────────────────────────────────────┐
│ Users   [+ New User]                                                       │
│ Name     Mobile      Businesses / Role                       Status        │
│ Kumar    98xxxx12    Sri Transport: Driver                   Active        │
│ Suresh   97xxxx88    CCTV Shop: Employee, Farm: Manager      Active        │
│  → Edit user opens membership editor: add business + pick role             │
└──────────────────────────────────────────────────────────────────────────┘
```

## 12. Responsive behavior

- ≥1200px: full left nav + multi-column cards.
- 768–1199px: collapsible nav (icons), 2-column cards.
- <768px: nav becomes a drawer/hamburger; cards stack; tables become card lists; forms full-width.
  (The same React components; layout via MUI breakpoints — the mobile *app* is separate RN code.)

## 13. Cross-cutting UI behaviors

- **Permission gating:** actions (New/Approve/Delete) hidden when the user lacks the permission;
  server still enforces (defense in depth).
- **Money & dates** formatted via shared formatters (₹, Asia/Kolkata).
- **Optimistic updates** with React Query; toast on success/error using the API error `code`.
- **Empty/loading/error states** standardized across all lists.
- **Export** buttons call `/reports/export` and download the returned signed URL.

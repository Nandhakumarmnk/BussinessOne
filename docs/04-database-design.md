# 04 · Database Design — ER Diagram & Data Dictionary

PostgreSQL 15. Conventions: `snake_case`; UUID primary keys (`uuid` default `gen_random_uuid()`);
money `numeric(14,2)`; timestamps `timestamptz` (UTC); soft delete + audit columns on every
business table. The tenant discriminator is `business_id` on every business-scoped table.

> The runnable DDL is in [05-database-scripts.sql](05-database-scripts.sql). This file is the
> conceptual model + the human-readable data dictionary.

## 1. Audit/soft-delete columns (on every business table)

| Column | Type | Notes |
|--------|------|-------|
| `id` | uuid PK | `gen_random_uuid()` |
| `created_at` | timestamptz | not null, default `now()` |
| `created_by` | uuid | FK → users.id (nullable for system) |
| `updated_at` | timestamptz | nullable |
| `updated_by` | uuid | FK → users.id (nullable) |
| `is_deleted` | boolean | not null default false |
| `deleted_at` | timestamptz | nullable |

These are omitted from per-table dictionaries below for brevity (assume present).

## 2. ER Diagram — Identity, Tenancy & Common modules

```mermaid
erDiagram
    TENANT ||--o{ BUSINESS : owns
    TENANT ||--o{ USER : has
    BUSINESS_TYPE ||--o{ BUSINESS : classifies
    USER ||--o{ USER_BUSINESS : "member of"
    BUSINESS ||--o{ USER_BUSINESS : "has members"
    ROLE ||--o{ USER_BUSINESS : "granted as"
    ROLE ||--o{ ROLE_PERMISSION : has
    PERMISSION ||--o{ ROLE_PERMISSION : in
    USER ||--o{ REFRESH_TOKEN : holds

    BUSINESS ||--o{ EMPLOYEE : employs
    EMPLOYEE ||--o{ SALARY_HISTORY : earns
    EMPLOYEE ||--o{ ATTENDANCE : logs
    BUSINESS ||--o{ EXPENSE : records
    EXPENSE_TYPE ||--o{ EXPENSE : categorizes
    BUSINESS ||--o{ CUSTOMER : serves
    CUSTOMER ||--o{ CUSTOMER_LEDGER : "owes/pays"
    CUSTOMER ||--o{ COLLECTION : "settles via"

    TENANT {
        uuid id PK
        string name
        string timezone
        uuid owner_user_id FK
    }
    BUSINESS_TYPE {
        uuid id PK
        string code "TRANSPORT|CCTV|FARM|COCONUT"
        string name
    }
    BUSINESS {
        uuid id PK
        uuid tenant_id FK
        uuid business_type_id FK
        string name
        string gst_number
        boolean is_active
    }
    USER {
        uuid id PK
        uuid tenant_id FK
        string full_name
        string mobile
        string email
        string password_hash
        boolean is_active
    }
    ROLE {
        uuid id PK
        string code
        string name
        boolean is_system
    }
    PERMISSION {
        uuid id PK
        string code "module.action"
        string description
    }
    ROLE_PERMISSION {
        uuid role_id FK
        uuid permission_id FK
    }
    USER_BUSINESS {
        uuid id PK
        uuid user_id FK
        uuid business_id FK
        uuid role_id FK
    }
    REFRESH_TOKEN {
        uuid id PK
        uuid user_id FK
        string token_hash
        timestamptz expires_at
        timestamptz revoked_at
    }
    EMPLOYEE {
        uuid id PK
        uuid business_id FK
        string name
        string mobile
        string address
        date joining_date
        numeric salary
        uuid role_id FK
        string status
    }
    SALARY_HISTORY {
        uuid id PK
        uuid employee_id FK
        date period_month
        numeric amount
        numeric paid_amount
        date paid_on
    }
    ATTENDANCE {
        uuid id PK
        uuid employee_id FK
        date attendance_date
        string status "present|absent|half|leave"
    }
    EXPENSE_TYPE {
        uuid id PK
        uuid business_id FK
        string name
    }
    EXPENSE {
        uuid id PK
        uuid business_id FK
        uuid expense_type_id FK
        date expense_date
        numeric amount
        string description
        string attachment_key
    }
    CUSTOMER {
        uuid id PK
        uuid business_id FK
        string name
        string mobile
        string address
        string gst_number
        numeric credit_limit
    }
    CUSTOMER_LEDGER {
        uuid id PK
        uuid business_id FK
        uuid customer_id FK
        date entry_date
        string ref_type "load|sale|collection|opening"
        uuid ref_id
        numeric debit
        numeric credit
        numeric running_balance
    }
    COLLECTION {
        uuid id PK
        uuid business_id FK
        uuid customer_id FK
        date collection_date
        numeric amount
        string mode "cash|upi|bank|cheque"
        string reference
    }
```

## 3. ER Diagram — Business 1: Goods Transport

```mermaid
erDiagram
    BUSINESS ||--o{ VEHICLE : owns
    BUSINESS ||--o{ DRIVER : employs
    BUSINESS ||--o{ LOAD : books
    CUSTOMER ||--o{ LOAD : "shipped for"
    VEHICLE ||--o{ LOAD : carries
    DRIVER ||--o{ LOAD : drives
    LOAD ||--o| CREDIT : "may have"

    VEHICLE {
        uuid id PK
        uuid business_id FK
        string vehicle_number
        string vehicle_type
        string model
        string fuel_type
        string rc_details
        string insurance_details
        date insurance_expiry
    }
    DRIVER {
        uuid id PK
        uuid business_id FK
        string name
        string mobile
        string driver_type "self|salaried"
        numeric salary
    }
    LOAD {
        uuid id PK
        uuid business_id FK
        string load_number
        string load_name
        uuid customer_id FK
        uuid vehicle_id FK
        uuid driver_id FK
        string source
        string destination
        numeric load_amount
        numeric loadman_charges
        numeric fuel_expense
        numeric maintenance_expense
        numeric driver_charges
        numeric other_expense
        numeric profit "generated/derived"
        date load_date
        string status
    }
    CREDIT {
        uuid id PK
        uuid business_id FK
        uuid load_id FK
        uuid customer_id FK
        numeric load_amount
        numeric paid_amount
        numeric balance_amount "generated"
        string status "open|partial|settled"
    }
```

**Load Profit** = `load_amount − (fuel_expense + maintenance_expense + driver_charges +
loadman_charges + other_expense)`. Stored as a Postgres **generated column** so it can never
drift from the inputs.

## 4. ER Diagram — Business 2: Electronics & CCTV

```mermaid
erDiagram
    BUSINESS ||--o{ ITEM : stocks
    BUSINESS ||--o{ SUPPLIER : "buys from"
    BUSINESS ||--o{ PURCHASE_ORDER : raises
    SUPPLIER ||--o{ PURCHASE_ORDER : fulfills
    PURCHASE_ORDER ||--o{ PURCHASE_ORDER_LINE : contains
    ITEM ||--o{ PURCHASE_ORDER_LINE : "ordered as"
    BUSINESS ||--o{ SALE : makes
    CUSTOMER ||--o{ SALE : buys
    SALE ||--o{ SALE_LINE : contains
    ITEM ||--o{ SALE_LINE : "sold as"
    BUSINESS ||--o{ SERVICE_COMPLAINT : tracks
    CUSTOMER ||--o{ SERVICE_COMPLAINT : raises
    EMPLOYEE ||--o{ SERVICE_COMPLAINT : "assigned to"

    ITEM {
        uuid id PK
        uuid business_id FK
        string item_code
        string item_name
        string uom
        string hsn_code
        numeric rate
        numeric tax_percentage
        numeric stock_quantity
    }
    SUPPLIER {
        uuid id PK
        uuid business_id FK
        string name
        string mobile
        string gst_number
        string address
    }
    PURCHASE_ORDER {
        uuid id PK
        uuid business_id FK
        string po_number
        uuid supplier_id FK
        date po_date
        numeric total_amount
        string status "draft|pending|approved|received|cancelled"
        uuid approved_by FK
        timestamptz approved_at
    }
    PURCHASE_ORDER_LINE {
        uuid id PK
        uuid purchase_order_id FK
        uuid item_id FK
        numeric quantity
        numeric rate
        numeric tax_percentage
        numeric line_total
    }
    SALE {
        uuid id PK
        uuid business_id FK
        string invoice_number
        uuid customer_id FK
        date sale_date
        numeric installation_charges
        numeric labour_charges
        numeric tax_amount
        numeric total_amount
        numeric paid_amount
        string status
    }
    SALE_LINE {
        uuid id PK
        uuid sale_id FK
        uuid item_id FK
        numeric quantity
        numeric rate
        numeric tax_percentage
        numeric line_total
    }
    SERVICE_COMPLAINT {
        uuid id PK
        uuid business_id FK
        string complaint_number
        uuid customer_id FK
        string issue_description
        uuid assigned_employee_id FK
        string status "open|in_progress|closed"
        timestamptz closed_at
    }
```

## 5. ER Diagram — Business 3: Farm Management

```mermaid
erDiagram
    BUSINESS ||--o{ FARM_BATCH : runs
    FARM_BATCH ||--o{ FEED_ENTRY : consumes
    FEED ||--o{ FEED_ENTRY : "used in"
    FARM_BATCH ||--o{ MEDICAL_RECORD : treats
    FARM_BATCH ||--o{ BATCH_EXPENSE : incurs
    FARM_BATCH ||--o{ BATCH_SALE : "sold via"
    BUSINESS ||--|| WALLET : "has"
    WALLET ||--o{ WALLET_TRANSACTION : logs

    FARM_BATCH {
        uuid id PK
        uuid business_id FK
        string batch_number
        string batch_name
        string animal_type "goat|hen|cow"
        date start_date
        int quantity_purchased
        numeric purchase_amount
        string status "active|sold|closed"
    }
    FEED {
        uuid id PK
        uuid business_id FK
        string feed_name
        string feed_type
        string uom
        numeric rate
    }
    FEED_ENTRY {
        uuid id PK
        uuid business_id FK
        uuid batch_id FK
        uuid feed_id FK
        date entry_date
        numeric quantity
        numeric rate
        numeric amount "generated"
    }
    MEDICAL_RECORD {
        uuid id PK
        uuid business_id FK
        uuid batch_id FK
        string medicine_name
        numeric amount
        numeric doctor_charges
        date record_date
    }
    BATCH_EXPENSE {
        uuid id PK
        uuid business_id FK
        uuid batch_id FK
        string expense_kind "animal|feed|medical|labour"
        numeric amount
        date expense_date
        string description
    }
    BATCH_SALE {
        uuid id PK
        uuid business_id FK
        uuid batch_id FK
        date sale_date
        int sale_quantity
        numeric total_weight
        numeric sale_amount
    }
    WALLET {
        uuid id PK
        uuid business_id FK
        numeric balance
    }
    WALLET_TRANSACTION {
        uuid id PK
        uuid wallet_id FK
        date txn_date
        string direction "credit|debit"
        numeric amount
        string reason
        string ref_type
        uuid ref_id
    }
```

**Batch P&L** = `Σ batch_sale.sale_amount − (purchase_amount + Σ feed + Σ medical + Σ labour)`.
Computed by a query/view (`v_farm_batch_pnl`), not stored, because expenses accrue over time.

## 6. ER Diagram — Business 4: Coconut Business

```mermaid
erDiagram
    BUSINESS ||--o{ PRODUCT : defines
    BUSINESS ||--o{ COCONUT_BATCH : purchases
    PRODUCT ||--o{ COCONUT_BATCH : "of product"
    COCONUT_BATCH ||--o{ LABOUR_CHARGE : incurs
    COCONUT_BATCH ||--o{ TRANSPORT_CHARGE : incurs
    COCONUT_BATCH ||--o{ COCONUT_BATCH_SALE : "sold via"

    PRODUCT {
        uuid id PK
        uuid business_id FK
        string name
        string category
        string uom
    }
    COCONUT_BATCH {
        uuid id PK
        uuid business_id FK
        uuid product_id FK
        string batch_number
        date purchase_date
        numeric quantity
        numeric purchase_amount
        string status "active|sold|closed"
    }
    LABOUR_CHARGE {
        uuid id PK
        uuid business_id FK
        uuid batch_id FK
        string labour_name
        numeric amount
        date charge_date
    }
    TRANSPORT_CHARGE {
        uuid id PK
        uuid business_id FK
        uuid batch_id FK
        string vehicle
        numeric amount
        date charge_date
    }
    COCONUT_BATCH_SALE {
        uuid id PK
        uuid business_id FK
        uuid batch_id FK
        date sale_date
        numeric sale_quantity
        numeric sale_value
    }
```

**Coconut Profit** = `Σ sale_value − (purchase_amount + Σ labour + Σ transport)`.
View: `v_coconut_batch_pnl`.

## 7. ER Diagram — Accounting & Audit (cross-business)

```mermaid
erDiagram
    BUSINESS ||--o{ ACCOUNT : has
    BUSINESS ||--o{ JOURNAL_TRANSACTION : posts
    JOURNAL_TRANSACTION ||--o{ LEDGER_ENTRY : "splits into"
    ACCOUNT ||--o{ LEDGER_ENTRY : "posted to"
    USER ||--o{ AUDIT_LOG : performs

    ACCOUNT {
        uuid id PK
        uuid business_id FK
        string code
        string name
        string type "asset|liability|income|expense|equity"
    }
    JOURNAL_TRANSACTION {
        uuid id PK
        uuid business_id FK
        date txn_date
        string source_module "load|sale|expense|collection|feed|..."
        uuid source_id
        string narration
    }
    LEDGER_ENTRY {
        uuid id PK
        uuid business_id FK
        uuid journal_transaction_id FK
        uuid account_id FK
        numeric debit
        numeric credit
    }
    AUDIT_LOG {
        uuid id PK
        uuid business_id
        uuid user_id FK
        string entity
        uuid entity_id
        string action "create|update|delete"
        jsonb old_values
        jsonb new_values
        timestamptz created_at
    }
```

Each financial event (a load, a sale, an expense, a collection, a feed purchase) **also** posts a
balanced `journal_transaction` with two-or-more `ledger_entry` rows. This is what powers the
Cash Book, Ledger, and P&L reports uniformly across all four verticals.

## 8. Data dictionary — selected non-obvious fields

| Table.Column | Type | Rule / meaning |
|--------------|------|----------------|
| `business.business_type_id` | uuid FK | Immutable after first transaction; drives module visibility |
| `load.profit` | numeric generated | `load_amount − sum(all expenses)`; cannot be written directly |
| `credit.balance_amount` | numeric generated | `load_amount − paid_amount`; check `paid_amount ≤ load_amount` |
| `item.stock_quantity` | numeric | Maintained by triggers/handlers on PO-receive (+) and Sale (−) |
| `purchase_order.status` | enum text | State machine: draft→pending→approved→received / cancelled |
| `service_complaint.status` | enum text | open→in_progress→closed; `closed_at` set on close |
| `wallet.balance` | numeric | = `Σ credit − Σ debit` of wallet_transactions (reconciled) |
| `attendance.status` | enum text | present / absent / half / leave; one row per employee per day (unique) |
| `customer_ledger.running_balance` | numeric | Maintained per customer in date order |
| `audit_log.old/new_values` | jsonb | Snapshot diff for compliance |

## 9. Indexing strategy (summary)

- Every business table: index on `business_id`, and composite `(business_id, <date>)` for the
  date-range reports that dominate the workload.
- Unique constraints scoped by tenant: `unique(business_id, vehicle_number)`,
  `unique(business_id, item_code)`, `unique(business_id, batch_number)`,
  `unique(business_id, invoice_number)`, `unique(employee_id, attendance_date)`.
- Foreign-key columns indexed (PostgreSQL does not auto-index FKs).
- Partial index `WHERE is_deleted = false` on hot tables.
- `audit_log`: index `(entity, entity_id)` and `(business_id, created_at)`.

Full constraints and indexes are in [05-database-scripts.sql](05-database-scripts.sql).

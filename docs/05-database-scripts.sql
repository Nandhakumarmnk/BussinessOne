-- =============================================================================
-- Multi-Business ERP — PostgreSQL 15 schema
-- Conventions: snake_case, uuid PKs, numeric(14,2) money, timestamptz (UTC),
-- soft delete + audit on every business table, business_id tenant discriminator.
-- Run order: extensions -> enums -> identity/tenancy -> common -> verticals ->
--            accounting/audit -> indexes -> seed.
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";   -- gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS "citext";     -- case-insensitive email/codes

-- ---------------------------------------------------------------------------
-- Reusable enums (kept as text + CHECK in app tables for portability/flexibility;
-- native enums used where the set is stable)
-- ---------------------------------------------------------------------------
DO $$ BEGIN
    CREATE TYPE business_type_code AS ENUM ('TRANSPORT','CCTV','FARM','COCONUT');
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

-- =============================================================================
-- 1. IDENTITY & TENANCY
-- =============================================================================

CREATE TABLE tenants (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(150) NOT NULL,
    timezone        varchar(64)  NOT NULL DEFAULT 'Asia/Kolkata',
    owner_user_id   uuid,                                   -- FK added after users
    is_active       boolean      NOT NULL DEFAULT true,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid,
    updated_at      timestamptz,
    updated_by      uuid,
    is_deleted      boolean      NOT NULL DEFAULT false,
    deleted_at      timestamptz
);

CREATE TABLE business_types (
    id      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code    business_type_code NOT NULL UNIQUE,
    name    varchar(80) NOT NULL,
    is_active boolean NOT NULL DEFAULT true
);

CREATE TABLE businesses (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         uuid NOT NULL REFERENCES tenants(id),
    business_type_id  uuid NOT NULL REFERENCES business_types(id),
    name              varchar(150) NOT NULL,
    gst_number        varchar(20),
    address           varchar(300),
    currency          varchar(3) NOT NULL DEFAULT 'INR',
    is_active         boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (tenant_id, name)
);

CREATE TABLE users (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid REFERENCES tenants(id),            -- null for Super Admin
    full_name       varchar(150) NOT NULL,
    mobile          varchar(20)  NOT NULL,
    email           citext,
    password_hash   text NOT NULL,
    is_super_admin  boolean NOT NULL DEFAULT false,
    is_active       boolean NOT NULL DEFAULT true,
    last_login_at   timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (tenant_id, mobile)
);

ALTER TABLE tenants
    ADD CONSTRAINT fk_tenants_owner FOREIGN KEY (owner_user_id) REFERENCES users(id);

CREATE TABLE roles (
    id        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code      varchar(40) NOT NULL UNIQUE,                  -- SUPER_ADMIN, OWNER, MANAGER...
    name      varchar(80) NOT NULL,
    is_system boolean NOT NULL DEFAULT true
);

CREATE TABLE permissions (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code        varchar(80) NOT NULL UNIQUE,                -- e.g. transport.load.create
    description varchar(200)
);

CREATE TABLE role_permissions (
    role_id       uuid NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id uuid NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

-- A user's membership in a business with a specific role (RBAC is per-business)
CREATE TABLE user_businesses (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    business_id uuid NOT NULL REFERENCES businesses(id),
    role_id     uuid NOT NULL REFERENCES roles(id),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (user_id, business_id)
);

CREATE TABLE refresh_tokens (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  text NOT NULL,
    device_info varchar(200),
    expires_at  timestamptz NOT NULL,
    revoked_at  timestamptz,
    created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE password_reset_tokens (
    id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash text NOT NULL,
    expires_at timestamptz NOT NULL,
    used_at    timestamptz,
    created_at timestamptz NOT NULL DEFAULT now()
);

-- =============================================================================
-- 2. COMMON MODULES (Employees, Expenses, Customers)
-- =============================================================================

CREATE TABLE employees (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id  uuid NOT NULL REFERENCES businesses(id),
    user_id      uuid REFERENCES users(id),                 -- optional app login
    name         varchar(150) NOT NULL,
    mobile       varchar(20),
    address      varchar(300),
    joining_date date,
    salary       numeric(14,2) NOT NULL DEFAULT 0 CHECK (salary >= 0),
    role_id      uuid REFERENCES roles(id),
    status       varchar(20) NOT NULL DEFAULT 'active'
                 CHECK (status IN ('active','inactive','terminated')),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE salary_history (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id  uuid NOT NULL REFERENCES businesses(id),
    employee_id  uuid NOT NULL REFERENCES employees(id),
    period_month date NOT NULL,                             -- first day of month
    amount       numeric(14,2) NOT NULL CHECK (amount >= 0),
    paid_amount  numeric(14,2) NOT NULL DEFAULT 0 CHECK (paid_amount >= 0),
    paid_on      date,
    note         varchar(200),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (employee_id, period_month)
);

CREATE TABLE attendance (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id     uuid NOT NULL REFERENCES businesses(id),
    employee_id     uuid NOT NULL REFERENCES employees(id),
    attendance_date date NOT NULL,
    status          varchar(10) NOT NULL DEFAULT 'present'
                    CHECK (status IN ('present','absent','half','leave')),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (employee_id, attendance_date)
);

CREATE TABLE expense_types (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    name        varchar(80) NOT NULL,
    is_active   boolean NOT NULL DEFAULT true,
    UNIQUE (business_id, name)
);

CREATE TABLE expenses (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id     uuid NOT NULL REFERENCES businesses(id),
    expense_type_id uuid REFERENCES expense_types(id),
    expense_date    date NOT NULL,
    amount          numeric(14,2) NOT NULL CHECK (amount >= 0),
    description     varchar(300),
    attachment_key  varchar(300),                           -- Cloud Storage object key
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE customers (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id  uuid NOT NULL REFERENCES businesses(id),
    name         varchar(150) NOT NULL,
    mobile       varchar(20),
    address      varchar(300),
    gst_number   varchar(20),
    credit_limit numeric(14,2) NOT NULL DEFAULT 0 CHECK (credit_limit >= 0),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE customer_ledger (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id     uuid NOT NULL REFERENCES businesses(id),
    customer_id     uuid NOT NULL REFERENCES customers(id),
    entry_date      date NOT NULL,
    ref_type        varchar(20) NOT NULL CHECK (ref_type IN ('opening','load','sale','collection','adjustment')),
    ref_id          uuid,
    debit           numeric(14,2) NOT NULL DEFAULT 0 CHECK (debit >= 0),
    credit          numeric(14,2) NOT NULL DEFAULT 0 CHECK (credit >= 0),
    running_balance numeric(14,2) NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid
);

CREATE TABLE collections (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id     uuid NOT NULL REFERENCES businesses(id),
    customer_id     uuid NOT NULL REFERENCES customers(id),
    collection_date date NOT NULL,
    amount          numeric(14,2) NOT NULL CHECK (amount > 0),
    mode            varchar(10) NOT NULL DEFAULT 'cash'
                    CHECK (mode IN ('cash','upi','bank','cheque')),
    reference       varchar(100),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

-- =============================================================================
-- 3. BUSINESS 1 — GOODS TRANSPORT
-- =============================================================================

CREATE TABLE vehicles (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id        uuid NOT NULL REFERENCES businesses(id),
    vehicle_number     varchar(20) NOT NULL,
    vehicle_type       varchar(40),
    model              varchar(80),
    fuel_type          varchar(20),
    rc_details         varchar(200),
    insurance_details  varchar(200),
    insurance_expiry   date,
    is_active          boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (business_id, vehicle_number)
);

CREATE TABLE drivers (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    name        varchar(150) NOT NULL,
    mobile      varchar(20),
    driver_type varchar(10) NOT NULL DEFAULT 'salaried'
                CHECK (driver_type IN ('self','salaried')),
    salary      numeric(14,2) NOT NULL DEFAULT 0 CHECK (salary >= 0),
    is_active   boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE loads (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id         uuid NOT NULL REFERENCES businesses(id),
    load_number         varchar(30) NOT NULL,
    load_name           varchar(120),
    customer_id         uuid REFERENCES customers(id),
    vehicle_id          uuid REFERENCES vehicles(id),
    driver_id           uuid REFERENCES drivers(id),
    source              varchar(120),
    destination         varchar(120),
    load_amount         numeric(14,2) NOT NULL DEFAULT 0 CHECK (load_amount >= 0),
    loadman_charges     numeric(14,2) NOT NULL DEFAULT 0 CHECK (loadman_charges >= 0),
    fuel_expense        numeric(14,2) NOT NULL DEFAULT 0 CHECK (fuel_expense >= 0),
    maintenance_expense numeric(14,2) NOT NULL DEFAULT 0 CHECK (maintenance_expense >= 0),
    driver_charges      numeric(14,2) NOT NULL DEFAULT 0 CHECK (driver_charges >= 0),
    other_expense       numeric(14,2) NOT NULL DEFAULT 0 CHECK (other_expense >= 0),
    -- Load Profit = amount - sum(expenses). Generated => always consistent.
    profit numeric(14,2) GENERATED ALWAYS AS
        (load_amount - (loadman_charges + fuel_expense + maintenance_expense
                        + driver_charges + other_expense)) STORED,
    load_date           date NOT NULL,
    status              varchar(15) NOT NULL DEFAULT 'completed'
                        CHECK (status IN ('planned','in_transit','completed','cancelled')),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (business_id, load_number)
);

CREATE TABLE load_credits (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id    uuid NOT NULL REFERENCES businesses(id),
    load_id        uuid NOT NULL REFERENCES loads(id),
    customer_id    uuid NOT NULL REFERENCES customers(id),
    load_amount    numeric(14,2) NOT NULL CHECK (load_amount >= 0),
    paid_amount    numeric(14,2) NOT NULL DEFAULT 0 CHECK (paid_amount >= 0),
    balance_amount numeric(14,2) GENERATED ALWAYS AS (load_amount - paid_amount) STORED,
    status         varchar(10) NOT NULL DEFAULT 'open'
                   CHECK (status IN ('open','partial','settled')),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    CONSTRAINT chk_credit_paid_le_amount CHECK (paid_amount <= load_amount),
    UNIQUE (load_id)
);

-- =============================================================================
-- 4. BUSINESS 2 — ELECTRONICS & CCTV
-- =============================================================================

CREATE TABLE items (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id     uuid NOT NULL REFERENCES businesses(id),
    item_code       varchar(40) NOT NULL,
    item_name       varchar(150) NOT NULL,
    uom             varchar(20) NOT NULL DEFAULT 'nos',
    hsn_code        varchar(20),
    rate            numeric(14,2) NOT NULL DEFAULT 0 CHECK (rate >= 0),
    tax_percentage  numeric(5,2)  NOT NULL DEFAULT 0 CHECK (tax_percentage >= 0),
    stock_quantity  numeric(14,2) NOT NULL DEFAULT 0,
    reorder_level   numeric(14,2) NOT NULL DEFAULT 0,
    is_active       boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (business_id, item_code)
);

CREATE TABLE suppliers (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    name        varchar(150) NOT NULL,
    mobile      varchar(20),
    gst_number  varchar(20),
    address     varchar(300),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE purchase_orders (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id  uuid NOT NULL REFERENCES businesses(id),
    po_number    varchar(30) NOT NULL,
    supplier_id  uuid NOT NULL REFERENCES suppliers(id),
    po_date      date NOT NULL,
    total_amount numeric(14,2) NOT NULL DEFAULT 0,
    status       varchar(12) NOT NULL DEFAULT 'draft'
                 CHECK (status IN ('draft','pending','approved','received','cancelled')),
    approved_by  uuid REFERENCES users(id),
    approved_at  timestamptz,
    note         varchar(300),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (business_id, po_number)
);

CREATE TABLE purchase_order_lines (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    purchase_order_id uuid NOT NULL REFERENCES purchase_orders(id) ON DELETE CASCADE,
    item_id           uuid NOT NULL REFERENCES items(id),
    quantity          numeric(14,2) NOT NULL CHECK (quantity > 0),
    rate              numeric(14,2) NOT NULL CHECK (rate >= 0),
    tax_percentage    numeric(5,2)  NOT NULL DEFAULT 0,
    line_total numeric(14,2) GENERATED ALWAYS AS
        (round(quantity * rate * (1 + tax_percentage/100), 2)) STORED
);

CREATE TABLE sales (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id         uuid NOT NULL REFERENCES businesses(id),
    invoice_number      varchar(30) NOT NULL,
    customer_id         uuid REFERENCES customers(id),
    sale_date           date NOT NULL,
    installation_charges numeric(14,2) NOT NULL DEFAULT 0 CHECK (installation_charges >= 0),
    labour_charges       numeric(14,2) NOT NULL DEFAULT 0 CHECK (labour_charges >= 0),
    sub_total            numeric(14,2) NOT NULL DEFAULT 0,
    tax_amount           numeric(14,2) NOT NULL DEFAULT 0,
    total_amount         numeric(14,2) NOT NULL DEFAULT 0,
    paid_amount          numeric(14,2) NOT NULL DEFAULT 0 CHECK (paid_amount >= 0),
    status               varchar(12) NOT NULL DEFAULT 'completed'
                         CHECK (status IN ('draft','completed','cancelled')),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (business_id, invoice_number)
);

CREATE TABLE sale_lines (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    sale_id        uuid NOT NULL REFERENCES sales(id) ON DELETE CASCADE,
    item_id        uuid NOT NULL REFERENCES items(id),
    quantity       numeric(14,2) NOT NULL CHECK (quantity > 0),
    rate           numeric(14,2) NOT NULL CHECK (rate >= 0),
    tax_percentage numeric(5,2)  NOT NULL DEFAULT 0,
    line_total numeric(14,2) GENERATED ALWAYS AS
        (round(quantity * rate * (1 + tax_percentage/100), 2)) STORED
);

CREATE TABLE service_complaints (
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id          uuid NOT NULL REFERENCES businesses(id),
    complaint_number     varchar(30) NOT NULL,
    customer_id          uuid NOT NULL REFERENCES customers(id),
    issue_description    varchar(500),
    assigned_employee_id uuid REFERENCES employees(id),
    status               varchar(12) NOT NULL DEFAULT 'open'
                         CHECK (status IN ('open','in_progress','closed')),
    opened_at  timestamptz NOT NULL DEFAULT now(),
    closed_at  timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (business_id, complaint_number)
);

-- =============================================================================
-- 5. BUSINESS 3 — FARM MANAGEMENT
-- =============================================================================

CREATE TABLE farm_batches (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id        uuid NOT NULL REFERENCES businesses(id),
    batch_number       varchar(30) NOT NULL,
    batch_name         varchar(120),
    animal_type        varchar(10) NOT NULL CHECK (animal_type IN ('goat','hen','cow')),
    start_date         date NOT NULL,
    quantity_purchased integer NOT NULL DEFAULT 0 CHECK (quantity_purchased >= 0),
    purchase_amount    numeric(14,2) NOT NULL DEFAULT 0 CHECK (purchase_amount >= 0),
    status             varchar(10) NOT NULL DEFAULT 'active'
                       CHECK (status IN ('active','sold','closed')),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (business_id, batch_number)
);

CREATE TABLE feeds (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    feed_name   varchar(120) NOT NULL,
    feed_type   varchar(60),
    uom         varchar(20) NOT NULL DEFAULT 'kg',
    rate        numeric(14,2) NOT NULL DEFAULT 0 CHECK (rate >= 0),
    is_active   boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE feed_entries (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    batch_id    uuid NOT NULL REFERENCES farm_batches(id),
    feed_id     uuid NOT NULL REFERENCES feeds(id),
    entry_date  date NOT NULL,
    quantity    numeric(14,2) NOT NULL CHECK (quantity > 0),
    rate        numeric(14,2) NOT NULL CHECK (rate >= 0),
    amount numeric(14,2) GENERATED ALWAYS AS (round(quantity * rate, 2)) STORED,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE medical_records (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id    uuid NOT NULL REFERENCES businesses(id),
    batch_id       uuid NOT NULL REFERENCES farm_batches(id),
    medicine_name  varchar(120) NOT NULL,
    amount         numeric(14,2) NOT NULL DEFAULT 0 CHECK (amount >= 0),
    doctor_charges numeric(14,2) NOT NULL DEFAULT 0 CHECK (doctor_charges >= 0),
    record_date    date NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE batch_expenses (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id  uuid NOT NULL REFERENCES businesses(id),
    batch_id     uuid NOT NULL REFERENCES farm_batches(id),
    expense_kind varchar(10) NOT NULL CHECK (expense_kind IN ('animal','feed','medical','labour','other')),
    amount       numeric(14,2) NOT NULL CHECK (amount >= 0),
    expense_date date NOT NULL,
    description  varchar(300),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE batch_sales (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id   uuid NOT NULL REFERENCES businesses(id),
    batch_id      uuid NOT NULL REFERENCES farm_batches(id),
    sale_date     date NOT NULL,
    sale_quantity integer NOT NULL CHECK (sale_quantity > 0),
    total_weight  numeric(14,2),
    sale_amount   numeric(14,2) NOT NULL CHECK (sale_amount >= 0),
    customer_id   uuid REFERENCES customers(id),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE wallets (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    balance     numeric(14,2) NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    UNIQUE (business_id)
);

CREATE TABLE wallet_transactions (
    id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    wallet_id  uuid NOT NULL REFERENCES wallets(id),
    business_id uuid NOT NULL REFERENCES businesses(id),
    txn_date   date NOT NULL,
    direction  varchar(6) NOT NULL CHECK (direction IN ('credit','debit')),
    amount     numeric(14,2) NOT NULL CHECK (amount > 0),
    reason     varchar(200),
    ref_type   varchar(30),
    ref_id     uuid,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid
);

-- =============================================================================
-- 6. BUSINESS 4 — COCONUT BUSINESS
-- =============================================================================

CREATE TABLE products (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    name        varchar(120) NOT NULL,           -- Coconut, Copra, Coconut Powder, Coconut Oil
    category    varchar(60),
    uom         varchar(20) NOT NULL DEFAULT 'kg',
    is_active   boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (business_id, name)
);

CREATE TABLE coconut_batches (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id     uuid NOT NULL REFERENCES businesses(id),
    product_id      uuid NOT NULL REFERENCES products(id),
    batch_number    varchar(30) NOT NULL,
    purchase_date   date NOT NULL,
    quantity        numeric(14,2) NOT NULL CHECK (quantity >= 0),
    purchase_amount numeric(14,2) NOT NULL DEFAULT 0 CHECK (purchase_amount >= 0),
    status          varchar(10) NOT NULL DEFAULT 'active'
                    CHECK (status IN ('active','sold','closed')),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    updated_at timestamptz, updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz,
    UNIQUE (business_id, batch_number)
);

CREATE TABLE coconut_labour_charges (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    batch_id    uuid NOT NULL REFERENCES coconut_batches(id),
    labour_name varchar(120),
    amount      numeric(14,2) NOT NULL CHECK (amount >= 0),
    charge_date date NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE coconut_transport_charges (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    batch_id    uuid NOT NULL REFERENCES coconut_batches(id),
    vehicle     varchar(60),
    amount      numeric(14,2) NOT NULL CHECK (amount >= 0),
    charge_date date NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

CREATE TABLE coconut_batch_sales (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id   uuid NOT NULL REFERENCES businesses(id),
    batch_id      uuid NOT NULL REFERENCES coconut_batches(id),
    sale_date     date NOT NULL,
    sale_quantity numeric(14,2) NOT NULL CHECK (sale_quantity > 0),
    sale_value    numeric(14,2) NOT NULL CHECK (sale_value >= 0),
    customer_id   uuid REFERENCES customers(id),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz
);

-- =============================================================================
-- 7. ACCOUNTING & AUDIT (cross-business)
-- =============================================================================

CREATE TABLE accounts (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    code        varchar(20) NOT NULL,
    name        varchar(120) NOT NULL,
    type        varchar(12) NOT NULL CHECK (type IN ('asset','liability','income','expense','equity')),
    is_active   boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid,
    UNIQUE (business_id, code)
);

CREATE TABLE journal_transactions (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id   uuid NOT NULL REFERENCES businesses(id),
    txn_date      date NOT NULL,
    source_module varchar(30) NOT NULL,   -- load|sale|expense|collection|feed|medical|...
    source_id     uuid,
    narration     varchar(300),
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid
);

CREATE TABLE ledger_entries (
    id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id            uuid NOT NULL REFERENCES businesses(id),
    journal_transaction_id uuid NOT NULL REFERENCES journal_transactions(id) ON DELETE CASCADE,
    account_id             uuid NOT NULL REFERENCES accounts(id),
    debit                  numeric(14,2) NOT NULL DEFAULT 0 CHECK (debit >= 0),
    credit                 numeric(14,2) NOT NULL DEFAULT 0 CHECK (credit >= 0),
    CONSTRAINT chk_debit_xor_credit CHECK ( (debit = 0) <> (credit = 0) )
);

CREATE TABLE audit_logs (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid,
    user_id     uuid REFERENCES users(id),
    entity      varchar(80) NOT NULL,
    entity_id   uuid,
    action      varchar(10) NOT NULL CHECK (action IN ('create','update','delete','login')),
    old_values  jsonb,
    new_values  jsonb,
    ip_address  varchar(45),
    created_at  timestamptz NOT NULL DEFAULT now()
);

-- Attachments registry (generic, any module)
CREATE TABLE attachments (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id uuid NOT NULL REFERENCES businesses(id),
    entity      varchar(80) NOT NULL,
    entity_id   uuid NOT NULL,
    object_key  varchar(300) NOT NULL,   -- Cloud Storage key
    file_name   varchar(200),
    content_type varchar(100),
    size_bytes  bigint,
    created_at timestamptz NOT NULL DEFAULT now(), created_by uuid
);

-- Outbox for mobile offline sync idempotency (server-side dedupe)
CREATE TABLE sync_client_requests (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_id  uuid NOT NULL REFERENCES businesses(id),
    client_uuid  uuid NOT NULL,            -- generated on device
    entity       varchar(80) NOT NULL,
    server_id    uuid,                      -- resolved id after apply
    applied_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (business_id, client_uuid)
);

-- =============================================================================
-- 8. INDEXES
-- =============================================================================
CREATE INDEX ix_businesses_tenant            ON businesses(tenant_id) WHERE is_deleted = false;
CREATE INDEX ix_user_businesses_user         ON user_businesses(user_id);
CREATE INDEX ix_user_businesses_business     ON user_businesses(business_id);
CREATE INDEX ix_refresh_tokens_user          ON refresh_tokens(user_id);

CREATE INDEX ix_employees_business           ON employees(business_id) WHERE is_deleted = false;
CREATE INDEX ix_salary_history_emp           ON salary_history(employee_id, period_month);
CREATE INDEX ix_attendance_emp_date          ON attendance(employee_id, attendance_date);
CREATE INDEX ix_expenses_business_date       ON expenses(business_id, expense_date) WHERE is_deleted = false;
CREATE INDEX ix_customers_business           ON customers(business_id) WHERE is_deleted = false;
CREATE INDEX ix_customer_ledger_cust         ON customer_ledger(customer_id, entry_date);
CREATE INDEX ix_collections_business_date    ON collections(business_id, collection_date);

CREATE INDEX ix_vehicles_business            ON vehicles(business_id) WHERE is_deleted = false;
CREATE INDEX ix_drivers_business             ON drivers(business_id) WHERE is_deleted = false;
CREATE INDEX ix_loads_business_date          ON loads(business_id, load_date) WHERE is_deleted = false;
CREATE INDEX ix_loads_vehicle                ON loads(vehicle_id);
CREATE INDEX ix_loads_driver                 ON loads(driver_id);
CREATE INDEX ix_load_credits_business        ON load_credits(business_id, status);

CREATE INDEX ix_items_business               ON items(business_id) WHERE is_deleted = false;
CREATE INDEX ix_po_business_status           ON purchase_orders(business_id, status);
CREATE INDEX ix_po_lines_po                  ON purchase_order_lines(purchase_order_id);
CREATE INDEX ix_sales_business_date          ON sales(business_id, sale_date) WHERE is_deleted = false;
CREATE INDEX ix_sale_lines_sale              ON sale_lines(sale_id);
CREATE INDEX ix_service_business_status      ON service_complaints(business_id, status);

CREATE INDEX ix_farm_batches_business        ON farm_batches(business_id, status);
CREATE INDEX ix_feed_entries_batch           ON feed_entries(batch_id, entry_date);
CREATE INDEX ix_medical_batch                ON medical_records(batch_id, record_date);
CREATE INDEX ix_batch_expenses_batch         ON batch_expenses(batch_id);
CREATE INDEX ix_batch_sales_batch            ON batch_sales(batch_id, sale_date);
CREATE INDEX ix_wallet_txn_wallet            ON wallet_transactions(wallet_id, txn_date);

CREATE INDEX ix_coconut_batches_business     ON coconut_batches(business_id, status);
CREATE INDEX ix_coconut_labour_batch         ON coconut_labour_charges(batch_id);
CREATE INDEX ix_coconut_transport_batch      ON coconut_transport_charges(batch_id);
CREATE INDEX ix_coconut_sales_batch          ON coconut_batch_sales(batch_id, sale_date);

CREATE INDEX ix_ledger_business_account      ON ledger_entries(business_id, account_id);
CREATE INDEX ix_journal_business_date        ON journal_transactions(business_id, txn_date);
CREATE INDEX ix_audit_entity                 ON audit_logs(entity, entity_id);
CREATE INDEX ix_audit_business_date          ON audit_logs(business_id, created_at);
CREATE INDEX ix_attachments_entity           ON attachments(entity, entity_id);

-- =============================================================================
-- 9. VIEWS — derived P&L (cannot be generated columns; accrue over time)
-- =============================================================================
CREATE VIEW v_farm_batch_pnl AS
SELECT b.id AS batch_id, b.business_id, b.batch_number, b.batch_name,
       b.purchase_amount,
       COALESCE((SELECT sum(amount) FROM feed_entries fe WHERE fe.batch_id = b.id AND fe.is_deleted=false),0)        AS feed_cost,
       COALESCE((SELECT sum(amount + doctor_charges) FROM medical_records mr WHERE mr.batch_id=b.id AND mr.is_deleted=false),0) AS medical_cost,
       COALESCE((SELECT sum(amount) FROM batch_expenses be WHERE be.batch_id=b.id AND be.expense_kind='labour' AND be.is_deleted=false),0) AS labour_cost,
       COALESCE((SELECT sum(sale_amount) FROM batch_sales bs WHERE bs.batch_id=b.id AND bs.is_deleted=false),0)      AS total_sales,
       COALESCE((SELECT sum(sale_amount) FROM batch_sales bs WHERE bs.batch_id=b.id AND bs.is_deleted=false),0)
         - ( b.purchase_amount
             + COALESCE((SELECT sum(amount) FROM feed_entries fe WHERE fe.batch_id=b.id AND fe.is_deleted=false),0)
             + COALESCE((SELECT sum(amount + doctor_charges) FROM medical_records mr WHERE mr.batch_id=b.id AND mr.is_deleted=false),0)
             + COALESCE((SELECT sum(amount) FROM batch_expenses be WHERE be.batch_id=b.id AND be.expense_kind='labour' AND be.is_deleted=false),0)
           ) AS profit_loss
FROM farm_batches b WHERE b.is_deleted = false;

CREATE VIEW v_coconut_batch_pnl AS
SELECT b.id AS batch_id, b.business_id, b.batch_number, b.purchase_amount,
       COALESCE((SELECT sum(amount) FROM coconut_labour_charges l WHERE l.batch_id=b.id AND l.is_deleted=false),0)    AS labour_cost,
       COALESCE((SELECT sum(amount) FROM coconut_transport_charges t WHERE t.batch_id=b.id AND t.is_deleted=false),0) AS transport_cost,
       COALESCE((SELECT sum(sale_value) FROM coconut_batch_sales s WHERE s.batch_id=b.id AND s.is_deleted=false),0)   AS total_sales,
       COALESCE((SELECT sum(sale_value) FROM coconut_batch_sales s WHERE s.batch_id=b.id AND s.is_deleted=false),0)
         - ( b.purchase_amount
             + COALESCE((SELECT sum(amount) FROM coconut_labour_charges l WHERE l.batch_id=b.id AND l.is_deleted=false),0)
             + COALESCE((SELECT sum(amount) FROM coconut_transport_charges t WHERE t.batch_id=b.id AND t.is_deleted=false),0)
           ) AS profit
FROM coconut_batches b WHERE b.is_deleted = false;

-- =============================================================================
-- 10. SEED — roles, permissions, business types
-- =============================================================================
INSERT INTO business_types (code, name) VALUES
    ('TRANSPORT','Goods Transport'),
    ('CCTV','Electronics & CCTV'),
    ('FARM','Farm Management'),
    ('COCONUT','Coconut Business')
ON CONFLICT (code) DO NOTHING;

INSERT INTO roles (code, name, is_system) VALUES
    ('SUPER_ADMIN','Super Admin', true),
    ('OWNER','Business Owner', true),
    ('MANAGER','Manager', true),
    ('EMPLOYEE','Employee', true),
    ('DRIVER','Driver', true),
    ('LABOUR','Labour', true)
ON CONFLICT (code) DO NOTHING;

-- Permission codes are module.action; full matrix in docs/10-security-rbac.md
INSERT INTO permissions (code, description) VALUES
    ('dashboard.view','View dashboard'),
    ('employee.manage','Manage employees'),
    ('expense.manage','Manage expenses'),
    ('customer.manage','Manage customers'),
    ('transport.load.create','Create transport loads'),
    ('transport.load.view','View transport loads'),
    ('cctv.po.approve','Approve purchase orders'),
    ('farm.batch.manage','Manage farm batches'),
    ('coconut.batch.manage','Manage coconut batches'),
    ('accounting.view','View accounting'),
    ('report.generate','Generate reports'),
    ('platform.read.all','Super admin: read across all tenants')
ON CONFLICT (code) DO NOTHING;

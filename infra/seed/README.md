# ERP data seeder

Populates a running ERP instance with a realistic, cross-vertical dataset by driving the
**public HTTP API** (not raw SQL) — so every domain rule stays intact: customer ledgers,
double-entry GL postings, CCTV stock movement, the PO state machine, and farm/coconut
batch P&L all reconcile exactly as they would in normal use.

## What it creates

Logs in as the seeded owner and, across all four business verticals, seeds:

| Business (type) | Highlights |
|---|---|
| **Sri Transport** (TRANSPORT) | 3 vehicles, 2 drivers, 5 loads (billed to customers → credits), partial payments, 3 customers, 2 employees w/ salary+attendance, expenses |
| **Bright Vision Systems** (CCTV) | 2 suppliers, 5 items, a received PO (stock-in via draft→submit→approve→receive), 2 sales (stock-out + receivables), 2 service complaints, employees, expenses |
| **Green Valley Farm** (FARM) | feeds, goat + hen batches with feed/medical/expense entries, wallet float (add/use), a batch sale, customer, employee |
| **Kerala Coconut Traders** (COCONUT) | 4 products, 2 batches with labour + transport charges and sales |

Plus shared modules (customers, employees, expenses, collections) per business.

## Idempotent

Safe to run repeatedly. Master records are matched by their natural key (business name,
vehicle number, item code, invoice/PO/batch number, feed/product name) and only created
when missing; keyless child records (collections, salary, attendance, feed/medical entries,
wallet transactions, batch sales) are only added when the parent has none yet. A partial run
resumes cleanly.

## Run it

```bash
# Local dev API (default)
node infra/seed/seed.mjs
node infra/seed/verify.mjs      # prints credits/outstanding, CCTV stock, batch P&L

# Against another instance
ERP_BASE=http://localhost:5153 ERP_USER=owner@business-one.local ERP_PASS=Owner@123 node infra/seed/seed.mjs
```

### Config (env)

| Var | Default |
|---|---|
| `ERP_BASE` | `http://localhost:5153` |
| `ERP_USER` | `owner@business-one.local` |
| `ERP_PASS` | `Owner@123` |
| `NODE_EXTRA_CA_CERTS` | — set to a CA bundle when the target is HTTPS behind a TLS-intercepting proxy |

> **Seeding the live VM:** the API there is internal (published on `127.0.0.1:8080` behind
> Caddy). Prefer running this on the VM against `http://localhost:8080` (no TLS proxy in the
> way). Requires Node 20+. Only run against production intentionally — it writes real data.

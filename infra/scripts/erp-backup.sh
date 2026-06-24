#!/usr/bin/env bash
# Nightly PostgreSQL backup -> Cloud Storage (docs/12 §4). RPO 24h.
#
# Install on the VM:
#   sudo cp infra/scripts/erp-backup.sh /etc/cron.daily/erp-backup
#   sudo chmod +x /etc/cron.daily/erp-backup
#
# Prerequisites on the VM:
#   - gsutil (Google Cloud SDK) authenticated with write access to the bucket
#   - the bucket exists:   gsutil mb -l us-central1 gs://business-one-backups
#   - 30-day retention:    gsutil lifecycle set lifecycle.json gs://business-one-backups
#     (lifecycle.json = {"rule":[{"action":{"type":"Delete"},"condition":{"age":30}}]})
#
# Override defaults via env if your names differ (e.g. export ERP_BACKUP_BUCKET=...).

set -euo pipefail

BUCKET="${ERP_BACKUP_BUCKET:-gs://business-one-backups}"
DB_CONTAINER="${ERP_DB_CONTAINER:-erp-db}"
DB_NAME="${POSTGRES_DB:-erp}"
DB_USER="${POSTGRES_USER:-postgres}"
TS="$(date +%F-%H%M)"
FILE="erp-${TS}.sql.gz"
TMP="/tmp/${FILE}"

docker exec "$DB_CONTAINER" pg_dump -U "$DB_USER" -d "$DB_NAME" | gzip > "$TMP"
gsutil cp "$TMP" "${BUCKET}/${FILE}"
rm -f "$TMP"
echo "backup uploaded: ${BUCKET}/${FILE}"

# Restore drill (rehearse before go-live — docs/12 §4):
#   gsutil cp ${BUCKET}/erp-<TS>.sql.gz .
#   gunzip -c erp-<TS>.sql.gz | docker exec -i erp-db psql -U postgres -d erp_restore

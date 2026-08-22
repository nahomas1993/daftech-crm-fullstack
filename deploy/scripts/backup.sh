#!/usr/bin/env bash
# Backs up the PostgreSQL database and the uploaded-files volume, both
# running inside Docker Compose (see ../docker-compose.yml). Intended to
# run via cron on the host, e.g.:
#   0 2 * * * /path/to/backup.sh >> /var/log/daftech-backup.log 2>&1
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_DIR="$SCRIPT_DIR/.."
BACKUP_ROOT="${BACKUP_ROOT:-/var/backups/daftech-crm}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

mkdir -p "$BACKUP_ROOT"

if [ -f "$COMPOSE_DIR/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  source "$COMPOSE_DIR/.env"
  set +a
fi

if [ -z "${POSTGRES_PASSWORD:-}" ]; then
  echo "ERROR: POSTGRES_PASSWORD not set (expected in $COMPOSE_DIR/.env)." >&2
  exit 1
fi

echo "[$TIMESTAMP] Starting backup..."

# --- Database dump ---
DB_DUMP="$BACKUP_ROOT/daftech-crm-db-$TIMESTAMP.sql.gz"
docker compose -f "$COMPOSE_DIR/docker-compose.yml" exec -T postgres \
  env PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -U daftech -d daftech_crm --no-owner --clean \
  | gzip > "$DB_DUMP"
echo "Database dump written to $DB_DUMP"

# --- Uploaded files volume ---
# Tars the contents of the api-storage named volume via a throwaway
# container, rather than reaching into the host filesystem directly —
# works the same regardless of the Docker storage driver.
FILES_DUMP="$BACKUP_ROOT/daftech-crm-storage-$TIMESTAMP.tar.gz"
docker run --rm \
  -v daftech-crm_api-storage:/data:ro \
  -v "$BACKUP_ROOT":/backup \
  alpine \
  tar -czf "/backup/$(basename "$FILES_DUMP")" -C /data .
echo "Storage volume archive written to $FILES_DUMP"

# --- Retention: delete backups older than RETENTION_DAYS ---
find "$BACKUP_ROOT" -name 'daftech-crm-*.gz' -mtime "+$RETENTION_DAYS" -delete
echo "Pruned backups older than $RETENTION_DAYS days."

echo "[$TIMESTAMP] Backup complete."

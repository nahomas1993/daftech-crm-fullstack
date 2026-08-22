#!/usr/bin/env bash
# Restores a database dump and/or storage archive produced by backup.sh.
# Usage: ./restore.sh <db-dump.sql.gz> [storage-archive.tar.gz]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_DIR="$SCRIPT_DIR/.."

DB_DUMP="${1:?Usage: restore.sh <db-dump.sql.gz> [storage-archive.tar.gz]}"
STORAGE_ARCHIVE="${2:-}"

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

read -r -p "This will OVERWRITE the current daftech_crm database. Continue? [y/N] " confirm
if [ "$confirm" != "y" ] && [ "$confirm" != "Y" ]; then
  echo "Aborted."
  exit 0
fi

echo "Restoring database from $DB_DUMP..."
gunzip -c "$DB_DUMP" | docker compose -f "$COMPOSE_DIR/docker-compose.yml" exec -T postgres \
  env PGPASSWORD="$POSTGRES_PASSWORD" psql -U daftech -d daftech_crm
echo "Database restored."

if [ -n "$STORAGE_ARCHIVE" ]; then
  echo "Restoring storage volume from $STORAGE_ARCHIVE..."
  docker run --rm \
    -v daftech-crm_api-storage:/data \
    -v "$(dirname "$(realpath "$STORAGE_ARCHIVE")")":/backup \
    alpine \
    sh -c "rm -rf /data/* && tar -xzf /backup/$(basename "$STORAGE_ARCHIVE") -C /data"
  echo "Storage volume restored."
fi

echo "Restore complete. Restart the api container to pick up any storage changes: docker compose restart api"

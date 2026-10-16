#!/usr/bin/env bash
# Backs up the database: a pg_dump (custom format) from the db container into backups/ next to this script, keeping
# the newest $KEEP. Run it nightly from the server's cron (deploy/README.md):
#   15 3 * * * /opt/apptemplate/backup.sh >> /opt/apptemplate/backups/backup.log 2>&1
# A dump is written under a temporary name and renamed only once complete, so a failed run never leaves a file that
# looks like a backup. A copy on the same disk goes down with the server: copy backups/ off it as well.
set -euo pipefail
# The dump holds every account's e-mail and password hash: the folder and the files for this user only.
umask 077

dir="$(cd "$(dirname "$0")" && pwd)"
backups="${BACKUP_DIR:-$dir/backups}"
env_file="${ENV_FILE:-$dir/.env}"
keep="${KEEP:-14}"
target="$backups/apptemplate-$(date -u +%Y%m%d-%H%M%S).dump"
partial="$target.partial"

mkdir -p "$backups"
trap 'rm -f "$partial"' EXIT

docker compose -f "$dir/compose.yml" --env-file "$env_file" exec -T db \
  pg_dump --username=apptemplate --dbname=apptemplate --format=custom > "$partial"
mv "$partial" "$target"
echo "$(date -u +%FT%TZ) backed up $(du -h "$target" | cut -f1) to $target"

# The newest $keep stay; older ones go.
ls -1t "$backups"/apptemplate-*.dump | tail -n +"$((keep + 1))" | xargs -r rm --

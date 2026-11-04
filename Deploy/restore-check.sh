#!/usr/bin/env bash
# Restores into a disposable database; never overwrites the pilot database.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 && $# == 1 ]] || { echo 'Usage: sudo restore-check.sh /var/backups/pointpass/pointpass-TIMESTAMP.dump' >&2; exit 1; }
backup=$(realpath -- "$1")
[[ $backup == /var/backups/pointpass/pointpass-*.dump && -f $backup ]] || { echo 'Expected a private PointPass dump.' >&2; exit 1; }
database="pointpass_restore_$(date -u +%Y%m%d%H%M%S)_$$"
cleanup() { runuser -u postgres -- dropdb --if-exists "$database"; }
trap cleanup EXIT
runuser -u postgres -- createdb --owner=pointpassowner "$database"
# Read protected dump as root; child postgres receives it on standard input.
runuser -u postgres -- pg_restore --exit-on-error --no-owner --no-privileges --role=pointpassowner --dbname="$database" < "$backup"
runuser -u postgres -- psql -Xv ON_ERROR_STOP=1 "$database" -c 'SELECT count(*) AS restored_players FROM players; SELECT count(*) AS restored_verified_results FROM matches WHERE blue_score IS NOT NULL AND red_score IS NOT NULL;'
echo 'Disposable restore succeeded. Pilot database was unchanged; restored database is now removed.'

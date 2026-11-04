#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run with sudo.' >&2; exit 1; }
exec 9>/run/lock/pointpass-backup.lock
flock -n 9 || { echo 'Another backup is running.' >&2; exit 1; }
directory=/var/backups/pointpass
install -d -m 0700 "$directory"
free_kib=$(df --output=avail "$directory" | tail -n 1 | tr -d ' ')
(( free_kib >= 3145728 )) || { logger -p daemon.err -t pointpass-backup 'Backup skipped: less than 3GiB free.'; exit 1; }
stamp=$(date -u +%Y%m%dT%H%M%S%NZ)
temporary="$directory/pointpass-$stamp.dump.partial"
final="$directory/pointpass-$stamp.dump"
trap '[[ ! -e $temporary ]] || unlink "$temporary"' EXIT
# Kernel file-size limit prevents an unexpectedly large database filling disk.
prlimit --fsize=1073741824:1073741824 -- runuser -u postgres -- pg_dump -Fc -Z6 --no-owner --no-privileges pointpass > "$temporary"
pg_restore --list "$temporary" >/dev/null
chmod 0600 "$temporary"
mv -- "$temporary" "$final"
mapfile -t backups < <(find "$directory" -maxdepth 1 -type f -name 'pointpass-*.dump' -print | LC_ALL=C sort -r)
# Keep at most seven successful files, at most 1GiB each; never delete player DB.
for ((i=7; i<${#backups[@]}; i++)); do
  [[ ${backups[i]} == /var/backups/pointpass/pointpass-*.dump ]] || exit 1
  unlink -- "${backups[i]}"
done
echo "Backup verified; retained at most seven private dumps. New dump: $(basename "$final")"

#!/usr/bin/env bash
# Install locally staged, trusted build outputs. No downloads, keys or secrets.
# API_PUBLISH_DIR, MATCH_BUILD_DIR and MIGRATION_DIR are explicit directories.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 && $# == 3 ]] || { echo 'Usage: sudo deploy-release.sh API_PUBLISH_DIR MATCH_BUILD_DIR MIGRATION_DIR' >&2; exit 1; }
api=$(realpath -- "$1")
match=$(realpath -- "$2")
migrations=$(realpath -- "$3")
[[ -f $api/PointPass.Api.dll && -f $match/PointPassServer.x86_64 && -f $migrations/001_initial.sql ]] || {
  echo 'Required build outputs or migration missing.' >&2; exit 1;
}
[[ -s /etc/pointpass/api.env && -s /etc/pointpass/match.env ]] || { echo 'Configure secrets first.' >&2; exit 1; }
[[ -s /var/lib/pointpass-match/tls/fullchain.pem ]] || { echo 'Configure trusted certificate first.' >&2; exit 1; }
free_kib=$(df --output=avail /opt/pointpass | tail -n 1 | tr -d ' ')
(( free_kib >= 4194304 )) || { echo 'Less than 4GiB free; review disk use before deployment.' >&2; exit 1; }
releases_kib=$(du -sk /opt/pointpass/releases | awk '{print $1}')
(( releases_kib <= 8388608 )) || { echo 'Release retention exceeds 8GiB; review and remove inactive releases before deploying.' >&2; exit 1; }
staged_kib=$(du -sk "$api" "$match" | awk '{total += $1} END {print total}')
(( staged_kib <= 2097152 )) || { echo 'Build outputs exceed 2GiB; inspect dedicated-server asset exclusion before deploying.' >&2; exit 1; }
release="/opt/pointpass/releases/$(date -u +%Y%m%dT%H%M%S%NZ)"
install -d -m 0755 "$release/api" "$release/match" "$release/migrations"
cp -a -- "$api/." "$release/api/"
cp -a -- "$match/." "$release/match/"
cp -a -- "$migrations/." "$release/migrations/"
chown -R root:root "$release"
find "$release" -type d -exec chmod 0755 {} +
find "$release" -type f -exec chmod a+r,go-w {} +
chmod 0755 "$release/match/PointPassServer.x86_64"
previous=$(readlink -f /opt/pointpass/current || true)
rollback() {
  if [[ -n $previous && $previous == /opt/pointpass/releases/* ]]; then
    ln -sfnT -- "$previous" /opt/pointpass/current
    systemctl restart pointpass-api pointpass-match || true
  fi
  echo 'Deployment failed. Previous binaries restored if available; database migrations are never automatically reversed.' >&2
}
trap rollback ERR
systemctl stop pointpass-match pointpass-api || true
# Pre-migration backup also bounds retention, including repeated deployments.
if runuser -u postgres -- psql -XAtqc "SELECT to_regclass('public.players') IS NOT NULL" pointpass | grep -qx t; then
  /opt/pointpass/bin/backup.sh
fi
has_migrations=$(runuser -u postgres -- psql -XAtqc "SELECT to_regclass('public.schema_migrations') IS NOT NULL" pointpass)
version_applied=f
if [[ $has_migrations == t ]]; then
  version_applied=$(runuser -u postgres -- psql -XAtqc 'SELECT EXISTS(SELECT 1 FROM schema_migrations WHERE version=1)' pointpass)
fi
if [[ $version_applied != t ]]; then
  {
    printf 'SET ROLE pointpassowner;\n'
    cat -- "$release/migrations/001_initial.sql"
  } | runuser -u postgres -- psql -X1v ON_ERROR_STOP=1 pointpass
fi
ln -sfnT -- "$release" /opt/pointpass/current
systemctl daemon-reload
systemctl enable --now pointpass-api
for attempt in {1..20}; do
  if curl --fail --silent --max-time 2 http://127.0.0.1:5000/health >/dev/null; then break; fi
  sleep 1
done
curl --fail --silent --max-time 3 http://127.0.0.1:5000/health >/dev/null
systemctl enable --now pointpass-match pointpass-backup.timer
sleep 2
systemctl is-active --quiet pointpass-api
systemctl is-active --quiet pointpass-match
trap - ERR
echo "Installed release $(basename "$release"). Test gameplay before opening UDP7777. Old releases retained; remove only reviewed inactive releases after successful verification."

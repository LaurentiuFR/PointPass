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
exec 9>/run/lock/pointpass-pilot-verification.lock
flock -n 9 || { echo 'Another PointPass deployment or verification is running.' >&2; exit 1; }
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
refuse_deployment() {
  echo "Deployment stopped: $1" >&2
  exit 1
}

check_outbox() {
  local path
  for path in /var/lib/pointpass-match/pending-result.json{,.tmp}; do
    [[ ! -e $path && ! -L $path ]] || refuse_deployment 'Worker result outbox needs review.'
  done
}

unit_is_stopped() {
  [[ $(systemctl show "$1" -p ActiveState --value) == inactive &&
     $(systemctl show "$1" -p MainPID --value) == 0 ]]
}

close_deployment_gate() {
  if [[ -n ${gate_write:-} ]]; then exec {gate_write}>&-; fi
  if [[ -n ${gate_pid:-} ]]; then wait "$gate_pid" || true; fi
}

stop_idle_services() {
  local api_state match_state invocation gate_sql reply
  api_state=$(systemctl show pointpass-api.service -p ActiveState --value)
  match_state=$(systemctl show pointpass-match.service -p ActiveState --value)
  [[ $api_state == active || $api_state == inactive ]] || refuse_deployment 'API service state is ambiguous.'
  [[ $match_state == active || $match_state == inactive ]] || refuse_deployment 'Worker service state is ambiguous.'
  check_outbox
  if [[ ! -e /opt/pointpass/current && ! -L /opt/pointpass/current ]] &&
     unit_is_stopped pointpass-api.service && unit_is_stopped pointpass-match.service; then
    return 0 # First installation has no worker row or match schema yet.
  fi

  # Join and ticket redemption take this same row lock. Keep it held until
  # both services are stopped so neither can admit a player after the check.
  coproc POINTPASS_DEPLOY_GATE { exec 9>&-; exec runuser -u postgres -- psql -XAtq -v ON_ERROR_STOP=1 pointpass; }
  gate_pid=${POINTPASS_DEPLOY_GATE_PID:-}
  gate_read=${POINTPASS_DEPLOY_GATE[0]:-}
  gate_write=${POINTPASS_DEPLOY_GATE[1]:-}
  trap close_deployment_gate EXIT
  [[ -n $gate_pid && -n $gate_read && -n $gate_write ]] || refuse_deployment 'Could not start the database gate.'
  read -r -d '' gate_sql <<'SQL' || true
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '8s';
SET LOCAL idle_in_transaction_session_timeout = '90s';
DO $gate$
BEGIN
    PERFORM 1 FROM workers WHERE singleton FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Worker lease row is missing'; END IF;
END;
$gate$;
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM matches WHERE state IN ('waiting','active')
       OR finished_utc > now() - interval '15 seconds'
) THEN 'busy' ELSE 'idle' END;
SQL
  printf '%s\n' "$gate_sql" >&"$gate_write"
  read -r -t 12 -u "$gate_read" reply && [[ $reply == idle ]] ||
    refuse_deployment 'The database could not confirm there are no open or recently finished matches.'

  if [[ $match_state == active ]]; then
    invocation=$(systemctl show pointpass-match.service -p InvocationID --value)
    [[ $invocation =~ ^[[:xdigit:]]{32}$ ]] || refuse_deployment 'Worker invocation is unavailable.'
    timeout 8s journalctl --no-pager -o cat --since '90 seconds ago' "_SYSTEMD_INVOCATION_ID=$invocation" |
      awk '/\[PointPass Worker\] metrics peers=/ { idle = /peers=0[[:space:]]/ }
           /\[PointPass Worker\] Two authenticated players/ { idle = 0 }
           END { exit !idle }' || refuse_deployment 'The current worker has no fresh zero-peer sample.'
    [[ $(systemctl show pointpass-match.service -p InvocationID --value) == "$invocation" ]] ||
      refuse_deployment 'Worker changed during the idle check.'
  else
    unit_is_stopped pointpass-match.service || refuse_deployment 'Worker changed during the idle check.'
  fi
  check_outbox
  printf "SELECT 'locked';\n" >&"$gate_write"
  read -r -t 5 -u "$gate_read" reply && [[ $reply == locked ]] ||
    refuse_deployment 'The database gate was lost during the idle check.'
  timeout 55s systemctl --job-mode=fail stop pointpass-match.service pointpass-api.service ||
    refuse_deployment 'Service stop failed. The current release was left in place.'
  unit_is_stopped pointpass-match.service && unit_is_stopped pointpass-api.service ||
    refuse_deployment 'Service stop could not be verified. The current release was left in place.'
  close_deployment_gate
  trap - EXIT
}

rollback() {
  if [[ -n $previous && $previous == /opt/pointpass/releases/* ]]; then
    ln -sfnT -- "$previous" /opt/pointpass/current
    systemctl restart pointpass-api pointpass-match || true
  fi
  echo 'Deployment failed. Previous binaries restored if available; database migrations are never automatically reversed.' >&2
}
stop_idle_services
trap rollback ERR
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

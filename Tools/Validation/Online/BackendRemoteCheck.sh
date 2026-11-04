#!/usr/bin/env bash
# Run only on the existing authorized pilot server, against its disposable database.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run as root for isolated service/database setup.' >&2; exit 1; }
[[ -s /var/tmp/pointpass-backend-publish.tar && -s /etc/pointpass/api.env ]] || { echo 'Stage backend archive and configure protected API environment first.' >&2; exit 1; }
build=/opt/pointpass/validation-build
unit=pointpass-api-validation
state_file="/var/lib/pointpass-api/validation-state-$(date -u +%Y%m%dT%H%M%S%N).json"
install -d -m 0755 "$build"
tar -xf /var/tmp/pointpass-backend-publish.tar -C "$build" --no-same-owner
find "$build" -type d -exec chmod 0755 {} +
find "$build" -type f -exec chmod 0644 {} +
install -d -m 0700 -o pointpassapi -g pointpassapi /var/lib/pointpass-api
if runuser -u postgres -- psql -XAtqc "SELECT to_regclass('public.schema_migrations') IS NOT NULL" pointpass_validation | grep -qx f; then
  { printf 'SET ROLE pointpassowner;\n'; cat "$build/Api/Migrations/001_initial.sql"; } |
    runuser -u postgres -- psql -X -1 -v ON_ERROR_STOP=1 pointpass_validation
fi
systemctl is-active --quiet "$unit" && { echo 'An isolated validation service is already running; review it first.' >&2; exit 1; }
cleanup() {
  systemctl stop "$unit" 2>/dev/null || true
  # The dedicated file contains server-only credentials; retain no extra copy.
  rm -f -- /etc/pointpass/validation.env
  # Only this invocation's private, disposable persistence fixture is removed.
  rm -f -- "$state_file"
}
trap cleanup EXIT
cp /etc/pointpass/api.env /etc/pointpass/validation.env
cat >> /etc/pointpass/validation.env <<'EOF'
ASPNETCORE_URLS=http://127.0.0.1:5001
ConnectionStrings__PointPass="Host=/var/run/postgresql;Database=pointpass_validation;Username=pointpassapi;Maximum Pool Size=8;Minimum Pool Size=0;Timeout=3;Command Timeout=5"
POINTPASS_TEST_ALLOW_MUTATION=isolated-validation
POINTPASS_API_TEST_URL=http://127.0.0.1:5001
EOF
printf 'POINTPASS_TEST_STATE_FILE=%s\n' "$state_file" >> /etc/pointpass/validation.env
chmod 0600 /etc/pointpass/validation.env
start_api() {
  systemd-run --quiet --collect --unit="$unit" --property=User=pointpassapi --property=Group=pointpassapi \
    --property=WorkingDirectory="$build/Api" --property=EnvironmentFile=/etc/pointpass/validation.env \
    --property=NoNewPrivileges=yes --property=PrivateTmp=yes --property=MemoryMax=256M \
    /usr/bin/dotnet "$build/Api/PointPass.Api.dll"
  for attempt in {1..30}; do
    curl --fail --silent --max-time 1 http://127.0.0.1:5001/health >/dev/null && return 0
    sleep 0.2
  done
  echo 'Isolated validation API did not become ready.' >&2
  return 1
}
start_api
set -a
source /etc/pointpass/validation.env
set +a
runuser -u pointpassapi -- /usr/bin/dotnet "$build/Checks/PointPass.Api.Checks.dll" --integration
systemctl stop "$unit"
# A restart must preserve the same PostgreSQL data and require a fresh access session.
start_api
runuser -u pointpassapi -- /usr/bin/dotnet "$build/Checks/PointPass.Api.Checks.dll" --verify-persistence
echo 'Isolated PostgreSQL/API checks and post-service-restart persistence checks completed.'

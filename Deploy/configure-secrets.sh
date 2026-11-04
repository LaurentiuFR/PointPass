#!/usr/bin/env bash
# Creates server-only environment files, never prints generated values.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run with sudo.' >&2; exit 1; }
install -d -m 0700 /etc/pointpass
if [[ -e /etc/pointpass/api.env || -e /etc/pointpass/match.env ]]; then
  [[ -s /etc/pointpass/api.env && -s /etc/pointpass/match.env ]] || { echo 'Partial secret setup needs manual review.' >&2; exit 1; }
  echo 'Existing configuration retained; no secret rotation performed.'
  exit 0
fi
worker_key=$(openssl rand -hex 32)
cat > /etc/pointpass/api.env <<EOF
ASPNETCORE_URLS=http://127.0.0.1:5000
ASPNETCORE_ENVIRONMENT=Production
DOTNET_EnableDiagnostics=0
ConnectionStrings__PointPass="Host=/var/run/postgresql;Database=pointpass;Username=pointpassapi;Maximum Pool Size=8;Minimum Pool Size=0;Timeout=3;Command Timeout=5"
PointPass__WorkerKey=$worker_key
PointPass__GameVersion=PointPass-Online-1
PointPass__GameHost=62.238.115.207
PointPass__TlsServerName=62.238.115.207
PointPass__GamePort=7777
EOF
cat > /etc/pointpass/match.env <<EOF
POINTPASS_API_BASE=http://127.0.0.1:5000
POINTPASS_WORKER_KEY=$worker_key
POINTPASS_GAME_PORT=7777
POINTPASS_GAME_VERSION=PointPass-Online-1
POINTPASS_TLS_NAME=62.238.115.207
POINTPASS_TLS_CERT=/var/lib/pointpass-match/tls/fullchain.pem
POINTPASS_TLS_KEY=/var/lib/pointpass-match/tls/privkey.pem
POINTPASS_STATE_PATH=/var/lib/pointpass-match
EOF
unset worker_key
chmod 0600 /etc/pointpass/api.env /etc/pointpass/match.env
echo 'Created root-readable service environment files; credentials were not printed.'

#!/usr/bin/env bash
# Legacy file mode is retained; --pipe sends one invitation only to redirected stdout.
set -Eeuo pipefail
umask 077
pipe=false
if [[ $# == 2 && $1 == --pipe ]]; then
  pipe=true
  shift
fi
[[ $EUID == 0 && $# == 1 && $1 =~ ^[A-Za-z0-9_-]{1,40}$ ]] || {
  echo 'Usage: sudo /opt/pointpass/bin/create-invite.sh [--pipe] TESTER_LABEL' >&2; exit 1;
}
if $pipe && [[ -t 1 ]]; then
  echo 'Private invitation pipe requires redirected stdout; no invitation was created.' >&2
  exit 1
fi
# Read only trusted root-owned configuration. Values never become CLI arguments.
set -a
source /etc/pointpass/api.env
set +a
if $pipe; then
  # The SSH caller captures this stream in memory and forwards it directly to stdin.
  # Never use tee, shell tracing, command-line secret arguments or a terminal here.
  exec runuser -u pointpassapi -- /usr/bin/dotnet /opt/pointpass/current/api/PointPass.Api.dll --create-invite --invite-stdout
else
  directory=/var/lib/pointpass-api/invitations
  install -d -m 0700 -o pointpassapi -g pointpassapi "$directory"
  destination="$directory/$(date -u +%Y%m%dT%H%M%S%NZ)-$1.txt"
  runuser -u pointpassapi -- /usr/bin/dotnet /opt/pointpass/current/api/PointPass.Api.dll --create-invite --invite-file "$destination"
  echo "Invitation created in private server file: $destination"
fi

#!/usr/bin/env bash
# Generates one private invitation file without printing its secret code.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 && $# == 1 && $1 =~ ^[A-Za-z0-9_-]{1,40}$ ]] || {
  echo 'Usage: sudo /opt/pointpass/bin/create-invite.sh TESTER_LABEL (letters, numbers, underscore, hyphen)' >&2; exit 1;
}
directory=/var/lib/pointpass-api/invitations
install -d -m 0700 -o pointpassapi -g pointpassapi "$directory"
destination="$directory/$(date -u +%Y%m%dT%H%M%S%NZ)-$1.txt"
# Read only trusted root-owned configuration. Values never become CLI arguments.
set -a
source /etc/pointpass/api.env
set +a
runuser -u pointpassapi -- /usr/bin/dotnet /opt/pointpass/current/api/PointPass.Api.dll --create-invite --invite-file "$destination"
echo "Invitation created in private server file: $destination"

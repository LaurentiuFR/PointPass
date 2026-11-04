#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Root-only certificate deploy hook.' >&2; exit 1; }
lineage=${RENEWED_LINEAGE:-/etc/letsencrypt/live/pointpass-ip}
[[ $lineage == /etc/letsencrypt/live/pointpass-ip ]] || exit 0
openssl verify -CApath /etc/ssl/certs -untrusted "$lineage/chain.pem" -verify_ip 62.238.115.207 "$lineage/cert.pem" >/dev/null
openssl x509 -in "$lineage/cert.pem" -checkend 172800 -noout >/dev/null
target=/var/lib/pointpass-match/tls
install -d -m 0700 -o pointpassmatch -g pointpassmatch "$target"
install -m 0600 -o pointpassmatch -g pointpassmatch "$lineage/fullchain.pem" "$target/fullchain.pem.new"
install -m 0600 -o pointpassmatch -g pointpassmatch "$lineage/privkey.pem" "$target/privkey.pem.new"
# The worker observes the marker only after both atomic renames have completed.
mv -f -- "$target/fullchain.pem.new" "$target/fullchain.pem"
mv -f -- "$target/privkey.pem.new" "$target/privkey.pem"
install -m 0600 -o pointpassmatch -g pointpassmatch /dev/null /var/lib/pointpass-match/tls-reload.request
if systemctl is-active --quiet nginx; then
  nginx -t
  systemctl reload nginx
fi
logger -t pointpass-cert 'Validated IP certificate deployed; match transport reload requested for next idle state.'

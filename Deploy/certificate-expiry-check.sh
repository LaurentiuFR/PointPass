#!/usr/bin/env bash
set -Eeuo pipefail
cert=/etc/letsencrypt/live/pointpass-ip/cert.pem
openssl x509 -in "$cert" -checkend 172800 -noout >/dev/null || {
  logger -p daemon.err -t pointpass-cert 'URGENT: IP certificate missing or expires within 48 hours; inspect renewal service.'
  exit 1
}
worker=/var/lib/pointpass-match/tls/fullchain.pem
openssl x509 -in "$worker" -checkend 86400 -noout >/dev/null || {
  logger -p daemon.err -t pointpass-cert 'URGENT: worker TLS certificate missing or expires within 24 hours.'
  exit 1
}
openssl verify -CApath /etc/ssl/certs -untrusted /etc/letsencrypt/live/pointpass-ip/chain.pem -verify_ip 62.238.115.207 "$cert" >/dev/null
echo 'Certificate chain, IP SAN and remaining lifetime checks passed.'

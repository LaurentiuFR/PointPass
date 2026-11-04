#!/usr/bin/env bash
# HTTP-01 validation must already be reachable on TCP80; never use --insecure.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run with sudo.' >&2; exit 1; }
[[ $# == 0 || ( $# == 1 && $1 == --production-only-maintenance ) ]] || {
  echo 'Usage: configure-certificate.sh [--production-only-maintenance]' >&2; exit 1;
}
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
certbot=/opt/pointpass-certbot/bin/certbot
[[ -x $certbot ]] || { echo 'Run install-base.sh first.' >&2; exit 1; }
nginx -t
version=$($certbot --version | awk '{print $2}')
dpkg --compare-versions "$version" ge 5.4.0 || { echo 'Certbot >=5.4 required for IP/webroot.' >&2; exit 1; }
common=(certonly --non-interactive --agree-tos --register-unsafely-without-email --preferred-profile shortlived --webroot --webroot-path /var/www/pointpass-acme --ip-address 62.238.115.207 --cert-name pointpass-ip --key-type rsa --rsa-key-size 2048)
if [[ ! -s /etc/letsencrypt/live/pointpass-ip/fullchain.pem ]]; then
  if [[ ${1:-} == --production-only-maintenance ]]; then
    echo 'Explicit maintenance mode: staging/dry-run pending official CA recovery. HTTP-01 reachability must have been verified externally. One production attempt follows with normal validation.'
  else
    # Keep untrusted staging keys/certificates in an entirely separate lineage.
    install -d -m 0700 /var/lib/pointpass-acme-staging/config /var/lib/pointpass-acme-staging/work /var/lib/pointpass-acme-staging/logs
    "$certbot" "${common[@]}" --staging --config-dir /var/lib/pointpass-acme-staging/config --work-dir /var/lib/pointpass-acme-staging/work --logs-dir /var/lib/pointpass-acme-staging/logs
  fi
  "$certbot" "${common[@]}"
fi
# This includes the intermediate chain and validates the IP SAN against OS roots.
openssl verify -CApath /etc/ssl/certs -untrusted /etc/letsencrypt/live/pointpass-ip/chain.pem -verify_ip 62.238.115.207 /etc/letsencrypt/live/pointpass-ip/cert.pem
install -d -m 0755 /etc/letsencrypt/renewal-hooks/deploy
install -m 0750 "$script_dir/certificate-deploy-hook.sh" /etc/letsencrypt/renewal-hooks/deploy/pointpass
RENEWED_LINEAGE=/etc/letsencrypt/live/pointpass-ip /opt/pointpass/bin/certificate-deploy-hook.sh
install -m 0644 "$script_dir/nginx-https.conf" /etc/nginx/sites-available/pointpass
nginx -t
systemctl reload nginx
systemctl enable --now pointpass-cert-renew.timer pointpass-cert-check.timer
/opt/pointpass/bin/certificate-expiry-check.sh
echo 'Trusted IP certificate installed. Renewal runs every 6 hours; a daily expiry check fails before 48h remaining.'

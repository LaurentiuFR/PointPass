#!/usr/bin/env bash
# Run as root on the audited, otherwise fresh Ubuntu 26.04 pilot host.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run with sudo.' >&2; exit 1; }
source /etc/os-release
[[ $ID == ubuntu && $VERSION_ID == 26.04 ]] || { echo 'Requires Ubuntu 26.04; no OS migration is performed.' >&2; exit 1; }
[[ $(uname -m) == x86_64 ]] || { echo 'Requires x86_64 Unity server build.' >&2; exit 1; }
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install --no-install-recommends -y aspnetcore-runtime-10.0 postgresql-18 postgresql-client-18 nginx ufw python3-venv ca-certificates curl openssl logrotate sysstat jq
for account in pointpassapi pointpassmatch; do
  if ! id "$account" >/dev/null 2>&1; then
    case "$account" in pointpassapi) account_home=/var/lib/pointpass-api ;; pointpassmatch) account_home=/var/lib/pointpass-match ;; esac
    useradd --system --user-group --home-dir "$account_home" --shell /usr/sbin/nologin "$account"
  fi
done
install -d -m 0755 /opt/pointpass /opt/pointpass/releases /opt/pointpass/bin /var/www/pointpass-acme
install -d -m 0700 /etc/pointpass /var/backups/pointpass /var/lib/pointpass-deploy
install -d -m 0700 -o pointpassapi -g pointpassapi /var/lib/pointpass-api
install -d -m 0700 -o pointpassmatch -g pointpassmatch /var/lib/pointpass-match /var/lib/pointpass-match/tls /var/lib/pointpass-match/outbox

# Distribution Certbot 4.0 cannot perform the required IP/webroot workflow.
# Pin a supported 5.x release in a dedicated virtual environment, never system pip.
if [[ ! -x /opt/pointpass-certbot/bin/python ]]; then
  python3 -m venv /opt/pointpass-certbot
fi
/opt/pointpass-certbot/bin/python -m pip install --disable-pip-version-check --no-cache-dir 'certbot==5.8.0'
/opt/pointpass-certbot/bin/certbot --version

for helper in backup.sh restore-check.sh certificate-deploy-hook.sh certificate-expiry-check.sh collect-metrics.sh create-invite.sh; do
  install -m 0750 "$script_dir/$helper" "/opt/pointpass/bin/$helper"
done
install -m 0644 "$script_dir/systemd/pointpass-api.service" /etc/systemd/system/
install -m 0644 "$script_dir/systemd/pointpass-match.service" /etc/systemd/system/
install -m 0644 "$script_dir/systemd/pointpass-backup.service" /etc/systemd/system/
install -m 0644 "$script_dir/systemd/pointpass-backup.timer" /etc/systemd/system/
install -m 0644 "$script_dir/systemd/pointpass-cert-renew.service" /etc/systemd/system/
install -m 0644 "$script_dir/systemd/pointpass-cert-renew.timer" /etc/systemd/system/
install -m 0644 "$script_dir/systemd/pointpass-cert-check.service" /etc/systemd/system/
install -m 0644 "$script_dir/systemd/pointpass-cert-check.timer" /etc/systemd/system/
install -m 0644 "$script_dir/logrotate-pointpass" /etc/logrotate.d/pointpass
install -d -m 0755 /etc/systemd/journald.conf.d
install -m 0644 "$script_dir/journald-pointpass.conf" /etc/systemd/journald.conf.d/pointpass.conf

# No default site is removed. A conflicting site is a deployment error to review.
if [[ -e /etc/nginx/sites-enabled/default ]]; then
  expected_hash=$(dpkg-query -W -f='${Conffiles}\n' nginx-common | awk '$1=="/etc/nginx/sites-available/default" {print $2}')
  actual_hash=$(md5sum /etc/nginx/sites-available/default | awk '{print $1}')
  if [[ -n $expected_hash && $expected_hash == "$actual_hash" && $(readlink -f /etc/nginx/sites-enabled/default) == /etc/nginx/sites-available/default ]]; then
    unlink /etc/nginx/sites-enabled/default
  else
    echo 'Existing custom nginx default site requires review.' >&2; exit 1
  fi
fi
install -m 0644 "$script_dir/nginx-http.conf" /etc/nginx/sites-available/pointpass
ln -sfn /etc/nginx/sites-available/pointpass /etc/nginx/sites-enabled/pointpass
nginx -t
systemctl enable --now nginx
systemctl reload nginx
systemctl restart systemd-journald
systemctl daemon-reload
echo 'Base installed. Database, secret configuration, trusted certificate and releases are separate steps. Gameplay firewall remains closed.'

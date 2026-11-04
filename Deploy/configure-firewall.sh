#!/usr/bin/env bash
# Firewall-only rollout. SSH login policy is intentionally a separate approval.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run with sudo.' >&2; exit 1; }
state=/var/lib/pointpass-deploy/firewall
[[ ! -e $state ]] || { echo 'Firewall snapshot exists; review before another rollout.' >&2; exit 1; }
install -d -m 0700 "$state"
cp -a -- /etc/ufw "$state/ufw"
ufw status | head -n 1 > "$state/status"
cat > "$state/rollback.sh" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail
state=/var/lib/pointpass-deploy/firewall
cp -a -- "$state/ufw/." /etc/ufw/
if [[ $(cat "$state/status") == 'Status: active' ]]; then
  ufw --force enable
  ufw reload
else
  ufw --force disable
fi
logger -p auth.warning -t pointpass-deploy 'PointPass firewall rollout automatically rolled back.'
EOF
chmod 0700 "$state/rollback.sh"
systemd-run --unit=pointpass-firewall-rollback --on-active=5m "$state/rollback.sh"
trap '"$state/rollback.sh"; exit 1' ERR
ufw limit 22/tcp comment 'PointPass administration'
ufw allow 80/tcp comment 'PointPass ACME HTTP-01'
ufw allow 443/tcp comment 'PointPass HTTPS API'
ufw default deny incoming
ufw default allow outgoing
ufw --force enable
trap - ERR
echo 'Verify NEW SSH connection and TCP80/443. Then cancel pointpass-firewall-rollback.timer. UDP7777 stays closed until local Linux worker validation.'

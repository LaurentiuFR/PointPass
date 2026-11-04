#!/usr/bin/env bash
# Read-only bounded sample. Contains process/resource data, never environment.
set -Eeuo pipefail
seconds=${1:-60}
[[ $seconds =~ ^[0-9]+$ ]] && (( seconds >= 5 && seconds <= 300 )) || { echo 'Sample duration must be 5..300 seconds.' >&2; exit 1; }
date -u +%FT%TZ
systemctl show pointpass-api pointpass-match postgresql@18-main -p Id -p ActiveState -p MainPID -p MemoryCurrent -p MemoryPeak -p CPUUsageNSec -p TasksCurrent
free -m
df -h /
du -sm /var/lib/postgresql/18/main /var/backups/pointpass /var/log/journal /opt/pointpass/releases 2>/dev/null || true
ss -lntup
ip -s link
echo "Sampling process CPU/RSS and aggregate interface traffic for $seconds seconds."
pids=$(systemctl show pointpass-api pointpass-match postgresql@18-main -p MainPID --value | awk '$1>0' | paste -sd,)
[[ -z $pids ]] || pidstat -u -r -p "$pids" 1 "$seconds"
ip -s link
systemctl show pointpass-api pointpass-match postgresql@18-main -p Id -p MemoryCurrent -p CPUUsageNSec
echo 'Per-player game traffic/timing requires worker counters and a measured two-client run; interface totals include SSH/API traffic.'

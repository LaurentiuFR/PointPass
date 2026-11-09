#!/usr/bin/env bash
# Read-only bounded sample. Contains resource counters, never protected environment.
set -Eeuo pipefail
seconds=${1:-60}
[[ $seconds =~ ^[0-9]+$ ]] && (( seconds >= 5 && seconds <= 300 )) || { echo 'Sample duration must be 5..300 seconds.' >&2; exit 1; }
units=(pointpass-api.service pointpass-match.service postgresql@18-main.service)
paths=(/var/lib/postgresql/18/main /var/backups/pointpass /var/log/journal /var/log/nginx /opt/pointpass/releases /var/lib/pointpass-match)
declare -A groups cpu_before pid_before memory_sum memory_count memory_min memory_max memory_before disk_before rx_before tx_before
pidstat_pid=''
cleanup() {
    if [[ -n $pidstat_pid ]] && kill -0 "$pidstat_pid" 2>/dev/null; then
        kill "$pidstat_pid" 2>/dev/null || true
        wait "$pidstat_pid" 2>/dev/null || true
    fi
}
trap cleanup EXIT

group_path() {
    local group
    group=$(systemctl show "$1" -p ControlGroup --value)
    [[ $group == /* && $group != *'..'* && -d /sys/fs/cgroup$group ]] || return 1
    printf '/sys/fs/cgroup%s' "$group"
}
cpu_usec() { awk '$1=="usage_usec" {print $2}' "$1/cpu.stat" 2>/dev/null; }
database_bytes() {
    runuser -u postgres -- psql -X -Atq -d postgres -c "SELECT pg_database_size('pointpass');" 2>/dev/null || printf 'unavailable\n'
}
disk_snapshot() {
    local phase=$1 path bytes
    echo "DISK_SNAPSHOT phase=$phase utc=$(date -u +%FT%TZ)"
    df -B1 /
    for path in "${paths[@]}"; do
        bytes=$(du -sb "$path" 2>/dev/null | awk '{print $1}') || bytes=''
        [[ $bytes =~ ^[0-9]+$ ]] || { echo "DISK path=$path phase=$phase unavailable=1"; continue; }
        echo "DISK path=$path phase=$phase bytes=$bytes"
        if [[ $phase == before ]]; then disk_before[$path]=$bytes
        elif [[ -n ${disk_before[$path]:-} ]]; then echo "DISK_DELTA path=$path bytes=$((bytes-disk_before[$path]))"; fi
    done
    echo "DATABASE phase=$phase pointpassBytes=$(database_bytes)"
    journalctl --disk-usage
}
sample_memory() {
    local unit group bytes
    for unit in "${units[@]}"; do
        group=${groups[$unit]:-}
        [[ -n $group && -r $group/memory.current ]] || continue
        read -r bytes < "$group/memory.current"
        [[ $bytes =~ ^[0-9]+$ ]] || continue
        memory_sum[$unit]=$(( ${memory_sum[$unit]:-0}+bytes ))
        memory_count[$unit]=$(( ${memory_count[$unit]:-0}+1 ))
        [[ -n ${memory_min[$unit]:-} && ${memory_min[$unit]} -le $bytes ]] || memory_min[$unit]=$bytes
        [[ -n ${memory_max[$unit]:-} && ${memory_max[$unit]} -ge $bytes ]] || memory_max[$unit]=$bytes
    done
}

echo "SAMPLE_START utc=$(date -u +%FT%TZ) requestedSeconds=$seconds logicalCpus=$(getconf _NPROCESSORS_ONLN)"
systemctl show "${units[@]}" -p Id -p ActiveState -p MainPID -p ControlGroup -p MemoryCurrent -p MemoryPeak -p CPUUsageNSec -p TasksCurrent
free -m
disk_snapshot before
ss -lntup
start_uptime=$(awk '{print $1}' /proc/uptime)
for unit in "${units[@]}"; do
    groups[$unit]=$(group_path "$unit") || { groups[$unit]=''; continue; }
    cpu_before[$unit]=$(cpu_usec "${groups[$unit]}") || cpu_before[$unit]=''
    pid_before[$unit]=$(systemctl show "$unit" -p MainPID --value)
    read -r "memory_before[$unit]" < "${groups[$unit]}/memory.current" || memory_before[$unit]=''
done
for interface in /sys/class/net/*; do
    name=${interface##*/}
    read -r "rx_before[$name]" < "$interface/statistics/rx_bytes"
    read -r "tx_before[$name]" < "$interface/statistics/tx_bytes"
done
sample_memory

# pidstat is supplementary; new PostgreSQL backend PIDs can appear mid-sample.
# The cgroup CPU/memory deltas below include all children throughout the interval.
pids=$(for unit in "${units[@]}"; do
    [[ -z ${groups[$unit]} ]] || cat "${groups[$unit]}/cgroup.procs"
done | sort -nu | paste -sd,)
if [[ -n $pids ]] && command -v pidstat >/dev/null; then
    pidstat -u -r -p "$pids" 1 "$seconds" & pidstat_pid=$!
fi
for (( tick=0; tick<seconds; tick++ )); do sleep 1; sample_memory; done
if [[ -n $pidstat_pid ]]; then wait "$pidstat_pid" || echo 'Supplementary pidstat exited early.' >&2; pidstat_pid=''; fi
end_uptime=$(awk '{print $1}' /proc/uptime)
elapsed=$(awk -v a="$start_uptime" -v b="$end_uptime" 'BEGIN {printf "%.3f", b-a}')
cores=$(getconf _NPROCESSORS_ONLN)
for unit in "${units[@]}"; do
    group=$(group_path "$unit") || { echo "CGROUP_DELTA unit=$unit unavailable=1"; continue; }
    cpu_after=$(cpu_usec "$group") || cpu_after=''
    pid_after=$(systemctl show "$unit" -p MainPID --value)
    if [[ ! ${cpu_before[$unit]:-} =~ ^[0-9]+$ || ! $cpu_after =~ ^[0-9]+$ || $group != "${groups[$unit]}" || $pid_after != "${pid_before[$unit]}" || $cpu_after -lt ${cpu_before[$unit]} ]]; then
        echo "CGROUP_DELTA unit=$unit unavailable=1 reason=service_changed"
        continue
    fi
    if ! read -r memory_after < "$group/memory.current" || ! read -r lifetime_peak < "$group/memory.peak" ||
        [[ -z ${memory_before[$unit]:-} || ${memory_count[$unit]:-0} -eq 0 ]]; then
        echo "CGROUP_DELTA unit=$unit unavailable=1 reason=memory_counter_unreadable"
        continue
    fi
    cpu_delta=$((cpu_after-cpu_before[$unit]))
    mean_memory=$((memory_sum[$unit]/memory_count[$unit]))
    awk -v unit="$unit" -v elapsed="$elapsed" -v cpu="$cpu_delta" -v cores="$cores" \
        -v before="${memory_before[$unit]}" -v after="$memory_after" -v mean="$mean_memory" \
        -v min="${memory_min[$unit]}" -v max="${memory_max[$unit]}" -v peak="$lifetime_peak" \
        'BEGIN {printf "CGROUP_DELTA unit=%s elapsedSeconds=%s cpuUsec=%.0f cpuPercentOneCore=%.3f cpuPercentHost=%.3f memoryStartBytes=%.0f memoryEndBytes=%.0f memoryMean1HzBytes=%.0f memoryMin1HzBytes=%.0f memoryMax1HzBytes=%.0f lifetimeMemoryPeakBytes=%.0f\n", unit,elapsed,cpu,cpu*100/(elapsed*1000000),cpu*100/(elapsed*1000000*cores),before,after,mean,min,max,peak}'
done
for interface in /sys/class/net/*; do
    name=${interface##*/}
    [[ -n ${rx_before[$name]:-} ]] || continue
    read -r rx_after < "$interface/statistics/rx_bytes"
    read -r tx_after < "$interface/statistics/tx_bytes"
    echo "INTERFACE_DELTA interface=$name elapsedSeconds=$elapsed rxBytes=$((rx_after-rx_before[$name])) txBytes=$((tx_after-tx_before[$name]))"
done
disk_snapshot after
echo "SAMPLE_END utc=$(date -u +%FT%TZ) measuredSeconds=$elapsed"
echo 'Cgroup CPU includes PostgreSQL backends; percentages are per logical CPU and whole host separately. RSS must not be added to cgroup memory.'
echo 'Memory means/min/max are 1 Hz observations; memory.peak is service-lifetime, not interval-only. Disk deltas are observations, not long-term growth projections.'
echo 'Per-slot game payload/timing comes from worker counters. Interface deltas include SSH/API and transport overhead; they are not per-player game bandwidth.'

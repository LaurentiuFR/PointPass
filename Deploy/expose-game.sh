#!/usr/bin/env bash
set -Eeuo pipefail
[[ $EUID == 0 && ${1:-} == --linux-local-test-passed ]] || {
  echo 'Run only after Linux startup, encrypted loopback/client and headless checks passed.' >&2; exit 1;
}
systemctl is-active --quiet pointpass-api
systemctl is-active --quiet pointpass-match
/opt/pointpass/bin/certificate-expiry-check.sh
ufw allow 7777/udp comment 'PointPass authenticated encrypted 1v1 pilot'
ufw status numbered
echo 'Only one worker/one match is configured. Admission remains invitation-based and capped at two players.'

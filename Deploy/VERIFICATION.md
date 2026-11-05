# Deployment verification record

## Live continuation — 5 November 2026

At 11:01–11:05 UTC, pinned SSH access confirmed the required host fingerprint, Ubuntu 26.04.1 and active API/match/PostgreSQL/Nginx services. No service was restarted. There was no open match, pending result outbox or other interactive root session.

**Item 1 passed:** a read-only transaction verified final match `570b1a48-3b81-47d4-ae54-9d9cdca95e8b` is completed 2–1 exactly once, with one history occurrence for slot 0 and one for slot 1. The completion timestamp is 4 November 2026 16:27:53.381579 UTC. The reusable assertion is `Tools/Validation/Online/AuditHistory.sql`; secret-free local evidence is `Logs/OnlinePilotVerification/CurrentAudit/history-proof.txt`. This proves persistence/uniqueness, not a fresh manual gameplay test.

The older checkpoint below is retained as historical evidence and will not be treated as newly verified. Current remaining items are tracked in `ONLINE_PILOT_HANDOFF.md`.

## Network exposure (item 5)

**PASS, rechecked from Windows and over pinned SSH on 5 November.** External TCP22/80/443 connected; TCP5000/5001/5432 were closed or filtered with bounded connection probes. Live listeners show the API only at 127.0.0.1:5000, no PostgreSQL TCP listener, Nginx TCP80/443, SSH TCP22 and game UDP7777. Local DNS/NTP/DHCP infrastructure listeners were also observed; they are not additional public application services. UFW is active with default incoming deny and only TCP22/80/443 + UDP7777 application rules for IPv4/IPv6. This combines listener/firewall inspection with targeted external probes, not a claim to have exhaustively scanned every port.

| Separate external check | Result |
| --- | --- |
| Default-trust HTTPS `/health` | 200, Online-1 |
| Unauthenticated `/v1/profile` | 401 |
| Public `/internal/security-counters` | 404 |
| Wrong-IP HTTPS identity routed to the same host | Rejected by Schannel, curl exit 60 (`SEC_E_WRONG_PRINCIPAL`); no bypass |

## Certificate lifecycle (item 6)

**PASS, live on 5 November.** `/opt/pointpass-certbot/bin/certbot --version` is 5.8.0, satisfying the IP/webroot requirement independently of the distro package. The current trusted Let's Encrypt YR2 certificate is valid until 10 November 2026 14:52:11 UTC. OpenSSL OS-root chain/IP verification and the deployed expiry guard passed. The worker certificate matches the current lineage; protected TLS/config permissions remain 0700/0600 as appropriate.

The real `certbot renew --dry-run --cert-name pointpass-ip --non-interactive` completed successfully against staging. It did not replace the trusted production certificate or disable validation. After checking no waiting/active match and taking the audit lock, the existing deployment hook validated/redeployed the current lineage, Nginx reloaded, and the worker acknowledged its marker while idle. API, worker and Nginx remained active. Renewal (six-hour timer), daily expiry check and latest timer executions reported success. Evidence: `Logs/OnlinePilotVerification/CurrentAudit/certificate-drill.txt`.

Separate actual Windows Unity/NGO probes reached ticket rejection with the correct trusted IP, rejected a wrong TLS name before admission, then recovered with the correct identity. No real ticket/invitation was used; continued gameplay is a separate check. Evidence: `Logs/OnlinePilotVerification/encrypted-admission-tests.txt`. No TLS/HTTPS bypass was used.

## Backups and retention (item 9)

**PASS, live drill at 5 November 11:24 UTC.** A new dump from the running production database restored successfully into the script's uniquely named disposable database: two profiles and two verified results. That restore database was removed; the pilot database was not overwritten. The private backup directory is root0700 and all six current dumps are root0600, approximately 29–31 KiB compressed each, with no partial dump and no dump above 1 GiB.

The deployed backup policy retains at most seven successful dumps, limits each with the kernel 1 GiB file-size guard, and checks free space; the daily backup timer's latest run succeeded. This run did not create synthetic production backups merely to evict old restore points. Journal configuration is 256 MiB/seven days (current usage 47.3 MiB); active logrotate timer and debug inspection confirmed daily seven-rotation application/certificate policies. `maxsize` is evaluated when logrotate runs, not a hard instantaneous active-log quota. Frequent manual/pre-deploy backups can replace older daily points within the seven-dump policy.

Evidence: `Logs/OnlinePilotVerification/CurrentAudit/backup-restore.txt`. Dumps are on the same SSD and do not survive server loss/account compromise. Off-host storage requires an approved destination/cost; no paid storage was enabled.

## Cost summary (item 10)

The owner reports 10.19/month plus usage costs. `Deploy/COSTS.md` records official traffic/storage/IPv4 terms without assuming the invoice currency, VAT, legacy tariff or whether IPv4 is already included. The exact billed total needs the account invoice/usage. No purchases, paid backups, snapshots, resizing or add-ons were enabled.

## Historical checkpoint

This is an intermediate, measured record from **4 November 2026, 11:36 UTC**. A configured room capacity does not establish tested gameplay capacity. Windows gameplay and active-match measurements must be recorded separately before the pilot is called complete.

## Applied and verified

- Existing Ubuntu 26.04.1 x86_64 host retained. No replacement machine, paid add-on, domain or external database was provisioned.
- API and Linux dedicated worker are active in release `/opt/pointpass/releases/20261104T113240384738482Z`, under separate `pointpassapi` and `pointpassmatch` service accounts. PostgreSQL's actual process runs as `postgres`.
- Public HTTPS health returns `200` with protocol `PointPass-Online-1`. Windows curl validates the ordinary trusted certificate without overrides. The certificate's IP SAN/chain also pass OpenSSL validation; it expires **10 November 2026 at 14:52:11 UTC**.
- The API binds only `127.0.0.1:5000`; PostgreSQL has no TCP listener. External connection attempts to TCP 5432, 5000 and 5001 fail. Public `/internal/security-counters` returns 404 and unauthenticated `/v1/profile` returns 401.
- The firewall permits only TCP 22, 80 and 443, plus UDP 7777. Its initial rollout used a five-minute rollback timer; a new independent SSH connection succeeded before that timer was cancelled. SSH account, root-login and password policy were left unchanged at the owner's request.
- The fresh Linux dedicated build starts with a null graphics device. Its detailed build report has zero errors and excludes the project's presentation assets. Before opening UDP publicly, actual loopback DTLS negotiated **DTLS 1.2 / ECDHE-RSA-CHACHA20-POLY1305** and passed normal CA and IP-identity verification. Repeating the check against the wrong identity `192.0.2.1` fails with IP-address mismatch.
- The worker acknowledged its initial certificate reload marker while idle. The certificate check, six-hour renewal and daily backup timers are active. Recent renewal checks completed with the current certificate not yet due; forced production renewal was not attempted.

## Backend and persistence checks

The guarded test executable ran against the isolated `pointpass_validation` database and a temporary API on loopback port 5001. It completed **45 checks**, followed by **15 after service restart**; the latter repeat 13 pure checks and add two actual persistence checks. This is **47 distinct checks**, covering strict input handling, identity isolation, invitation admission, version/capacity limits, ticket expiry/reuse, reconnects, trusted/idempotent results and history. It also verifies recovery when a ticket redemption commits but its HTTP response is ignored: only the trusted heartbeat exposes the connection identity needed for cleanup.

The temporary API was stopped, its protected environment/persistence fixture was removed, and the specifically named disposable validation database was dropped. No fake match worker was registered against the production database.

A production database dump was restored successfully into a separate disposable database, then that restore database was removed. This baseline dump contained **zero production profiles**; guest/result persistence across an API restart was established in the isolated test database. A backup containing real pilot participants should be checked again after gameplay validation.

## Idle measurements

The 60-second sample began at **11:33:52 UTC**, shortly after the new services started. CPU percentages below are relative to **one logical CPU**, not the whole four-vCPU host.

| Process | Average process CPU | Average process RSS | Cgroup memory at sample end |
| --- | ---: | ---: | ---: |
| Unity worker | 1.77% | 116.4 MiB | 77.4 MiB |
| API | 1.45% | 95.4 MiB | 36.4 MiB |
| PostgreSQL parent | 0.00% | 30.2 MiB | 44.8 MiB for the database service |

RSS includes shared mappings and must not be added to cgroup memory as a second allocation. The API sample includes its first minute of database/worker traffic and warm-up. Across consecutive worker summaries, `peers`, simulated seconds, commands, snapshots and game payload bytes all remained **zero**. There was no idle football simulation.

At this baseline the production database occupied 8,222,399 bytes; releases occupied approximately 91 MiB, backups less than 1 MiB, and the shared system journal approximately 48 MiB. These are snapshots, not measured long-term growth. Interface traffic includes deployment, SSH and API traffic, so it does not establish bandwidth per player.

## Still pending at this checkpoint

- Actual Windows Unity-transport checks, two authenticated Windows players, full gameplay, disconnect/rejoin and a verified completed match.
- Active-match CPU/RAM, per-player bandwidth, simulation timing and storage growth; tested concurrent gameplay capacity remains unestablished.
- A renewal dry run after the CA's documented staging maintenance ends on 4 November at 21:00 UTC; no certificate-validation bypass was used during the outage.
- A verified recovery administration account and later SSH hardening, if the owner chooses to proceed.
- Android/iOS device validation; no phone testing has occurred.

Invitations remain in private server files. They were not copied into the repository, client build, chat or audit bundle. Non-secret evidence is stored locally under `Logs/OnlinePilotVerification/Remote`, including the deployment status, backend checks, correct/wrong-identity DTLS results, worker journal and idle resource sample.

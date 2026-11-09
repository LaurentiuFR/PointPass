# PointPass online pilot handoff

This is the entry point for the next coding agent. Read this file, `README.md`, `DESIGN.md`, `CLAUDE.md`, `Tools/Validation/Online/README.md`, `Backend/README.md`, `Deploy/README.md` and `Deploy/VERIFICATION.md` before editing.

## Workspace and Git

- Current continuation worktree: `C:\Dev\PointPass` (9 November 2026).
- Branch: `codex/online-server-integration`, based on `main` commit `6f44996d174259aeb2b1fc7af688b37aaf51a61d`.
- The owner now authorizes a commit and `git push -u origin codex/online-server-integration` after each completed item. Never merge main or force-push. Stage only explicit reviewed files; exclude secrets and generated folders.
- The separate menu/gameplay draft remains in local stash `99b13ef623842d8bb5bd8da480593634aad8e2c8`, from `codex/mobile-hud-simplified-menu`. Do not apply or merge it during this online audit. The owner must identify the other agent's branch before a later integration.
- A friend owns current menu, mobile action-button and magnetic-dribbling changes. This pilot deliberately uses the older main gameplay baseline. Coordinate those changes before merging; do not overwrite their menu or silently introduce unvalidated tackling/stamina network actions.
- The delivery patch includes Unity-generated metadata and the new empty server scene. Do not invent or hand-edit metadata.

To apply the delivery patch in another clone, start from the exact base in a separate branch/worktree:

```powershell
git worktree add -b codex/online-pilot-review ../PointPass-online 6f44996d174259aeb2b1fc7af688b37aaf51a61d
Set-Location ../PointPass-online
git apply --check C:/path/to/pointpass-online-pilot.patch
git apply C:/path/to/pointpass-online-pilot.patch
```

Read the diff before committing. Integrating a newer menu/gameplay branch is a subsequent review, not an assumption that the patch applies cleanly to it.


## Current continuation status — 9 November 2026

Prior implementation and deployment claims below are historical until rechecked. Current access uses the friend's SSH key in place with the exact pinned host fingerprint. The test runner streams invitations through memory and anonymous pipes and records only the consumption count.

| Item | Current evidence/status |
| --- | --- |
| 1. Final match history | **PASS, live read-only SQL**: match `570b1a48-3b81-47d4-ae54-9d9cdca95e8b`, completed 4 Nov 16:27:53 UTC, score 2–1, one match row and one history occurrence in each of two distinct slots. Repeat with `Tools/Validation/Online/AuditHistory.sql`. |
| 2. Invalid-command counters | **PASS, live**. One authenticated three-byte invalid input raised `malformedRejected` from 0 to 1 on the same worker process. Both clients continued receiving state. Other rejection categories stayed at zero through the next summary. |
| 3. Separate security/transport checks | **PASS, live and isolated**. Trusted TLS, unauthorized profile access, malformed input and disconnect/rejoin were checked live. Expired/reused tickets, duplicate results and third-guest rejection were checked against the isolated API and PostgreSQL. See the case-by-case record in `Deploy/VERIFICATION.md`. |
| 4. Restart persistence | **PASS, isolated Ubuntu/PostgreSQL restart**. The same guest credential authenticated after the API process restarted, returning the same profile and verified result. The production profiles were left running for this check. |
| 5. Exposure | **PASS, live**: UFW default-deny and `ss` expose only application TCP22/80/443 + UDP7777; API stays loopback5000, PostgreSQL has no TCP listener. External TCP22/80/443 connected; 5000/5001/5432 closed/filtered. Public internal route is 404, unauthenticated profile 401. |
| 6. Certificates | **PASS, live**: Certbot 5.8.0, normal OS trust/IP checks, wrong-IP/name rejection, successful staging renewal dry run, idle deploy-hook/reload acknowledgement and expiry guard. The scheduled renewal served a new certificate on 7 Nov, trusted by Windows on 9 Nov and expiring 14 Nov at 03:18 UTC. |
| 7. Idle worker | **PASS, live**. After the full match, the updated worker had zero peers and unchanged physics and snapshot counters. In 65.43 s it used 1.741% of one core and averaged 83.35 MiB cgroup memory. |
| 8. Active resources/capacity | **PASS for one concurrent 1v1**. Over 70.44 s with the match active, worker CPU averaged 4.853% of one core and memory averaged 82.63 MiB. A 60.01 s mid-match window measured about 7.5 kbit/s inbound and 35.25 kbit/s outbound of game payload per player. See the disk and timing measurements in `Deploy/VERIFICATION.md`. Linux release Physics2D CPU marker unavailable. |
| 9. Backups/retention | **PASS, live drill**: fresh private dump restored 2 profiles/2 verified results into a disposable DB, then that DB was removed. Six successful dumps, each <=1GiB, mode0600 under root0700; no partial files. Seven-dump policy and active daily/logrotate/journal retention verified. Same-SSD/off-host limit documented. |
| 10. Costs | **PASS, documented** in `Deploy/COSTS.md`: owner-reported 10.19/month plus usage charges, published EU traffic/IPv4/optional-storage terms and explicit unknown currency/VAT/account tariff. No paid add-ons enabled. |
| 11. Regressions | **PASS**. Local automated offline, protocol, headless, command-boundary, rendered gameplay and API suites passed. Fresh Linux and Windows builds succeeded. Two Windows clients on one PC passed 41 live checks, agreed on 6,069 snapshots, and finished 2–1 with a verified PostgreSQL result. |
| 12. Invitations/Git | **PASS**. Both audit invitations were consumed exactly once, verified by exact hash match with count-only output. The reachable branch and new runner passed the secret/generated-path scan. Retries reuse the protected profiles. |
| 13. Compatibility | **PASS, contract review and automated checks**. Keep `PointPass-Online-1` and wire version 1. Audit tooling preserves the online payloads and action set. All 248 codec checks passed, and the isolated API rejected a mismatched identifier. |

The audited Linux worker and API are live as release `20261109T201829172528171Z`. The guarded rollout took a backup, then restarted the API and worker with the room idle. The profile and completed-result ID inventories matched across the restart. Menu, HUD, tackling, stamina and dribbling are out of scope. Phone, touch and second-PC checks remain pending.

## Implemented architecture

`OnlinePilotClient` owns guest sign-in, HTTPS admission, encrypted client connection, interpolation and cleanup. `OnlinePilotPanel` is a small replaceable menu adapter; a redesigned menu can call `OnlinePilotClient.Instance.Open()`.

`PlayerController` remains the command boundary. `NetworkPlayerInput` now accepts a sender callback, preserving the old host/client constructor. `NetworkMatchWorld` renders either authenticated local slot and disables local ball/player simulation. Input and snapshots use explicit versioned little-endian fields in `OnlineProtocol`; no native struct padding crosses the online wire. Protocol is `PointPass-Online-1`.

`OnlineMatchWorker` is one Linux dedicated room with capacity two. Both humans are clients. Only the server simulates movement, sprint, possession, charge, physical kicks/curve, goals, score, clock and restarts. It validates ordering, numbers, action legality, sizes, rates and recent snapshot acknowledgements. A 30-second reconnect window freezes the authoritative match. Idle gameplay time scale is zero; transport remains responsive. Rates remain 50 Hz physics and 30 Hz snapshots.

`ServerMatchWorld`, `PhysicsActorFactory`, `PitchBuilder.BuildPhysics`, headless `GoalNet` and `SimulationStride` preserve existing football rules without cameras, UI, rendered actors, stadium, sound or replay. The Linux builder excludes project presentation Resources through AssetDatabase moves/restoration and checks packed assets. Windows/Linux modules also caused Unity to add its pinned Linux SDK/toolchain packages; NGO/Transport versions were not upgraded.

`Backend/PointPass.Api` is .NET 10 with Npgsql 10.0.3 and PostgreSQL 18. It provides persistent invited guest identities, limited bearer sessions, profiles/history, version-bound 30-second single-use tickets, one worker lease/room and trusted transactional idempotent results. Local wallet/inventory remain untrusted demo data. Trusted heartbeat reconciliation recovers a redemption whose HTTP response was lost without accepting a ticket twice.

## Deployed system

- Existing Hetzner Ubuntu 26.04 x86_64 server: **62.238.115.207**, four vCPUs, 8 GB RAM, 80 GB SSD.
- API: **https://62.238.115.207**; `/health` is public. Internal routes return 404 through Nginx. API itself binds only `127.0.0.1:5000`.
- Game: authenticated encrypted DTLS **UDP 7777**. Trusted Let's Encrypt IP certificate; normal certificate validation is enabled. Correct IP accepted and wrong IP/name rejected from real clients/tools.
- PostgreSQL 18 uses a private Unix socket, separate restricted runtime role and bounded pool of eight; no public TCP database listener.
- Non-root systemd services: `pointpass-api` and `pointpass-match`, separate accounts, resource/privilege/filesystem limits. Public firewall allows TCP22/80/443 and UDP7777 only.
- Root/password SSH policy is intentionally unchanged at the owner's request. Do not harden it until a separately approved recovery administrator is verified. An earlier unrestricted passwordless-sudo proposal was rejected and never ran.
- Current release symlink: `/opt/pointpass/current`. API and match protected configuration: `/etc/pointpass/api.env` and `match.env`. These files contain secrets and must never be printed or packaged.
- Root-owned release binaries; writable worker state `/var/lib/pointpass-match`. Result outbox is `pending-result.json`; retry it before replacing a worker lease. A permanently conflicting/corrupt outbox needs operator review rather than silent deletion.
- Use the locally authorized SSH key in place. Verified ED25519 host fingerprint is `SHA256:cinqGMWTldXWlxt9FWNy62f5GcFvvb+00N1/ti71Tv0`. Windows OpenSSH uses `-o KexAlgorithms=curve25519-sha256` with strict host checking. The audit pin file is under the ignored `Logs/OnlinePilotVerification/CurrentAudit` directory.
- The audit used two memory-only invitations. Both were consumed once. The reusable guest credentials are client-local, protected by Windows DPAPI.

Certificate renewal checks run every six hours. The daily expiry guard fails when fewer than 48 hours remain. The trusted certificate served on 9 November expires **2026-11-14 03:18 UTC**. Staging renewal passed, and the rollout drill verified nginx reload and the worker's idle certificate reload. Certificate validation stayed enabled.

Daily bounded database backups, a disposable restore check, limited journals and Nginx rotation are configured. A real backup containing two guest profiles and a completed result was restored successfully. Backups are on the same SSD and do not protect against server loss. No paid backup destination or other paid add-on was enabled. Existing hosting and possible provider traffic/storage overage charges still apply; the actual account tariff was not available here.

## Builds and tests

Use Unity **6000.3.27f2** with Windows and Linux Dedicated Server/Mono support. Menu commands and CLI instructions are in `Tools/Validation/Online/README.md`.

Windows build: `Builds/OnlinePilot/Client/PointPass.exe` plus its entire adjacent data/runtime directory. Linux build: `Builds/OnlinePilot/Server/PointPassServer.x86_64` plus adjacent runtime/data. Both builds compiled without errors. Presentation exclusion passed for Linux. Incidental Unity-generated rendering/project settings changes were removed from this worktree; no original user settings were reset.

Completed automated checks include 409 offline engine assertions, 51 rendered offline checks, 28 headless checks, 16 command-boundary checks, 248 wire-codec checks and 54 distinct isolated Ubuntu/PostgreSQL checks. Windows Unity/NGO negative-admission tests exercised trusted transport, invalid tickets, the wrong TLS name and recovery.

The audit used two Windows clients on one PC over the public Helsinki server. The harness can reuse the protected profiles as follows.

```powershell
./Tools/Validation/Online/Test-OnlinePilotSsh.ps1 -SshKeyPath $key -KnownHostsFile $knownHosts -BlueProfile audit-blue-1109 -RedProfile audit-red-1109 -ReuseProfiles -FullMatch
```

See `Deploy/VERIFICATION.md` for the complete results and resource measurements. The corrected 9 November run finished 2–1 and passed 41 checks, including the two-way passing, curved goals and same-guest reconnect. The full run report is `Logs/OnlinePilotVerification/CurrentAudit/Clients-20261109-retry/two-windows-clients.txt`.

## Important limits and follow-up

- Only one concurrent 1v1 match is implemented and tested; do not infer ten-player capacity or add workers without measurement.
- No client prediction/reconciliation yet. Authoritative input plus 100 ms presentation interpolation adds visible latency. Test by hand on two physical PCs/different access networks before inviting more players.
- The F3 retry/drop estimate is a cumulative reliable-pipeline proxy that includes retransmissions due to delay. It is not an independent IP-loss measurement. Snapshot gaps and round-trip probes are separate. Hidden-player frame-loop FPS is not visible rendering performance.
- The worker records malformed input and ordinary waiting/closing heartbeats in separate counters. The live run recorded one malformed rejection and 242 ordinary closing heartbeats.
- Credential storage and device input/performance have only been validated on Windows. No Android/iOS hardware testing, signing, TestFlight or deployment was done.
- Keep the friend's menu/dribbling/tackle work separate until integration is explicitly requested. Extend server commands and bump the protocol when introducing authoritative gameplay actions.
- Keep future validation labelled automated, manual or pending. Push only to `codex/online-server-integration` under this audit's authorization.

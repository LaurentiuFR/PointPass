# PointPass online pilot handoff

This is the entry point for the next coding agent. Read this file, `README.md`, `DESIGN.md`, `CLAUDE.md`, `Tools/Validation/Online/README.md`, `Backend/README.md`, `Deploy/README.md` and `Deploy/VERIFICATION.md` before editing.

## Workspace and Git

- Current continuation worktree: `C:\Dev\PointPass` (5 November 2026).
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


## Current continuation status — 5 November 2026

Prior implementation/deployment claims below are historical until rechecked. Current access uses the friend's SSH key in place with the exact pinned host fingerprint; no SSH/root policy changes are authorised. Invitations for new automated tests must remain in memory/anonymous pipes, never terminal output, arguments, files or Git. The legacy file-only helper is being adapted before any new invitation is created.

| Item | Current evidence/status |
| --- | --- |
| 1. Final match history | **PASS, live read-only SQL**: match `570b1a48-3b81-47d4-ae54-9d9cdca95e8b`, completed 4 Nov 16:27:53 UTC, score 2–1, one match row and one history occurrence in each of two distinct slots. Repeat with `Tools/Validation/Online/AuditHistory.sql`. |
| 2. Invalid-command counters | Baseline worker aggregate `rejected=489`; category-specific proof still pending. Closing-phase heartbeats must not be mislabelled malformed commands. |
| 3. Separate security/transport checks | HTTPS `/health` 200 with default OS trust; remaining separate cases pending current run. |
| 4. Restart persistence | Two existing profiles observed; controlled restart verification pending an idle check. |
| 5. Exposure | **PASS, live**: UFW default-deny and `ss` expose only application TCP22/80/443 + UDP7777; API stays loopback5000, PostgreSQL has no TCP listener. External TCP22/80/443 connected; 5000/5001/5432 closed/filtered. Public internal route is 404, unauthenticated profile 401. |
| 6. Certificates | **PASS, live**: Certbot 5.8.0, normal OS trust/IP checks, wrong-IP/name rejection, successful staging renewal dry run, idle deploy-hook/reload acknowledgement and expiry guard. Unity/NGO negative-admission/recovery probes also passed without credentials or validation bypass. |
| 7–8. Idle/active resources/capacity | Worker currently peers=0 with stable simulation counter; measured samples pending. No capacity claim beyond one configured room. |
| 9. Backups/retention | **PASS, live drill**: fresh private dump restored 2 profiles/2 verified results into a disposable DB, then that DB was removed. Six successful dumps, each <=1GiB, mode0600 under root0700; no partial files. Seven-dump policy and active daily/logrotate/journal retention verified. Same-SSD/off-host limit documented. |
| 10. Costs | **PASS, documented** in `Deploy/COSTS.md`: owner-reported 10.19/month plus usage charges, published EU traffic/IPv4/optional-storage terms and explicit unknown currency/VAT/account tariff. No paid add-ons enabled. |
| 11. Regressions | Local suites being rerun; record exact fresh outcomes before marking complete. |
| 12. Invitations/Git | Server currently has 2 consumed, 1 unused invitation; do not revoke an owner invitation. Reachable Git scan found no matching secret/generated files; ignore hardening under review. |
| 13. Compatibility | Static audit: operational/test-only changes retain `PointPass-Online-1` / wire1. Recheck final changes before completion. |

No service restart, production configuration change, or full gameplay test has occurred in this continuation at this checkpoint. Menu, HUD, tackling, stamina and dribbling are out of scope. Phone/touch/second-PC testing remains untested.

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
- Existing SSH key path on Larry's machine: `C:\Users\Larry\.ssh\pointpass_hetzner_ed25519`. Use it for SSH only; never read, print, copy, commit or upload its private contents. Verified host fingerprint: `SHA256:cinqGMWTldXWlxt9FWNy62f5GcFvvb+00N1/ti71Tv0`.
- Windows OpenSSH needs `-o KexAlgorithms=curve25519-sha256` with this host. Verified known-hosts file is `C:\Dev\PointPass\Logs\OnlinePilotAudit\known_hosts`. Keep strict host verification enabled.
- Invitations remain in protected server files. Two test profiles were signed in by the owner through the actual game UI. Their reusable credentials are DPAPI-protected client-local files; they are not in the repository/build/delivery. Do not export server invitations without explicit authorization.

Certificate renewal runs every six hours; a daily check warns/fails below 48 hours remaining. The certificate issued for this rollout expires around **2026-11-10 14:52 UTC**. Nginx reloads immediately; the match worker reloads its copied certificate on an idle transport restart. ACME staging/renewal dry-run was unavailable during published maintenance until November 4 at 21:00 UTC: verify it afterwards. No certificate-validation bypass was used.

Daily bounded database backups, a disposable restore check, limited journals and Nginx rotation are configured. A real backup containing two guest profiles and a completed result was restored successfully. Backups are on the same SSD and do not protect against server loss. No paid backup destination or other paid add-on was enabled. Existing hosting and possible provider traffic/storage overage charges still apply; the actual account tariff was not available here.

## Builds and tests

Use Unity **6000.3.27f2** with Windows and Linux Dedicated Server/Mono support. Menu commands and CLI instructions are in `Tools/Validation/Online/README.md`.

Windows build: `Builds/OnlinePilot/Client/PointPass.exe` plus its entire adjacent data/runtime directory. Linux build: `Builds/OnlinePilot/Server/PointPassServer.x86_64` plus adjacent runtime/data. Both builds compiled without errors. Presentation exclusion passed for Linux. Incidental Unity-generated rendering/project settings changes were removed from this worktree; no original user settings were reset.

Completed engine/backend checks: 409 offline regressions, 28 headless Play Mode assertions, 16 command-boundary assertions, 248 wire-codec assertions, 47 distinct actual Ubuntu/PostgreSQL assertions. Windows Unity/NGO negative-admission tests reached the real server with a correctly validated certificate, rejected invalid tickets and a wrong TLS name, and recovered normally afterwards.

Two actual Windows client processes on one PC connect through the public Helsinki server. This is an internet-path test, not proof of two physical PCs or phones. The owner completed first sign-in for profiles `pilot-blue` and `pilot-red`; the harness reuses their protected credentials without invitation files:

```powershell
./Tools/Validation/Online/Test-OnlinePilot.ps1 -FullMatch
```

The first two failures were test-fixture issues, preserved under ignored verification logs: a press before interpolated possession was eligible, and a defender correctly blocking a shot. The development driver now waits for local acceptance, guards cleanup and the scripted defender moves out of the lane through ordinary input. No gameplay was changed to force test success.

Final results, exact snapshot count, active measurements and any remaining tests are recorded in `Deploy/VERIFICATION.md` and `Logs/OnlinePilotVerification/Clients/two-windows-clients.txt`. A real earlier 0–0 match completed through normal overtime, was saved exactly once and returned both clients home. The final 2–1 scenario additionally checks both charged curved goals, common scores/restarts, paused clock on disconnect and successful same-guest rejoin. Consult its final report rather than assuming completion from this document.

## Important limits and follow-up

- Only one concurrent 1v1 match is implemented and tested; do not infer ten-player capacity or add workers without measurement.
- No client prediction/reconciliation yet. Authoritative input plus 100 ms presentation interpolation adds visible latency. Test by hand on two physical PCs/different access networks before inviting more players.
- The F3 retry/drop estimate is a cumulative reliable-pipeline proxy that includes retransmissions due to delay. It is not an independent IP-loss measurement. Snapshot gaps and round-trip probes are separate. Hidden-player frame-loop FPS is not visible rendering performance.
- The worker's aggregate rejection counter currently also counts normal heartbeats arriving during its four-second full-time/result closing phase. These are not proof of cheating; there are no automatic bans. The malformed-command rejection proof was sampled before that closing phase.
- Credential storage and device input/performance have only been validated on Windows. No Android/iOS hardware testing, signing, TestFlight or deployment was done.
- Keep the friend's menu/dribbling/tackle work separate until integration is explicitly requested. Extend server commands and bump the protocol when introducing authoritative gameplay actions.
- Finish any pending verification in `Deploy/VERIFICATION.md`, keep tests labelled automated/manual/pending, and review the full delivery patch before requesting a commit/push. Do not merge main automatically.

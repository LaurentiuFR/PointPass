# PointPass private online pilot

This branch adds one dedicated, authoritative 1v1 room. It uses Unity **6000.3.27f2**, NGO **2.13.3**, Transport **2.7.4**, ASP.NET Core **10** and PostgreSQL **18**. Both footballers are clients. The server runs the existing movement, sprint, possession, charged passes/shots, physical curved ball, scoring, clock and kickoff rules at **50 Hz physics / 30 Hz snapshots**.

The implementation started from `main` commit `6f44996`. Tackling, stamina, revised dribbling and your friend's menu redesign are separate work. Do not merge those changes without checking their command and presentation contracts against the server. No local demo wallet or inventory is uploaded or treated as trusted currency.

## Current verification record

Reports are generated under ignored `Logs/OnlinePilotVerification` and `Logs/PR2Verification`. A build report is evidence of compilation and asset packing, not proof of an internet match.

| Check | Result |
| --- | --- |
| Existing offline engine regressions | 409 assertions passed |
| Physics-only headless composition, physical goals, both players' kicks and restarts | 28 Play Mode assertions passed |
| Charge ownership, cancellation, stale/duplicate releases | 16 Play Mode assertions passed |
| Explicit binary encoding, malformed/truncated/nonfinite payloads, NGO buffer boundaries | 248 assertions passed in Unity |
| Backend/database security, capacity, single-use tickets, orphan reconciliation and restart persistence | 54 distinct assertions passed against the disposable Ubuntu/PostgreSQL API; repeated checks counted once |
| Linux dedicated-server build | Compiled without errors; build report excludes project art/audio/Resources presentation assets |
| Windows client build | Compiled without errors |
| Linux DTLS and Windows Unity/NGO encrypted connection | Correct IP certificate accepted; wrong certificate name/IP and invalid ticket rejected; recovery passed |
| Two actual Windows clients playing a full match | 41 automated live checks passed on 9 November with two Windows processes on one PC. Full-time score 2–1, 6,069 identical shared snapshots, and a unique PostgreSQL result |
| Phone testing | Unavailable; neither Android nor iOS has been validated |

Deployment state, resource measurements, encrypted transport and actual client results must be recorded after running them. See [deployment and operations](../../../Deploy/README.md) and [backend contracts](../../../Backend/README.md).

## Build

Open this worktree in the matching Editor. Save or open `Assets/_Project/Scenes/SampleScene.unity`; an unsaved untitled scene must be resolved before building. Install **Linux Dedicated Server Build Support** and **Linux Mono Build Support** in addition to Windows support.

Use **PointPass > Online Pilot > Build Linux Dedicated Server**, then **Build Windows Client**. Outputs:

```text
Builds/OnlinePilot/Server/PointPassServer.x86_64
Builds/OnlinePilot/Client/PointPass.exe
```

Copy the entire output directory for each executable, including its data and runtime directories. Never copy server environment files, database configuration or TLS private keys into the client build.

The server builder uses an empty server scene, dedicated-server optimizations and Mono. During packing it moves the project's presentation Resources folder through Unity's AssetDatabase and restores it in `finally`, preserving GUIDs and metadata. If an Editor crash interrupts that operation, use **PointPass > Online Pilot > Restore Presentation After Interrupted Build** before a client build. Inspect `Logs/OnlinePilotVerification/server-build.json`; a forbidden project presentation asset fails the build check.

Command-line builds use the same methods:

```powershell
unity run C:/Dev/PointPass/Logs/OnlineServerIntegration --timeout 900 --log-file Logs/server-build.log -- -executeMethod PointPass.EditorTools.OnlinePilotBuild.BuildLinuxServer -nographics
unity run C:/Dev/PointPass/Logs/OnlineServerIntegration --timeout 900 --log-file Logs/client-build.log -- -executeMethod PointPass.EditorTools.OnlinePilotBuild.BuildWindowsClient -nographics
```

Close another Editor using that worktree before a plain batch `unity run`, or invoke the build through its connected Pipeline Editor. The original development worktree can remain separate.

## Join and leave

The configured API is **https://62.238.115.207** and encrypted game transport is **UDP 7777**. Clients need internet access; they do not need router port forwarding or inbound firewall exceptions.

1. Launch the Windows client and select **ONLINE TEST**.
2. Enter a player name and a private pilot invitation for the first sign-in. Invitations are single-use admission credentials; keep them private. Existing guests can leave the invitation blank.
3. Select **JOIN TEST ROOM**. The first player is Blue, the second Red. Both connect to the same Linux worker; neither hosts the simulation. The match starts when both authenticated connections are ready.
4. Use the ordinary movement, sprint, charged pass/shot and curve controls. **F3** toggles FPS, round-trip ping, jitter, a reliable retry/drop estimate, snapshot gaps and bandwidth. The Transport estimate counts retransmissions, including retries caused by delayed delivery; it is not an independent measurement of actual IP packet loss. Snapshot gaps separately count missing gameplay-state messages.
5. Select **LEAVE ONLINE** to return home. A disconnected participant has up to 30 seconds to rejoin their slot; the server pauses the match while waiting. An expired reconnect window abandons that match and frees the room. A completed match records a verified result before room cleanup.

Windows stores a random guest credential protected for the current OS user with DPAPI in the game's persistent-data `OnlinePilot` folder. It is separate from the prototype wallet. Losing that protected file loses access to that guest; a free-form display name is not an account recovery method. Mobile credential storage/signing and physical-device validation remain future work.

## Two actual Windows clients

The opt-in test driver is compiled only for Editor/development clients. It reads file commands and submits ordinary local controller inputs. It cannot place actors, grant possession, change scores or clocks, access a worker credential or submit a trusted result.

Use distinct guest profiles when running two clients on one Windows account. For first sign-in, the pinned SSH runner acquires the shared server test lock, checks the room is idle, and passes single-use invitations directly from SSH output to the clients' input streams. It reports only how many invitations were consumed. Point it at the existing local key and a known-hosts file with the expected Ed25519 entry.

```powershell
./Tools/Validation/Online/Test-OnlinePilotSsh.ps1 -SshKeyPath $key -KnownHostsFile $knownHosts -FullMatch
```

Optional parameters include `-Player`, `-BlueProfile`, `-RedProfile` and `-ReportDirectory`. Add `-ReuseProfiles` with the existing profile names to reuse their Windows-protected guest credentials. The harness creates its own command, state and capture files under the report directory and stops only the processes it launches.

It checks two real Windows processes, client physics disabled, independent movement, shared possession, charged passes in both directions, sprint/cancel, both curved shots/goals, common scores/restarts, reconnecting and exact matching server snapshots. `-FullMatch` additionally waits for the normal-duration match and server completion; a shorter smoke run explicitly leaves that verification pending. Allow the 30-second abandonment window before repeating a smoke run that exits mid-match.

Assess gameplay responsiveness by hand as well. This first pilot uses authoritative movement with snapshot interpolation and does not yet provide client prediction/reconciliation. Its local animation, kick-charge display and motion can therefore lag by the network round trip plus the interpolation delay. Do not describe a successful connection as proof of good play under latency or packet loss.

## Menu integration contract

The menu redesign can call `PointPass.Online.OnlinePilotClient.Instance.Open()` from its Online Test button. `Status`, `Busy`, `HasSession`, `LocalSlot`, `Join(invitation, displayName)` and `Leave()` expose the connection flow without network simulation code in the menu. `OnlinePilotPanel` is a replaceable Canvas adapter. `MatchBootstrap` attaches the flow after constructing the offline world and menu. Offline play, practice and the older development host/client mode remain separate entry paths.

Only the dedicated server creates `ServerMatchWorld`. Clients use `NetworkMatchWorld` presentation with their authenticated slot and forward commands through `PlayerController`/`NetworkPlayerInput`. New gameplay actions require server validation and a coordinated explicit protocol-version change; adding a mobile button alone must not grant a new authoritative action.

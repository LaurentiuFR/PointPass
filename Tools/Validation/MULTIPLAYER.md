# Windows host/client development prototype

This milestone uses Unity **6000.3.27f2**, Netcode for GameObjects **2.13.3** and Unity Transport **2.7.4**. Unity Package Manager resolved these versions against the installed Editor. Official references: [NGO for Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.netcode.gameobjects.html), [Transport for Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.transport.html), and [NGO connection approval](https://mp-docs.dl.it.unity3d.com/netcode/current/basics/connection-approval/index.html).

The default launch still opens the offline game. Multiplayer has separate development controls and does not use or edit the main menu.

## Build

1. Open the project in the matching Unity Editor, with Windows Build Support installed. Allow package resolution and compilation to finish.
2. Choose **PointPass > Multiplayer Development > Build Windows Test Player**.
3. The development build is `Builds/Multiplayer/PointPass.exe`. Keep its adjacent data folder and DLLs together when copying it.

With the project closed in the Editor, the equivalent batch build from the repository root is:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe' -batchmode -nographics -projectPath C:\Dev\PointPass -executeMethod PointPass.EditorTools.MultiplayerDevelopmentBuild.BuildWindows -quit -logFile C:\Dev\PointPass\Logs\NetworkVerification\build-batch.log
```

To use the running Editor as one player, choose **PointPass > Multiplayer Development > Run in Editor**. Choose **Use Offline Mode** to stop it and restore the normal offline Play workflow. This toggle is an Editor session preference, not a scene or project-setting change.

## Two players on one Windows PC

From the repository root, launch these two interactive windows:

```powershell
& .\Builds\Multiplayer\PointPass.exe --pp-host --pp-port 7777 -screen-fullscreen 0 -screen-width 1280 -screen-height 720
& .\Builds\Multiplayer\PointPass.exe --pp-client --pp-ip 127.0.0.1 --pp-port 7777 -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

Alternatively launch both with `--pp-network`, then select **Host** in one and **Join** in the other, using `127.0.0.1` and matching ports. The first player is blue; the joining player is red. The match begins after both have connected. Each window has exactly one local input owner. Keyboard input follows the focused window; switching windows cancels the departing window's held actions without pausing the match. Touch/pointer joystick and action buttons retain their existing controller path.

Controls: WASD/arrows move, Shift sprints using stamina, L tackles, J charges a pass, K charges a shot, Q/E bends a shot, and Escape cancels a charge. Gamepad: left stick moves, RB sprints, West tackles, South passes, East shoots, right stick bends and North cancels. Touch uses the same command route. The HUD gear and Settings open the saved control-layout editor. The development **Restart match (host)** button resets both peers. The HUD pause button cancels local actions; an individual player cannot freeze the shared match.

Use **Disconnect** to leave. Client departure abandons the current match and leaves the host listening for another player. Joining again creates a clean match. Host departure ends the client match. Both can then host/join again without restarting the application.

## Live performance and network statistics

The top-left development overlay is visible by default. Press **F3** in that game window to hide/show it. It is separate from the menus and does not intercept pointer input. Rebuild and relaunch both players to use the latest input fix and overlay.

| Reading | Meaning |
| --- | --- |
| FPS / ms per frame | This instance's game-loop frame rate and average frame interval, sampled every half-second. Hidden/minimized players can have different pacing from a visible game; this is not a GPU profiler. |
| Ping | Last successful round trip to the other peer, using a small echo probe once per second. Includes application/frame scheduling; it is not a one-way latency estimate. |
| Jitter | Smoothed change between successive round-trip measurements. |
| Loss est. | Unity Transport's connection-lifetime reliable-pipeline loss estimate, based on dropped packets and retransmissions. Retransmissions can also follow delays, so this is not an exact count of all lost UDP datagrams. |
| Upload / download | Driver traffic over the last second, in kilobits per second, including networking overhead. |
| State Hz / Gaps / Age | Client snapshot receive rate, missed or superseded snapshot sequences over roughly five seconds, and time since the last received snapshot. Age excludes the additional 100 ms presentation buffer. |

Unavailable network readings show a dash while waiting for a peer. Snapshot gaps are distinct from the transport loss estimate: unreliable snapshots can be skipped even when reliable traffic eventually arrives. Ping and snapshot state reset when the peer/session changes; traffic counters and bandwidth follow the underlying driver's lifetime and rolling window. Telemetry never adjusts the simulation rate or ball physics.

The client kick fix gives the human input handler immediate confirmation that its command was accepted locally, so it remembers which keyboard/gamepad/touch button owns the later release. It does not wait for a charge snapshot before recording that release source. The host still decides possession, charge power and whether a kick can execute. Repeated inactive/focus-loss cancellations are sent once until the next kick, preventing a high frame rate from flooding the input channel.

## Repeat the automated test

After building, run:

```powershell
.\Tools\Validation\Test-WindowsMultiplayer.ps1
```

The script launches two actual Windows players with graphics enabled, plus a temporary third connection for capacity rejection. It feeds ordinary controller commands through an explicitly enabled development-only driver, and logs host/client snapshots. Host-only fixture commands place the starting positions for isolated tests; the passes, shots, pickup, curve, collisions, goals and restarts then use the normal gameplay systems. This does not replace manual keyboard, gamepad or multitouch testing.

Reports, player logs, CSV snapshots and screenshots go to `Logs/NetworkVerification/TwoPlayers`. Build results are in `Logs/NetworkVerification/build.txt`. Repeat the existing offline regression checks with `Tools/Validation/Run-PR2Validation.ps1`; that script builds a disposable project under ignored `Logs` and reuses locally resolved packages.

## Architecture

- `PlayerController` remains the command entry point. Its optional `IPlayerCommandSink` sends a locally controlled client footballer's commands to the host. Offline and host controllers retain the existing movement/kicker/dribbler path.
- `NetworkSession` owns connection approval, the two-player capacity, authenticated sender-to-slot mapping, input sequencing/rate limits, stale-input cancellation, snapshots and shutdown. Clients cannot submit a player identity, position, kick power, ownership, goal or score. Only the sender's assigned footballer receives its validated inputs.
- `NetworkMatchWorld` composes the existing pitch/actor factories and presentation. Only the host creates `MatchManager`; its network initialization skips offline menu, practice and replay setup. The existing scoring, periods, golden goal, celebration and reset rules run on the host.
- The host simulates all three dynamic bodies. Client rigidbodies and colliders do not simulate; movement, dribbling, ball physics and kicking components are disabled there. Received state controls presentation and possession feedback.
- Commands use reliable sequenced NGO named messages, with movement/sprint heartbeats at 30 Hz. Every command includes retained aim, so a direction tap shorter than a heartbeat still reaches the kicker after movement stops. Host snapshots use unreliable sequenced messages at up to 30 Hz. Clients interpolate approximately 100 ms behind the latest state, and snap cleanly across explicit reset versions. Animation velocity, kick events and ball roll follow replicated state.
- Actors are spawned through the existing runtime factories, rather than network prefab instances. NGO manages the connection and custom messages; the server's fixed two-slot roster manages actor lifecycle and ownership. No prefab or scene edits are needed. NGO may generate an empty default prefab-list asset during import.
- The connection payload checks `PointPass-PC-4` and `Application.version`. Tackle commands and replicated stamina/tackle fields use input/state message version 3. Older builds are rejected. Rebuild and distribute the same complete player folder to both players. This is a version check, not account authentication.

The simulation rules can initialize without a HUD. A later dedicated-server milestone must also remove actor/world presentation creation and local-player composition; this milestone is a player-hosted match.

## Current limits

- Network matches have two human players only, with one on each team. Offline goalkeeper AI and clubhouse preview squad slots are not created in the network world. No network team selection, public matchmaking, join codes, cloud setup or paid services.
- No client prediction or reconciliation yet. Client controls and charge feedback include network delay and the presentation buffer. Automated loopback impairment has been checked; second-PC LAN/internet feel still needs hands-on validation.
- Offline highlights/practice remain offline; network goal replay is not implemented. Client presentation reproduces goals, player animations and crowd reactions, but physical net-impact ripple events are not yet replicated.
- The host listens on IPv4 UDP; this milestone is verified on loopback. Second-PC Wi-Fi and physical iPhone testing are separate milestones. No router forwarding is needed for a later LAN test. Do not disable Windows Firewall; if LAN access needs a rule, limit it to the test executable, selected UDP port, Private profile and local subnet.
- The clubhouse main menu is integrated with offline gameplay. Its online match-format controls remain labelled previews; actual multiplayer still uses the separate temporary development panel and launch arguments above.

## Validation record

Completed on **3 November 2026**, on Windows, from `codex/authoritative-multiplayer`:

- Windows development build succeeded with **zero build errors**.
- Two actual standalone processes passed independent movement, both players' held sprint, physical passes in both directions, shared possession, charged client kicks, charge cancellation without a delayed release, exact retained aim after a sub-heartbeat direction tap, curved shots, rejection of kicks without possession, a physical goal, shared score/clock, goal celebration and kickoff reset.
- The third connection was rejected as full. Client disconnect/rejoin, host departure, hosting again on the same port, reconnecting both peers and explicit host restart passed without duplicate footballers.
- **770** matching snapshot sequences had identical ball/player positions and velocity, possession, scores, clock, kick counters and curve. Client dynamic bodies were confirmed unsimulated.
- Host and client logs contained no runtime exceptions. Direct camera captures from each process were inspected for the full pitch, stadium, players, ball, goals and UI. These captures deliberately render offscreen because a hidden Windows player may return a black desktop screenshot.
- The **371** existing offline engine checks passed again after the final controller change. The **30** existing offline Play Mode checks passed, including default main-menu startup, pause/resume, goal physics and golden goal.

The final report for each run is `Logs/NetworkVerification/TwoPlayers/tests.txt`; captures are `host.png` and `client.png` in the same directory. Read the report together with player logs and matching snapshot sequences. Automated local controller commands establish network simulation behavior; physical keyboard/gamepad feel and real mobile multitouch still require hands-on testing. The later tackling/mobile validation below adds controlled latency/loss checks. A second PC, iPhones and internet sessions remain untested.

The expanded suite also exercises Red through the real `HumanPlayerInput` keyboard and HUD button handlers: an immediate touch-shot press/release before a snapshot acknowledgement, a held keyboard shot, and a touch pass. It checks live FPS/ping/jitter, transport packet counters, bandwidth and snapshot rate, plus known sequence-gap/reordering/wrap/reset cases. These synthetic input events run in two standalone players; physical keyboard/controller and touchscreen playtests remain useful. The earlier controller-only tests missed the delayed-release-source bug reported during manual playtesting.

The client-input/diagnostics follow-up passed on **3 November 2026** with **916** matching snapshots. Its report and reviewed screenshots are in `Logs/NetworkVerification/InputAndDiagnostics`. The shared-input change also passed all **371** offline engine checks and **30** offline Play Mode checks. The hidden-player harness explicitly enables synthetic keyboard input in the background; ordinary game windows retain their existing focus behavior.

## Tackling, stamina and mobile controls

See [PLAYER_FEATURES.md](PLAYER_FEATURES.md) for tuning, mobile controls, current test evidence and device limitations. The host now also resolves tackles and sprint budgets. Client presentation interpolates continuous positions/stamina while preserving a snapshot's discrete possession and tackle events until their presentation time.

```powershell
.\Tools\Validation\Test-TacklingStaminaMultiplayer.ps1 -Port 7787
.\Tools\Validation\Test-TacklingStaminaMultiplayer.ps1 -Port 7797 -LatencyMs 70 -ReportDirectory C:/Dev/PointPass/Logs/PlayerFeatures/Multiplayer-Impaired
```

The second command uses a localhost UDP proxy with 70 ms delay per leg, ±15 ms jitter and 1% configured packet loss. Each client endpoint has an independent upstream endpoint so reconnects behave like separate real UDP connections. It changes neither game authority nor system network/firewall settings. Both commands launch two real Windows development players and clean them up afterward.

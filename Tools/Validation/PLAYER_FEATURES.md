# Tackling, dribbling, stamina and mobile controls

Implemented on `codex/tackling-stamina-mobile-controls`, based on main `6f44996`. This feature branch is prepared for review and merging separately from main.

## Gameplay

- **Tackle:** L / gamepad West / touch Tackle. Face an exposed ball within 1.05 world units. A 90 ms windup leads into a 130 ms contact window. Body shielding prevents winning through an opponent's back. Success knocks the ball loose with real velocity; it does not assign possession or teleport the ball. Miss recovery lasts about 0.76 seconds overall. Challenges slow the player and block kicks. Green contact feedback means success; red feedback means a miss, with a cooldown ring and short HUD message.
- **Dribbling:** pickup requires the ball within the foot contact envelope (0.25 radius around the foot position, and within 0.75 of the body). The lead is approximately 0.53, with a small stride variation and 0.09 extra during sprinting. Boot touches happen at intervals; the old continuous positional spring is removed. Sharp turns and overrun can release possession. Opponents can intercept the ball or tackle it.
- **Stamina:** five seconds of continuous moving sprint drains a full budget. Recharge starts after 0.65 seconds and takes 2.8 seconds from empty. Standing sprint does not drain it. Exhaustion removes the speed boost; release sprint and recover at least 25% to enable it again. The Sprint ring shows remaining stamina, interpolating green/yellow/red; RECOVER indicates the exhaustion latch. The same fixed-step budget serves every input method and is replicated from the host.

Both peers must use a fresh build: protocol **PointPass-PC-4**, input/state messages **version 3**. The client runs no independent ball/player physics. This prototype still has a 100 ms presentation buffer and no client prediction; latency is perceptible in remote actions.

## Mobile control layout

Open **Settings → Touch Control Layout**, or the HUD gear. Select and drag Joystick, Pass, Shoot, Sprint, Tackle or Cancel. Adjust Size, Opacity and Joystick Sensitivity in the live preview. **Left-handed preset** mirrors all six positions. **Reset to Default** restores the default layout. **Save Controls** persists locally using `PointPass.MobileControls.v1`; Cancel discards edits.

Control bounds are clamped inside `Screen.safeArea`. Targets retain at least 44 screen pixels at small tested resolutions. Overlapping targets produce an amber warning; the editor allows saving a deliberate overlap. Independent pointer ownership prevents one finger from releasing another control. Gameplay input is suppressed while editing and held actions must be released on return. In network play the other player and match keep running.

## Rendering budget

Android/iOS use a runtime clone of the project's URP asset, leaving the saved desktop pipeline unchanged. The policy targets 60 Hz, uses 2x MSAA, disables unused opaque/depth camera copies and HDR, and uses 90% world render scale when reported GPU memory is at most 2 GB. Overlay UI remains at native resolution. Both landscape orientations are supported. Confetti remains pooled, with 48 mobile pieces instead of 72 desktop pieces. These are conservative starting budgets, not a verified phone frame rate.

## Validation evidence — 3 November 2026

| Check | Result / evidence |
| --- | --- |
| Unity Windows development build | Succeeded, zero errors; `Logs/NetworkVerification/build.txt` |
| Offline engine regression | 410 checks passed, including straight sprint boot touches and loose-ball reversals; `Logs/PR2Verification/engine-tests.txt` |
| Real Unity Play Mode | 101 checks passed: keyboard/controller command routing, independent touches, pass/shoot flicks, tackle input, stamina/exhaustion/recharge, close pickup, layout persistence, mirror/reset/drag and cleanup; `Logs/PlayerFeatures/Editor/tests.txt` |
| Native Editor viewports | 844×390, 1280×720, 1024×768, 2340×1080; distinct default controls, minimum targets and synthetic side/gesture safe-area clamps passed |
| Two standalone Windows players | Tackles, misses, positioning, spam recovery, touch tackle, shared stamina, exhausted speed, goals and disconnect/rejoin passed; 662 matching raw state snapshots; `Logs/PlayerFeatures/Multiplayer-Loopback/tests.txt` |
| Existing standalone regression | 50 checks passed, including passes both ways, retained aim, curved shots, goals, capacity rejection, rehost and reconnect; 989 matching snapshots; `Logs/PlayerFeatures/Multiplayer-Regression/tests.txt` |
| Same two players with impairment | 70±15 ms each direction, 1% configured loss; 709 matching snapshots, including possession, ball/player positions, stamina and tackle events; reconnect and host departure passed; `Logs/PlayerFeatures/Multiplayer-Impaired/tests.txt` |
| Android and iOS C# player compilation | Both passed using Unity PlayerBuildInterface; `Logs/PlayerFeatures/MobileCompile/results.txt`. This is script compilation, not APK/IPA, IL2CPP/native linking or device execution. |

The final impairment run measured about 123 ms client RTT at the captured goal moment, within the configured 110–170 ms delay range. Timing samples cover sprinting, tackles and a goal celebration before test screenshots/readbacks: peak recorded main-thread time was approximately 19–20 ms on these hidden Windows processes. Peak recorded GC was approximately 23–25 KB per frame, including the development driver's CSV/JSON reporting. Hidden-player frame intervals do not measure displayed rendering or GPU performance. Raw samples are `busy-host-profile.json` and `busy-client-profile.json` in the impairment directory.

### Not established by these checks

No Android device appeared in `adb devices`, and no iPhone was available. Physical multitouch, controller hardware, notches/gesture navigation, thermal/battery behaviour and sustained mobile GPU/frame pacing remain untested. No native Android/iOS build or device profiler capture is claimed. Windows cannot perform the required Xcode/iOS signing and native device build. Two machines over Wi-Fi, internet/NAT traversal, mobile suspend/resume during a connection and manual competitive feel also remain to be checked. Online matchmaking and larger squads remain previews; the working development network mode is two-player host/join.

## Repeat

1. Rebuild through **PointPass → Multiplayer Development → Build Windows Test Player**; launch both players from the same build folder.
2. Run the two PowerShell commands in [MULTIPLAYER.md](MULTIPLAYER.md). Keep logs/snapshots with the test report.
3. In the real project's offline Play Mode, with CLI auto tick enabled, run:

```powershell
unity command script run --caller plugin --skill ui-ugui --format json -- --file Tools/Validation/GameplayMobilePlayMode.cs --entry PointPass.Validation.GameplayMobilePlayMode.Run --timeout 60
```

4. On each target phone, play with three fingers (move, sprint, pass/shot), cancel a charge, curve a shot, tackle while moving, exhaust/recover, rotate between landscape orientations and save/relaunch a custom layout. Capture CPU/GPU/allocations during repeated tackles and both teams' celebrations. Then connect two devices to the same host and repeat possession, goals and disconnect/rejoin checks under real Wi-Fi latency.

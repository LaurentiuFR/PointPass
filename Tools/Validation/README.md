# PR 2 engine validation

For the Windows host/client prototype, use the separate [multiplayer build and two-instance verification guide](MULTIPLAYER.md).

Run from PowerShell after opening PointPass in Unity at least once:

```powershell
./Tools/Validation/Run-PR2Validation.ps1
```

Use `-UnityEditor 'path/to/Unity.exe'` if the editor is installed elsewhere. Use the version in `ProjectSettings/ProjectVersion.txt`.

The script copies current runtime source, shader includes and validation helpers into `Logs/PR2Validation`, reuses installed Input System/uGUI packages, and runs assertions inside Unity in batch mode. It does not edit the main project's scenes, settings or metadata. The bootstrap factories are included for replay construction; the editor-only harness does not enter Play Mode. Reports go to `Logs/PR2Verification/engine-tests.txt` and `unity-validation.log`. These generated directories are ignored by Git. Logs is used because Unity clears Temp when opening the main project.

Checks cover charged kick powers, immediate/retained aim, ownership, queued execution, cancellation, sprint composition, reversal braking, independent fingers, physical ball travel, reception, wall rebound and synthetic gamepad/keyboard input. The harness explicitly invokes lifecycle/physics methods in an isolated editor scene; charge time is injected to cover minimum, intermediate and maximum values reproducibly.

This is engine-level validation, not a Play Mode or device test. It does not establish frame-by-frame match flow, input latency, real touchscreen behavior or gameplay feel. Use the Play Mode checks and the real project before merging.

Additional helpers validate bounded turning, completing turns after stick release, kick-animation isolation, ball trails/reset, presentation event ordering and menu/pause/input/navigation contracts. Inspect `engine-tests.txt` for the current assertion count and results.

The stadium/curve pass adds checks for eased steering, arm counter-swing and elbow articulation, physical left/right shot trajectories, curve fading and cancellation, Shoot-button flick routing, and stadium mesh/collider boundaries. Goal checks cover power-dependent localized net flex, anchored front ropes, settling/reset, net drag restoration, crowd celebration/reset and overhead sorting. Flick tests use synthetic pointer events; tuning still needs real touchscreen play.

For visual QA, run `./Tools/Validation/Render-AppearancePreview.ps1`. It creates a separate `Logs/AppearancePreview` project, copies the current runtime scripts and art, and uses the installed Unity editor with graphics enabled to render match-scale, close-up, kick-pose and red-kit previews. It needs a working graphics device. Result images and the render log go to `Logs/AppearanceVerification`. The preview uses a staged editor scene and does not save or change the production scene or default roster.

The same renderer includes main-menu (16:9 and 4:3), controls, pause and results captures. These are staged editor renders for layout inspection.

It also captures goal layering/impact and idle/celebrating crowd close-ups. Check `goal-layering-preview.png`, `goal-impact-preview.png`, `crowd-idle-preview.png`, `crowd-goal-preview.png` and the two running poses. The real Play Mode smoke test now fires hard/soft shots into opposite goals and checks continued ball motion, net energy absorption, crowd reaction, duplicate-score prevention, pause during celebration, kickoff and golden-goal results.

Match-day previews include `floodlight-preview.png`, `broadcast-camera-preview.png`, `referee-idle-preview.png`, `referee-goal-preview.png` and `stadium-celebration-preview.png`. The renderer copies the real turf shader as well as the art. Engine checks cover inward elbows and facing-aligned arm travel, visibly raised fan hands, the referee signal/layering/reset, visible confetti opacity/cadence/expiry/pause, four towers, six broadcast rigs, and lighting/marking draw order. Play Mode verifies that a real goal starts the referee and confetti and that kickoff clears both. Projected 2D lighting and sprite shadows should also be assessed on a target phone; no device frame-rate claim is made by these checks.

Run `./Tools/Validation/Render-AppearancePreview.ps1 -ReviewMatch` for a graphics-enabled Play Mode review. It uses the real bootstrap, menu, moving/jogging/sprinting player, a charged shot, both teams' goal reactions, scorer/own-goal attribution, saved replay, pause/back/resume and kickoff. It captures the UI at 1280x720, 1920x1080 and 2560x1440, plus gameplay, flashes, scorer emote, replay and haze. Outputs and `review.txt` are in `Logs/MatchVisualReview`. Frame-time and GC samples are taken during the running match, after warm-up and excluding screenshot frames. The report records the actual editor game-view resolution separately from the screenshot resolutions; editor timing includes editor overhead and is not a phone benchmark.

Goal-moment engine checks additionally cover opposite supporter reactions, team-section confetti, local flash/reset/pause, haze-only beams, bounded replay history, actual clip persistence and restore, replay isolation, and scorer-pose reset. JSON serialization is included in both disposable projects to exercise the real save path.

Replay checks now cover complete factory-built world presentation, disabled physics, recorded articulated poses/ball rotation, real strike-event capture, the full celebration tail, all four playback speeds, independent arrow controls and camera shutdown. Theme tests cover persistence, live HUD/menu updates and unchanged replay settings. Lighting tests cover matching fixture pools, replay origins, directed shadows and dry/wet material state.

The appearance renderer also exports all three colour schemes plus menu/controls layouts at 720p, 1080p, 1440p and tablet aspect ratios, with a portrait layout stress capture. Visible label/button bounds are checked during capture. The disposable project uses a minimal rendering setup; additionally inspect the actual project's Linear URP output for shader and colour-space issues.

Offline improvements add engine checks for real physical gate crossings, one-hit-per-pass rules, session/reset/pause isolation, touch cancellation and stale-release suppression, bend feedback, replay transport and sound preferences/cue generation. Audio assertions verify finite bounded waveforms and cue dispatch; they do not establish subjective sound quality or device audibility.

Run `./Tools/Validation/Render-AppearancePreview.ps1 -VerifyOffline` for the new graphics-enabled Play Mode integration checks. These exercise Sound navigation/controls, practice entry, a physical pass hitting a gate, pause/restart, mobile charge cancellation, returning to a normal match, and actual replay Pause/Restart/Resume button callbacks. Reports go to `Logs/OfflineVerification/playmode-tests.txt`; captures use `Logs/MatchVisualReview/offline-*.png`. This mode must run separately from the other appearance-project runs. The standard renderer also captures the Sound page at desktop/phone/tablet sizes.

Before committing, playtest soft/hard/off-centre shots into both nets; walk into each goal; watch crowd and arm motion at run/sprint speed; pause during a goal and restart the match. Assess image quality in a Game view at 1x or a fixed 1920x1080 resolution rather than enlarging a low-resolution preview. Device frame rate, touchscreen behavior and the subjective animation/shot feel still need hands-on testing.

Run `./Tools/Validation/Render-AppearancePreview.ps1 -VerifyMenus` for a real Play Mode smoke test in that disposable project. It enters Play Mode, exercises the actual startup and kickoff coroutines, pauses/resumes, restarts, returns home, navigates controls, shortens a test-only final-period clock, checks Results and starts a rematch. It exits Play Mode and Unity automatically, with a 45-second timeout. Reports are `Logs/AppearanceVerification/playmode-tests.txt` and `menu-playmode.log`. No production match durations or scene files are changed.


## Live goalkeeper and audio review

With the real project open in Unity, use the installed Pipeline CLI:

```powershell
unity command set_auto_tick --project-path C:/Dev/PointPass --caller plugin --skill unity-cli
unity command editor.playmode start --project-path C:/Dev/PointPass --caller plugin --skill unity-cli
unity status --until-ready --project-path C:/Dev/PointPass
unity command script run --project-path C:/Dev/PointPass --timeout 65 --caller plugin --skill unity-cli -- --file Tools/Validation/GoalkeeperPlayMode.cs --entry PointPass.Validation.GoalkeeperPlayMode.Run --timeout 60
```

The review operates the actual menu and physics in the main project's Play Mode. It temporarily disables human input, creates a test receiver, exercises 10m tap and 14m charged passes, catches/distributions at both goals, pause during a keeper distribution, hard-shot parrying, reaction-delayed diving, beatable corner shots, mirrored crowd audio, bounded mix output, new highlights and keeper replay rigs, and practice/reset flow. It restores sound preferences and human input and returns to Main Menu. Do not manually stop Play Mode while it runs. It resumes incidental editor-focus pauses and simulates game focus for sound playback during the review; production focus-pause behaviour remains unchanged.

Reports and native Game-view captures go to ignored `Logs/KeeperVerification`. Frame time and GC measurements include editor, replay-recording and validation overhead and do not establish mobile performance. The native menu capture, goal moment, keeper dive/replay and sound settings should also be visually inspected. Headphone balance, real touchscreen input and keeper difficulty still benefit from hands-on playtesting.


## PointPass lobby review

`LobbyValidation` is included in the engine regression suite and covers all squad sizes, locked/central slots, preview guest input, six-page routing, persistent gold/points purchases, duplicate-purchase protection, inventory equipment and sound settings.

For the current front end, run the real-project review in Play Mode:

```powershell
unity command script run --project-path C:/Dev/PointPass --timeout 65 --caller plugin --skill unity-cli -- --file Tools/Validation/FigmaMenuPlayMode.cs --entry PointPass.Validation.FigmaMenuPlayMode.Run --timeout 60000
```

This current clubhouse review checks pointer navigation, all three saved colour choices, the rendered footballer, My Club's URP camera targets, passing practice, free play, a penalty preset and keeper difficulty through the actual buttons. It captures native 844x390, 1280x720, 1920x1080 and 1280x960 views, restores the prior view and input/theme preferences, and checks that rendering and viewport cleanup emit no runtime errors. Reports and screenshots are in ignored `Logs/PdfMenuReview`. Avoid manually stopping Play Mode during the review. `LobbyPlayMode.cs` targets the superseded five-pod layout; the engine suite still covers wallet persistence and six-page data routing. The disposable `-VerifyMenus` smoke test has also been updated for the clubhouse and defending keepers.

See [the menu/multiplayer integration record](MENU_INTEGRATION.md) for the combined branch, compatibility decisions and validation results.

See [the tackling and mobile controls record](PLAYER_FEATURES.md) for the stamina/tackle controls, layout editor, current mobile compilation and screen-size checks, two-client latency test commands, and physical-device testing limits.

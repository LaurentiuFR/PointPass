# PR 2 engine validation

Run from PowerShell after opening PointPass in Unity at least once:

```powershell
./Tools/Validation/Run-PR2Validation.ps1
```

Use `-UnityEditor 'path/to/Unity.exe'` if the editor is installed elsewhere. Use the version in `ProjectSettings/ProjectVersion.txt`.

The script copies current gameplay source into `Logs/PR2Validation`, reuses the installed Input System and uGUI packages, and runs assertions inside Unity in batch mode. It does not edit the main project's scenes, settings or metadata. Reports go to `Logs/PR2Verification/engine-tests.txt` and `unity-validation.log`. These generated directories are ignored by Git. Logs is used because Unity clears Temp when opening the main project.

Checks cover charged kick powers, immediate/retained aim, ownership, queued execution, cancellation, sprint composition, reversal braking, independent fingers, physical ball travel, reception, wall rebound and synthetic gamepad/keyboard input. The harness explicitly invokes lifecycle/physics methods in an isolated editor scene; charge time is injected to cover minimum, intermediate and maximum values reproducibly.

This is engine-level validation, not a Play Mode or device test. It does not establish frame-by-frame match flow, input latency, real touchscreen behavior or gameplay feel. The normal match bootstrap is excluded intentionally. Test those in the real project before merging.

Additional helpers validate bounded turning, completing turns after stick release, kick-animation isolation, ball trails/reset, presentation event ordering and menu/pause/input/navigation contracts. Inspect `engine-tests.txt` for the current assertion count and results.

The stadium/curve pass adds checks for eased steering, arm counter-swing and elbow articulation, physical left/right shot trajectories, curve fading and cancellation, Shoot-button flick routing, and stadium mesh/collider boundaries. Goal checks cover power-dependent localized net flex, anchored front ropes, settling/reset, net drag restoration, crowd celebration/reset and overhead sorting. Flick tests use synthetic pointer events; tuning still needs real touchscreen play.

For visual QA, run `./Tools/Validation/Render-AppearancePreview.ps1`. It creates a separate `Logs/AppearancePreview` project, copies the current runtime scripts and art, and uses the installed Unity editor with graphics enabled to render match-scale, close-up, kick-pose and red-kit previews. It needs a working graphics device. Result images and the render log go to `Logs/AppearanceVerification`. The preview uses a staged editor scene and does not save or change the production scene or default roster.

The same renderer includes main-menu (16:9 and 4:3), controls, pause and results captures. These are staged editor renders for layout inspection.

It also captures goal layering/impact and idle/celebrating crowd close-ups. Check `goal-layering-preview.png`, `goal-impact-preview.png`, `crowd-idle-preview.png`, `crowd-goal-preview.png` and the two running poses. The real Play Mode smoke test now fires hard/soft shots into opposite goals and checks continued ball motion, net energy absorption, crowd reaction, duplicate-score prevention, pause during celebration, kickoff and golden-goal results.

Match-day previews include `floodlight-preview.png`, `broadcast-camera-preview.png`, `referee-idle-preview.png`, `referee-goal-preview.png` and `stadium-celebration-preview.png`. The renderer copies the real turf shader as well as the art. Engine checks cover inward elbows and facing-aligned arm travel, visibly raised fan hands, the referee signal/layering/reset, visible confetti opacity/cadence/expiry/pause, four towers, six broadcast rigs, and lighting/marking draw order. Play Mode verifies that a real goal starts the referee and confetti and that kickoff clears both. Projected 2D lighting and sprite shadows should also be assessed on a target phone; no device frame-rate claim is made by these checks.

Run `./Tools/Validation/Render-AppearancePreview.ps1 -ReviewMatch` for a graphics-enabled Play Mode review. It uses the real bootstrap, menu, moving/jogging/sprinting player, a charged shot, both teams' goal reactions, scorer/own-goal attribution, saved replay, pause/back/resume and kickoff. It captures the UI at 1280x720, 1920x1080 and 2560x1440, plus gameplay, flashes, scorer emote, replay and haze. Outputs and `review.txt` are in `Logs/MatchVisualReview`. Frame-time and GC samples are taken during the running match, after warm-up and excluding screenshot frames. The report records the actual editor game-view resolution separately from the screenshot resolutions; editor timing includes editor overhead and is not a phone benchmark.

Goal-moment engine checks additionally cover opposite supporter reactions, team-section confetti, local flash/reset/pause, haze-only beams, bounded replay history, actual clip persistence and restore, replay isolation, and scorer-pose reset. JSON serialization is included in both disposable projects to exercise the real save path.

Before committing, playtest soft/hard/off-centre shots into both nets; walk into each goal; watch crowd and arm motion at run/sprint speed; pause during a goal and restart the match. Assess image quality in a Game view at 1x or a fixed 1920x1080 resolution rather than enlarging a low-resolution preview. Device frame rate, touchscreen behavior and the subjective animation/shot feel still need hands-on testing.

Run `./Tools/Validation/Render-AppearancePreview.ps1 -VerifyMenus` for a real Play Mode smoke test in that disposable project. It enters Play Mode, exercises the actual startup and kickoff coroutines, pauses/resumes, restarts, returns home, navigates controls, shortens a test-only final-period clock, checks Results and starts a rematch. It exits Play Mode and Unity automatically, with a 45-second timeout. Reports are `Logs/AppearanceVerification/playmode-tests.txt` and `menu-playmode.log`. No production match durations or scene files are changed.

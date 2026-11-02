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

The stadium/curve pass adds checks for eased steering and arm counter-swing, physical left/right shot trajectories, curve fading and cancellation, Shoot-button flick routing, and static stadium mesh/collider boundaries. Flick tests use synthetic pointer events; tuning still needs real touchscreen play.

For visual QA, run `./Tools/Validation/Render-AppearancePreview.ps1`. It creates a separate `Logs/AppearancePreview` project, copies the current runtime scripts and art, and uses the installed Unity editor with graphics enabled to render match-scale, close-up, kick-pose and red-kit previews. It needs a working graphics device. Result images and the render log go to `Logs/AppearanceVerification`. The preview uses a staged editor scene and does not save or change the production scene or default roster.

The same renderer includes main-menu (16:9 and 4:3), controls, pause and results captures. These are staged editor renders for layout inspection.

Run `./Tools/Validation/Render-AppearancePreview.ps1 -VerifyMenus` for a real Play Mode smoke test in that disposable project. It enters Play Mode, exercises the actual startup and kickoff coroutines, pauses/resumes, restarts, returns home, navigates controls, shortens a test-only final-period clock, checks Results and starts a rematch. It exits Play Mode and Unity automatically, with a 45-second timeout. Reports are `Logs/AppearanceVerification/playmode-tests.txt` and `menu-playmode.log`. No production match durations or scene files are changed.

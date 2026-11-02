# PointPass

Mobile game for iOS (App Store) and Android (Google Play).

## Status
Early development.

Offline prototype: movement, physical ball control, scoring and timed matches. The PR 2 feature branch adds charged passing/shooting and held sprint through the shared player controller.

## Prototype controls
- Move: WASD/arrows, gamepad left stick, or the on-screen joystick.
- Pass: hold **J** or **PASS**, then release.
- Shoot: hold **K** or **SHOOT**, then release.
- Curve shots: flick sideways from **SHOOT** as you release. The bend follows the sideways component relative to your aim; a normal release stays straight. Keyboard: hold **Q/E** during a shot for left/right bend. Gamepad: use the **right stick** during a shot.
- Sprint: hold **Left Shift** or **SPRINT**.
- Cancel a charge: **Escape**. Gamepad: **South** pass, **East** shoot, **Right Shoulder** sprint, **North** cancel.
- Pause/resume: **P**, gamepad **Start**, or the HUD pause button. In menus, use pointer/touch or arrows/stick plus **Enter/South**; **Escape/East** goes back.

## Menus
The prototype opens on a main menu with **Play Offline** and **How to Play**. The pause menu offers Resume, Controls, Restart Match and Main Menu. Full time shows the final score with explicit **Play Again** and **Main Menu** actions.

Pausing retains possession, positions, momentum, match period and clock. Returning home abandons the current match; replay starts a clean kickoff. Focus loss/backgrounding pauses an active match, and returning to the app waits for Resume. Menu input is consumed before gameplay resumes to prevent an accidental pass or sprint.

Menus are generated at runtime from scripts; no scene setup or build-settings changes are required. The default mode remains an offline, single-player prototype.

Kicks follow the current movement direction immediately and retain that exact direction when the stick is released. A short guide shows the intended trajectory while charging. Passes reach full power in **0.65 seconds**; shots take **1 second**. Holding longer keeps maximum power. You can move and sprint while charging. Passing and shooting require possession, and the first kick action pressed owns the charge. Sprint has no stamina in this milestone.

Current feel tuning: run **4.8** world units/second, sprint **6.48**, with a small dribbling slowdown. Pass speed ranges from **8–14**, shot speed from **10–20**; forward movement adds a small follow-through contribution. Velocity eases toward the new direction with a **0.12-second** steering response and **0.09-second** braking response, preserving a brief arc through sudden turns. Kick buttons dim without possession; the HUD shows contextual controls. These are prototype tuning values that still need hands-on device playtesting.

Facing turns at **840 degrees/second**: a 90-degree turn settles in about **120ms** and a reversal in **220ms** at 50Hz. A quick stick release finishes the intended turn. Kick aim still follows the command immediately. The local player has a subtle identification ring, animated running lean, separate arms swinging opposite the legs, and a visible kick/recovery pose. The ball has direction-aware visual rolling and a short trail on fast kicks.

Curved shots rotate the ball's physical velocity gradually, without moving its position directly or adding speed. Full bend starts at **72 degrees/second**, fades over **1.1 seconds**, and clears on collision, pickup or reset. A curved guide and bend label provide charge feedback. Passing stays straight. Mobile flicks use recent motion scaled to the UI canvas; long holds and slow drags are not intended to produce bend.

Updated player, boot and ball sprites live alongside the originals as `*_polished.png`. The animated player uses `*_rigged.png` torsos plus the separate `arm_runner.png` limb. They use transparent backgrounds and a 512-pixel import limit. See [sprite prompts and provenance](Tools/Art/SPRITE_PROMPTS.md). Render actual game art without changing a scene using `./Tools/Validation/Render-AppearancePreview.ps1`; outputs go to `Logs/AppearanceVerification`. These staged editor renders complement gameplay tests and do not replace on-device testing.

The pitch uses a separate, lightly toned stadium-turf texture with subtle mowing bands and accurate code-drawn white markings. Static runoff, perimeter boards, tiered seats/spectators, aisles and floodlight structures provide a stadium surround; these decorations are batched into two meshes and add no colliders. A modestly wider camera shows the enclosure. Existing pitch dimensions, walls and goal triggers are preserved. See the [grass generation prompt](Tools/Art/STADIUM_PROMPT.md).

The default test match has one human-controlled player. Multiplayer and AI are not implemented.

## Validation
See [the repeatable Unity engine checks](Tools/Validation/README.md). The checks use a separate temporary project and leave the main scene untouched. The real menu/coroutine smoke test runs with `./Tools/Validation/Render-AppearancePreview.ps1 -VerifyMenus`. Real-device multitouch, safe-area appearance, hardware controller navigation and gameplay feel still need hands-on testing.

## Tech
- Unity (Universal 2D template), C#
- Git + Git LFS

## Getting started
1. Install Unity Hub and the same editor version as the owner (see `ProjectSettings/ProjectVersion.txt`).
2. Install Git and Git LFS, then run `git lfs install` once.
3. Clone the repo somewhere **outside OneDrive** (e.g. `C:\Dev`):
   `git clone https://github.com/LaurentiuFR/PointPass.git`
4. In Unity Hub choose **Add > Add project from disk** and select the cloned folder.

## Project structure
```
Assets/_Project/
  Scripts/
  Scenes/
  Prefabs/
  Art/
  Audio/
```

## Team workflow
- Never commit directly to `main`. Branch as `yourname/feature-name` and merge by pull request.
- `git pull` before starting work.
- Commit `.meta` files with their assets. Never edit or delete them by hand.
- Each person owns different scenes and prefabs to avoid merge conflicts.

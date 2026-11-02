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

Updated player, boot and ball sprites live alongside the originals as `*_polished.png`. The animated player uses `*_rigged.png` torsos and reuses regions of `arm_runner.png` for shoulder/elbow articulation, mirrored counter-swing and shot balance. Detailed sprites use a 1024-pixel import limit, trilinear filtering and mipmaps; gameplay enables 4x MSAA at native render scale. Import settings refresh automatically after editor compilation, or through **PointPass > Refresh presentation quality**. See [sprite prompts and provenance](Tools/Art/SPRITE_PROMPTS.md). Render actual game art without changing a scene using `./Tools/Validation/Render-AppearancePreview.ps1`; outputs go to `Logs/AppearanceVerification`. These staged editor renders complement gameplay tests and do not replace on-device testing.

The pitch retains its natural grass texture, remapped by `NaturalTurf.shader` to softer greens with reduced contrast, gentle texture filtering, subtle mowing bands and irregular colour variation. White markings remain independent and sharp. Running arms stay tucked close to the torso, swing along the athlete's local forward/back direction and bend inward at the elbows.

Runoff, perimeter boards, tiered seats and aisles use two static meshes. A third shared-material mesh draws hundreds of varied spectators with idle movement and staggered clapping, waving and jumping after goals. Small, pooled confetti bursts appear on the first goal and every third goal afterwards. A yellow-shirted sideline referee near halfway raises a whistle hand and points toward midfield to acknowledge goals; this character has no physics or officiating rules. Match restart resets the confetti cadence; kickoff clears all presentation reactions.

Four floodlight towers have braced masts, twelve-lamp banks, restrained halos and soft projected 2D illumination. Players, the ball and referee have feathered shadows directed away from the floodlights, independent of their facing. Six pitch-side broadcast camera props have low tripods, long ribbed lenses, viewfinders and red tally lamps. Stadium presentation uses eleven mesh renderers plus small shared-sprite actor shadows; equipment adds no colliders, live cameras or dynamic light passes. Existing pitch dimensions, wall faces and goal triggers are preserved. See the [original grass generation prompt](Tools/Art/STADIUM_PROMPT.md).

Goals use separate floors, translucent overhead netting and rigid front crossbars, so a player inside the recess appears under the ropes. Back/side net impacts produce a localized, damped ripple scaled by shot speed. Scoring stops players and the clock while the ball continues physically into the net; net contact absorbs energy and adds drag so it settles in the goal. Kickoff restores normal ball drag and clears net/crowd reactions. User pause freezes the entire celebration, and golden goal still waits for the celebration before Results.

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

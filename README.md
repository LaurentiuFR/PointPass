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
- Touch cancellation: tap the separate **CANCEL** control while holding Pass or Shoot. It clears the charge and ignores the old finger's release, retaining possession. Shoot shows straight/left/right bend strength while charging.
- Pause/resume: **P**, gamepad **Start**, or the HUD pause button. In menus, use pointer/touch or arrows/stick plus **Enter/South**; **Escape/East** goes back.

## Menus
Use **Colours** on Main or Controls to cycle between **Navy + Ice Blue** (default), **Navy + Coral**, and **Navy + Lime**. The choice is saved locally and updates the existing menus and HUD immediately; club kit colours and replay settings remain independent.
The prototype opens on a branded match-day menu with **Step Onto the Pitch** (offline play) and **How to Play**. A dynamic player/ball composition and vector pitch graphic animate gently without obscuring navigation. The pause menu offers Resume, Controls, Restart Match and Main Menu. Full time shows the final score with explicit **Play Again** and **Main Menu** actions. After a goal has been clipped, Pause and Results also offer **Watch Last Goal**; Back returns to the originating menu while the live match remains paused.

Pausing retains possession, positions, momentum, match period and clock. Returning home abandons the current match; replay starts a clean kickoff. Focus loss/backgrounding pauses an active match, and returning to the app waits for Resume. Menu input is consumed before gameplay resumes to prevent an accidental pass or sprint.

Menus are generated at runtime from scripts; no scene setup or build-settings changes are required. The default mode remains an offline, single-player prototype.

**Passing Practice** opens a separate untimed drill using the same physical ball and player controls. Pass the whole ball through the lit gate to score a hit; the target alternates between right and left. Only executed passes count, once per pass; shots, dribbling and reverse crossings do not count. The posts are visual and add no colliders. Hits/passes/active-gate feedback replace the match scoreboard and clock. **Reset Ball** explicitly clears flight/charge/ownership and returns the ball near your current feet; normal passing never teleports it. Pause/resume, Restart Practice and Main Menu retain their usual roles. Leaving practice restores normal match rules, scoring and replays.

**Sound** is available from Main and Controls (also accessible while paused). Volume defaults to 65%; mute and volume save independently on the device. Passes, shots, match whistles and accepted goals have distinct, bounded procedural placeholder cues through one live-session AudioSource. These are generated waveforms, not licensed recordings or final production sound design. Cancelled charges and replay actors emit no kick cues. Pausing/backgrounding suspends playback; returning waits for the match to resume. The app does not change OS or other apps' volume.

Kicks follow the current movement direction immediately and retain that exact direction when the stick is released. A short guide shows the intended trajectory while charging. Passes reach full power in **0.65 seconds**; shots take **1 second**. Holding longer keeps maximum power. You can move and sprint while charging. Passing and shooting require possession, and the first kick action pressed owns the charge. Sprint has no stamina in this milestone.

Current feel tuning: run **4.8** world units/second, sprint **6.48**, with a small dribbling slowdown. Pass speed ranges from **8–14**, shot speed from **10–20**; forward movement adds a small follow-through contribution. Velocity eases toward the new direction with a **0.12-second** steering response and **0.09-second** braking response, preserving a brief arc through sudden turns. Kick buttons dim without possession; the HUD shows contextual controls. These are prototype tuning values that still need hands-on device playtesting.

Facing turns at **840 degrees/second**: a 90-degree turn settles in about **120ms** and a reversal in **220ms** at 50Hz. A quick stick release finishes the intended turn. Kick aim still follows the command immediately. The local player has a subtle identification ring, animated running lean, separate arms swinging opposite the legs, and a visible kick/recovery pose. The ball has direction-aware visual rolling and a short trail on fast kicks.

Curved shots rotate the ball's physical velocity gradually, without moving its position directly or adding speed. Full bend starts at **72 degrees/second**, fades over **1.1 seconds**, and clears on collision, pickup or reset. A curved guide and bend label provide charge feedback. Passing stays straight. Mobile flicks use recent motion scaled to the UI canvas; long holds and slow drags are not intended to produce bend.

Updated player, boot and ball sprites live alongside the originals as `*_polished.png`. The animated player uses `*_rigged.png` torsos and reuses regions of `arm_runner.png` for shoulder/elbow articulation, mirrored counter-swing and shot balance. Detailed sprites use a 1024-pixel import limit, trilinear filtering and mipmaps; gameplay enables 4x MSAA at native render scale. Import settings refresh automatically after editor compilation, or through **PointPass > Refresh presentation quality**. See [sprite prompts and provenance](Tools/Art/SPRITE_PROMPTS.md). Render actual game art without changing a scene using `./Tools/Validation/Render-AppearancePreview.ps1`; outputs go to `Logs/AppearanceVerification`. These staged editor renders complement gameplay tests and do not replace on-device testing.

The pitch retains its natural grass texture with softer filtered detail, restrained colour variation and mowing bands. Four fixed floodlights share one projected 2D lighting model across turf, kits, limbs and ball. Dry grass is matte; `TurfSurface.SetWetness` explicitly enables a restrained wet sheen. White markings remain independent. `MatchPalette` supplies deep navy surfaces, selectable interface accents, and consistent blue/coral club colours. World meshes and graded turf handle Gamma/Linear colour conversion explicitly. UI characters use Canvas-compatible materials, preserving source shading. Text rasterizes dynamic glyphs at twice the canvas density with pixel rounding; menus and HUD use pixel-aligned Canvases. Inspect a native-resolution or fixed-size Game view rather than enlarging a small preview.

Running uses shoulder pitch, rather than large sideways arm rotations. Opposite arms project forward toward the chest and behind the torso, with inward elbow bends and foreground/background hand sorting. Speed drives stride cadence, jogging intensity and sprint response; returning to idle clears the pose. Physical movement, hitboxes and kick aim remain independent.

Runoff, perimeter boards, tiered seats and aisles use two static meshes. A shared-material crowd mesh draws hundreds of varied spectators in identifiable blue (left) and red (right) sections. Scoring supporters clap, wave and jump; opponents slump, shake their heads or hold their heads, with staggered individual timing. Brief, pooled team-colour confetti bursts stay in the scoring side's sections. A yellow-shirted sideline referee near halfway acknowledges goals; this character has no physics or officiating rules. Kickoff clears presentation reactions.

Four floodlight towers have braced masts, twelve-lamp banks, restrained halos and sustained soft 2D illumination. The turf has light pools and subtle surface highlights; actors have feathered shadows directed away from the lights. Atmospheric beams are disabled in clear air and can be enabled with `StadiumFixtures.SetHaze`. Six broadcast camera props remain distinct from the towers. Twelve photographers produce staggered, brief flashes and local surface reflections after a goal. Stadium presentation uses thirteen meshes plus shared-sprite shadows/flash pools; equipment adds no colliders, extra scene cameras or dynamic light passes. Existing pitch dimensions, wall faces and goal triggers are preserved. See the [original grass generation prompt](Tools/Art/STADIUM_PROMPT.md).

Goals use separate floors, translucent overhead netting and rigid front crossbars, so a player inside the recess appears under the ropes. Back/side net impacts produce a localized, damped ripple scaled by shot speed. Scoring stops players and the clock while the ball continues physically into the net; net contact absorbs energy and adds drag so it settles in the goal. Kickoff restores normal ball drag and clears net/crowd reactions. User pause freezes the entire celebration, and golden goal still waits for the celebration before Results.

The 3.2-second goal moment coordinates supporter reactions, photographer flashes, scorer animation and replay feedback. Last-touch tracking covers kicks, possession and physical deflections; an own-goal scorer does not celebrate for the opponent. A preallocated 20 Hz history records positions, headings, articulated local poses and ball roll, plus strike direction and net-impact events. A highlight retains up to five seconds before the goal (or three seconds before an earlier recorded shot, within its 12-second buffer) and the full celebration. The saved cue appears after capture completes.

Replay renders the same pitch, goals, nets, stadium and actor factories through a separate MSAA render texture and camera. Its colliders, physics and automatic animation clocks are disabled; interpolated samples and recorded events drive playback while the live match stays paused. A stable camera envelope contains the recorded action, scorer and target net without cuts or motion blur. Select **0.5× / 1× / 1.5× / 2×** during playback; **Show shot arrow** is off by default and displays the recorded strike direction for 0.7 replay seconds. New playback defaults to 1× and arrow off. Closing the view disables the replay camera.

Replay also provides **Pause/Resume**, **Restart**, and elapsed/total time. Pause freezes the shared replay timeline, including poses and event effects. Restart returns to the first recorded frame while retaining speed, arrow preference and paused state. Opening a new highlight starts playing with the standard 1×/arrow-off defaults.

The latest highlight is saved as `last-goal.json` under `Logs/Highlights` in the editor, or the application's persistent `Highlights` folder on devices. Each new highlight replaces the previous one. Legacy position-only clips remain viewable, but cannot recover previously unrecorded limbs, shot metadata or the longer celebration; score a new goal to capture these. This is an in-game replay, not a video export.

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

# Clubhouse and multiplayer integration

Integration branch: `codex/menu-multiplayer-integration`.

Sources reviewed on 3 November 2026:

- Multiplayer/input/diagnostics: `3d92bb1`, from `codex/client-shooting-network-stats`.
- Menu and design-reference commits: `09d387e` and `534fa73`, from `origin/menu`.
- Both descend from `32b815c`; the other published gameplay feature branches are already ancestors of that baseline. They have not been rewritten.

The integration was prepared and reviewed on its own branch for the owner's explicitly approved merge into `main`. Both source branches are preserved. Existing local Unity-generated settings were backed up and stashed before integration, then reapplied; those local changes are excluded from the integration commit. The menu branch's explicit `runInBackground` change is retained for PC testing.

## Compatibility work

- Resolve shared controller/match-manager conflicts while retaining network command routing, the immediate accepted-command return that fixes Red's kick release, authoritative scores/clock/reset state and nullable server HUD access. Keep the new offline goalkeeper and practice hooks.
- Use the incoming shared passing speeds, reception rules and pitch damping in both offline and authoritative multiplayer gameplay. The network world still creates exactly two human-controlled footballers, without offline AI or menu preview squad slots.
- Update the connection compatibility identifier to `PointPass-PC-3`; input/state message layouts stay unchanged. Rebuild both peers. Previous `PC-2` builds must not join this build with different ball rules.
- Route received goal events through the same team-aware goal/crowd audio presentation used by the host.
- Preserve the clubhouse design, bundled fonts, artwork and audio. Restore the previously supported saved Ice Blue/Coral/Lime choice in Settings and expose the already implemented training/free-play/scenario/difficulty flows from the mode selector. Menu online formats remain previews; actual network connections use the separate development panel.
- Give the My Club stadium camera target a depth buffer required by the installed URP Render Graph renderer. The blur-only destination remains colour-only. Fix the test viewport cleanup to remove custom presets using Unity's full index.
- Update legacy menu checks for the new guide and navigation, select the human actor explicitly now that offline keepers exist, and keep isolated kick fixtures outside the goal mouth so valid goals do not interfere with subsequent input checks.

## Verification

Unity 6000.3.27f2 on Windows:

| Check | Result | Local report |
| --- | --- | --- |
| Windows development build | Succeeded, zero errors | `Logs/NetworkVerification/build.txt` |
| Engine regression | 409 checks passed | `Logs/PR2Verification/engine-tests.txt` |
| Current clubhouse, actual Play Mode | 64 checks passed; no runtime/rendering/viewport-cleanup errors | `Logs/PdfMenuReview/verification.txt` |
| Keepers, physical long passes, audio and goalkeeper replays, actual Play Mode | 51 checks passed | `Logs/KeeperVerification/playmode-tests.txt` |
| Startup, pause, rematch, both goals, celebration and golden goal, actual Play Mode | 30 checks passed | `Logs/AppearanceVerification/playmode-tests.txt` |
| Two standalone Windows instances plus capacity-rejection client | 50 checks passed, including 919 matching authoritative snapshot sequences | `Logs/NetworkVerification/MenuIntegration/tests.txt` |

The standalone run verifies independent movement/sprint, passes in both directions, possession, Red's immediate touch and held-keyboard shots through `HumanPlayerInput`, charged curve shots, exact retained aim, shared scoring and kickoffs, F3 diagnostics data, full-server rejection, disconnect/rejoin, host departure/rehost and explicit restart. Client bodies remain unsimulated. Both player logs are free of runtime exceptions.

Native menu views at 844x390, 1280x720, 1920x1080 and 1280x960 were captured and inspected, including Settings and My Club. These are editor/standalone checks; physical touch input, device performance and second-PC LAN/internet behaviour have not been established by this integration.

## Repeat

1. Use the build and launch steps in [MULTIPLAYER.md](MULTIPLAYER.md), or rebuild from **PointPass > Multiplayer Development > Build Windows Test Player**. Close old player instances and distribute the whole rebuilt output folder to both players.
2. Run `./Tools/Validation/Test-WindowsMultiplayer.ps1 -ReportDirectory C:/Dev/PointPass/Logs/NetworkVerification/MenuIntegration`.
3. Run `./Tools/Validation/Run-PR2Validation.ps1` and `./Tools/Validation/Render-AppearancePreview.ps1 -VerifyMenus`.
4. For native menu/keeper reviews, enter the real project's offline Play Mode, enable the CLI's `set_auto_tick`, then use the script-run commands in [README.md](README.md).

No scenes, prefabs, package versions, cloud resources or paid services are added or changed by the compatibility fixes. The incoming menu assets and its existing offline systems are retained. The complete integration diff was supplied for review before the owner approved committing and publishing to `main`.

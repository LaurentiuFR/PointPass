# PointPass menu in Unity

The clubhouse reference supplied in `PointPass — Main Menu.pdf` is implemented as native uGUI in `LobbyClubhouseView`, using the existing `LobbyMenuView` and `MatchMenu` navigation. The 1600 × 900 reference layout supplies the profile, descriptive 2 × 2 action cards, season card, central footballer, party column, and ice-blue Play button. Existing shop previews, local cosmetics, audio settings, and saved balances are retained.

The main footballer is native vector geometry extracted from the supplied PDF by `Extract-ClubhousePlayer.py`; no flattened menu screenshot is used. `ClubhousePlayerGraphic` adds breathing and a periodic parabolic keep-up using unscaled time. `ClubhouseFootballer.json` contains editable shape colours, vertices, and triangles. The prior overhead pitch panel is removed from the main menu. The existing match-asset renderer remains available in My Club and is disabled on the clubhouse screen.

The five-step touch guide follows the real joystick, pass, shoot/curve, sprint, and cancel controls. Its final action launches the existing scored passing practice. The selector also offers the real local goalkeeper challenge. Multiplayer formats and invitations remain explicitly labelled previews; no online session or invitation is created.

Barlow Condensed Regular and Bold are included under `Resources/Fonts` with their SIL Open Font License, sourced from the official Google Fonts repository. Labels remain editable text and use the existing supersampled `SharpText` renderer. The wallet uses local saved balances; the menu does not invent live friend counts, server latency, or online rankings.

Verification runs with `Tools/Validation/FigmaMenuPlayMode.cs` through Unity's script runner. It checks real Play-mode frames, submitted footballer geometry, pointer-click navigation, practice and local-play transitions, the single EventSystem, and native Game-view renders at 844 × 390, 1280 × 720, and 1920 × 1080. Captures and the report are written to `Logs/PdfMenuReview/`.

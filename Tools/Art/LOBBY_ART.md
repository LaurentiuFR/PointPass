# PointPass lobby artwork

The current clubhouse menu follows the supplied PointPass main-menu PDF. Its
footballer is scalable vector geometry rendered by `ClubhousePlayerGraphic`.
Navigation, labels, icons, cards, and buttons are native, editable uGUI controls.

## Runtime assets

- `Assets/_Project/Resources/Menu/ClubhouseFootballer.json`: footballer shape
  colours, vertices, and triangle indices extracted from the reference.
- `Assets/_Project/Resources/Fonts/BarlowCondensed-Regular.ttf` and
  `BarlowCondensed-Bold.ttf`: menu typography, with the included SIL Open Font License.
- `Assets/_Project/Resources/Menu/NativeBackdropBlur.shader`: the existing
  match-art preview renderer used by My Club.

`Tools/Design/Extract-ClubhousePlayer.py` reproduces the footballer geometry from
the supplied PDF. `Tools/Design/UNITY_MENU_HANDOFF.md` describes the runtime layout,
preview multiplayer flows, and Play-mode verification.

The earlier Figma authoring scripts, node ledger, and prototype screenshot in
`Tools/Design` are design references. The game does not load those files.

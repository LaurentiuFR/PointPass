# PointPass: project notes for Claude

Unity (Universal 2D template) mobile game for iOS and Android. Two-person team.

## Folder structure
Assets/_Project/
  Scripts/   C# code
  Scenes/    Unity scenes
  Prefabs/   Prefabs
  Art/       Sprites, textures
  Audio/     Music and sound effects

## Rules
- Use C# with a namespace (PointPass.*), one class per script file.
- Keep game logic in scripts rather than hand-configured in the Inspector where possible.
- NEVER edit or delete .meta files by hand.
- When moving or renaming an asset, move its .meta file with it (git mv both), or do it inside the Unity editor.
- Do not commit Library/, Temp/, Logs/, UserSettings/, or any keystore/signing files.
- Commit before any large restructure so it can be undone.

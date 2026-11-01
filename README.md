# PointPass

Mobile game for iOS (App Store) and Android (Google Play).

## Status
Early development.

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

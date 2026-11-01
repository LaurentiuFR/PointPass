# PointPass



## Status
Early development. Targeting iOS (App Store) and Android (Google Play).

## Tech
- Unity [6000.3.27f2 LTS]
- C#
- Git + Git LFS

## Getting started
1. Install Unity Hub and the **exact editor version above**, with Android Build Support (and iOS Build Support on a Mac).
2. Install Git and Git LFS, then run `git lfs install` once.
3. Clone the repo:
   git clone https://github.com/LaurentiuFR/PointPass.git
4. In Unity Hub choose **Add > Add project from disk** and select the cloned folder.
5. Open `Assets/_Project/Scenes/[MainScene]` and press Play.

## Project structure
Assets/_Project/
  Scripts/
  Scenes/
  Prefabs/
  Art/
  Audio/

## Team workflow
- Never commit directly to `main`. Branch as `yourname/feature-name` and merge by pull request.
- `git pull` before starting work.
- Commit `.meta` files with their assets. Never edit or delete them by hand.
- Move or rename assets with `git mv`, moving the `.meta` file too, or do it inside Unity.
- Each person owns different scenes and prefabs to avoid merge conflicts.

## Contributors
- [@LaurentiuFR](https://github.com/LaurentiuFR)
- [friend's GitHub handle]

## License
All rights reserved.

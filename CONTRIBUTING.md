# Contributing and Git workflow

Echo Protocol is a solo project, but I work on it the way a small team would: short-lived branches, pull requests, a readable history and tagged releases.

## Branching model (trunk-based)

- `main` is always **playable**. It opens in Unity without errors and the sandbox scene runs.
- Every change happens in a short-lived branch and goes in through a **pull request** (I review it myself), in a day or two at most.

| Prefix | Use | Example |
|---|---|---|
| `feat/` | New feature | `feat/enemy-hearing` |
| `fix/` | Bug fix | `fix/mic-loop-wraparound` |
| `docs/` | Documentation only | `docs/architecture-diagram` |
| `refactor/` | No change in behaviour | `refactor/split-player-look` |
| `chore/` | Tooling, packages, settings | `chore/add-multiplayer-playmode` |

## Commit messages ([Conventional Commits](https://www.conventionalcommits.org/))

```
<type>(<scope>): <short imperative summary>

feat(audio): add silence calibration
fix(player): stop camera jitter when cursor unlocks
docs(readme): add controls section
```

Types: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore`.

## Pull request flow

1. Create the branch from an up-to-date `main`: `git switch -c feat/my-feature`.
2. Make small, focused commits.
3. Open a PR with the template and link the related issue (`Closes #12`).
4. Read the diff on GitHub before merging, as a reviewer would.
5. Use **Squash and merge**, so `main` gets one clean commit per feature, then delete the branch.
6. Update [CHANGELOG.md](CHANGELOG.md) under `[Unreleased]`.

## Releases

While the game is in development I use loose semantic versions: `v0.1.0` (first public build), `v0.2.0` (vertical slice), and so on up to `v1.0.0` (Steam release). Before tagging, I bump *Player Settings > Version* (`bundleVersion`), because Steam lobbies only accept players on the same build version.
I tag from `main` and publish a GitHub Release with notes and a build.

## Unity-specific rules

- Always commit `.meta` files together with their assets. Never commit `Library/`, `Temp/` or `Logs/`.
- Keep scene changes in their own PR when possible, because scene YAML diffs are hard to review.
- Large binary assets (models, audio, textures) go through **Git LFS**, which is set up in `.gitattributes`.
- Open the project from a single local clone, and never from a cloud-synced folder (OneDrive, Dropbox).
- Check that the Unity Console has no errors before opening a PR.

## Code conventions

- C# scripts live in `Assets/Scripts/<Module>/`, with one responsibility per script.
- Private serialized fields look like this: `[SerializeField] private float _moveSpeed = 5f;`.
- Use Unity 6 APIs (`FindFirstObjectByType<T>()`) and the new Input System.
- Comment the *why*, not the *what*. Public APIs get XML doc comments.
- Network code (Fish-Net) stays in `Scripts/Networking/`, with explicit server and client logic.

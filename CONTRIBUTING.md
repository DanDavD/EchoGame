# Contributing & Git Workflow

Echo Protocol is a solo project, but it is developed the way a small team would work: short-lived branches,
pull requests, readable history and tagged releases.

## Branching model (trunk-based)

- `main` is always **playable**: it opens in Unity without errors and the sandbox scene runs.
- Every change happens in a short-lived branch, merged through a **pull request** (reviewed by myself) in a day or two at most.

| Prefix | Use | Example |
|---|---|---|
| `feat/` | New feature | `feat/enemy-hearing` |
| `fix/` | Bug fix | `fix/mic-loop-wraparound` |
| `docs/` | Documentation only | `docs/architecture-diagram` |
| `refactor/` | No behaviour change | `refactor/split-player-look` |
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

1. `git switch -c feat/my-feature` from an up-to-date `main`.
2. Make small, focused commits.
3. Open a PR using the template; link the related issue (`Closes #12`).
4. Re-read the diff on GitHub before merging, as a reviewer would.
5. **Squash and merge** (one clean commit per feature on `main`), then delete the branch.
6. Update [CHANGELOG.md](CHANGELOG.md) under `[Unreleased]`.

## Releases

Semantic-ish versioning while in prototype: `v0.1.0` (echo prototype), `v0.2.0` (enemy AI), … `v1.0.0` (Steam release).
Tag from `main` and publish a GitHub Release with notes and a build.

## Unity-specific rules

- **Always commit `.meta` files** together with their assets; never commit `Library/`, `Temp/`, `Logs/`.
- Keep scene changes in their own PR whenever possible. Scene YAML diffs are hard to review.
- Large binary assets (models, audio, textures) go through **Git LFS** (configured in `.gitattributes`).
- Only open the project from one local clone, and **never from a cloud-synced folder** (OneDrive, Dropbox).
- Check the Unity Console is free of errors before opening a PR.

## Code conventions

- C# scripts live in `Assets/Scripts/<Module>/`; one responsibility per script.
- Private serialized fields: `[SerializeField] private float _moveSpeed = 5f;`.
- Use Unity 6 APIs (`FindFirstObjectByType<T>()`) and the new Input System.
- Comment the *why*, not the *what*. Public APIs get XML doc comments.
- Network-specific code (Fish-Net) stays in `Scripts/Networking/`, with explicit server vs client logic.

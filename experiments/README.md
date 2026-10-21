# experiments — dev-only gameplay ideas

Ideas we want to try out, playtest or film without making them part of the game (rule `C8` in
`docs/standards.md`). Nothing here is ever in the final game: a release export (`ExportRelease`) compiles none of it,
whatever the switch below says. Why each idea is out, and what would bring it in: `docs/game-loop.md` § Parked ideas.

| Folder | What it does |
|---|---|
| `meteor/` | **The camping meteor.** Stay within 50 px for 5 s and a meteor falls where you stand: a ground warning, a burning rock trailing fire and smoke, then a flash, shockwave, debris, dust, scorch and a camera shake. It hits like an enemy attack (half a block, knocked back). |

## Switching one on

Name its folder in `EnabledExperiments` in `mygamedev.csproj` (`;`-separated; empty = none), then build:

```xml
<EnabledExperiments>meteor</EnabledExperiments>
```

A compiled-in experiment is simply on; there is no key. Set it back to empty before committing — the committed
default is what the editor and every export build. For a one-off build without touching the file:
`dotnet build mygamedev.csproj -p:EnabledExperiments=meteor` (the editor's own build still uses the file).

## Adding one

1. A folder `experiments/<name>/` holding everything it needs. Build its nodes in code — no scene may reference an
   experiment's class (Godot would fail at runtime when it is compiled out).
2. One class implementing `IExperiment` (`records/experiments/`) with a public parameterless constructor.
   `Experiments.AttachAll` finds it when it is compiled in and calls `Attach` with an `ExperimentHost`: the player
   and the arena root to add nodes under. That is all it gets — it never edits game files. An idea that needs more of
   the game is ready to become real game code instead.
3. Mark it `EXPERIMENT` in its doc comment, add a row above and to `docs/game-loop.md` § Parked ideas.

Every QA pass builds with `-p:EnabledExperiments=all`, so a switched-off experiment can't silently stop compiling.

## When an idea earns its place

It is rebuilt as real game code — numbers in `configs/`, the hooks it needs, the docs — and its folder deleted.

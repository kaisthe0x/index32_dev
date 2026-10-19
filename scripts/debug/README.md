# debug — the developer tools

Everything here is a `DEBUG` aid: nothing in this folder is part of the game. It lives in one place so the game code
stays free of it, and so it can all be removed at once (rule `C5` in `docs/standards.md`).

**How the game reaches it.** Only two places in the game code know this folder exists, each marked `DEBUG`:
`RunManager` (its `_debug` field — it adds a `DebugTools` child and skips the round while `DebugTools.Recording` is
on) and the `HUD` autoload (one `BuildLog.PrintLastCompile()` call). Removing the tools = delete this folder and
those lines.

| File | Class | What it does |
|---|---|---|
| `DebugTools.cs` | `DebugTools` | The developer keys, one input handler: **0** rebuild the arena, **Z** hurt the player, **X** one block of Ruh, **B** / **N** buffs, **H** recording mode, **M** the camping meteor. |
| `BuffCycler.cs` | `BuffCycler` | **B** grants the next `BuffCatalog` buff (middle strength, cycling through the set); **N** removes every buff he holds (shots and the character ability stay). |
| `RecordingMode.cs` | `RecordingMode` | **H**: just Khalid and the arena, for capturing footage — the HUD is hidden, the round does not run, the stalls show no prompt and don't open. Each toggle rebuilds the arena. |
| `MeteorStrike.cs` | `MeteorStrike` | **M**: the camping meteor, for a devlog (not part of the game — a council ruled it out). Stay within 50 px for 5 s and a meteor falls where you stand: a ground warning, a burning rock trailing fire and smoke, then a flash, shockwave, debris, dust, scorch and camera shake; it hits like an enemy attack. Everything is in this one file; remove it by deleting the file and the M lines in `DebugTools`. |
| `BuildLog.cs` | `BuildLog` | Prints the last C# compile time when the game starts from the editor (does nothing in an exported game). |

**Adding a tool:** a new class here, driven from `DebugTools`. If the game has to react to it, give `DebugTools` a
read-only property for the game to ask (as `Recording` does) rather than putting debug state in a game class.

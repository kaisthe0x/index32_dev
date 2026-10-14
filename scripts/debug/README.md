# debug — the developer tools

Everything here is a `DEBUG` aid: nothing in this folder is part of the game. It lives in one place so the game code
stays free of it, and so it can all be removed at once (rule `C5` in `docs/standards.md`).

**How the game reaches it.** Only two places in the game code know this folder exists, each marked `DEBUG`:
`RunManager` (its `_debug` field — it adds a `DebugTools` child and skips the round while `DebugTools.Recording` is
on) and the `HUD` autoload (one `BuildLog.PrintLastCompile()` call). Removing the tools = delete this folder and
those lines.

| File | Class | What it does |
|---|---|---|
| `DebugTools.cs` | `DebugTools` | The developer keys, one input handler: **0** rebuild the arena, **Z** hurt the player, **X** one block of Ruh, **B** / **N** buffs, **H** recording mode. |
| `BuffCycler.cs` | `BuffCycler` | **B** grants the next `BuffCatalog` buff (middle strength, cycling through the set); **N** removes every buff he holds (shots and the character ability stay). |
| `RecordingMode.cs` | `RecordingMode` | **H**: just Khalid and the arena, for capturing footage — the HUD is hidden, the round does not run, the stalls show no prompt and don't open. Each toggle rebuilds the arena. |
| `BuildLog.cs` | `BuildLog` | Prints the last C# compile time when the game starts from the editor (does nothing in an exported game). |

**Adding a tool:** a new class here, driven from `DebugTools`. If the game has to react to it, give `DebugTools` a
read-only property for the game to ask (as `Recording` does) rather than putting debug state in a game class.

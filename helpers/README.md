# helpers — shared static utilities

Small, dependency-free `static` classes that several unrelated scripts would otherwise each reimplement. A helper
goes here when a second, unrelated class needs the same few lines (rule `O2` in `docs/standards.md`).

| File | Class | What it does |
|---|---|---|
| `AnimMeta.cs` | `AnimMeta` | Reads the per-animation metadata the sprite generator writes into each `SpriteFrames` — hit frames, sheet start, loop bounds — parsed once per resource and cached. Used by the player's combo / strike logic and the enemies' attack timing. |
| `ParticleNodes.cs` | `ParticleNodes` | `SetEmitting` / `SetOneShot` / `OnFinished` for a node that is a `CPUParticles2D` or a `GPUParticles2D` (the engine gives the two no shared typed base). |
| `Nodes.cs` | `Nodes` | `PlaceAt(node, pos)` — put a node at a world position and reset physics interpolation, so it does not smear in from where it was. |
| `BuildLog.cs` | `BuildLog` | `DEBUG`: prints the last C# compile time when the game starts from the editor. |

# music/

Background music, organised **per stage**. Each stage folder's audio files ARE its playlist — the `Music`
service auto-discovers them, plays them in sequence, crossfading gently between them and looping back to the first.

```
music/
  stage1/
    1_intro.ogg    played first  (order = sorted filename)
    2_drive.ogg    crossfades in after #1, then loops back to #1
```

## Adding / changing a stage's music

1. **Drop the track files in `music/<stage>/`** (`.ogg` preferred; `.mp3`/`.wav` work too). That's it —
   **no code to edit, and the filenames don't matter** except that the play order is the **sorted filename**
   (so prefix `1_`, `2_`, … if you want a specific sequence; for two tracks it just alternates either way).
2. Play it with `Music.play_stage("<stage>")` — `RunManager.BuildArena` does this for `"stage1"` on every
   run start (and death-restart). The folder is re-scanned each call, so swapping files just needs a run restart.

- The crossfade between consecutive tracks is `Music.CrossfadeBetween`; the start/stop fade is `StartFade`.
- Tracks are force-looped as a safety net, but the playlist normally crossfades to the next before a track ends.
- A single-track folder just loops that one track. An empty/missing folder warns and plays nothing (no crash).
- `Music.stop()` fades out + ends the playlist; `Music.pause()`/`resume()` freeze/continue at position.

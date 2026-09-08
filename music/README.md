# music/

Background music, organised **per stage**. Each stage has its own folder holding an ordered playlist; the
`Music` service plays those tracks in sequence, crossfading gently between them and looping back to the first.

```
music/
  stage1/
    stage1_bg_music_1.ogg   played first
    stage1_bg_music_2.ogg   crossfades in after #1, then loops back to #1
```

## Adding / changing a stage's music

1. Drop the track files in `music/<stage>/` (`.ogg` preferred; `.mp3`/`.wav` work too).
2. Register the ordered list in `scripts/audio/Music.cs` (`StagePlaylists["<stage>"] = new[] { …paths… }`).
3. Play it with `Music.play_stage("<stage>")` — `RunManager.BuildArena` does this for `"stage1"` on every
   run start (and death-restart).

- The crossfade between consecutive tracks is `Music.CrossfadeBetween`; the start/stop fade is `StartFade`.
- Tracks are force-looped as a safety net, but the playlist normally crossfades to the next before a track ends.
- A single-track playlist just loops that one track. A missing file warns and plays nothing (no crash).
- `Music.stop()` fades out + ends the playlist; `Music.pause()`/`resume()` freeze/continue at position.

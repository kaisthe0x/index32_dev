#!/usr/bin/env python3
"""Generate SpriteFrames (.tres) resources from the character sprite sheets.

The source sheets are single-row uniform grids, but frame size varies per
animation (32x32 idle vs 143x48 attack) and some sheets carry a constant
horizontal padding bias. Slicing them as-is makes the character jump around
when the animation changes.

This script normalises every frame onto one shared canvas:
  - vertically   : frame bottom == canvas bottom (the sheets are foot-anchored)
  - horizontally : frame 0 of every animation is the neutral pre-action pose
                   (its bounding box matches idle frame 0 exactly), so we anchor
                   on that. Anchoring on the mean instead would let a dash's
                   fire trail or an attack's swing arc drag the body off-centre.
                   Later frames keep their own offsets, so lunges still lunge.

Normalisation is expressed with AtlasTexture.margin, so no images are rewritten
and no extra VRAM is used -- the atlases still point at the original PNGs.

Re-run after adding a character or re-exporting a sheet:
    python3 tools/gen_spriteframes.py
"""

from __future__ import annotations

import math
import re
from pathlib import Path

from PIL import Image

PROJECT = Path(__file__).resolve().parent.parent

# Each group is a folder of items (characters, enemies, ...) that share one
# animation set and one normalised canvas, generated into its own resources dir.
# name -> (fps, loop) per anim; "idle" first so it is the default animation.
CHARACTER_ANIMS = {
    "idle": (6.0, True),
    "run": (10.0, True),
    "jump": (10.0, False),
    "fall": (10.0, True),  # loops while descending, after the jump anim finishes
    "land": (12.0, False),  # brief touchdown squash; plays once, then idle/run
    "slam": (12.0, False),  # air-down ground slam; plays once during the plunge
    "dash": (12.0, False),
    "attack": (12.0, False),
    "special": (10.0, False),
    "death": (10.0, False),  # plays once on death, then holds the last (dead) frame
    "spawn": (10.0, False),  # plays once when the character (re)spawns, then hands to idle
    "hurt": (12.0, False),  # brief flinch played while staggered by a hit (player HURT state)
}
ENEMY_ANIMS = {
    "idle": (6.0, True),
    "walk": (8.0, True),  # walk cycle used while walking/patrolling (was "patrol"/"stroll")
    "attack": (12.0, False),  # a strike/melee (ground AoE, launch, front hit)
    "attack_projectile": (10.0, False),  # launches a projectile (Kebus bolt, Baghel wave)
    "death": (6.0, False),  # slow + graceful -- plays once on death, then the corpse fades
}
WARDEN_ANIMS = {
    "idle": (6.0, True),
    "walk": (8.0, True),
    "spawn": (
        9.0,
        False,
    ),  # cinematic entrance -- plays once when a Fissure is sealed, then hands to idle/chase
    "attack": (10.0, False),  # timing template for attack_* variants (prefix inheritance)
    "attack_lunge": (10.0, False),  # lunge in + body-check; the last frame holds (see OVERRIDES)
    "death": (
        5.0,
        False,
    ),  # plays once; the corpse then PERSISTS (WardenEnemy keeps the last frame in-world)
}
GROUPS = {
    "characters": CHARACTER_ANIMS,
    "enemies": ENEMY_ANIMS,
    "wardens": WARDEN_ANIMS,
}


def anim_timing(anim: str, base: dict) -> tuple[float, bool]:
    """(fps, loop) for `anim`. Base anims use their entry; named variants inherit
    by prefix -- attack_* like `attack`, special_* like a special --
    so a character can add attack_finger_guns / special_poison_raiser sheets with
    no config and they play at the right speed."""
    if anim in base:
        return base[anim]
    if anim.startswith("attack"):
        return base.get("attack", (12.0, False))
    if anim.startswith("special"):
        return base.get("special", (10.0, False))
    return (10.0, False)


# Frame 0 of every sheet is a static idle-reference pose the artist includes so
# the animation lines up with idle. It is the alignment anchor (see Sheet.bias),
# and for every animation EXCEPT idle it is dropped from playback -- action
# animations start on their real first frame. All frame indices in OVERRIDES and
# HIT_FRAMES below are SHEET-relative (they count the idle frame as 0); the
# generator converts them to the emitted indices the player/enemy sees.

# Per-character timing tweaks, layered over ANIMS. Frame counts differ a lot
# between characters, so a single fps makes some swings drag and others snap.
#   fps       -- override playback speed for that one animation
#   hold_last -- multiply the final frame's duration, to let a pose land before
#                the character retracts
#   loop      -- force the animation's loop flag on/off, overriding the ANIMS default
#                (e.g. a hold-to-repeat "flurry" attack that should cycle, not play once)
#   loop_from -- the sheet frame a loop restarts at; frames before it play once as an
#                intro. Written as metadata (does NOT set Godot's loop flag): player.gd
#                clamps a LOOPING anim to it, and enemy.gd restarts a RE-PLAYED attack there
#                (a channel/rage), so the lead-in only plays once. Works with loop on or off.
#   loop_to   -- optional end of the loop (sheet frame, inclusive). Without it the
#                loop runs to the last frame; with it the cycle is loop_from..loop_to
#                and any frames past loop_to only ever show in the intro pass. Lets
#                a character loop a mid-sheet range (e.g. a 3-8 idle flourish).
#   keep_first -- KEEP the idle-reference frame 0 in playback (normally dropped from
#                every non-idle anim). For a run/anim that should ease out of the
#                neutral pose; pair with loop_from so that frame plays once, not in the
#                loop. (idle always keeps frame 0 -- this is for the others.)
# loop_from / loop_to are sheet-relative (they count the idle-reference frame 0),
# same numbering as HIT_FRAMES. Anything not listed here uses the ANIMS default.
OVERRIDES: dict[tuple[str, str], dict[str, float | int | bool]] = {
    # Khalid
    ("khalid", "run"): {"fps": 10.0},
    ("khalid", "attack_ora_ora"): {"loop": True, "fps": 18.0},
    # Twin Reaper: a spinning FLURRY -- hold attack and the whole spin LOOPS, each pass firing its
    # 5 Slash nodes on the emitter frames. loop is REQUIRED (a non-looping flurry hangs on the last
    # frame); no HIT_FRAMES so the spin plays smooth (segmented snapping is for click-combos).
    ("khalid", "attack_twin_reaper"): {"loop": True, "fps": 20.0},
    # Rope Dart: upgraded Twin Reaper -- same flurry rules (loop, no HIT_FRAMES). Its
    # sheet stands in on Twin Reaper's spin until reskinned; own name -> its own VFX/SFX.
    ("khalid", "attack_rope_dart"): {"loop": True, "fps": 20.0},
    ("khalid", "special_ground_breaker"): {"fps": 15.0, "hold_last": 1.6},
    ("khalid", "slam"): {"fps": 15.0, "hold_last": 1.6},
    # Bakshen: a charged slash. fps 10 so the per-frame math is clean (see FRAME_DURATIONS: the
    # wind-up frame holds 1s); hold_last lets the final slash pose linger while its VFX plays.
    ("khalid", "attack_bakshen"): {"fps": 10.0, "hold_last": 10.0},
    # Zahluq: a fast burst-forward dash-attack (COOLDOWN). Snappy so the lunge reads as a
    # quick slide; hold_last lets the follow-through pose settle after he stops.
    ("khalid", "attack_zahluq"): {"fps": 14.0, "hold_last": 2.0},
    # Aegis surge (surge_aegis, formerly special_default): the flex pose sheet. The surge is passive
    # so this anim isn't played today, but keep the sheet + timing under its new name (renamed 1:1).
    ("khalid", "surge_aegis"): {"fps": 8.0, "hold_last": 3.0},
    ("khalid", "surge_jnoon"): {
        "fps": 8.0,
        "hold_last": 3.0,
    },  # Jnoon surge flex -- same timing as aegis
    ("khalid", "surge_asra"): {
        "fps": 8.0,
        "hold_last": 3.0,
    },  # Asra surge flex -- same timing as aegis
    # Nem: a sleep channel. keep_first keeps the "still awake" pose
    # (idle-ref frame 0) so the fall-asleep
    # reads; player.gd pauses playback on the second-to-last frame (head down).
    # fps ~6 so the slump takes
    # ~0.5s. No hold_last -- the pause is code-driven, not a frame duration.
    ("khalid", "surge_nem"): {"fps": 6.0, "keep_first": True},
    ("khalid", "surge_wara"): {"fps": 12.0},  # Wara cast flex -- same timing as aegis
    # New specials: short casts, hold the last pose so the beckon / shield / throw reads.
    ("khalid", "special_come_closer"): {"fps": 9.0, "hold_last": 1.5},
    ("khalid", "special_redere_shield"): {"fps": 9.0, "hold_last": 2.0},
    ("khalid", "special_redere_frisbee"): {"fps": 11.0, "hold_last": 2.0},
    ("khalid", "death"): {"fps": 7.0},
    # Swing (orb traversal): a HELD grapple.
    # F0 is the idle-ref (dropped); F1 is the reach/grab intro
    # (plays once); F2-F7 loop as the swing cycle while attached to the orb; F8 is the release pose
    # (past loop_to, shown once on let-go by player.gd). loop is REQUIRED so the hang cycles for a
    # variable, physics-driven duration.
    # loop_from/loop_to are SHEET-relative (they count the idle-ref F0).
    ("khalid", "swing"): {"fps": 9.0, "loop": True, "loop_from": 2, "loop_to": 7},
    # Hurt flinch: slower than the 12fps default + a lingering recoil so the 3-frame
    # reaction reads (the Player holds HURT at least this long, not cut short by a brief stagger).
    ("khalid", "hurt"): {"fps": 10.0, "hold_last": 1.6},
    # Nasen (enemy): a deliberate rage yell. `loop_from` = sheet frame 2 (= emitted 1, the
    # first yell frame) so his re-played rage loops the yell and the wake-up (emitted 0)
    # plays only once. Enemy.gd honours loop_from for a re-played attack (see _replay_from).
    ("nasen", "attack_aoe"): {"fps": 8.0, "loop_from": 2},
    # Mazab (enemy): let the throw's release pose land before he retracts, so the lob reads.
    ("mazab", "attack_delayed_projectile"): {"hold_last": 1.5},
    # Kroj (warden): the lunge -- last frame HOLDS while he slides forward
    # (Zahluq-style follow-through).
    ("kroj", "attack_lunge"): {"hold_last": 2.5},
    # Ein (enemy): the stab loops for the whole charge (which lasts a variable dive time, not
    # one anim cycle) -- ein.gd ends it by exploding on arrival, not on anim_finished.
    ("ein", "attack_kamikaze"): {"loop": True},
}

# Attack hit frames (sheet-relative). An attack combo plays one segment per
# click, each segment ending on a hit frame; the frames between hits animate for
# smoothness. Any attack not listed treats every frame as its own hit (so each
# click advances one frame -- the old snap feel). Emitted as resource metadata.
HIT_FRAMES: dict[tuple[str, str], list[int]] = {
    ("khalid", "special_ground_breaker"): [2, 3, 4, 5, 6],
    ("khalid", "attack_spear"): [6, 9, 13],  # thrust, thrust, big spinning finisher (strongest)
    ("khalid", "special_frenemy"): [
        1,
        2,
        3,
    ],  # the short stun blast fires on the forward-thrust frame
    ("khalid", "attack_bakshen"): [3],  # single charged hit on the last (slash) frame
    ("khalid", "attack_zahluq"): [2],  # burst frame -- the Strike + lunge fire here
    ("khalid", "special_come_closer"): [3],  # beckon on the last frame -> spawn the magnet field
    ("khalid", "special_redere_shield"): [3],  # shield fully up on the last frame (per spec)
    ("khalid", "special_redere_frisbee"): [3],  # release the throw on the last frame
    # attack_twin_reaper: NONE on purpose -- it's a flurry (loops smooth), not a click-combo. Its 5
    # hits come from the emitter Slash1-5 rows firing on frames 3/4/6/7/9, not combo segments.
    ("khalid", "attack_cherry_shots"): [
        3,
        7,
    ],  # two laser shots -- small on frame 3, big on frame 7
    ("kebus", "attack_melee"): [3],
    ("baghel", "attack_projectile"): [6],
    ("nasen", "attack_aoe"): [2],  # the rage AoE erupts on this frame
    ("kroj", "attack_lunge"): [
        2
    ],  # warden lunge: the forward impulse + body-check fire as he commits
    ("mazab", "attack_delayed_projectile"): [
        5
    ],  # release frame -- the lobbed bomb leaves his hand here
    ("matat", "attack_aoe"): [
        4
    ],  # the AoE erupts as his arms sweep round (its hitbox+VFX span frames 4-8)
    ("tarri", "attack_blast"): [
        3
    ],  # the stationary forward blast erupts on the LAST frame (he holds+vibrates there)
    ("breski", "attack_melee"): [
        4,
        9,
    ],
    # a 2-hit melee combo -- a jab (f4) then a heavier follow-up (f9);
    # each spawns its own strike
}

# Per-frame duration multipliers, (char, anim) -> {sheet_frame: multiplier}. Each
# frame normally shows for 1/fps; 2.0 holds it twice as long, 0.5 half -- so you can
# make a wind-up snap or a key pose linger WITHOUT changing the whole animation's
# fps. Sheet-relative indices (the idle-reference frame 0 counts), same numbering as
# HIT_FRAMES / loop_from. This is the general form of OVERRIDES' `hold_last` (which
# is just "the last frame"); a value set here wins over hold_last for that frame.
FRAME_DURATIONS: dict[tuple[str, str], dict[int, float]] = {
    # e.g. ("some_char", "some_attack"): {2: 1.5}  # linger on those hit frames
    ("khalid", "attack_bakshen"): {1: 2.0},  # wind-up frame holds 1 full second (10 x 1/10fps)
    ("khalid", "attack_rope_dart"): {1: 0.5, 2: 0.5},
}

# Snappy swings: in a CHARACTER attack/special that has explicit HIT_FRAMES, the frames BETWEEN
# hits (the wind-up / in-between poses) play at this fraction of a hit frame's duration -- so the
# swing snaps and only the hits (and any held finisher/recovery) linger. Applied automatically to
# every attack_*/special_* with hit frames; per-frame FRAME_DURATIONS and an explicit hold_last
# still override it. Enemies are untouched (their attack telegraphs stay readable).
BETWEEN_HIT_MULT = 0.25


def uid_for(png: Path) -> str:
    """Read the Godot-assigned uid out of the sibling .import file."""
    imp = png.with_suffix(png.suffix + ".import")
    if not imp.exists():
        raise SystemExit(
            f"{png.name} has not been imported by Godot yet.\n"
            f"Run:  godot --headless --import\n"
            f"(or just open the project in the editor once), then re-run this."
        )
    m = re.search(r'^uid="(uid://[^"]+)"', imp.read_text(), re.M)
    if not m:
        raise SystemExit(f"no uid in {imp} -- try deleting it and re-importing")
    return m.group(1)


def existing_uid(path: Path) -> str:
    """The resource uid already in `path`'s header, or "" if none/missing. Kept so
    regeneration doesn't orphan scenes that reference the .tres by uid."""
    if not path.exists():
        return ""
    m = re.search(r'uid="(uid://[^"]+)"', path.read_text().split("\n", 1)[0])
    return m.group(1) if m else ""


def content_columns(alpha, w: int, h: int) -> list[bool]:
    return [any(alpha[x, y] for y in range(h)) for x in range(w)]


def frame_count(cols: list[bool], w: int) -> int:
    """Largest N (the SLOT count) dividing w such that no content run straddles a slice
    boundary and every slice UP TO the last non-empty one has content. TRAILING empty
    slices are allowed -- a sheet padded to a wider power-of-two than its real frame count
    (e.g. 7 death frames in an 8-slot 1024px sheet) would otherwise fail the old "every
    slice has content" test and fall back to merging pairs. The caller (Sheet) derives the
    frame width from this and trims the blank trailing slots off playback. An INTERIOR
    empty slice still rejects N (that's a real gap, not padding)."""
    best = 1
    for n in range(1, 33):
        if w % n:
            continue
        fw = w // n
        if any(cols[i * fw - 1] and cols[i * fw] for i in range(1, n)):
            continue  # a content run straddles a boundary -> wrong grid at this N
        content = [any(cols[i * fw : (i + 1) * fw]) for i in range(n)]
        if not any(content):
            continue
        last = max(i for i, c in enumerate(content) if c)
        if not all(content[: last + 1]):
            continue  # an INTERIOR slice is empty -> not a clean frame grid at this N
        best = n
    return best


class Sheet:
    def __init__(self, png: Path):
        self.png = png
        im = Image.open(png).convert("RGBA")
        self.w, self.h = im.size
        alpha = im.getchannel("A").load()
        cols = content_columns(alpha, self.w, self.h)
        slots = frame_count(cols, self.w)  # slot grid; may include trailing blank pad slots
        self.fw = self.w // slots
        # Playback frame count = the LEADING non-empty slots. A sheet padded to a wider
        # power-of-two than its real frame count (kebus death: 7 frames in an 8-slot 1024px
        # sheet) leaves blank trailing slots -- drop them so the animation doesn't slice
        # (and play) empty frames off the end.
        filled = [any(cols[i * self.fw : (i + 1) * self.fw]) for i in range(slots)]
        self.n = (max(i for i, c in enumerate(filled) if c) + 1) if any(filled) else slots
        # Offset of frame 0's content centre from its frame centre, in pixels.
        # Rounded to a whole pixel so the art stays on the pixel grid.
        xs = [x for x in range(self.fw) if cols[x]]
        self.bias = round((min(xs) + max(xs)) / 2 - self.fw / 2)

    # Half-widths needed either side of the desired centre.
    def half_left(self) -> float:
        return self.fw / 2 + self.bias

    def half_right(self) -> float:
        return self.fw / 2 - self.bias


def process_group(group: str, anims: dict) -> None:
    src_dir = PROJECT / "sprites" / group
    out_dir = PROJECT / "resources" / group
    if not src_dir.is_dir():
        print(f"== {group}: no {src_dir.relative_to(PROJECT)}, skipping")
        return

    print(f"== {group} ==")
    characters = sorted(p.name for p in src_dir.iterdir() if p.is_dir())
    sheets: dict[str, dict[str, Sheet]] = {}

    for char in characters:
        sheets[char] = {}
        # Discover every <char>_<anim>_frames.png. Base anims come first in their
        # canonical order (idle first = the default animation); named attack_* then
        # special_* sheets follow. So a character can carry several attacks/specials.
        globbed: dict[str, Path] = {}
        for png in (src_dir / char).glob(f"{char}_*_frames.png"):
            globbed[png.name[len(char) + 1 : -len("_frames.png")]] = png
        base_order = [a for a in anims if a in globbed]
        extras = [a for a in globbed if a not in anims]
        attacks = sorted(a for a in extras if a.startswith("attack"))
        specials = sorted(a for a in extras if a.startswith("special"))
        rest = sorted(a for a in extras if a not in attacks and a not in specials)
        for anim in base_order + attacks + specials + rest:
            sheets[char][anim] = Sheet(globbed[anim])

    all_sheets = [s for per_char in sheets.values() for s in per_char.values()]
    if not all_sheets:
        print("  (no sheets found)")
        return

    # One canvas shared by every item and animation in the group, so a scene can
    # swap SpriteFrames without touching the sprite offset or collider.
    half = max(max(s.half_left(), s.half_right()) for s in all_sheets)
    canvas_w = 2 * math.ceil(half)
    canvas_h = max(s.h for s in all_sheets)

    out_dir.mkdir(parents=True, exist_ok=True)
    print(f"canvas: {canvas_w}x{canvas_h} (feet on the bottom edge)")

    # The canvas only collapses to the frame size when frame 0's character is
    # centred in its frame. Anything off-centre has to be padded to line up with
    # the other animations, widening the canvas for every character. Call out
    # the worst offenders so they can be re-centred at the source.
    widest = max(s.fw for s in all_sheets)
    culprits = sorted(
        (s for s in all_sheets if abs(s.bias) >= 1),
        key=lambda s: -abs(s.bias),
    )
    if canvas_w > widest and culprits:
        excess = canvas_w - widest
        scale = "off-centre" if excess > 2 else "a hair off-centre"
        print(
            f"  note: canvas is {excess}px wider than the widest frame "
            f"({widest}px) because frame 0 is {scale} in:"
        )
        for s in culprits[:5]:
            print(f"        {s.png.parent.name}/{s.png.stem.replace('_frames', '')}  {s.bias:+d}px")

    for char in characters:
        per_char = sheets[char]
        if not per_char:
            continue

        ext, sub, anim_entries, timings = [], [], [], []
        loop_points: dict[str, int] = {}
        loop_ends: dict[str, int] = {}
        hit_points: dict[str, list[int]] = {}
        # Per-anim sheet->emitted frame offset (1 where frame 0 was dropped),
        # so hand-authored configs (e.g. particle emitters) can use the same
        # sheet-frame numbers the artist sees and the player converts.
        sheet_starts: dict[str, int] = {}
        for idx, (anim, sheet) in enumerate(per_char.items(), start=1):
            fps, loop = anim_timing(anim, anims)
            tweak = OVERRIDES.get((char, anim), {})
            fps = tweak.get("fps", fps)
            loop = bool(tweak.get("loop", loop))  # e.g. a held "flurry" attack that cycles
            hold_last = tweak.get("hold_last", 1.0)
            frame_durs = FRAME_DURATIONS.get((char, anim), {})
            # Snappy rule: a character attack/special with explicit hit frames plays its NON-hit
            # frames fast (BETWEEN_HIT_MULT). `anim_hits` is sheet-relative, like FRAME_DURATIONS.
            anim_hits = HIT_FRAMES.get((char, anim))
            snappy = (
                group == "characters"
                and (anim.startswith("attack") or anim.startswith("special"))
                and anim_hits is not None
            )
            res_id = f"{idx}_{anim}"

            # Drop the idle-reference frame 0 from action animations, so they start
            # on their real first frame. `start` maps sheet index -> emitted index
            # (emitted = sheet - start). "idle" always keeps frame 0; any other anim
            # can too via a `keep_first` override (e.g. a run that eases out of the
            # neutral pose -- pair with loop_from so frame 0 only shows as the intro).
            start = 0 if (anim == "idle" or tweak.get("keep_first")) else 1
            if sheet.n - start < 1:
                raise SystemExit(
                    f"{char}/{anim}: only {sheet.n} frame(s); need at least "
                    f"{start + 1} (frame 0 is the idle reference)"
                )
            n_emitted = sheet.n - start
            sheet_starts[anim] = start
            for fi in frame_durs:
                if not start <= fi < sheet.n:
                    raise SystemExit(
                        f"{char}/{anim}: FRAME_DURATIONS index {fi} out of range; "
                        f"sheet frames are {start}-{sheet.n - 1} (0 is the idle reference)"
                    )
            rel = sheet.png.relative_to(PROJECT).as_posix()
            ext.append(
                f'[ext_resource type="Texture2D" uid="{uid_for(sheet.png)}" '
                f'path="res://{rel}" id="{res_id}"]'
            )

            # Left pad places the animation's content centre on the canvas centre;
            # top pad drops the frame onto the canvas bottom edge.
            pad_x = round(canvas_w / 2 - sheet.fw / 2 - sheet.bias)
            pad_y = canvas_h - sheet.h
            assert 0 <= pad_x <= canvas_w - sheet.fw, (char, anim, pad_x)

            frames = []
            total_dur = 0.0
            for i in range(start, sheet.n):
                sid = f"{anim}_{i}"
                sub.append(
                    f'[sub_resource type="AtlasTexture" id="{sid}"]\n'
                    f'atlas = ExtResource("{res_id}")\n'
                    f"region = Rect2({i * sheet.fw}, 0, {sheet.fw}, {sheet.h})\n"
                    f"margin = Rect2({pad_x}, {pad_y}, "
                    f"{canvas_w - sheet.fw}, {canvas_h - sheet.h})"
                )
                if i in frame_durs:
                    duration = frame_durs[i]  # explicit per-frame override wins
                elif i == sheet.n - 1 and hold_last != 1.0:
                    duration = hold_last  # explicit hold of the final pose wins
                elif snappy and i not in anim_hits:
                    duration = BETWEEN_HIT_MULT  # non-hit frame -> fast wind-up / in-between
                else:
                    duration = 1.0  # a hit frame (or an anim the snappy rule doesn't touch)
                total_dur += duration
                frames.append(f'{{\n"duration": {duration},\n"texture": SubResource("{sid}")\n}}')

            # loop_from / loop_to write metadata but DON'T force a Godot loop flag: a looping
            # anim (player idle/run) clamps its cycle to the range, while a non-looping one
            # (an enemy's channeled/rage attack) uses loop_from as the frame a code re-play
            # restarts at -- so the lead-in plays once. See player.gd and enemy._replay_from.
            loop_from = int(tweak.get("loop_from", 0))
            if loop_from:
                if not start <= loop_from < sheet.n:
                    raise SystemExit(
                        f"{char}/{anim}: loop_from={loop_from} out of range; sheet "
                        f"frames are {start}-{sheet.n - 1} (0 is the idle reference)"
                    )
                loop_points[anim] = loop_from - start  # emitted index

            loop_to = int(tweak.get("loop_to", 0))
            if loop_to:
                if not loop_from <= loop_to < sheet.n:
                    raise SystemExit(
                        f"{char}/{anim}: loop_to={loop_to} out of range; must be "
                        f"loop_from ({loop_from})..{sheet.n - 1}"
                    )
                loop_ends[anim] = loop_to - start  # emitted index

            # Hit frames -> emitted indices. Default: every emitted frame is a hit
            # (one frame per click). Player reads these to drive the combo.
            raw_hits = HIT_FRAMES.get((char, anim))
            if raw_hits is not None:
                hits = sorted({h - start for h in raw_hits})
                if hits and not (0 <= hits[0] and hits[-1] < n_emitted):
                    raise SystemExit(
                        f"{char}/{anim}: hit frames {raw_hits} out of range; sheet "
                        f"frames are {start}-{sheet.n - 1}"
                    )
                if hits and hits[-1] != n_emitted - 1:
                    print(
                        f"  note: {char}/{anim} last hit is frame "
                        f"{hits[-1] + start}, not the final frame "
                        f"{sheet.n - 1}; trailing frames won't play"
                    )
            else:
                hits = list(range(n_emitted))
            # Emit hit_frames for the player's light attack (always) and for any
            # anim with an explicit entry (e.g. an enemy's attack).
            if anim == "attack" or (char, anim) in HIT_FRAMES:
                hit_points[anim] = hits

            # Playtime is the sum of every frame's duration (hold_last and any
            # FRAME_DURATIONS multipliers included) over the fps.
            seconds = total_dur / fps
            note = ""
            if loop_from or loop_to:
                lo = loop_from - start if loop_from else 0
                hi = loop_to - start if loop_to else n_emitted - 1
                note += f"[loop {lo}-{hi}]"
            if raw_hits is not None:
                note += f"[hits{hits}]"
            timings.append(f"{anim}:{n_emitted}f/{seconds:.2f}s" + ("*" if tweak else "") + note)

            anim_entries.append(
                "{\n"
                '"frames": [' + ", ".join(frames) + "],\n"
                f'"loop": {str(loop).lower()},\n'
                f'"name": &"{anim}",\n'
                f'"speed": {fps}\n'
                "}"
            )

        load_steps = len(ext) + len(sub) + 1
        # Read back by player.gd: loop_from/loop_to bound a looping animation's
        # cycle; hit_frames drives the segmented attack combo; sheet_start lets
        # hand-authored configs use sheet-frame numbers.
        meta = ""
        if loop_points:
            pairs = ", ".join(f'"{a}": {i}' for a, i in loop_points.items())
            meta += f"metadata/loop_from = {{{pairs}}}\n"
        if loop_ends:
            pairs = ", ".join(f'"{a}": {i}' for a, i in loop_ends.items())
            meta += f"metadata/loop_to = {{{pairs}}}\n"
        if hit_points:
            pairs = ", ".join(f'"{a}": {i}' for a, i in hit_points.items())
            meta += f"metadata/hit_frames = {{{pairs}}}\n"
        if sheet_starts:
            pairs = ", ".join(f'"{a}": {i}' for a, i in sheet_starts.items())
            meta += f"metadata/sheet_start = {{{pairs}}}\n"
        # Preserve any existing resource uid so scenes referencing this .tres by uid
        # (player.tscn holds khalid.tres as its design-time default) don't dangle on
        # regen. Files without one stay uid-less -- everything else loads by path.
        out = out_dir / f"{char}.tres"
        uid_attr = f' uid="{uid}"' if (uid := existing_uid(out)) else ""
        body = (
            f'[gd_resource type="SpriteFrames" load_steps={load_steps} format=3{uid_attr}]\n\n'
            + "\n".join(ext)
            + "\n\n"
            + "\n\n".join(sub)
            + "\n\n[resource]\n"
            + meta
            + "animations = ["
            + ", ".join(anim_entries)
            + "]\n"
        )
        out.write_text(body)
        print(f"  {out.relative_to(PROJECT)}")
        print(f"      {'  '.join(timings)}")


def main() -> int:
    for group, anims in GROUPS.items():
        process_group(group, anims)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

using Godot;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// The reward catalog — PURE DATA (the <see cref="Rewards"/> service turns these into <see cref="Reward"/>
/// objects, applies the build conditions, and runs the effects). <c>door_type → [reward dicts]</c>; each level
/// rolls ONE door type (RunManager), clearing the arena opens it, and the player picks one offered reward.
/// C# port of <c>configs/rewards_catalog.gd</c> — the rows keep the same shape (<see cref="Reward.Make"/> dicts).
/// Numbers are placeholders; the typed reward foundation (docs/rewards-design.md) supersedes these as the real
/// set lands.
/// </summary>
public static class RewardsCatalog
{
    public static readonly Dictionary<DoorType, GArr> POOLS = Build();

    // Terse reward-row builder: id/name/desc + an optional extras dict merged on top.
    private static GDict R(string id, string name, string desc, GDict extra = null)
    {
        var d = new GDict { { "id", id }, { "name", name }, { "desc", desc } };
        if (extra != null)
            foreach (var k in extra.Keys)
                d[k] = extra[k];
        return d;
    }

    private static Dictionary<DoorType, GArr> Build() => new()
    {
        [DoorType.Health] = new GArr
        {
            R(RewardIds.Mend, "Mend", "Heal +40 HP now"),
            R(RewardIds.MaxHp, "Second Skin", "+25 max HP (and heal it)"),
        },
        [DoorType.Athletic] = new GArr
        {
            R(RewardIds.AirJump, "Extra Wind", "+1 air jump"),
            R(RewardIds.Run, "Fleetfoot", "+10% run speed"),
            R(RewardIds.Tough, "Thick Hide", "-10% damage taken"),
            R(RewardIds.SlamDmg, "Meteor", "+25% slam damage"),
            R(RewardIds.CrimsonVortex, "Crimson Vortex", "Your dash leaves a damaging vortex"),
        },
        [DoorType.Attack] = new GArr
        {
            // SYNERGY: a charm special equipped makes reach ~3x likelier to roll (still just a nudge).
            R(RewardIds.Reach, "Long Arm", "+15% attack reach", new GDict
            {
                { "synergy", new GDict { { "when", new GDict { { "tag", "charm" } } }, { "weight", 3.0 } } },
            }),
            R(RewardIds.AtkDmg, "Bloodlust", "+12% attack damage"),
            R(RewardIds.Lifesteal, "Leech", "Heal 8% of damage dealt", new GDict { { "passive", PassiveIds.Leech } }),
            R(RewardIds.Multishot, "Split Shot", "+1 projectile (WIP)"),
            // PER-MOVE BUFF: +25% Twin Reaper only; offered once it's equipped; unique.
            R(RewardIds.ReaperEdge, "Reaper's Edge", "+25% Twin Reaper damage", new GDict
            {
                { "unique", true },
                { "requires", new GDict { { "equipped", AttackIds.TwinReaper } } },
                { "passive", PassiveIds.ReaperEdge },
            }),
            // INDEPENDENT MOVE: a standalone attack you can swap to (upgrades via its own buffs).
            R(RewardIds.RopeDart, "Rope Dart", "A weighted blade on a whipping rope", new GDict
            {
                { "icon", "res://vfx/shared/textures/blast1.png" },
                { "tier", (int)Tier.Epic },
                { "unique", true },
                { "equip", new GDict { { "category", "attack" }, { "id", AttackIds.RopeDart } } },
            }),
        },
        [DoorType.Special] = new GArr
        {
            R(RewardIds.RuhCap, "Deeper Ruh", "+1 Ruh charge (max 5)"),
            R(RewardIds.LongerImp, "Fortitude", "+3s Aegis (invuln) duration"),
            R(RewardIds.ImpUntilHit, "Last Stand", "Aegis lasts until you're hit (WIP)"),
            R(RewardIds.BiggerBlast, "Wide Impact", "+20% special hit radius (WIP)"),
            // PER-MOVE BUFF: a perfect parry with Redere Shield also heals; offered once it's equipped.
            R(RewardIds.ParryMend, "Guardian's Mend", "Perfect parry also heals you", new GDict
            {
                { "unique", true },
                { "requires", new GDict { { "equipped", SpecialIds.RedereShield } } },
                { "passive", PassiveIds.ParryMend },
            }),
        },
    };
}

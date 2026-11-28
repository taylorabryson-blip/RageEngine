// Rage Engine 0.11 - foundation.
//
// 1. GROUNDED HIT REACTIONS FOR EVERY BRAIN.
//    The game's stock brains (HumanEasy/Medium/Hard...) turn head and leg hits of push level 2+ into "Destabilize"
//    (= fall over) and level 1 head hits into a full stagger. Every NPC that wasn't using Rage Engine's own brain
//    (most of them: waves and creature tables pick their own brain) fell over after a couple of hits. Rage Engine now
//    retunes the hit-reaction table of whatever brain an NPC has, at runtime, so no brain files are replaced and other
//    mods' brains keep everything else they change:
//      level 0-1  -> no canned stagger (physics only)
//      level 2    -> small stagger at most
//      level 3    -> medium/full stagger, never a fall
//      level 4    -> full stagger; a fall only if already staggering or mid-jump
//    Original values are kept and put back when the mod is disabled.
//
// 2. CLEAN NPCs (pooling). The game re-uses NPC bodies. Anything Rage Engine changed on a body (closed eyes, a broken
//    jaw pose, a crushed head's scale, loosened joints, muscle drives, limb strength) is now put back the moment the
//    NPC despawns - before 0.11 a re-used body could spawn with a squashed head, droopy eyes and a hanging jaw.
//
// 3. COMPATIBILITY. If another death mod (e.g. Realistic Deaths) is loaded, Rage Engine leaves deaths to it: no
//    revives (dying moments, adrenaline, dismemberment survival) and no forced kills (shock deaths). Detected
//    automatically; also a manual option.
using System;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using Effect = ThunderRoad.BrainModuleHitReaction.PushBehaviour.Effect;
using PushBehaviour = ThunderRoad.BrainModuleHitReaction.PushBehaviour;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ---------------- 1. grounded push tables ----------------
        struct PushOrig { public Effect idle, combat, stagger, staggerFull, attack, jump; }
        static readonly Dictionary<PushBehaviour, PushOrig> pushOrig = new Dictionary<PushBehaviour, PushOrig>();

        static void GroundBrain(NpcState s)
        {
            if (!groundedReactions) return;
            var c = s.creature;
            var hr = c.brain != null && c.brain.instance != null ? c.brain.instance.GetModule<BrainModuleHitReaction>(false) : null;
            if (hr == null || hr.pushHitBehaviors == null) { Log($"{c.name}: no hit-reaction module to ground"); return; }
            int changed = 0;
            foreach (var p in hr.pushHitBehaviors)
            {
                if (p == null || pushOrig.ContainsKey(p)) continue;
                pushOrig[p] = new PushOrig { idle = p.duringIdle, combat = p.duringCombat, stagger = p.duringStagger, staggerFull = p.duringStaggerFull, attack = p.duringAttack, jump = p.duringAttackJump };
                p.duringIdle = Grounded(p.level, p.duringIdle, false);
                p.duringCombat = Grounded(p.level, p.duringCombat, false);
                p.duringStagger = Grounded(p.level, p.duringStagger, false);
                p.duringStaggerFull = Grounded(p.level, p.duringStaggerFull, true);
                p.duringAttack = Grounded(p.level, p.duringAttack, false);
                p.duringAttackJump = Grounded(p.level, p.duringAttackJump, true);
                changed++;
            }
            if (changed > 0) Log($"{c.name}: grounded {changed} hit-reaction entries (brain {c.brain.instance.id})");
        }

        /// The most a hit of this push level may do. 'unstable' = they were already off balance (staggering / jumping).
        static Effect Grounded(int level, Effect e, bool unstable)
        {
            Effect cap;
            if (level <= 1) cap = Effect.None;
            else if (level == 2) cap = Effect.StaggerLight;
            else if (level == 3) cap = unstable ? Effect.StaggerFull : Effect.StaggerMedium;
            else cap = Effect.StaggerFull;              // even mid-stagger a hit doesn't floor them (punch-drunk does)
            return (int)e > (int)cap ? cap : e;
        }

        static void UngroundAll()
        {
            foreach (var kv in pushOrig)
            {
                var p = kv.Key; var o = kv.Value;
                if (p == null) continue;
                p.duringIdle = o.idle; p.duringCombat = o.combat; p.duringStagger = o.stagger;
                p.duringStaggerFull = o.staggerFull; p.duringAttack = o.attack; p.duringAttackJump = o.jump;
            }
            pushOrig.Clear();
        }

        // ---------------- 2. put the body back the way the game made it ----------------
        static readonly Dictionary<CharacterJoint, SoftJointLimit[]> origLimits = new Dictionary<CharacterJoint, SoftJointLimit[]>();

        internal static void RememberLimits(CharacterJoint j)
        {
            if (j == null || origLimits.ContainsKey(j)) return;
            origLimits[j] = new[] { j.swing1Limit, j.swing2Limit, j.lowTwistLimit, j.highTwistLimit };
        }

        /// Undo every change Rage Engine made to this NPC's body (called on despawn / release).
        static void RestoreBody(NpcState s)
        {
            var c = s.creature;
            if (c == null) return;
            Guard("restore", () =>
            {
                // head scale (crushed skull)
                if (s.headMesh != null) { s.headMesh.localScale = s.headScale; RemoveCrushed(s.headMesh); s.headMesh = null; }
                HeadDeform.Restore(c);
                SkinPaint.Restore(c);
                // jaw pose
                if (s.jawBroken && c.jaw != null) c.jaw.localRotation = s.jawRest;
                if (s.jawBroken) try { var speak = c.brain.instance.GetModule<BrainModuleSpeak>(false); if (speak != null) speak.moveMouth = true; } catch { }   // 0.54: the next NPC can talk again
                s.jawBroken = s.jawTorn = false;
                // eyes
                c.autoEyeClipsActive = true;
                if (c.allEyes != null) foreach (var e in c.allEyes) if (e != null) { e.closeAmount = 0f; e.SetClose(); }
                RemoveCorpse(c);
                // sclera colour from choking
                if (s.scleraTinted) { c.SetColor(s.scleraOrig, Creature.ColorModifier.EyesSclera, true); s.scleraTinted = false; }
                // joint limits on broken limbs
                if (c.ragdoll != null && c.ragdoll.parts != null)
                    foreach (var p in c.ragdoll.parts)
                    {
                        var j = p != null ? p.characterJoint : null;
                        if (j == null || !origLimits.TryGetValue(j, out var l)) continue;
                        j.swing1Limit = l[0]; j.swing2Limit = l[1]; j.lowTwistLimit = l[2]; j.highTwistLimit = l[3];
                        origLimits.Remove(j);
                    }
            });
        }

        static void RemoveCrushed(Transform t)
        {
            for (int i = crushedHeads.Count - 1; i >= 0; i--) if (crushedHeads[i].t == t) crushedHeads.RemoveAt(i);
        }

        static void RemoveCorpse(Creature c)
        {
            for (int i = corpses.Count - 1; i >= 0; i--) if (corpses[i].c == c) corpses.RemoveAt(i);
        }

        /// True while the game still has this NPC in the world (not despawned / pooled).
        static bool InWorld(Creature c)
        {
            if (c == null || !c.loaded || !c.gameObject.activeInHierarchy) return false;
            var all = Creature.allActive;
            return all == null || all.Contains(c);
        }

        // ---------------- 3. compatibility ----------------
        static bool otherDeathModChecked, otherDeathMod;

        /// Another mod that handles deaths is loaded (Realistic Deaths or similar) - or the user said so.
        internal static bool DeathsHandledElsewhere
        {
            get
            {
                if (leaveDeathsToOtherMods) return true;
                if (!otherDeathModChecked)
                {
                    otherDeathModChecked = true;
                    try
                    {
                        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            string n = a.GetName().Name ?? "";
                            if (n.StartsWith("RageEngine", StringComparison.OrdinalIgnoreCase)) continue;
                            string l = n.ToLowerInvariant();
                            if (l.Contains("realisticdeath") || l.Contains("realistic_death") || l.Contains("realistic death")
                                || (l.Contains("death") && (l.Contains("realistic") || l.Contains("crawl") || l.Contains("dying"))))
                            {
                                otherDeathMod = true;
                                Debug.Log("[RageEngine] compatibility: '" + n + "' handles deaths - Rage Engine will not revive or force-kill NPCs");
                            }
                        }
                    }
                    catch (Exception e) { Debug.LogWarning("[RageEngine] compatibility scan failed: " + e); }
                }
                return otherDeathMod;
            }
        }
    }
}

// Nomad Rage Engine 0.10 - combat feel.
//   - Full-body physics: the game normally only switches an NPC's ragdoll physics on right where your hand touches
//     them (everything else just plays animation), which is why pushes, bullets and the Euphoria reactions didn't
//     show. Rage Engine forces the whole body physical for NPCs near you (and anyone just shot / blasted).
//   - Punch-drunk: knockdowns from punches build up. Clean shots to the head fill a "rocked" meter (body shots a bit);
//     when it's full they drop. It drains over time. Not tied to health - a full-health and a half-dead NPC take the
//     same number of clean shots (glass jaws take half). One giant force still drops anyone instantly.
//   - Slams: a body part hitting the world hard (thrown into a wall, slammed into the floor) counts as an impact.
//   - Haymaker holds: hold an NPC's arm or leg with one hand and hit it with the other - it breaks easily, bone out.
//   - Tougher limbs: arms/legs don't rip off from ordinary grabbing and punching.
//   - Brawlers: NPCs from the Bar Brawl wave (or everyone, with the option) drop their weapons and fight with fists.
//   - Death: the eyes close and there's a last gasp, so you know they're gone.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static readonly object PhysHandler = new object();
        static readonly Dictionary<RagdollPart, float> origBreakMult = new Dictionary<RagdollPart, float>();
        static CollisionInstance lastHandled;

        sealed class Corpse { public Creature c; public float t0; }
        static readonly List<Corpse> corpses = new List<Corpse>();
        struct Pending { public float at; public Transform t; public string clip; public float vol; }
        static readonly List<Pending> pending = new List<Pending>();

        // ---------------- full-body physics ----------------
        void TickPhysics(NpcState s)
        {
            var c = s.creature;
            var rag = c.ragdoll;
            if (rag == null) return;
            bool want = false;
            if (forcePhysics && !c.isKilled)
            {
                if (Time.time < s.physUntil) want = true;
                else
                {
                    var cam = Player.local != null && Player.local.head != null && Player.local.head.cam != null ? Player.local.head.cam.transform : null;
                    if (cam != null && (c.transform.position - cam.position).sqrMagnitude <= physicsRange * physicsRange) want = true;
                }
            }
            if (want == s.physForced) return;
            SetForcePhysic(s, want);
        }

        static void SetForcePhysic(NpcState s, bool on)
        {
            var rag = s.creature != null ? s.creature.ragdoll : null;
            if (rag == null) return;
            Guard("forcephysic", () =>
            {
                if (on) rag.forcePhysic.Add(PhysHandler); else rag.forcePhysic.Remove(PhysHandler);
                s.physForced = on;
            });
            Log($"{s.creature.name} full-body physics {(on ? "ON" : "off")}");
        }

        /// Makes sure this NPC is physical right now (bullets, blasts) and stays so for a few seconds.
        internal void PhysicalNow(Creature c, float seconds)
        {
            if (!TryGet(c, out var s)) return;
            s.physUntil = Mathf.Max(s.physUntil, Time.time + seconds);
            if (!s.physForced) SetForcePhysic(s, true);
        }

        // ---------------- tougher limbs ----------------
        static void ToughenLimbs(NpcState s, bool on)
        {
            var rag = s.creature != null ? s.creature.ragdoll : null;
            if (rag == null || rag.parts == null) return;
            Guard("limbs", () =>
            {
                foreach (var p in rag.parts)
                {
                    if (p == null || !Has(p.type, ArmParts | LegParts)) continue;
                    if (on)
                    {
                        if (origBreakMult.ContainsKey(p)) continue;
                        origBreakMult[p] = p.breakForceMultiplier;
                        p.breakForceMultiplier = Mathf.Max(p.breakForceMultiplier, 1f) * 3f;
                    }
                    else if (origBreakMult.TryGetValue(p, out var o)) { p.breakForceMultiplier = o; origBreakMult.Remove(p); }
                }
            });
        }

        // ---------------- Haymaker holds ----------------
        /// True if the player is holding this NPC by the same arm/leg (or exact part) that was hit.
        static bool PlayerHolding(NpcState s, RagdollPart part)
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null || part == null) return false;
            var limb = LimbOf(part.type);
            foreach (var h in new[] { pc.handLeft, pc.handRight })
            {
                if (h == null) continue;
                var hr = h.grabbedHandle as HandleRagdoll;
                if (hr == null || hr.ragdollPart == null || hr.ragdollPart.ragdoll != part.ragdoll) continue;
                if (hr.ragdollPart == part || (limb != 0 && LimbOf(hr.ragdollPart.type) == limb)) return true;
            }
            return false;
        }

        // ---------------- brawlers ----------------
        static bool IsBrawler(Creature c)
        {
            bool b = false;
            Guard("brawler", () =>
            {
                string id = c.brain != null && c.brain.instance != null ? c.brain.instance.id : null;
                if (string.IsNullOrEmpty(id) && c.data != null) id = c.data.brainId;
                b = id != null && id.IndexOf("Brawler", System.StringComparison.OrdinalIgnoreCase) >= 0;
            });
            return b;
        }

        void TickBrawler(NpcState s)
        {
            if (!s.brawler) return;
            var c = s.creature;
            if (c.isKilled) return;
            foreach (var h in new[] { c.handLeft, c.handRight })
            {
                if (h == null || h.grabbedHandle == null || h.grabbedHandle is HandleRagdoll) continue;
                var item = h.grabbedHandle.item;
                h.UnGrab(false);
                if (item != null) Guard("despawn", () => item.Despawn());
                Log($"{c.name} brawler drops weapon");
            }
        }

        // ---------------- death: eyes close, last gasp ----------------
        void OnFinalDeath(Creature victim, NpcState s)
        {
            bool gasp = deathGasp && (s == null || !s.noGasp);
            // 0.25: no synthesized gasp any more - the game's own recorded death voice plays
            if (deathEyesClose)
            {
                while (corpses.Count >= 40) corpses.RemoveAt(0);
                corpses.Add(new Corpse { c = victim, t0 = Time.time + (gasp ? 0.9f : 0.25f) });
            }
        }

        static void TickPending()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (Time.time < p.at) continue;
                pending.RemoveAt(i);
                if (p.t != null) Sfx.Play(p.clip, p.t, p.vol);
            }
        }

        static void LateUpdateCorpses()
        {
            for (int i = corpses.Count - 1; i >= 0; i--)
            {
                var k = corpses[i];
                if (k.c == null || !InWorld(k.c) || !k.c.isKilled)
                {
                    // despawned (the body will be re-used) or alive again: open the eyes back up
                    var cc = k.c;
                    if (cc != null) Guard("eyes", () =>
                    {
                        cc.autoEyeClipsActive = true;
                        if (cc.allEyes != null) foreach (var e in cc.allEyes) if (e != null) { e.closeAmount = 0f; e.SetClose(); }
                    });
                    corpses.RemoveAt(i); continue;
                }
                float f = (Time.time - k.t0) / 1.3f;
                if (f <= 0f) continue;
                float close = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(f));
                Guard("eyes", () =>
                {
                    k.c.autoEyeClipsActive = false;
                    var eyes = k.c.allEyes;
                    if (eyes == null) return;
                    for (int e = 0; e < eyes.Count; e++)
                    {
                        if (eyes[e] == null) continue;
                        eyes[e].closeAmount = close;
                        eyes[e].SetClose();
                    }
                });
            }
        }

        // ---------------- slams into the world ----------------
        /// A body part hitting static geometry (wall, floor, pillar) at speed: thrown, slammed, dropped.
        void HandleSlam(NpcState s, CollisionInstance hit, RagdollPart part, bool dead)
        {
            if (part.physicBody == null) return;
            float v = Mathf.Max(hit.impactVelocity.magnitude, part.physicBody.velocity.magnitude);
            if (v > 25f) return;                                                   // teleport / snap, not a real slam
            float slam = Mathf.Max(0f, v - 4f) * 0.5f;                            // 6 m/s = 1, 12 m/s = 4, 14 m/s = 5
            if (slam < 1f) return;
            if (Time.time - s.lastSlamTime < 0.2f) return;
            s.lastSlamTime = Time.time;
            Vector3 point = hit.contactPoint;
            Vector3 dir = part.physicBody.velocity.sqrMagnitude > 0.01f ? part.physicBody.velocity.normalized : -hit.contactNormal;
            if (slam >= 1.5f && goreTextures) Stamp(s.creature, point, Mathf.Clamp(0.03f + slam * 0.006f, 0.03f, 0.06f), "abrasion", 0.8f);   // 0.22: scraped up
            if (Time.time >= s.nextHitSfx)
            {
                s.nextHitSfx = Time.time + 0.15f;
                Sfx.PlayAt("NRE_BodyHit", point, Mathf.Clamp(0.5f + slam * 0.15f, 0.5f, 1f));
            }
            HeavyBlunt(s, part, point, dir, slam, true);
            if (!dead)
            {
                Guard("bruises", () => AddBruise(s, part, point, hit.contactNormal, slam));
                s.pain = Mathf.Min(1f, s.pain + 0.15f * slam * painIntensity);
                s.daze += Has(part.type, RagdollPart.Type.Head) ? 0.3f * slam : 0f;
            }
            Log($"{s.creature.name} SLAM {part.type} v={v:0.0} sev={slam:0.0}");
        }
    }
}

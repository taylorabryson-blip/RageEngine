// Nomad Rage Engine - muscles. B&S NPCs are active ragdolls: every body part is a physics body pulled toward the
// animation by a joint "drive" (a spring). Two layers, applied every frame so the game can't override them:
//   1. the game's Ragdoll.SetPinForceMultiplier (per body part)
//   2. the actual physics joint drives of each part (Ragdoll.Bone.animationJoint), scaled directly
// Uses: hit reactions (the struck region goes loose so physics moves it), wounded posture (hunching as they get hurt),
// pained arms (weak, trembling grip), and dead limbs (broken / severed tendons / stumps hang with ~zero muscle -
// they can't reach, grab or put their hands up).
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const float LimpMuscle = 0.02f;

        struct Drives { public JointDrive slerp, ax, ayz, x, y, z; }
        static readonly Dictionary<ConfigurableJoint, Drives> origDrives = new Dictionary<ConfigurableJoint, Drives>();

        /// Make a region go loose: amount 0..1 (1 = fully limp), recovering over duration seconds.
        internal static void Loosen(NpcState s, RagdollPart.Type parts, float amount, float duration)
        {
            amount = Mathf.Clamp01(amount);
            if (amount <= 0f || duration <= 0f) return;
            float now = Time.time;
            if (now < s.looseUntil)
            {
                s.looseParts |= parts;
                s.looseAmount = Mathf.Max(s.looseAmount * ((s.looseUntil - now) / Mathf.Max(0.01f, s.looseDuration)), amount);
                s.looseUntil = Mathf.Max(s.looseUntil, now + duration);
            }
            else { s.looseParts = parts; s.looseAmount = amount; s.looseUntil = now + duration; }
            s.looseDuration = s.looseUntil - now;
        }

        /// Muscle multiplier (0..1) this NPC's part should have right now.
        const float CoreBrace = 2.2f;

        static float PartMuscle(NpcState s, RagdollPart.Type t, bool standing, float looseMult, float hunch, bool root)
        {
            bool leg = Has(t, LegParts);
            // core brace: while the upper body takes a hit, hips and legs get STRONGER so it can bend without falling
            if (s.shocked) return 1f;                                               // 0.20: electrocuted - the game owns the body
            if (standing && s.ppSinkUntil > 0f && (leg || root)) return Mathf.Lerp(1f, 0.12f, SinkT(s));
            if (s.bioStrength > 1.01f && Has(t, ArmParts) && !Has(s.limpParts, t) && Time.time >= s.looseUntil) return s.bioStrength;   // 0.24: serum strength   // 0.22: sinks through the knees
            if (standing && leg && Time.time < s.buckleUntil && Has(s.buckleLegs, t)) return 0.5f;   // 0.21: bends, doesn't fold   // 0.20: the knee gives
            if (standing && (root || leg) && Time.time < s.coreUntil && !Has(s.limpParts, t)) return CoreBrace;
            if (standing && leg && !Has(s.limpParts, t))
            {
                float ls = LegStrength(s, Has(t, LeftLegParts) ? Side.Left : Side.Right);
                if (ls < 0.85f) return Mathf.Lerp(0.72f, 1f, ls);   // 0.21: bowed but still carrying him                    // 0.20: cut leg muscles: the leg stays bowed
            }
            // spine bend: the chest may give with the hit, only while braced (never the hips)
            if (standing && !root && Has(t, RagdollPart.Type.Torso) && Time.time < s.coreUntil && looseMult < 1f && Has(s.looseParts, t))
                return Mathf.Max(0.35f, looseMult);
            if (Has(s.limpParts, t))
                // a hurt leg still has to carry them while they're up: weak (limp), never zero - zero = they trip and fall
                return standing && leg && !s.spineBroken ? 0.6f : LimpMuscle;
            // standing legs are never loosened by reactions: feet stay planted (0.11 fix for random tripping)
            if (standing && (leg || Has(t, RagdollPart.Type.Torso))) return 1f;   // trunk too: a weak trunk drops B&S NPCs (0.12)
            float m = 1f;
            if (standing)
            {
                if (looseMult < 1f && Has(s.looseParts, t)) m = looseMult;
                if (hunch > 0f && Has(t, RagdollPart.Type.Torso | RagdollPart.Type.Neck | RagdollPart.Type.Head)) m *= 1f - hunch;
            }
            if (Time.time < s.weakArmLUntil && Has(t, LeftArmParts)) m *= 0.5f;
            if (Time.time < s.weakArmRUntil && Has(t, RightArmParts)) m *= 0.5f;
            // 0.14: cut muscles - a cut biceps/deltoid can't hold the arm up as well, a cut neck lets the head loll
            if (Has(t, LeftArmParts)) m *= Mathf.Lerp(0.15f, 1f, ArmStrength(s, Side.Left));
            else if (Has(t, RightArmParts)) m *= Mathf.Lerp(0.15f, 1f, ArmStrength(s, Side.Right));
            else if (Has(t, RagdollPart.Type.Neck | RagdollPart.Type.Head)) m *= Mathf.Lerp(0.25f, 1f, NeckStrength(s));
            return m;
        }

        void TickMuscles(NpcState s)
        {
            var c = s.creature;
            var rag = c.ragdoll;
            if (rag == null || (c.isKilled && !s.onAdrenaline && !s.dying)) return;
            bool standing = rag.state == Ragdoll.State.Standing;
            float now = Time.time;

            float looseMult = 1f;
            if (now < s.looseUntil && s.looseAmount > 0f)
            {
                float f = (s.looseUntil - now) / Mathf.Max(0.01f, s.looseDuration);
                looseMult = 1f - s.looseAmount * Mathf.SmoothStep(0f, 1f, f);
            }
            float hunch = Pumped(s) ? 0f : WoundedHunch(s);

            var parts = rag.parts;
            if (parts == null) return;
            bool anyChange = false;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null) continue;
                float m = PartMuscle(s, p.type, standing, looseMult, hunch, p == rag.rootPart);
                if (Mathf.Abs(m - 1f) > 0.001f) { ScaleJoint(s, p, m); anyChange = true; }
                else if (s.scaledParts.Contains(p)) RestoreJoint(s, p);
            }

            // game-side pin multipliers as a second layer
            if (standing && looseMult < 1f)
            {
                var upper = s.looseParts & ~LegParts;
                if (upper != 0) rag.SetPinForceMultiplier(looseMult, looseMult, looseMult, looseMult, noRoot: true, partTypes: upper);
                s.pinOverridden = true;
            }
            else if (s.pinOverridden && standing) { rag.ResetPinForce(); s.pinOverridden = false; }
            var limp = LimpMask(s, standing);
            if (limp != 0) rag.SetPinForceMultiplier(LimpMuscle, 0.2f, LimpMuscle, LimpMuscle, noRoot: true, partTypes: limp);
            s.musclesActive = anyChange;
        }

        /// As they get hurt they hunch: weaker trunk/neck muscles, from pain and lost health.
        static float WoundedHunch(NpcState s)
        {
            float hp = HealthFraction(s);
            return Mathf.Clamp01((1f - hp) * 0.15f + s.pain * painIntensity * 0.08f + (s.ribsBroken > 0 ? 0.12f : 0f));   // 0.20: less permanent leaning
        }

        internal static float HealthFraction(NpcState s)
        {
            float hp = 1f;
            Guard("health", () => { var c = s.creature; if (c.maxHealth > 0f) hp = Mathf.Clamp01(c.currentHealth / c.maxHealth); });
            return hp;
        }

        static void ScaleJoint(NpcState s, RagdollPart p, float m)
        {
            var j = p.bone != null ? p.bone.animationJoint : null;
            if (j == null) return;
            if (!origDrives.TryGetValue(j, out var o))
            {
                o = new Drives { slerp = j.slerpDrive, ax = j.angularXDrive, ayz = j.angularYZDrive, x = j.xDrive, y = j.yDrive, z = j.zDrive };
                origDrives[j] = o;
            }
            j.slerpDrive = Scale(o.slerp, m); j.angularXDrive = Scale(o.ax, m); j.angularYZDrive = Scale(o.ayz, m);
            j.xDrive = Scale(o.x, m); j.yDrive = Scale(o.y, m); j.zDrive = Scale(o.z, m);
            s.scaledParts.Add(p);
        }

        static JointDrive Scale(JointDrive d, float m)
        {
            d.positionSpring *= m;
            d.positionDamper *= Mathf.Max(m, 0.15f);
            d.maximumForce *= Mathf.Max(m, 0.02f);
            return d;
        }

        static void RestoreJoint(NpcState s, RagdollPart p)
        {
            s.scaledParts.Remove(p);
            var j = p.bone != null ? p.bone.animationJoint : null;
            if (j == null || !origDrives.TryGetValue(j, out var o)) return;
            j.slerpDrive = o.slerp; j.angularXDrive = o.ax; j.angularYZDrive = o.ayz; j.xDrive = o.x; j.yDrive = o.y; j.zDrive = o.z;
        }

        static void RestoreMuscles(NpcState s)
        {
            foreach (var p in new List<RagdollPart>(s.scaledParts)) if (p != null) RestoreJoint(s, p);
            s.scaledParts.Clear();
            var rag = s.creature != null ? s.creature.ragdoll : null;
            if (rag == null) return;
            if (s.pinOverridden || s.limpParts != 0) rag.ResetPinForce();
            s.pinOverridden = false;
        }

        /// Permanently kill the muscles of a limb (broken bone, severed tendons, stump).
        internal static void MakeLimp(NpcState s, RagdollPart.Type limb)
        {
            if (limb == 0) return;
            s.limpParts |= limb;
            var rag = s.creature.ragdoll;
            if (rag == null) return;
            var mask = LimpMask(s, rag.state == Ragdoll.State.Standing);
            if (mask != 0) rag.SetPinForceMultiplier(LimpMuscle, 0.2f, LimpMuscle, LimpMuscle, noRoot: true, partTypes: mask);
        }

        /// Parts that get zero muscle right now: dead limbs, except legs that still have to hold a standing NPC up.
        static RagdollPart.Type LimpMask(NpcState s, bool standing)
            => standing && !s.spineBroken ? s.limpParts & ~LegParts : s.limpParts;
    }
}

// Rage Engine 0.14 - musculature. Every NPC has the real muscles of the human body, per side. A cut or stab damages
// the muscle under the blade (found from where it landed on the limb: upper/lower segment, front/back, near the
// joint), and a damaged muscle does what it would do in a real person:
//   forearm flexors / hand tendons  -> grip strength (a weak grip drops the weapon; a ruined one can't close at all)
//   biceps / deltoid / triceps      -> arm strength and how long they can hold a weapon up (endurance)
//   quadriceps / hamstrings / calf  -> leg strength: slower, limping, and a ruined thigh won't hold them
//   neck muscles                    -> the head lolls
//   chest / abs / back              -> weaker swings and a hunched trunk
// Muscle damage never heals during a fight. Nothing here is a setting - it follows the wound.
// Pain on top of that: past a point the NPC is in too much agony to hold anything and lets go of everything.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal enum Mu
    {
        Deltoid, Biceps, Triceps, ForearmFlexors, ForearmExtensors, HandTendons,
        Quadriceps, Hamstrings, Calf, Tibialis, Glutes,
        Pectoralis, Abdominals, BackErectors, Neck, Count
    }

    public sealed partial class RageEngineScript
    {
        static readonly string[] MuName =
        {
            "deltoid", "biceps", "triceps", "forearm flexors", "forearm extensors", "hand tendons",
            "quadriceps", "hamstrings", "calf (gastrocnemius/soleus)", "tibialis anterior", "gluteus",
            "pectoralis", "abdominals", "back (erector spinae)", "neck (sternocleidomastoid/trapezius)"
        };
        static readonly object MuscleHandler = new object(), EyeHandler = new object();

        static float Mus(NpcState s, Side side, Mu m) => s.muscles[(side == Side.Left ? 0 : (int)Mu.Count) + (int)m];

        /// Which muscle a blade at this point on this part would cut.
        static Mu MuscleAt(NpcState s, RagdollPart part, Vector3 point, out Side side)
        {
            var c = s.creature;
            var t = part.type;
            Vector3 fwd = c.transform.forward, right = c.transform.right;
            Vector3 local = point - part.transform.position;
            bool front = Vector3.Dot(local, fwd) >= 0f;
            side = Has(t, LeftArmParts | LeftLegParts) ? Side.Left
                 : Has(t, RightArmParts | RightLegParts) ? Side.Right
                 : (Vector3.Dot(point - c.transform.position, right) < 0f ? Side.Left : Side.Right);

            if (Has(t, RagdollPart.Type.LeftHand | RagdollPart.Type.RightHand)) return Mu.HandTendons;
            if (Has(t, RagdollPart.Type.LeftFoot | RagdollPart.Type.RightFoot)) return Mu.Calf;   // Achilles / foot
            if (Has(t, RagdollPart.Type.LeftArm | RagdollPart.Type.RightArm))
            {
                // how far along this segment the blade landed (0 = near the joint above)
                Vector3 axis = LimbAxis(part);
                float along = Vector3.Dot(local, axis);
                if (IsLowerSegment(part))
                    // forearm: palm side = flexors (they close the hand), back of the arm = extensors
                    return front ? Mu.ForearmFlexors : Mu.ForearmExtensors;
                if (along < 0.06f) return Mu.Deltoid;              // the shoulder cap
                return front ? Mu.Biceps : Mu.Triceps;
            }
            if (Has(t, RagdollPart.Type.LeftLeg | RagdollPart.Type.RightLeg))
            {
                Vector3 axis = LimbAxis(part);
                float along = Vector3.Dot(local, axis);
                if (IsLowerSegment(part)) return front ? Mu.Tibialis : Mu.Calf;
                if (along < 0.05f && !front) return Mu.Glutes;
                return front ? Mu.Quadriceps : Mu.Hamstrings;
            }
            if (Has(t, RagdollPart.Type.Neck | RagdollPart.Type.Head)) return Mu.Neck;
            if (Has(t, RagdollPart.Type.Torso))
            {
                if (!front) return Mu.BackErectors;
                return point.y > part.transform.position.y + 0.05f ? Mu.Pectoralis : Mu.Abdominals;
            }
            return Mu.Count;
        }

        internal static bool IsLowerSegment(RagdollPart part)
        {
            string n = part.name.ToLowerInvariant();
            if (n.Contains("fore") || n.Contains("lower") || n.Contains("calf") || n.Contains("shin") || n.Contains("knee") || n.Contains("elbow")) return true;
            if (n.Contains("upper") || n.Contains("upleg") || n.Contains("thigh")) return false;
            var pp = part.parentPart;
            return pp != null && Has(pp.type, RagdollPart.Type.LeftArm | RagdollPart.Type.RightArm | RagdollPart.Type.LeftLeg | RagdollPart.Type.RightLeg);
        }

        /// A cut/stab landed: damage the muscle under it. Returns the damage done (0..1).
        float CutMuscle(NpcState s, RagdollPart part, Vector3 point, float dmg, bool deep)
        {
            var m = MuscleAt(s, part, point, out var side);
            if (m == Mu.Count) return 0f;
            float d = Mathf.Clamp(dmg / Mathf.Max(8f, woundThreshold * 4f), 0.08f, 0.9f) * (deep ? 1.6f : 1f);
            int i = (side == Side.Left ? 0 : (int)Mu.Count) + (int)m;
            float before = s.muscles[i];
            s.muscles[i] = Mathf.Min(1f, before + d);
            s.musclesDirty = true;
            Log($"{s.creature.name}: cut {(m == Mu.Neck || m >= Mu.Pectoralis ? "" : side + " ")}{MuName[(int)m]} {before * 100f:0}% -> {s.muscles[i] * 100f:0}% damaged" +
                $" | grip L {Grip(s, Side.Left) * 100f:0}% R {Grip(s, Side.Right) * 100f:0}%, legs L {LegStrength(s, Side.Left) * 100f:0}% R {LegStrength(s, Side.Right) * 100f:0}%");
            return s.muscles[i] - before;
        }

        /// How hard this hand can close (0..1). Pain takes some of it too.
        internal static float Grip(NpcState s, Side side)
        {
            if (Dead(s, side)) return 0f;
            float g = 1f - (0.6f * Mus(s, side, Mu.ForearmFlexors) + 0.55f * Mus(s, side, Mu.HandTendons)
                           + 0.2f * Mus(s, side, Mu.ForearmExtensors) + 0.15f * Mus(s, side, Mu.Biceps));
            g *= 1f - 0.3f * Mathf.Clamp01(s.pain * painIntensity);
            g += s.hpK * 0.5f * (1f - g);                                         // 0.23: high-HP NPCs hold on harder
            return Mathf.Clamp01(g);
        }

        /// How strong the arm is (lifting, swinging, holding a weapon up).
        internal static float ArmStrength(NpcState s, Side side)
        {
            if (Dead(s, side)) return 0f;
            float a = 1f - (0.45f * Mus(s, side, Mu.Biceps) + 0.4f * Mus(s, side, Mu.Deltoid) + 0.3f * Mus(s, side, Mu.Triceps)
                           + 0.2f * Mus(s, side, Mu.Pectoralis));
            return Mathf.Clamp01(a);
        }

        internal static float LegStrength(NpcState s, Side side)
            => Mathf.Clamp01(1f - (0.7f * Mus(s, side, Mu.Quadriceps) + 0.35f * Mus(s, side, Mu.Hamstrings)
                                  + 0.35f * Mus(s, side, Mu.Calf) + 0.15f * Mus(s, side, Mu.Tibialis) + 0.3f * Mus(s, side, Mu.Glutes)));

        internal static float NeckStrength(NpcState s)
            => Mathf.Clamp01(1f - 0.8f * Mathf.Max(Mus(s, Side.Left, Mu.Neck), Mus(s, Side.Right, Mu.Neck)));

        internal static float CoreStrength(NpcState s)
            => Mathf.Clamp01(1f - 0.4f * (Mus(s, Side.Left, Mu.Abdominals) + Mus(s, Side.Right, Mu.Abdominals))
                                - 0.4f * (Mus(s, Side.Left, Mu.BackErectors) + Mus(s, Side.Right, Mu.BackErectors)));

        /// Can this hand pick something up and hold it right now?
        internal static bool HandUsable(NpcState s, Side side) => !Dead(s, side) && Grip(s, side) >= 0.35f && Time.time >= s.agonyUntil;

        /// Every frame: weak grips give out, holding a weapon up with a cut arm tires it, ruined hands can't grab,
        /// cut legs slow them.
        void TickMusculature(NpcState s, float dt)
        {
            var c = s.creature;
            if (c.isKilled) return;
            float now = Time.time;
            for (int k = 0; k < 2; k++)
            {
                Side side = k == 0 ? Side.Left : Side.Right;
                if (Dead(s, side)) continue;
                var hand = Hand(c, side);
                if (hand == null) continue;
                float grip = Grip(s, side), arm = ArmStrength(s, side);
                bool ruined = grip < 0.3f;
                bool agony = now < s.agonyUntil;
                bool block = ruined || agony;
                if (block != (k == 0 ? s.gripBlockL : s.gripBlockR))
                {
                    if (k == 0) s.gripBlockL = block; else s.gripBlockR = block;
                    Guard("blockgrab", () => hand.SetBlockGrab(block, true));       // also stops the game's AI from picking things up with it
                }
                bool holding = hand.grabbedHandle != null && !(hand.grabbedHandle is HandleRagdoll)
                               && !(hand.grabbedHandle.item != null && phantoms.Contains(hand.grabbedHandle.item));
                ref float fat = ref (k == 0 ? ref s.holdFatigueL : ref s.holdFatigueR);
                if (holding)
                {
                    if (ruined)
                    {
                        Log($"{c.name}: {side} hand can't close any more (grip {grip * 100f:0}%) - drops {hand.grabbedHandle.item?.data?.id}");
                        Guard("drop", () => hand.UnGrab(false));
                        fat = 0f; continue;
                    }
                    float weak = 1f - Mathf.Min(grip, arm);
                    if (weak > 0.35f)                                                       // 0.25.1: only a really weak grip tires out
                    {
                        // holding a sword up with a cut arm: it tires fast, the hand shakes, then it slips
                        fat += dt * weak * weak * 0.15f * (1f + s.pain) * (1f - 0.6f * s.hpK);
                        if (fat >= 1f)
                        {
                            Log($"{c.name}: {side} {(grip < arm ? "grip" : "arm")} gave out ({Mathf.Min(grip, arm) * 100f:0}%) - drops {hand.grabbedHandle.item?.data?.id}");
                            Guard("drop", () => hand.UnGrab(false));
                            fat = 0.3f;
                            if (k == 0) s.weakArmLUntil = Mathf.Max(s.weakArmLUntil, now + 3f); else s.weakArmRUntil = Mathf.Max(s.weakArmRUntil, now + 3f);
                        }
                        else if (fat > 0.5f && hand.physicBody != null && Random.value < dt * 4f)
                            hand.physicBody.AddForce(Random.insideUnitSphere * (0.6f + fat), ForceMode.VelocityChange);   // trembling
                    }
                }
                else fat = Mathf.Max(0f, fat - dt * 0.08f);
            }

            if (s.musclesDirty)
            {
                s.musclesDirty = false;
                float l = LegStrength(s, Side.Left), r = LegStrength(s, Side.Right);
                if (l < 0.45f && !Has(s.limpParts, LeftLegParts)) { MakeLimp(s, LeftLegParts); s.legWounded = true; Log($"{c.name}: left leg muscles ruined - limping"); }
                if (r < 0.45f && !Has(s.limpParts, RightLegParts)) { MakeLimp(s, RightLegParts); s.legWounded = true; Log($"{c.name}: right leg muscles ruined - limping"); }
                float speed = Mathf.Lerp(0.35f, 1f, Mathf.Min(l, r));
                Guard("speed", () =>
                {
                    c.locomotion.globalMoveSpeedMultiplier.Remove(MuscleHandler);
                    if (speed < 0.99f) c.locomotion.globalMoveSpeedMultiplier.Add(MuscleHandler, speed);
                });
                // 0.18: a leg whose big muscles are cut can't hold them up: down, and they don't get back up
                if ((l < 0.5f || r < 0.5f) && !s.cantStand)
                {
                    s.cantStand = true;
                    Log($"{c.name}: leg muscles cut (L {l * 100f:0}% R {r * 100f:0}%) - can't stand any more");
                }
                if (s.cantStand && c.ragdoll.state == Ragdoll.State.Standing)
                {
                    AllowFall(s, 2f);
                    Stagger(s, -c.transform.forward, BrainModuleHitReaction.PushBehaviour.Effect.Destabilize, RagdollPart.Type.LeftLeg);
                }
            }
            if (s.agonyUntil > 0f && now >= s.agonyUntil) { s.agonyUntil = 0f; Log($"{c.name}: agony eases"); }
        }

        /// Pain so bad they let go of everything. Called after every hit that hurts.
        void AgonyCheck(NpcState s, float severity)
        {
            var c = s.creature;
            if (c.isKilled || Pumped(s) || s.bioNoQuit || Time.time < s.agonyUntil) return;
            float p = s.pain * painIntensity * s.painSensitivity;
            if (p < 0.88f || (severity < 0.9f && p < 0.98f)) return;                    // 0.21: only real pain from a real hit
            float chance = (p - 0.85f) * 1.4f * Mathf.Clamp(severity, 0.5f, 2f) / Mathf.Max(0.6f, s.toughness);
            if (s.brave) chance *= 0.5f;
            if (Random.value > chance) return;
            s.agonyUntil = Time.time + Mathf.Lerp(3f, 9f, Mathf.Clamp01((p - 0.75f) * 4f));
            bool dropped = false;
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                var h = Hand(c, side);
                if (h == null || h.grabbedHandle == null || h.grabbedHandle is HandleRagdoll) continue;
                var h2 = h;
                Guard("drop", () => h2.UnGrab(false));
                dropped = true;
            }
            SetMelee(s, false, s.agonyUntil);
            Scream(s, true);
            Log($"{c.name}: in agony (pain {p * 100f:0}%) - {(dropped ? "drops everything" : "can't fight")} for {s.agonyUntil - Time.time:0.0}s");
        }

        static void ClearMusculature(NpcState s)
        {
            var c = s.creature;
            for (int i = 0; i < s.muscles.Length; i++) s.muscles[i] = 0f;
            s.holdFatigueL = s.holdFatigueR = 0f; s.agonyUntil = 0f; s.musclesDirty = false;
            if (c == null) return;
            Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Remove(MuscleHandler));
            if (s.gripBlockL || s.gripBlockR)
                Guard("blockgrab", () =>
                {
                    if (c.handLeft != null) c.handLeft.SetBlockGrab(false, false);
                    if (c.handRight != null) c.handRight.SetBlockGrab(false, false);
                });
            s.gripBlockL = s.gripBlockR = false;
        }

        // ---------------- eyes ----------------
        /// Closes the eye nearest to the point (stabbed/cut eye) or both (blood running in). destroyed = blind in it.
        void HurtEyes(NpcState s, Vector3 point, bool stabbed, bool blood)
        {
            var c = s.creature;
            var eyes = c.allEyes;
            if (eyes == null || eyes.Count == 0) return;
            int nearest = -1; float best = 0.07f;
            for (int i = 0; i < eyes.Count; i++)
            {
                if (eyes[i] == null) continue;
                float d = Vector3.Distance(point, eyes[i].transform.position);
                if (d < best) { best = d; nearest = i; }
            }
            if (stabbed && nearest >= 0)
            {
                s.blindEyes |= 1 << nearest;
                Log($"{c.name}: eye {eyes[nearest].eyeTag ?? nearest.ToString()} destroyed{(AllBlind(s) ? " - fully blind" : "")}");
            }
            if (blood) s.eyesShutUntil = Mathf.Max(s.eyesShutUntil, s.eyeBloodUntil);
            UpdateVision(s);
            if (!livingEyes.Contains(s)) livingEyes.Add(s);
        }

        static bool AllBlind(NpcState s)
        {
            var eyes = s.creature != null ? s.creature.allEyes : null;
            if (eyes == null || eyes.Count == 0) return false;
            for (int i = 0; i < eyes.Count; i++) if (eyes[i] != null && (s.blindEyes & (1 << i)) == 0) return false;
            return true;
        }

        static void UpdateVision(NpcState s)
        {
            var c = s.creature;
            float v = 1f;
            if (AllBlind(s)) v = 0.02f;                                         // can't see: they lose you unless you're on top of them
            else
            {
                if (s.blindEyes != 0) v = 0.6f;                                 // one eye
                if (Time.time < s.eyesShutUntil) v = Mathf.Min(v, 0.15f);       // eyes screwed shut with blood in them
            }
            Guard("vision", () =>
            {
                c.detectionFOVModifier.Remove(EyeHandler);
                if (v < 0.999f) c.detectionFOVModifier.Add(EyeHandler, v);
            });
        }

        static readonly System.Collections.Generic.List<NpcState> livingEyes = new System.Collections.Generic.List<NpcState>();

        /// After the game's own eye animation: hold hurt eyes shut. Blood: squeezed shut, opening a crack when they wipe.
        static void LateUpdateLivingEyes()
        {
            for (int i = livingEyes.Count - 1; i >= 0; i--)
            {
                var s = livingEyes[i];
                var c = s.creature;
                if (c == null || c.isKilled || !s.active) { livingEyes.RemoveAt(i); continue; }
                bool shut = Time.time < s.eyesShutUntil;
                if (!shut && s.blindEyes == 0)
                {
                    Guard("eyes", () =>
                    {
                        c.autoEyeClipsActive = true;
                        if (c.allEyes != null) foreach (var e in c.allEyes) if (e != null) { e.closeAmount = 0f; e.SetClose(); }
                    });
                    UpdateVision(s);
                    livingEyes.RemoveAt(i); continue;
                }
                if (!shut && s.eyesWereShut) { s.eyesWereShut = false; UpdateVision(s); }
                if (shut) s.eyesWereShut = true;
                float blink = s.clutching && s.clutchWipe ? 0.65f + 0.2f * Mathf.Sin(Time.time * 7f) : 0.97f;   // wiping: peeks
                Guard("eyes", () =>
                {
                    c.autoEyeClipsActive = false;
                    var eyes = c.allEyes;
                    if (eyes == null) return;
                    for (int e = 0; e < eyes.Count; e++)
                    {
                        if (eyes[e] == null) continue;
                        float close = (s.blindEyes & (1 << e)) != 0 ? 1f : shut ? blink : 0f;
                        eyes[e].closeAmount = close;
                        eyes[e].SetClose();
                    }
                });
            }
        }
    }
}

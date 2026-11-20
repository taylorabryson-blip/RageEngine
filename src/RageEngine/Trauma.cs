// Nomad Rage Engine - trauma: screaming, dismemberment survival, flee-or-fight, adrenaline.
//   - lose an arm or leg -> they live (for now): scream, grab the stump with the other hand, bleed.
//       * most run away screaming (game "panic" behaviour); brave personalities keep pushing at you, screaming.
//       * losing a leg puts them on the ground; they keep trying and keep falling.
//       * adrenaline crashes: any moment they may just drop.
//   - adrenaline: a 'killed' NPC can keep fighting for a few seconds - even after stabs to the head - screaming, then
//     collapses for good. Decapitation always ends it.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        internal static void Speak(NpcState s, string dialogId)
        {
            Guard("voice", () =>
            {
                var speak = s.creature.brain.instance?.GetModule<BrainModuleSpeak>(false);
                if (speak != null) { speak.Play(dialogId, true, false); Log($"{s.creature.name} says '{dialogId}'"); }
                else Log($"{s.creature.name} has no speak module - can't voice '{dialogId}'");
            });
        }

        /// Screams (game voice). Rate-limited unless forced.
        internal static void Scream(NpcState s, bool force)
        {
            if (!screaming || !modVoices || s.creature == null || Silent(s)) return;
            if (!force && Time.time < s.nextScream) return;
            s.nextScream = Time.time + Random.Range(2.5f, 4.5f);
            // real recorded voices (male/female); the game's own dialog only if those didn't load
            bool female = s.creature.name.IndexOf("Female", System.StringComparison.OrdinalIgnoreCase) >= 0;
            string clip = (force ? "NRE_Scream" : "NRE_Pain") + (female ? "F" : "M");
            // 0.19: every NPC has his own voice pitch; personality changes how loud
            var pt = s.personality != null ? s.personality.type : PersonalityType.Calm;
            float vol = (force ? 1f : 0.85f) * (pt == PersonalityType.Aggressive ? 1.1f : pt == PersonalityType.Cautious ? 0.85f : pt == PersonalityType.Nervous ? 0.95f : 1f);
            float pitch = s.voicePitch * (pt == PersonalityType.Nervous ? 1.05f : 1f) * (s.jawBroken ? 0.93f : 1f);
            if (Sfx.Has(clip)) Sfx.Play(clip, Mouth(s.creature), vol, pitch);
            else Speak(s, "falling");
            Emit(Mouth(s.creature).position, force ? 14f : 8f, Stim.Scream, s.creature);
        }

        internal static void Panic(NpcState s, float duration)
        {
            Guard("panic", () =>
            {
                var fear = s.creature.brain.instance?.GetModule<BrainModuleFear>(false);
                if (fear != null) { fear.Panic(duration); s.fleeing = true; }
            });
        }

        void OnHurt(NpcState s, float severity)
        {
            if (severity > 1.2f && s.pain > 0.6f && Random.value < 0.4f) Scream(s, false);
        }

        // ---------------- dismemberment ----------------
        void OnRagdollSliced(RagdollPart part, EventTime time)
        {
            if (!modEnabled || time != EventTime.OnEnd || part == null || part.ragdoll == null) return;
            Guard("dismember", () => HandleSlice(part));
        }

        void HandleSlice(RagdollPart part)
        {
            var c = part.ragdoll.creature;
            if (!TryGet(c, out var s)) return;
            var t = part.type;
            if (Has(t, RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso))
            {
                // no surviving that - even if a revive (dying moments / adrenaline) started a moment ago
                s.finalKill = true; s.dying = false; s.onAdrenaline = false; s.noGasp = true;
                if (!c.isKilled) Guard("kill", () => c.Kill());
                return;
            }

            s.dismembered = true;
            s.dismemberTime = Time.time;
            s.pain = 1f;
            s.bleedRate += 1.2f;
            RagdollPart.Type limb = LimbOf(t);
            if (Has(limb, ArmParts)) KillArm(s, Has(limb, LeftArmParts) ? Side.Left : Side.Right);
            else if (Has(limb, LegParts)) { s.lostLeg = true; MakeLimp(s, limb); }

            if (!surviveDismember) return;
            Scream(s, true);
            Sfx.PlayAt("NRE_Dismember", part.transform.position, 1f);

            // the stump: what's left of the limb on the body
            var stump = part.parentPart != null && !part.parentPart.isSliced ? part.parentPart : null;
            if (stump != null) Guard("bonemesh", () => s.boneMeshes.Add(BoneMesh.Attach(stump, part.transform.position, part.transform.position - stump.transform.position)));
            bool flee = !s.brave && Random.value < fleeChance * (1.3f - s.personality.bravery);
            if (stump != null)
            {
                Side holdWith = Has(limb, LeftArmParts) ? Side.Right : Has(limb, RightArmParts) ? Side.Left : (s.leftArmDead ? Side.Right : Side.Left);
                StartClutch(s, stump, part.transform.position, holdWith, false, flee);
                s.clutchUntil = Time.time + (flee ? 30f : ClutchTime(s));
            }
            if (s.lostLeg) { AllowFallAlways(s, 1.5f); Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized)); }
            if (flee) Panic(s, 25f);
            Log($"{c.name} lost {t} -> {(flee ? "flees" : "fights on")}");
        }

        // ---------------- adrenaline / survival ----------------
        /// Called when an NPC is killed. Returns true if they keep going instead.
        bool TryAdrenaline(NpcState s, CollisionInstance hit)
        {
            var c = s.creature;
            if (s.finalKill || c == null) return false;
            var head = c.ragdoll != null ? c.ragdoll.headPart : null;
            if (head != null && head.isSliced) return false;                       // decapitated: always the end

            // still on adrenaline: shrug it off until it runs out
            if (s.onAdrenaline && Time.time < s.adrenalineEnd) { Revive(s, 20f); Scream(s, false); return true; }

            // the limb came off this moment: the cut itself doesn't kill them
            if (surviveDismember && !DeathsHandledElsewhere && s.dismembered && Time.time - s.dismemberTime < 1.5f && !s.revivedFromSlice)
            {
                s.revivedFromSlice = true;
                Revive(s, 15f);
                return true;
            }

            // 0.16: only NPCs with an adrenaline profile ever get it, and only once (Relentless / Last Stand at the killing blow)
            if (!s.adrenalineUsed && (s.adrenProfile == AdrenProfile.Relentless || s.adrenProfile == AdrenProfile.LastStand))
            {
                s.adrenalineUsed = true;
                s.onAdrenaline = true;
                s.adrenalineEnd = Time.time + adrenalineSeconds * Random.Range(0.8f, 1.2f) * (s.adrenProfile == AdrenProfile.LastStand ? 1.6f : 1f);
                Revive(s, 30f);
                Scream(s, true);
                Log($"{c.name} ADRENALINE for {s.adrenalineEnd - Time.time:0.0}s");
                return true;
            }
            return false;
        }

        static void Revive(NpcState s, float health)
        {
            var c = s.creature;
            Guard("revive", () =>
            {
                c.Resurrect(health, null);
                if (c.brain.instance == null || !c.brain.instance.isActive) c.brain.Load(c.data.brainId);
            });
            Guard("wave.rejoin", () => RejoinWave42(s));                         // 0.42: no replacement spawned on top of him
            // 0.15: the body is wherever it fell - get up from there. Leaving it "standing" straight from a dead ragdoll is
            // what made revived NPCs float.
            AllowFallAlways(s, 3f);
            Guard("revive-getup", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
            s.balance = Mathf.Min(s.balance, 0.5f);
        }

        void TickTrauma(NpcState s, float dt)
        {
            var c = s.creature;
            if (s.onAdrenaline)
            {
                if (Time.time >= s.adrenalineEnd)
                {
                    s.onAdrenaline = false;
                    s.finalKill = true;
                    if (!c.isKilled)
                    {
                        Speak(s, "deathlong");

                        Guard("kill", () => c.Kill());
                    }
                    return;
                }
                Scream(s, false);
            }
            if (c.isKilled) return;
            float now = Time.time;
            bool standing = c.ragdoll.state == Ragdoll.State.Standing;

            // you can SEE the damage: they wince and flinch with pain, more often the worse it is
            if (s.pain > 0.3f && standing && now >= s.nextWince && !Pumped(s))
            {
                s.nextWince = now + Random.Range(2.5f, 6f) / (0.5f + s.pain);
                Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Torso, 0.3f + 0.3f * s.pain, 0.45f);
                var head = c.ragdoll.headPart;
                if (head != null && head.physicBody != null) head.physicBody.AddForce(Random.insideUnitSphere * 0.8f, ForceMode.VelocityChange);
            }

            // shock: a long time in heavy pain / blood loss and their body gives up, whatever HP they have left.
            // Keeps tanky NPCs from standing around forever bleeding.
            if (shockDeaths && !s.onAdrenaline && !DeathsHandledElsewhere)
            {
                float hp = HealthFraction(s);
                bool critical = s.pain >= 0.75f + 0.05f * (Tough33(s) - 1f) || s.bleedRate >= 0.5f || hp < 0.25f;   // 0.41: deaths back - shock from pain, tougher men hold out longer
                if (critical) s.shock += dt * (0.5f + s.bleedRate * 0.5f + (hp < 0.25f ? 0.5f : 0f));
                else s.shock = Mathf.Max(0f, s.shock - dt * 0.25f);
                if (s.shockDeathAt <= 0f && s.shock >= shockSeconds * Tough33(s))
                {
                    s.shockDeathAt = now + Random.Range(3f, 7f);
                    s.stayDown = 999f;
                    { AllowFall(s, 1.5f); Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized)); }
                    Log($"{c.name} goes into SHOCK");
                }
                if (s.shockDeathAt > 0f && now >= s.shockDeathAt)
                {
                    s.finalKill = true;
                    Speak(s, "deathlong");
                    Guard("kill", () => c.Kill());
                    return;
                }
            }

            if (s.dismembered || s.fleeing)
            {
                Scream(s, false);
                // a lost leg: they keep trying to stand and keep going down
                if (s.lostLeg && c.ragdoll.state == Ragdoll.State.Standing && Random.value < 0.6f * dt * 4f)
                    { AllowFall(s, 1.5f); Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized)); }
                // adrenaline crash: sometimes they just drop
                if (s.dismembered && c.ragdoll.state == Ragdoll.State.Standing && Random.value < 0.04f * dt)
                {
                    Scream(s, true);
                    { AllowFall(s, 1.5f); Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized)); }
                }
            }
        }
    }
}

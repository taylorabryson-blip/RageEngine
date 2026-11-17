// Nomad Rage Engine 0.21 - punch-drunk state and active balance.
//   PUNCH-DRUNK: every blunt hit to the head (and, less, the body) adds to it; it wears off over about a minute.
//     tipsy  (0.35+): confused face, head lolls a little, slower on their feet.
//     drunk  (0.65+): the whole body sways, they lurch a step and catch themselves, arms out, swings come late,
//                     they groan; enough on top of this and the next solid head shot knocks them out.
//   ACTIVE BALANCE: a standing NPC who is tipped (after a hit, a stumble or a lurch) pulls his upper body back over
//     his feet instead of toppling - so they wobble and recover rather than trip.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static readonly object DrunkHandler = new object();

        /// Called from HandleHit for every blunt hit.
        void AddDrunk(NpcState s, RagdollPart.Type t, float severity)
        {
            if (s.knockedOut || s.creature.isKilled) return;
            bool head = Has(t, RagdollPart.Type.Head | RagdollPart.Type.Neck);
            float add = head ? 0.13f * Mathf.Clamp(severity, 0.3f, 2f) : Has(t, RagdollPart.Type.Torso) ? 0.025f * Mathf.Clamp(severity, 0.3f, 2f) : 0f;
            if (add <= 0f) return;
            add /= Mathf.Max(0.6f, s.toughness);
            s.drunk = Mathf.Min(1.5f, s.drunk + add);
        }

        void TickDrunk(NpcState s, float now)
        {
            var c = s.creature;
            float dt = s.lastDrunkTick > 0f ? Mathf.Min(0.5f, now - s.lastDrunkTick) : 0.1f;
            s.lastDrunkTick = now;
            if (s.drunk <= 0f) return;
            s.drunk = Mathf.Max(0f, s.drunk - dt * (s.drunk > 1f ? 0.02f : 0.013f));      // sobers up over ~a minute

            int stage = s.drunk >= 0.65f ? 2 : s.drunk >= 0.35f ? 1 : 0;
            if (stage != s.drunkStage)
            {
                if (stage > s.drunkStage) Log(stage == 2 ? $"{WhoIs(s)} is PUNCH-DRUNK - swaying, lurching" : $"{WhoIs(s)} is getting punch-drunk ({s.drunk:0.00})");
                else if (stage == 0) Log($"{WhoIs(s)} shakes it off - sober again");
                s.drunkStage = stage;
                float speed = stage == 2 ? 0.7f : stage == 1 ? 0.88f : 1f;
                Guard("speed", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(DrunkHandler); if (speed < 0.99f) c.locomotion.globalMoveSpeedMultiplier.Add(DrunkHandler, speed); });
                s.rnasDirty = true;
            }
            if (stage == 0 || c.isKilled || s.knockedOut || s.shocked || c.ragdoll.state != Ragdoll.State.Standing || Pumped(s)) return;
            // 0.35: punch-drunk enough and his stomach turns
            if (s.drunk >= 0.85f && now >= s.nextVomit && s.vomitUntil < now)
            {
                if (Random.value < 0.4f) Guard("drunk.vomit", () => Vomit(s));
                else s.nextVomit = now + 4f;
            }

            float k = Mathf.Clamp01((s.drunk - 0.3f) / 0.9f);
            // head lolls
            Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.25f + 0.35f * k, 0.3f);
            // the body sways: slow figure-of-eight over the feet
            float ph = now * (1.1f + 0.3f * k) + s.drunkPhase;
            Vector3 sway = c.transform.right * Mathf.Sin(ph) * 1f + c.transform.forward * Mathf.Sin(ph * 0.5f + 1.3f) * 0.5f;
            var head = c.ragdoll.headPart;
            if (now < s.heldUntil) return;                                                   // 0.23: being held - no swaying in your hands
            if (head != null && head.physicBody != null) head.physicBody.AddForce(sway * (1.8f * k), ForceMode.Acceleration);   // 0.23: calmer
            if (stage < 2) return;

            // lurch: a stagger step they catch, arms out
            if (now >= s.nextLurch && s.punchPhase == 0)
            {
                s.nextLurch = now + Random.Range(4f, 8f) / (0.6f + k);                       // 0.23: fewer lurches
                Vector3 dir = Quaternion.Euler(0f, Random.Range(-110f, 110f), 0f) * c.transform.right * (Random.value < 0.5f ? -1f : 1f);
                ShoveBodyQuiet(s, dir, 0.3f + 0.35f * k);
                foreach (var side in new[] { Side.Left, Side.Right })
                {
                    var h = Hand(c, side);
                    if (h == null || h.physicBody == null || Dead(s, side)) continue;
                    h.physicBody.AddForce((c.transform.right * (side == Side.Left ? -1f : 1f) + Vector3.up * 0.5f) * 1.2f, ForceMode.Impulse);
                }
                // swings come late: brief gaps in their attacks
                if (Random.value < 0.3f) SetMelee(s, false, now + Random.Range(0.5f, 1.0f));
            }
            if (now >= s.nextDrunkGroan)
            {
                s.nextDrunkGroan = now + Random.Range(5f, 10f);
                bool female = c.name.IndexOf("Female", System.StringComparison.OrdinalIgnoreCase) >= 0;
                string clip = "NRE_Pain" + (female ? "F" : "M");
                if (Sfx.Has(clip)) Sfx.Play(clip, Mouth(c), 0.45f, s.voicePitch * 0.85f);
            }
        }

        /// Active balance: pull a tipped upper body back over the feet (after the hit itself has played out).
        void TickSteady(NpcState s, float now)
        {
            var c = s.creature;
            if (c.isKilled || s.knockedOut || s.shocked || s.cantStand || s.spineBroken || s.dying || s.surrendered) return;
            if (c.ragdoll.state != Ragdoll.State.Standing || now - s.lastHitTime < 0.3f || s.punchPhase != 0 || now < s.heldUntil) return;
            var head = c.ragdoll.headPart; var root = c.ragdoll.rootPart;
            if (head == null || root == null || head.physicBody == null) return;
            Vector3 lean = Vector3.ProjectOnPlane(head.transform.position - root.transform.position, Vector3.up);
            float m = lean.magnitude;
            if (m < 0.12f) return;                                                        // upright enough
            float legs = Mathf.Min(LegStrength(s, Side.Left), LegStrength(s, Side.Right));
            float strength = Mathf.Clamp01((m - 0.12f) / 0.25f) * 6f * Mathf.Lerp(0.4f, 1f, legs) * (1f - 0.4f * Mathf.Clamp01(s.drunk));
            // 0.22: on the run they catch themselves harder (a stumble mid-sprint shouldn't become a faceplant)
            var lpb = c.locomotion != null ? c.locomotion.physicBody : null;
            if (lpb != null && Vector3.ProjectOnPlane(lpb.velocity, Vector3.up).magnitude > 2f) { strength *= 1.6f; s.coreUntil = Mathf.Max(s.coreUntil, now + 0.3f); }
            head.physicBody.AddForce(-lean / m * strength, ForceMode.Acceleration);
            foreach (var p in c.ragdoll.parts)
                if (p != null && p.physicBody != null && p.type == RagdollPart.Type.Torso)
                    p.physicBody.AddForce(-lean / m * strength * 0.6f, ForceMode.Acceleration);
            s.balance = Mathf.Min(1f, s.balance + 0.02f * legs);                          // catching it helps them recover
        }

        static void ClearDrunk(NpcState s)
        {
            s.drunk = 0f; s.drunkStage = 0; s.lastDrunkTick = 0f; s.nextLurch = 0f; s.nextDrunkGroan = 0f;
            s.drunkPhase = Random.Range(0f, 6.28f);
            var c = s.creature;
            if (c != null) Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Remove(DrunkHandler));
        }
    }
}

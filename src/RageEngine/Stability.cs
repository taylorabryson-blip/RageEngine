// Rage Engine 0.19 - physical stability and hit recognition.
//
// HIT RECOGNITION: one collision never reacts twice (same source within 0.15 s is ignored), each body region has its
//   own reaction cooldown (a head hit doesn't block a body hit a moment later), and glancing contact (the weapon
//   sliding along the body rather than into it) only makes a minor reaction.
// STUMBLING: a solid hit on someone already off balance makes them stumble a few steps in the hit direction, arms
//   thrown out for balance. At the end a balance check decides: they catch themselves (how fast depends on their
//   balance/recovery) or they go down. Walls nearby: the existing wall-brace reflex catches them.
// DAZED: a high punch-drunk meter = drunken, uncoordinated sway: unsteady steps in random directions, late
//   corrections, knees dipping, hands up protectively.
// SETTLING: bodies on the ground stop jittering (gentle damping after they come to rest; restored on despawn).
// STUCK / CROWDING: an NPC walking into a wall (trying to move, not moving) gets nudged around it; after repeated
//   fails it pauses its path for a moment instead of grinding. NPCs standing on top of each other get spaced out.
// GETTING UP: they don't try to stand up while something is lying on them / they're wedged under something.
// ONE FIXER: all position corrections (clip lift, floating drop, unstick nudge) share one cooldown so they can never
//   fight each other.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using Effect = ThunderRoad.BrainModuleHitReaction.PushBehaviour.Effect;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ---------------- hit recognition ----------------
        static int RegionIndex(RagdollPart.Type t)
            => Has(t, RagdollPart.Type.Head | RagdollPart.Type.Neck) ? 0 : Has(t, RagdollPart.Type.Torso) ? 1 : Has(t, ArmParts) ? 2 : 3;

        /// Gate for every physical reaction. Returns false = ignore this one. May reduce severity (glancing).
        static bool AcceptReaction(NpcState s, Collider source, Vector3 velocity, Vector3 normal, RagdollPart.Type t, ref float severity)
        {
            float now = Time.time;
            if (source != null && source == s.lastSrcCol && now - s.lastSrcTime < 0.15f) return false;    // same collision again
            int r = RegionIndex(t);
            if (now < s.regionReactUntil[r]) return false;
            if (velocity.sqrMagnitude > 0.25f && normal.sqrMagnitude > 0.01f)
            {
                float into = Mathf.Abs(Vector3.Dot(velocity.normalized, normal.normalized));
                if (into < 0.3f) { severity *= 0.35f; s.glancing++; }            // a glancing blow
            }
            s.lastSrcCol = source; s.lastSrcTime = now;
            s.regionReactUntil[r] = now + (r == 0 ? 0.18f : r == 1 ? 0.15f : 0.12f);
            return true;
        }

        // ---------------- stumbling ----------------
        void MaybeStumble(NpcState s, Vector3 dir, float eff)
        {
            var c = s.creature;
            float now = Time.time;
            if (c.ragdoll.state != Ragdoll.State.Standing || now < s.stumbleUntil || Pumped(s) || s.cantStand) return;
            if (s.balance > 0.45f || eff < 0.8f) return;                                 // 0.21: fewer stumbles
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) return;
            s.stumbleDir = flat.normalized;
            s.stumbleUntil = now + Mathf.Lerp(0.9f, 0.5f, Mathf.Clamp01(s.balanceStability - 0.5f));
            s.nextStumbleStep = now;
            s.stumbleSteps = 0;
            AddTag(s, Tag.Staggered);
            Log($"{WhoIs(s)} stumbles (balance {s.balance:0.00})");
        }

        void TickStumble(NpcState s, float now)
        {
            var c = s.creature;
            if (s.stumbleUntil <= 0f) return;
            if (c.ragdoll.state != Ragdoll.State.Standing || c.isKilled) { s.stumbleUntil = 0f; return; }
            if (now < s.stumbleUntil)
            {
                if (now >= s.nextStumbleStep)
                {
                    s.nextStumbleStep = now + 0.17f;
                    s.stumbleSteps++;
                    float k = 1f / s.stumbleSteps;
                    ShoveBodyQuiet(s, s.stumbleDir + Random.insideUnitSphere * 0.25f, 0.65f * k + 0.15f);
                    // arms out for balance
                    foreach (var side in new[] { Side.Left, Side.Right })
                    {
                        var h = Hand(c, side);
                        if (h == null || h.physicBody == null || Dead(s, side) || s.punchPhase != 0) continue;
                        Vector3 outward = c.transform.right * (side == Side.Left ? -1f : 1f);
                        h.physicBody.AddForce((outward + Vector3.up * 0.4f) * 1.4f, ForceMode.Impulse);
                    }
                    Loosen(s, ArmParts | RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.4f, 0.25f);
                }
                return;
            }
            // end of the stumble: balance check
            s.stumbleUntil = 0f;
            if (s.balance < 0.2f && Random.value > s.balanceStability * 0.8f) { AllowFallHurt(s, 1.5f); Fall(s, s.stumbleDir, 1.2f); Log($"{WhoIs(s)} couldn't catch himself"); }
            else { s.balance = Mathf.Max(s.balance, 0.55f); AddTag(s, Tag.Recovering, 1f / Mathf.Max(0.5f, s.personality.recoveryMult)); Log($"{WhoIs(s)} catches his balance"); }
        }

        // ---------------- dazed (punch-drunk) ----------------
        void TickDazed(NpcState s, float now)
        {
            var c = s.creature;
            if (s.daze < 0.45f || c.ragdoll.state != Ragdoll.State.Standing || c.isKilled || now < s.nextDazeSway || s.stumbleUntil > 0f) return;
            s.nextDazeSway = now + Random.Range(0.6f, 1.4f) / Mathf.Max(0.5f, s.daze);
            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            ShoveBodyQuiet(s, dir, Random.Range(0.25f, 0.6f) * s.daze);
            Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.35f * s.daze, 0.5f);          // 0.20: head wobbles, the trunk doesn't fold over
            if (Random.value < 0.25f * s.daze) Stagger(s, dir, Effect.StaggerLight, RagdollPart.Type.Torso);       // knees dip
            if (!RealWeapon(c, Side.Left) && !RealWeapon(c, Side.Right) && s.guardUntil <= 0f && c.ragdoll.headPart != null && Random.value < 0.5f)
                RaiseGuard(s, c.ragdoll.headPart);                                                                // covers up
            if (debugLog && Random.value < 0.2f) Log($"{WhoIs(s)} is dazed ({s.daze:0.00})");
        }

        // ---------------- settling ----------------
        void TickSettle(NpcState s, float now)
        {
            var c = s.creature;
            var rag = c.ragdoll;
            bool down = c.isKilled || rag.state == Ragdoll.State.Inert;
            if (!down) { if (s.settled) Unsettle(s); s.downQuietSince = 0f; return; }
            if (s.settled) return;
            var root = rag.rootPart;
            if (root == null || root.physicBody == null) return;
            if (root.physicBody.velocity.magnitude > 0.35f) { s.downQuietSince = 0f; return; }
            if (s.downQuietSince <= 0f) { s.downQuietSince = now; return; }
            if (now - s.downQuietSince < 1.2f) return;
            s.settled = true;
            s.settleOrig = s.settleOrig ?? new List<(Rigidbody, float, float)>();
            foreach (var p in rag.parts)
            {
                var rb = p != null && p.physicBody != null ? p.physicBody.rigidBody : null;
                if (rb == null) continue;
                s.settleOrig.Add((rb, rb.drag, rb.angularDrag));
                rb.drag = Mathf.Max(rb.drag, 0.6f);
                rb.angularDrag = Mathf.Max(rb.angularDrag, 2.5f);
            }
        }

        static void Unsettle(NpcState s)
        {
            if (s.settleOrig != null) foreach (var (rb, d, a) in s.settleOrig) if (rb != null) { rb.drag = d; rb.angularDrag = a; }
            s.settleOrig?.Clear();
            s.settled = false;
        }

        // ---------------- stuck + crowding ----------------
        void TickNavigation(NpcState s, float dt, float now)
        {
            var c = s.creature;
            if (c.isKilled || c.ragdoll.state != Ragdoll.State.Standing || c.locomotion == null) { s.stuckTime = 0f; return; }
            Vector3 want = Vector3.zero, vel = Vector3.zero;
            Guard("nav", () => { want = c.locomotion.moveDirection; vel = c.locomotion.physicBody != null ? c.locomotion.physicBody.velocity : Vector3.zero; });
            bool trying = want.sqrMagnitude > 0.09f && s.behavior != Behavior.Pinned && s.behavior != Behavior.Incapacitated && s.behavior != Behavior.Surrender;
            bool moving = new Vector3(vel.x, 0f, vel.z).magnitude > 0.08f;
            s.stuckTime = trying && !moving ? s.stuckTime + dt : 0f;
            if (s.stuckTime > 2f && TryFix(s, now))
            {
                s.stuckTime = 0f;
                if (now - s.lastNavFail > 10f) s.navFails = 0;
                s.navFails++; s.lastNavFail = now; navFailures++;
                if (s.navFails >= 3) { s.navPauseUntil = now + 2f; s.navFails = 0; Log($"{WhoIs(s)} can't get through - stops trying that way"); }
                else
                {
                    Vector3 side = Vector3.Cross(Vector3.up, new Vector3(want.x, 0f, want.z).normalized) * (Random.value < 0.5f ? -1f : 1f);
                    ShoveBodyQuiet(s, side - want.normalized * 0.3f, 1f);
                    Log($"{WhoIs(s)} is stuck on something - sidesteps");
                }
            }
            // spacing: don't stand inside each other
            if (now >= s.nextSpacing)
            {
                s.nextSpacing = now + 0.5f;
                for (int i = 0; i < MaxTracked; i++)
                {
                    var o = pool[i];
                    if (o == s || !o.active || o.creature == null || o.creature.isKilled || o.creature.ragdoll == null || o.creature.ragdoll.state != Ragdoll.State.Standing) continue;
                    Vector3 d = c.transform.position - o.creature.transform.position; d.y = 0f;
                    if (d.sqrMagnitude > 0.7f * 0.7f || d.sqrMagnitude < 1e-4f) continue;
                    ShoveBodyQuiet(s, d.normalized, 0.25f);
                }
            }
        }

        /// One correction at a time per NPC (clip lift, float drop, unstick): returns false if another one just ran.
        static bool TryFix(NpcState s, float now)
        {
            if (now < s.fixerUntil) return false;
            s.fixerUntil = now + 1.5f;
            physicsFixes++;
            return true;
        }

        /// Get-up blocked: something lying on top of them / they're wedged under something.
        static bool GetUpBlocked(NpcState s)
        {
            var c = s.creature;
            var root = c.ragdoll != null ? c.ragdoll.rootPart : null;
            if (root == null) return false;
            int n = Physics.OverlapSphereNonAlloc(root.transform.position + Vector3.up * 0.55f, 0.22f, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var o = overlap[i];
                if (o == null || o.GetComponentInParent<Creature>() == c) continue;
                if (o.attachedRigidbody != null && !o.attachedRigidbody.isKinematic && o.attachedRigidbody.mass < 15f) continue;   // small loose stuff
                return true;
            }
            return false;
        }

        /// Like ShoveBody but without marking them unbalanced (idle shifts, spacing, footwork).
        static void ShoveBodyQuiet(NpcState s, Vector3 dir, float dv)
        {
            var c = s.creature;
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f || dv < 0.02f) return;
            flat.Normalize();
            Guard("shove", () => { var pb = c.locomotion != null ? c.locomotion.physicBody : null; if (pb != null) pb.velocity += flat * Mathf.Min(dv, 2.4f); });
        }
    }
}

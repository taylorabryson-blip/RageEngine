// Nomad Rage Engine - Physical State System. One state per NPC that every behaviour reads from:
//   balance + posture (Steady -> Unsteady -> Stumbling -> Down -> GettingUp -> Shaky -> Steady)
//   limb condition (broken / cut / missing), pain, fatigue, adrenaline
// and drives:
//   - locomotion: they slow down when unsteady, hurt or injured, and don't snap back to full speed after getting up
//   - balance reflexes: when they're losing balance their free arms go out to catch it; next to a wall they put a
//     hand on it to steady themselves (recover balance much faster while braced)
//   - recovery: hurt NPCs stay down longer and writhe / hold wounds on the ground; everyone gets up at a different
//     pace; after getting up they're shaky for a while (reduced balance cap that comes back gradually)
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public enum Posture { Steady, Unsteady, Stumbling, Down, GettingUp, Shaky }

    public sealed partial class RageEngineScript
    {
        static readonly object PostureHandler = new object();
        static readonly Vector3[] BraceDirs =
        {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            (Vector3.right + Vector3.forward).normalized, (Vector3.left + Vector3.forward).normalized,
            (Vector3.right + Vector3.back).normalized, (Vector3.left + Vector3.back).normalized
        };

        internal static float MaxBalance(NpcState s)
        {
            float cap = 1f;
            if (Has(s.brokenParts, LegParts) || s.legWounded) cap = 0.65f;
            if (s.lostLeg) cap = 0.4f;
            if (Time.time < s.shakyUntil)
            {
                float k = 1f - (s.shakyUntil - Time.time) / Mathf.Max(0.1f, s.shakyDuration);
                cap = Mathf.Min(cap, Mathf.Lerp(0.5f, 1f, k));
            }
            return cap;
        }

        void TickBalance(NpcState s, float dt)
        {
            var c = s.creature;
            var rag = c.ragdoll;
            if (rag == null) return;
            if (s.spineBroken && !c.isKilled && rag.state == Ragdoll.State.Standing) { AllowFallAlways(s, 1.5f); Guard("fall", () => rag.SetState(Ragdoll.State.Destabilized)); }
            UpdatePosture(s);

            float max = MaxBalance(s);
            if (rag.state == Ragdoll.State.Standing && s.balance < max)
            {
                // balance comes back in a second or two (it's a stagger meter, not a health bar)
                float regain = Mathf.Max(0.45f, s.personality.wobbleRecoveryPerSec) * recoverySpeed * (1f - 0.3f * s.fatigue);
                if (Time.time < s.braceUntil) regain *= 2.5f;                   // steadying on a wall
                s.balance = Mathf.Min(max, s.balance + regain * dt);
            }
            else if (s.balance > max) s.balance = max;
            if (s.fatigue > 0f) s.fatigue = Mathf.Max(0f, s.fatigue - 0.02f * dt);

            // locomotion follows the physical state
            float speed = Mathf.Lerp(0.75f, 1f, s.balance) * (1f - 0.2f * Mathf.Clamp01(s.pain * painIntensity));
            if (s.onAdrenaline) speed = Mathf.Max(speed, 1f);
            speed = Mathf.Round(speed * 20f) / 20f;
            if (!Mathf.Approximately(speed, s.lastSpeedMult))
            {
                s.lastSpeedMult = speed;
                Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Add(PostureHandler, speed));
            }

            TickRecovery(s);
            if (balanceReflexes) TickReflexes(s);
        }

        void UpdatePosture(NpcState s)
        {
            var rag = s.creature.ragdoll;
            var st = rag.state;
            Posture p;
            if (st == Ragdoll.State.Destabilized || st == Ragdoll.State.Inert) p = rag.standingUp ? Posture.GettingUp : Posture.Down;
            else if (Time.time < s.shakyUntil) p = Posture.Shaky;
            else if (s.balance < 0.3f) p = Posture.Stumbling;
            else if (s.balance < 0.7f) p = Posture.Unsteady;
            else p = Posture.Steady;

            if (p != s.posture)
            {
                var prev = s.posture;
                s.posture = p;
                if (p == Posture.Down && prev != Posture.GettingUp && rag.rootPart != null) Sfx.Play("NRE_BodyFall", rag.rootPart.transform, 0.9f);
                if (p == Posture.Down && prev != Posture.GettingUp) OnWentDown(s);
                if (st == Ragdoll.State.Standing && (prev == Posture.Down || prev == Posture.GettingUp)) OnGotUp(s);
                Log($"{s.creature.name} posture {prev} -> {p}");
            }
        }

        void OnWentDown(NpcState s)
        {
            s.downSince = Time.time;
            ReleaseReflexes(s);
            float hurt = Mathf.Clamp01(s.pain * painIntensity);
            float injuries = (Has(s.brokenParts, LegParts) ? 2f : 0f) + (s.legWounded ? 1f : 0f) + (s.bleedRate > 0.3f ? 1f : 0f);
            float grit = s.personality.type == PersonalityType.Aggressive || s.personality.type == PersonalityType.Confident ? 0.6f : 1f;
            // they get up fast; only the badly hurt (heavy pain AND serious injuries) stay down a bit
            bool badlyHurt = hurt > 0.8f && injuries >= 2f;
            s.stayDown = stayDownWhenHurt && badlyHurt ? Random.Range(1f, 2.5f) * grit : 0f;
            s.stayDown += s.fatigue * 1.5f / Mathf.Max(0.5f, s.personality.recoveryMult);     // 0.19: tired / slow recoverers take longer
            // 0.28.2: a broken leg keeps him on the floor a good while (both legs: he isn't getting up at all)
            if (Has(s.brokenParts, LegParts) && !RegenActive(s)) s.stayDown = Mathf.Max(s.stayDown, Random.Range(8f, 15f) * grit);
            if (s.lostLeg) s.stayDown = 30f;
            if (s.spineBroken) s.stayDown = 1e6f;
        }

        void OnGotUp(NpcState s)
        {
            float hurt = Mathf.Clamp01(s.pain * painIntensity);
            s.shakyDuration = Random.Range(1f, 2f) + hurt * 1.5f;
            s.shakyUntil = Time.time + s.shakyDuration;
            s.balance = Mathf.Min(s.balance, 0.6f);
            SetCancelGetUp(s, false);
            Guard("charge", () => OnGotUpAngry(s));
        }

        /// Hurt NPCs stay on the ground longer (and writhe / hold their wounds there); everyone recovers differently.
        void TickRecovery(NpcState s)
        {
            if (s.posture != Posture.Down && s.posture != Posture.GettingUp) { if (s.cancelGetUpSet) SetCancelGetUp(s, false); return; }
            var rag = s.creature.ragdoll;
            bool hold = s.spineBroken || s.cantStand || (Time.time < s.downSince + s.stayDown && !s.onAdrenaline);
            if (!hold && Time.time >= s.nextBlockCheck) { s.nextBlockCheck = Time.time + 0.5f; s.getUpBlocked = GetUpBlocked(s); if (s.getUpBlocked && debugLog) Log($"{WhoIs(s)} can't get up - something on top of him"); }
            hold |= s.getUpBlocked;
            SetCancelGetUp(s, hold);
            if (hold && rag.standingUp) Guard("getup", () => rag.CancelGetUp());

            // writhing on the ground
            if (hold && s.pain > 0.4f && Time.time >= s.nextWrithe)
            {
                s.nextWrithe = Time.time + Random.Range(0.8f, 2f);
                Loosen(s, RagdollPart.Type.Torso | LegParts, 0.3f, 0.6f);
                var root = rag.rootPart;
                if (root != null && root.physicBody != null) root.physicBody.AddForce(Random.insideUnitSphere * 0.6f, ForceMode.VelocityChange);
            }
        }

        void SetCancelGetUp(NpcState s, bool on)
        {
            if (s.cancelGetUpSet == on) return;
            s.cancelGetUpSet = on;
            Guard("getup", () =>
            {
                var hr = s.creature.brain.instance?.GetModule<BrainModuleHitReaction>(false);
                if (hr != null) hr.cancelGetUp = on;
            });
        }

        // ---------------- balance reflexes ----------------
        void TickReflexes(NpcState s)
        {
            var c = s.creature;
            bool standing = c.ragdoll.state == Ragdoll.State.Standing;
            float now = Time.time;
            if (!standing || s.onAdrenaline || s.balance > 0.6f) { if (s.reflexLeft || s.reflexRight) ReleaseReflexes(s); return; }
            if (now >= s.braceUntil && now >= s.flailUntil && (s.reflexLeft || s.reflexRight)) ReleaseReflexes(s);
            if (s.balance > 0.4f || now < s.nextReflex || now - s.lastHitTime > 1f) return;   // only right after a real hit
            s.nextReflex = now + 0.5f;

            var torso = c.ragdoll.rootPart != null ? c.ragdoll.rootPart.transform : c.transform;
            Vector3 chest = torso.position + Vector3.up * 0.35f;

            // look for a wall to put a hand on
            float best = 1.05f; RaycastHit bestHit = default; bool found = false;
            foreach (var d in BraceDirs)
            {
                Vector3 dir = c.transform.TransformDirection(d);
                if (Physics.Raycast(chest, dir, out var h, 1.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && h.rigidbody == null && h.distance < best && Vector3.Dot(h.normal, Vector3.up) < 0.5f)
                { best = h.distance; bestHit = h; found = true; }
            }

            if (found)
            {
                Side side = Vector3.Dot(bestHit.point - chest, c.transform.right) >= 0f ? Side.Right : Side.Left;
                if (!ReflexHandFree(s, side)) side = Other(side);
                if (!ReflexHandFree(s, side)) return;
                var a = ReflexAnchor(s, side);
                a.SetParent(null, false);
                a.position = bestHit.point + bestHit.normal * 0.04f;
                a.rotation = Quaternion.LookRotation(-bestHit.normal, Vector3.up);
                SetReflexHand(s, side, a);
                s.braceUntil = now + 1.8f;
                Log($"{c.name} braces on wall ({side})");
            }
            else
            {
                // arms out to catch their balance
                foreach (var side in new[] { Side.Left, Side.Right })
                {
                    if (!ReflexHandFree(s, side)) continue;
                    var a = ReflexAnchor(s, side);
                    a.SetParent(torso, false);
                    float sign = side == Side.Right ? 1f : -1f;
                    a.position = chest + c.transform.right * (0.6f * sign) + Vector3.up * Random.Range(0.0f, 0.25f) + c.transform.forward * Random.Range(-0.1f, 0.25f);
                    a.rotation = Quaternion.LookRotation(c.transform.forward, Vector3.up);
                    SetReflexHand(s, side, a);
                }
                s.flailUntil = now + 0.7f;
            }
        }

        bool ReflexHandFree(NpcState s, Side side)
        {
            if (Dead(s, side) || (s.clutching && s.clutchSide == side)) return false;
            var h = Hand(s.creature, side);
            return h != null && h.grabbedHandle == null;
        }

        static Transform ReflexAnchor(NpcState s, Side side)
        {
            ref Transform t = ref (side == Side.Left ? ref s.reflexAnchorL : ref s.reflexAnchorR);
            if (t == null) t = new GameObject("RageEngine_Reflex_" + side).transform;
            return t;
        }

        static void SetReflexHand(NpcState s, Side side, Transform anchor)
        {
            var ik = s.creature.ragdoll.ik;
            if (ik == null) return;
            ik.SetHandAnchor(side, anchor);
            ik.SetHandState(side, true, false);
            if (side == Side.Left) s.reflexLeft = true; else s.reflexRight = true;
        }

        internal static void ReleaseReflexes(NpcState s)
        {
            var ik = s.creature != null && s.creature.ragdoll != null ? s.creature.ragdoll.ik : null;
            if (ik != null)
            {
                if (s.reflexLeft && !(s.clutching && s.clutchSide == Side.Left)) { ik.SetHandAnchor(Side.Left, null); ik.SetHandState(Side.Left, false, false); }
                if (s.reflexRight && !(s.clutching && s.clutchSide == Side.Right)) { ik.SetHandAnchor(Side.Right, null); ik.SetHandState(Side.Right, false, false); }
            }
            s.reflexLeft = s.reflexRight = false;
            s.braceUntil = s.flailUntil = 0f;
        }
    }
}

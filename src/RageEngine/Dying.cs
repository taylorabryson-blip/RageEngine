// Nomad Rage Engine - dying moments. A fatal blow doesn't switch an NPC off like a light:
//   - they keep their momentum for a moment: an attack already in motion is finished, they reel from the blow,
//     stumble a step or two as their strength drains away (muscles fade, legs go)
//   - then they collapse into a ragdoll, carried in the direction of the killing blow
// Head/neck killing blows give the shortest window; gut/chest the longest. A second blow during the window drops them
// at once. Decapitation is always instant.
// Works alongside death-animation mods: if another mod changes deaths and you prefer its version, switch
// "Dying moments" off.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        bool TryDyingMoments(NpcState s, CollisionInstance hit)
        {
            if (!dyingMoments || s.dyingUsed) return false;
            var c = s.creature;
            var head = c.ragdoll != null ? c.ragdoll.headPart : null;
            if (head != null && head.isSliced) return false;
            if (s.posture == Posture.Down || s.posture == Posture.GettingUp) return false;    // already on the ground

            var part = hit != null ? hit.damageStruct.hitRagdollPart : null;
            var t = part != null ? part.type : RagdollPart.Type.Torso;
            float window = Has(t, RagdollPart.Type.Head | RagdollPart.Type.Neck) ? Random.Range(0.4f, 0.9f)
                         : Has(t, RagdollPart.Type.Torso) ? Random.Range(0.8f, 1f) * dyingSeconds
                         : Random.Range(0.6f, 1f) * dyingSeconds;

            s.dyingUsed = true;
            s.dying = true;
            s.dyingStart = Time.time;
            s.dyingEnd = Time.time + window;
            Revive(s, 3f);
            // reel from the blow
            Vector3 flat = Vector3.ProjectOnPlane(s.lastHitDir, Vector3.up);
            if (flat.sqrMagnitude > 0.01f) Guard("push", () => c.TryPush(Creature.PushType.Hit, flat.normalized, 1, t));
            Log($"{c.name} DYING for {window:0.0}s");
            return true;
        }

        void TickDying(NpcState s)
        {
            if (!s.dying) return;
            var c = s.creature;
            float k = Mathf.InverseLerp(s.dyingStart, s.dyingEnd, Time.time);       // 0 -> 1 as life drains

            // strength fades: muscles weaken, they slow, balance goes, legs give out at the end
            Loosen(s, WholeBody, Mathf.Lerp(0.2f, 0.85f, k), 0.15f);
            s.balance = Mathf.Min(s.balance, Mathf.Lerp(0.6f, 0.1f, k));
            if (k > 0.6f) Loosen(s, LegParts, 0.95f, 0.15f);

            if (Time.time >= s.dyingEnd || c.ragdoll.state != Ragdoll.State.Standing && k > 0.3f)
            {
                s.dying = false;
                s.finalKill = true;
                if (!c.isKilled) Guard("kill", () => c.Kill());
                // collapse carried by the killing blow
                Vector3 flat = Vector3.ProjectOnPlane(s.lastHitDir, Vector3.up);
                if (flat.sqrMagnitude < 0.01f) flat = -c.transform.forward;
                PushBody(c, flat.normalized + Vector3.down * 0.3f, 1.5f);
                Log($"{c.name} collapses");
            }
        }
    }
}

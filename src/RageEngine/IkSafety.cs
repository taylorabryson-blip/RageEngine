// Nomad Rage Engine 0.22 - IK safety (fix for "NPCs stuck to the middle of the map, arm clipping through it").
//   Rage Engine moves NPC hands with IK anchors (guard, punch, clutch, beckon, point, cradle, surrender).
//   An anchor left behind - a gesture that ended without letting go, a body the game recycled from its pool still
//   carrying an old NPC's anchor, an anchor that was parented to a despawned body - pins the hand to a far-away
//   point, and the hand's physics drags the whole NPC there (often the world origin = the middle of the map).
//   Every frame, for EVERY creature in the level (tracked or not):
//     - a Rage Engine anchor on an untracked creature is removed;
//     - a destroyed anchor is removed;
//     - an anchor more than 1.2 m from the NPC's chest is removed (no real gesture reaches that far).
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const float IkMaxReach = 1.2f;
        static float nextIkLog;

        void IkWatchdog()
        {
            var all = Creature.allActive;
            if (all == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.isPlayer || c.ragdoll == null || c.ragdoll.ik == null) continue;
                TryGet(c, out var s);
                CheckIkSide(c, s, Side.Left);
                CheckIkSide(c, s, Side.Right);
            }
        }

        void CheckIkSide(Creature c, NpcState s, Side side)
        {
            var ik = c.ragdoll.ik;
            Transform t = side == Side.Left ? ik.handLeftTarget : ik.handRightTarget;
            if (ReferenceEquals(t, null)) return;
            string why = null;
            if (t == null) why = "destroyed anchor";                                         // Unity-null: the object is gone
            else
            {
                if (!t.name.StartsWith("RageEngine_")) return;                                 // the game's own IK - not ours to touch
                if (s == null || !s.active || s.creature != c) why = "anchor left on an untracked body";
                else
                {
                    var chest = ChestPart(c);
                    Vector3 from = chest != null ? chest.transform.position : c.transform.position + Vector3.up * 1.3f;
                    float d = Vector3.Distance(from, t.position);
                    if (d > IkMaxReach) why = $"anchor {d:0.0} m away";
                }
            }
            if (why == null) return;
            Guard("ik", () => { ik.SetHandAnchor(side, null); ik.SetHandState(side, false, false); });
            if (s != null && s.creature == c)
            {
                if (s.punchPhase != 0 && s.punchSide == side) Guard("punch", () => EndPunch(s, true));
                if (side == Side.Right) { s.beckonUntil = 0f; s.pointAtUntil = 0f; }
            }
            if (Time.time >= nextIkLog) { nextIkLog = Time.time + 2f; Log($"IK safety: freed {c.name}'s {side} hand ({why})"); }
        }

        /// Strip every Rage Engine anchor off a body (on track and on release) and unparent our anchors from it.
        static void ClearOurIk(Creature c, NpcState s)
        {
            if (c == null || c.ragdoll == null) return;
            var ik = c.ragdoll.ik;
            if (ik != null)
                foreach (var side in new[] { Side.Left, Side.Right })
                {
                    Transform t = side == Side.Left ? ik.handLeftTarget : ik.handRightTarget;
                    if (ReferenceEquals(t, null)) continue;
                    if (t == null || t.name.StartsWith("RageEngine_")) { ik.SetHandAnchor(side, null); ik.SetHandState(side, false, false); }
                }
            if (s == null) return;
            foreach (var a in new[] { s.guardL, s.guardR, s.punchAnchor, s.beckonAnchor, s.pointAnchor, s.clutchAnchor, s.clutchAnchor2, s.cradleAnchor, s.surrenderL, s.surrenderR })
                if (a != null) a.SetParent(null, true);
        }
    }
}

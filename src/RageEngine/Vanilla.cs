// Rage Engine 0.20 - working WITH the vanilla systems instead of on top of them.
//   - Weapon theft OFF: the game's forced disarms (grab an arm / stab an arm = weapon pops out of the hand) are turned
//     off per NPC - Rage Engine's muscles, grip, pain and agony decide when a weapon is dropped. Yanking a weapon out of
//     an NPC's hand ("steal") is turned off on the handles of weapons NPCs carry. Everything is restored on disable.
//   - Electrocution: while the game's electrocute is running (lightning spells, shock imbues), Rage Engine stands back:
//     no reactions, no IK arms, no muscle overrides, no punches - the game's convulsions play cleanly.
//   - Faces: uses the game's own facial expression system (Pain, Angry, Fear, Tired, Confusion) driven by Rage
//     Engine's state, instead of fighting it.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static readonly Dictionary<HandleData, bool> stealOrig = new Dictionary<HandleData, bool>();

        void VanillaOnTrack(NpcState s)
        {
            var c = s.creature;
            Guard("vanilla.disarm", () =>
            {
                var eq = c.brain.instance?.GetModule<BrainModuleEquipment>(false);
                if (eq != null)
                {
                    s.origArmGrabDisarm = eq.allowArmGrabDisarm; s.origArmStabDisarm = eq.allowArmStabDisarm; s.disarmPatched = true;
                    eq.allowArmGrabDisarm = false; eq.allowArmStabDisarm = false;
                }
            });
        }

        void VanillaOnRelease(NpcState s)
        {
            var c = s.creature;
            if (c == null || !s.disarmPatched) return;
            Guard("vanilla.disarm", () =>
            {
                var eq = c.brain.instance?.GetModule<BrainModuleEquipment>(false);
                if (eq != null) { eq.allowArmGrabDisarm = s.origArmGrabDisarm; eq.allowArmStabDisarm = s.origArmStabDisarm; }
            });
            s.disarmPatched = false;
        }

        /// Every tick: weapons in NPC hands can't be stolen; electrocution state; facial expression.
        void TickVanilla(NpcState s, float now)
        {
            var c = s.creature;
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                var h = Hand(c, side);
                var hd = h != null ? h.grabbedHandle : null;
                if (hd == null || hd is HandleRagdoll || hd.data == null || !hd.data.allowSteal) continue;
                if (!stealOrig.ContainsKey(hd.data)) stealOrig[hd.data] = true;
                hd.data.allowSteal = false;
            }
            // electrocution
            bool shocked = false;
            Guard("vanilla.shock", () =>
            {
                var el = c.brain.instance?.GetModule<BrainModuleElectrocute>(false);
                shocked = el != null && (el.coroutine != null || now - el.lastElectrocuteTime < 0.5f);
            });
            if (shocked && !s.shocked) { s.shocked = true; StandBack(s); Log($"{WhoIs(s)} is being electrocuted - Rage Engine stands back"); }
            else if (!shocked && s.shocked) { s.shocked = false; s.rnasDirty = true; }
            // face
            if (now >= s.nextFace && !c.isKilled)
            {
                s.nextFace = now + 0.6f;
                var e = s.knockedOut ? FaceAnimator.Expression.Death
                      : s.pain * s.painSensitivity > 0.45f || HasTag(s, Tag.Agony) ? FaceAnimator.Expression.Pain
                      : s.daze > 0.5f || s.drunk >= 0.35f ? FaceAnimator.Expression.Confusion
                      : HasTag(s, Tag.Fear) || s.surrendered ? FaceAnimator.Expression.Fear
                      : HasTag(s, Tag.Anger) || s.stanceOn ? FaceAnimator.Expression.Angry
                      : s.fatigue > 0.6f ? FaceAnimator.Expression.Tired : FaceAnimator.Expression.Neutral;
                if (e != s.lastFace)
                {
                    s.lastFace = e;
                    Guard("vanilla.face", () => c.brain.instance?.GetModule<BrainModuleExpression>(false)?.SetFaceExpression(e));
                }
            }
        }

        /// Electrocuted: drop every Rage Engine override so the game's own shock animation owns the body.
        void StandBack(NpcState s)
        {
            if (s.punchPhase != 0) EndPunch(s, true);
            ReleaseGuard(s); ReleaseClutch(s);
            if (s.cradling) EndCradle(s);
            s.stumbleUntil = 0f; s.looseUntil = 0f;
            Guard("muscles", () => RestoreMuscles(s));
        }

        static void RestoreStealFlags()
        {
            foreach (var kv in stealOrig) if (kv.Key != null) kv.Key.allowSteal = kv.Value;
            stealOrig.Clear();
        }
    }
}

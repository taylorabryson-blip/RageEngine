// Rage Engine 0.17 - real punches for barehanded NPCs. Nomad has no punch animations for NPCs, so the punches are
// procedural: the game's own arm IK drives a fist along a jab / cross / hook / uppercut path at your head or body,
// with a wind-up, a snap, a follow-through and a recovery. The torso turns into it, they step in, and a fist that
// connects hurts you (the existing contact damage). A whiff leaves them briefly off balance.
// Combos and timing come from the NPC's personality (aggressive = more, faster; cautious = single shots).
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        internal enum PunchType : byte { Jab, Cross, Hook, Uppercut }
        const float Windup = 0.06f, Strike = 0.17f, Recover = 0.28f;   // 0.25: measured medians from mocap (out 0.15-0.27 s, back 0.23-0.30 s)

        /// From the fist tick (0.1 s): start a combo when the player is in reach.
        void ThinkPunch(NpcState s, float now)
        {
            if (s.ally) return;
            var c = s.creature;
            if (s.punchPhase != 0 || now < s.nextPunch || s.behavior != Behavior.Unarmed) return;
            if (c.ragdoll.state != Ragdoll.State.Standing || s.clutching && s.pain > 0.4f) return;
            var target = PlayerTarget();
            if (target == null || c.ragdoll.headPart == null) return;
            float d = Vector3.Distance(c.ragdoll.headPart.transform.position, target.position);
            if (d > 1.3f || d < 0.25f) return;
            var pt = s.personality.type;
            int combo = pt == PersonalityType.Aggressive || pt == PersonalityType.Unpredictable ? Random.Range(2, 5)
                      : pt == PersonalityType.Cautious || pt == PersonalityType.Nervous ? Random.Range(1, 3) : Random.Range(1, 4);   // 0.25: more combinations
            s.punchTempo = 0.85f * (pt == PersonalityType.Aggressive ? 0.9f : pt == PersonalityType.Cautious ? 1.1f : 1f) * Random.Range(0.92f, 1.1f) * (1f + 0.4f * Mathf.Clamp01(s.drunk) + 0.3f * Mathf.Clamp01(s.fatigue));
            if (CapOf(s, Cap.ContinueCombat) == Ability.Impaired) combo = 1;
            // 0.26: real combinations - a planned sequence (1-2, 1-2-3, body-body-head ...) instead of random single shots
            s.comboPlan = PickCombo(s, combo); s.comboIdx = 0;
            combo = s.comboPlan.Length;
            s.comboLeft = combo;
            if (!s.stanceOn) ReleaseGuard(s);                                   // in stance the other hand stays up
            StartPunch(s, now, true);
        }

        void StartPunch(NpcState s, float now, bool first)
        {
            var c = s.creature;
            var ik = c.ragdoll.ik;
            if (ik == null) { s.punchPhase = 0; return; }
            // pick the hand: lead (left) jabs, rear (right) crosses; only hands that work
            bool lOk = HandUsable(s, Side.Left) && !(s.clutching && s.clutchSide == Side.Left);
            bool rOk = HandUsable(s, Side.Right) && !(s.clutching && s.clutchSide == Side.Right);
            if (!lOk && !rOk) { s.punchPhase = 0; s.comboLeft = 0; return; }
            float r = Random.value;
            // 0.19: every NPC has his own favourites (a jabber vs. a hooker)
            float jab = s.punchPrefJab, cross = jab + (1f - jab) * 0.35f, hook = cross + (1f - cross) * 0.65f;
            s.punchType = (first || Random.value < 0.4f) && r < jab ? PunchType.Jab : r < cross ? PunchType.Cross : r < hook ? PunchType.Hook : PunchType.Uppercut;
            Side side = s.punchType == PunchType.Jab ? Side.Left : s.punchType == PunchType.Cross ? Side.Right : (Random.value < 0.5f ? Side.Left : Side.Right);
            bool planned = s.comboPlan != null && s.comboIdx < s.comboPlan.Length;
            bool body = Random.value > 0.7f;
            if (planned)
            {
                int code = s.comboPlan[s.comboIdx++];
                s.punchType = (PunchType)(code & 3); side = (code & 4) != 0 ? Side.Right : Side.Left; body = (code & 8) != 0;
            }
            // 0.28 smarter: read your guard - hands up by your head = go to the body, hands down = go upstairs
            { bool b2 = body; Guard("readguard", () => b2 = ReadGuard28(s, b2, planned)); body = b2; }
            if (side == Side.Left && !lOk) side = Side.Right; else if (side == Side.Right && !rOk) side = Side.Left;
            s.punchSide = side;
            if (s.punchAnchor == null) s.punchAnchor = new GameObject("RageEngine_Punch").transform;
            s.punchAnchor.SetParent(null, true);
            var hand = Hand(c, side);
            s.punchFrom = hand != null ? hand.transform.position : c.ragdoll.headPart.transform.position;
            s.punchAnchor.position = s.punchFrom;
            ik.SetHandAnchor(side, s.punchAnchor);
            ik.SetHandState(side, true, true);
            s.punchPhase = 1; s.punchT = now; s.punchLanded = false;
            s.punchAimHead = !body;
            s.punchFirst = first;
            SetMelee(s, false, now + Windup + Strike + Recover + 0.15f);       // the phantom swing must not fight the fist
        }

        // 0.25: punch paths come from real boxing motion capture (PunchMocap.cs, CMU mocap): the fist follows the
        // recorded path relative to the NPC's own shoulder, scaled to his arm, and is steered onto you at full extension.
        static Vector3[] MocapOut(PunchType t) => t == PunchType.Jab ? PunchMocap.JabOut : t == PunchType.Cross ? PunchMocap.CrossOut : t == PunchType.Hook ? PunchMocap.HookOut : PunchMocap.UppercutOut;
        static Vector3[] MocapBack(PunchType t) => t == PunchType.Jab ? PunchMocap.JabBack : t == PunchType.Cross ? PunchMocap.CrossBack : t == PunchType.Hook ? PunchMocap.HookBack : PunchMocap.UppercutBack;
        static float MocapOutTime(PunchType t) => t == PunchType.Jab ? PunchMocap.JabOutTime : t == PunchType.Cross ? PunchMocap.CrossOutTime : t == PunchType.Hook ? PunchMocap.HookOutTime : PunchMocap.UppercutOutTime;
        static float MocapBackTime(PunchType t) => t == PunchType.Jab ? PunchMocap.JabBackTime : t == PunchType.Cross ? PunchMocap.CrossBackTime : t == PunchType.Hook ? PunchMocap.HookBackTime : PunchMocap.UppercutBackTime;

        /// The punching arm's shoulder and its length (shoulder -> elbow -> hand), measured on this body.
        static Transform Shoulder(Creature c, Side side, out float reach)
        {
            reach = 0.6f;
            var armType = side == Side.Left ? RagdollPart.Type.LeftArm : RagdollPart.Type.RightArm;
            RagdollPart upper = null, fore = null;
            foreach (var p in c.ragdoll.parts)
            {
                if (p == null || p.type != armType) continue;
                if (p.parentPart != null && p.parentPart.type == armType) fore = p; else upper = p;
            }
            var hand = Hand(c, side);
            if (upper == null) return null;
            if (fore != null && hand != null)
                reach = Mathf.Clamp(Vector3.Distance(upper.transform.position, fore.transform.position) + Vector3.Distance(fore.transform.position, hand.transform.position), 0.4f, 0.9f);
            return upper.transform;
        }

        /// Every frame while a punch is in progress: move the fist along its path.
        void TickPunch(NpcState s)
        {
            var c = s.creature;
            float now = Time.time;
            if (c == null || c.isKilled || c.ragdoll.state != Ragdoll.State.Standing || s.punchAnchor == null) { EndPunch(s, true); return; }
            var target = PlayerTarget();
            var pc = Player.local != null ? Player.local.creature : null;
            if (target == null || pc == null) { EndPunch(s, true); return; }
            var headN = c.ragdoll.headPart.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(target.position - headN.position, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float mx = s.punchSide == Side.Left ? -1f : 1f;                                  // templates are right-handed
            var sh = Shoulder(c, s.punchSide, out float reach);
            Vector3 shoulder = sh != null ? sh.position : headN.position - Vector3.up * 0.2f + right * mx * 0.18f;
            Vector3 Pose(Vector3 k) => shoulder + (right * (k.x * mx) + Vector3.up * k.y + fwd * k.z) * reach;
            Vector3 aim = s.punchAimHead ? target.position : (pc.ragdoll.rootPart != null ? pc.ragdoll.rootPart.transform.position + Vector3.up * 0.25f : target.position - Vector3.up * 0.4f);
            aim += fwd * 0.08f;                                                              // punch THROUGH the target
            var outP = MocapOut(s.punchType);
            Vector3 peak = Pose(outP[outP.Length - 1]);
            Vector3 fix = Vector3.ClampMagnitude(aim - peak, 0.5f);                         // steer the recorded path onto you
            float tempo = s.punchTempo > 0f ? s.punchTempo : 1f;
            float t = now - s.punchT;
            Vector3 p;
            if (s.punchPhase == 1)
            {
                // gather: fist settles at the recorded start of this punch (guard position)
                float u = Mathf.Clamp01(t / (Windup * tempo * (s.punchFirst ? 1f : 0.5f)));                 // inside a combination the next shot is already loaded
                p = Vector3.Lerp(s.punchFrom, Pose(outP[0]), u * (2f - u));
                if (u >= 1f)
                {
                    s.punchPhase = 2; s.punchT = now;
                    var chest = ChestPart(c);
                    // 0.28: the whole body goes into it - hips and shoulders turn, weight comes forward, the shoulder drives through
                    bool power = s.punchType != PunchType.Jab;
                    if (chest != null && chest.physicBody != null)
                    {
                        chest.physicBody.AddForce((fwd - right * mx * 0.4f + (s.punchType == PunchType.Uppercut ? Vector3.up * 0.6f : Vector3.down * 0.1f)).normalized * (power ? 1.5f : 0.7f), ForceMode.VelocityChange);
                        Guard("twist", () => chest.physicBody.AddTorque(Vector3.up * -mx * (power ? 5.5f : 2.5f), ForceMode.VelocityChange));
                    }
                    s.coreUntil = Mathf.Max(s.coreUntil, now + 0.4f);
                    ShoveBody(s, fwd, power ? 0.8f : 0.45f);
                    Guard("shoulder", () => DriveShoulder28(s, true));
                    Sfx.PlayAt("NRE_Swoosh", headN.position, 0.5f);
                    if (Random.value < 0.3f) Speak(s, "attackmelee");                         // 0.25: the game's own recorded effort shout
                }
            }
            else if (s.punchPhase == 2)
            {
                float u = Mathf.Clamp01(t / (MocapOutTime(s.punchType) * tempo));
                float w = u * u * (3f - 2f * u);
                p = Pose(PunchMocap.Sample(outP, u)) + fix * w + fwd * ((s.punchType == PunchType.Jab ? 0.06f : 0.16f) * w);      // punch THROUGH the target
                Guard("shoulder", () => MoveShoulder28(s, sh.position, fwd, right * mx, w));
                if (u >= 1f) { s.punchPhase = 3; s.punchT = now; Guard("shoulder", () => DriveShoulder28(s, false)); }
            }
            else
            {
                float u = Mathf.Clamp01(t / (MocapBackTime(s.punchType) * tempo * (s.comboLeft > 1 ? 0.6f : 1f)));   // hands flow: the returning fist crosses the next one going out
                p = Pose(PunchMocap.Sample(MocapBack(s.punchType), u)) + fix * (1f - u);
                if (u >= 1f)
                {
                    if (!s.punchLanded)
                    {
                        // whiffed: overextended for a moment
                        Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.35f, 0.3f);
                        s.nextPunch = Mathf.Max(s.nextPunch, now + 0.35f);
                    }
                    if (--s.comboLeft > 0) StartPunch(s, now, false);
                    else EndPunch(s, false);
                    return;
                }
            }
            s.punchAnchor.position = p;
            s.punchAnchor.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // 0.26 combination book. Code = type (0 jab, 1 cross, 2 hook, 3 uppercut) | 4 = right hand | 8 = to the body.
        const int J = 0, X = 1 | 4, HL = 2, HR = 2 | 4, UL = 3, UR = 3 | 4, B = 8;
        static readonly int[][] Combos =
        {
            new[] { J },                                  // 0  single jab
            new[] { X },                                  // 1  single cross
            new[] { J, X },                               // 2  1-2
            new[] { J, J, X },                            // 3  1-1-2
            new[] { J, X, HL },                           // 4  1-2-3
            new[] { X, HL, X },                           // 5  2-3-2
            new[] { J, X | B, HL },                       // 6  jab, cross to the body, hook upstairs
            new[] { HL | B, HR | B, HL },                 // 7  body, body, head
            new[] { J, UR, HL, X },                       // 8  1-6-3-2
            new[] { J, X, HL | B, X },                    // 9  1-2, hook to the liver, cross
            new[] { J, X, UL, X },                        // 10 1-2-5-2
            new[] { HL, X, HL | B, UR },                  // 11 hook, cross, body hook, uppercut
        };

        int[] PickCombo(NpcState s, int maxLen)
        {
            var pt = s.personality.type;
            float skill = Mathf.Clamp01(0.35f + 0.5f * s.trAggression + 0.35f * s.hpK + (s.bioActive ? 0.3f : 0f) - 0.4f * Mathf.Clamp01(s.fatigue) - 0.3f * Mathf.Clamp01(s.drunk));
            if (pt == PersonalityType.Cautious || pt == PersonalityType.Nervous) skill *= 0.7f;
            int lo, hi;
            if (maxLen <= 1) { lo = 0; hi = 2; }
            else if (skill < 0.35f) { lo = 0; hi = 4; }
            else if (skill < 0.65f) { lo = 2; hi = 8; }
            else { lo = 2; hi = Combos.Length; }
            var pick = Combos[Random.Range(lo, hi)];
            if (s.comboLast == pick && hi - lo > 1) pick = Combos[Random.Range(lo, hi)];           // don't throw the same one twice in a row (usually)
            s.comboLast = pick;
            return pick;
        }

        // ------------------------------------------------------------------ 0.28 helpers
        bool ReadGuard28(NpcState s, bool body, bool planned)
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null || pc.ragdoll == null || pc.ragdoll.headPart == null || pc.handLeft == null || pc.handRight == null) return body;
            float hy = pc.ragdoll.headPart.transform.position.y;
            bool lUp = pc.handLeft.transform.position.y > hy - 0.32f, rUp = pc.handRight.transform.position.y > hy - 0.32f;
            if (lUp && rUp) return Random.value < (planned ? 0.6f : 0.8f) || body;       // tight high guard: dig to the body
            if (!lUp && !rUp) return Random.value < 0.12f && body;                       // hands down: head hunting
            return body;
        }

        void DriveShoulder28(NpcState s, bool on)
        {
            var ik = s.creature.ragdoll.ik; if (ik == null) return;
            if (on)
            {
                if (s.shoulderAnchor == null) s.shoulderAnchor = new GameObject("RageEngine_Shoulder").transform;
                var sh = Shoulder(s.creature, s.punchSide, out _);
                s.shoulderAnchor.position = sh != null ? sh.position : s.creature.transform.position; s.shoulderSide = s.punchSide; s.shoulderOn = true;
                ik.SetShoulderAnchor(s.punchSide, s.shoulderAnchor); ik.SetShoulderState(s.punchSide, true, false); ik.SetShoulderWeight(s.punchSide, 0.85f, 0f);
            }
            else if (s.shoulderOn)
            {
                s.shoulderOn = false;
                ik.SetShoulderState(s.shoulderSide, false, false); ik.SetShoulderAnchor(s.shoulderSide, null);
            }
        }

        static void MoveShoulder28(NpcState s, Vector3 shoulder, Vector3 fwd, Vector3 outward, float w)
        {
            if (!s.shoulderOn || s.shoulderAnchor == null) return;
            float thrust = s.punchType == PunchType.Jab ? 0.07f : 0.15f;
            s.shoulderAnchor.position = shoulder + (fwd * thrust - outward * 0.05f + Vector3.down * 0.02f) * w;
        }

        void EndPunch(NpcState s, bool interrupted)
        {
            var c = s.creature;
            var ik = c != null && c.ragdoll != null ? c.ragdoll.ik : null;
            if (ik != null && s.punchPhase != 0 && !(s.clutching && s.clutchSide == s.punchSide))
            { ik.SetHandAnchor(s.punchSide, null); ik.SetHandState(s.punchSide, false, false); }
            s.punchPhase = 0; s.comboLeft = 0; s.comboPlan = null;
            Guard("shoulder", () => DriveShoulder28(s, false));
            if (s.stanceOn) s.guardUntil = 0f;                                  // hands back up next tick
            var pt = s.personality != null ? s.personality.type : PersonalityType.Calm;
            float gap = pt == PersonalityType.Aggressive ? Random.Range(0.3f, 0.8f) : pt == PersonalityType.Cautious || pt == PersonalityType.Nervous ? Random.Range(0.9f, 1.8f) : Random.Range(0.5f, 1.2f);   // 0.25: keeps the pressure on
            gap *= Mathf.Lerp(1.4f, 0.6f, s.trAggression) * Mathf.Lerp(0.8f, 1.25f, s.trPatience) * (HasTag(s, Tag.Fear) ? 1.5f : 1f);
            s.nextPunch = Time.time + (interrupted ? 1f : gap);
        }
    
        /// 0.18 boxer stance: an unarmed NPC squared up with you keeps both hands up by the chin, moves his head
        /// (slips and weaves), and uses footwork - circles, steps in to close the gap, steps out when you crowd him.
        void TickStance(NpcState s, float now)
        {
            if (s.ally) { if (s.stanceOn) { s.stanceOn = false; ReleaseGuard(s); } return; }
            var c = s.creature;
            var head = c.ragdoll.headPart;
            var target = PlayerTarget();
            float d = head != null && target != null ? Vector3.Distance(head.transform.position, target.position) : 99f;
            bool want = !s.shocked && s.behavior == Behavior.Unarmed && c.ragdoll.state == Ragdoll.State.Standing && d < 2.6f
                        && !(s.clutching && s.pain > 0.4f) && (HandUsable(s, Side.Left) || HandUsable(s, Side.Right));
            if (!want)
            {
                if (s.stanceOn) { s.stanceOn = false; if (s.punchPhase == 0) ReleaseGuard(s); }
                return;
            }
            if (!s.stanceOn) Log($"{WhoIs(s)} squares up (boxer stance)");
            s.stanceOn = true;
            if (s.punchPhase == 0) { if (s.guardUntil <= 0f) RaiseGuard(s, head); s.guardUntil = now + 0.5f; s.nextGuard = 0f; }
            // 0.20: the "come on" wave - lead hand out, palm up, fingers curling you in
            if (s.beckonUntil > 0f && now >= s.beckonUntil)
            {
                s.beckonUntil = 0f;                                                          // 0.22: the wave is over - hand goes back to the guard
                if (s.punchPhase == 0 && !Dead(s, Side.Right)) Guard("guard", () => RaiseGuard(s, head));
            }
            if (now < s.beckonUntil && !Dead(s, Side.Right) && c.ragdoll.ik != null && s.punchPhase == 0)
            {
                if (s.beckonAnchor == null) s.beckonAnchor = new GameObject("RageEngine_Beckon").transform;
                Vector3 bf = Vector3.ProjectOnPlane(target.position - head.transform.position, Vector3.up).normalized;
                var chestB = ChestPart(c);
                Vector3 bAt = (chestB != null ? chestB.transform.position : head.transform.position - Vector3.up * 0.3f) + bf * (0.42f + 0.07f * Mathf.Sin(now * 9f)) + Vector3.up * 0.05f + Vector3.Cross(Vector3.up, bf) * 0.12f;
                s.beckonAnchor.position = bAt;
                s.beckonAnchor.rotation = Quaternion.LookRotation(bf, -Vector3.up);          // palm up
                c.ragdoll.ik.SetHandAnchor(Side.Right, s.beckonAnchor); c.ragdoll.ik.SetHandState(Side.Right, true, true);
                return;
            }
            if (now < s.nextFootwork) return;
            var pt = s.personality.type;
            s.nextFootwork = now + Random.Range(0.3f, 0.75f) * (pt == PersonalityType.Cautious ? 0.8f : pt == PersonalityType.Aggressive ? 1.2f : 1f);
            Vector3 fwd = Vector3.ProjectOnPlane(target.position - head.transform.position, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float sgn = Random.value < 0.5f ? -1f : 1f;
            float r = Random.value;
            if (r < 0.35f)
            {
                // head movement: slip / weave
                head.physicBody?.AddForce((right * sgn + Vector3.down * 0.4f) * 2.2f, ForceMode.Impulse);
                Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso, 0.25f, 0.3f);
                s.coreUntil = Mathf.Max(s.coreUntil, now + 0.4f);
            }
            // 0.25: they fight at punching range - out of range they step in first, then circle
            else if (d > 1.05f) ShoveBody(s, fwd, pt == PersonalityType.Cautious ? 0.6f : 0.85f);   // close the distance
            else if (r < 0.65f) ShoveBody(s, right * sgn, 0.45f);                  // circle
            else if (d < 0.55f) ShoveBody(s, -fwd, 0.45f);                         // step out when crowded
        }
    }
}

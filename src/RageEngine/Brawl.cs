// Rage Engine 0.20 - brawling, trauma and smarter fighting.
//   BACK HITS     : hit from behind = the spine arches (hips driven forward, shoulders and head whip back), they stumble
//                   forward, a hand goes to the small of the back; a low hit (kidney) folds them.
//   LEGS          : a hit or cut on a leg buckles that knee (the leg really bends, the other one braces); cut leg
//                   muscles leave the leg permanently bowed while standing.
//   KNOCKOUT      : enough head trauma (the daze meter maxed by a big shot, or one huge blow) = out cold. They drop
//                   limp, eyes shut, and never wake up - they die in their sleep a few seconds later (counts as a kill).
//   BIG CUTS      : a deep slash across the chest/belly: weapon dropped, both hands on it, out of the fight a while.
//   HANDS         : a hand can be broken (blunt on the hand): grip gone, weapon dropped, hand kinks and bruises.
//   BONE BREAKS   : always a scream and a full-body jolt, the weapon arm stops swinging for a moment.
//   RIBS          : cracked ribs = coughing and spitting blood right away; heavy body shots sometimes too.
//   IMPALED       : with a free hand they grab the blade in them and try to pull it out - and do (scream, blood).
//   FIGHT TALK    : going barehanded is announced - they square up and BECKON you in. Some confident NPCs, seeing you
//                   barehanded, throw their own weapon down to fight you fair.
//   GROUPS        : only two attack you at once; the rest circle to your sides and back, waiting for an opening, and
//                   swap in. Thrown / knocked down while angry = they come charging back.
//   ARMED         : they sidestep swings aimed at them and take a step back to make space after getting hit.
//   SURPRISE 1 - RAGE MOMENT: a knockout punch, a crushed skull or a snapped spine you deal triggers a brief slow-mo
//                   beat (the game's own slow motion) with a heavy thud.
//   SURPRISE 2 - OLD SCARS: people who escaped you come back MARKED - the scars and bruises from last time are on
//                   them, they limp if you broke a leg, and they point you out before they come for you.
using ThunderRoad;
using UnityEngine;
using Effect = ThunderRoad.BrainModuleHitReaction.PushBehaviour.Effect;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ---------------- back hits ----------------
        /// From Impact: dir = direction of the blow. Returns true if it was a back hit (handled here).
        bool BackHit(NpcState s, RagdollPart part, RagdollPart.Type t, Vector3 point, Vector3 dir, float severity, bool standing)
        {
            var c = s.creature;
            if (!Has(t, RagdollPart.Type.Torso | RagdollPart.Type.Neck)) return false;
            Vector3 fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized;
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude < 0.01f || Vector3.Dot(flat.normalized, fwd) < 0.45f) return false;   // not from behind
            // also the contact point must be on the back side
            if (Vector3.Dot(point - part.transform.position, fwd) > 0.02f) return false;
            float k = Mathf.Clamp01(severity / 1.5f);
            var rag = c.ragdoll;
            Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Neck | RagdollPart.Type.Head, 0.55f + 0.3f * k, 0.5f + 0.4f * k);
            s.coreUntil = Mathf.Max(s.coreUntil, Time.time + 0.8f);
            if (rag.rootPart?.physicBody != null) rag.rootPart.physicBody.AddForce(fwd * (2f + 5f * k), ForceMode.Impulse);   // hips driven forward
            var chest = ChestPart(c);
            chest?.physicBody?.AddForce((-fwd * 0.4f + Vector3.up * 0.3f) * (1.5f + 3f * k), ForceMode.Impulse);           // shoulders pulled back: the arch
            rag.headPart?.physicBody?.AddForce((-fwd + Vector3.up * 0.2f) * (1.5f + 3f * k), ForceMode.Impulse);          // head whips back
            bool low = point.y < (rag.rootPart != null ? rag.rootPart.transform.position.y + 0.18f : point.y);
            if (standing)
            {
                ShoveBody(s, fwd, 0.6f + 1.2f * k);
                if (s.balance < 0.7f || k > 0.5f) MaybeStumble(s, fwd, 0.6f + k);
            }
            if (k > 0.35f) QueueClutch(s, part, point, false, 0.25f);                       // hand to the small of the back
            if (low && k > 0.5f) { Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.8f, 1.2f); s.pain = Mathf.Min(1f, s.pain + 0.25f); Log($"{WhoIs(s)} takes a kidney shot - folds"); }
            if (k > 0.4f) Scream(s, k > 0.75f);
            if (debugLog) Log($"{WhoIs(s)} hit in the back ({(low ? "low" : "upper")}, {k:0.00}) - arches forward");
            return true;
        }

        // ---------------- legs ----------------
        /// A leg hit / cut: the knee buckles for a moment (that leg really bends; the other braces).
        void BuckleLeg(NpcState s, RagdollPart.Type t, float k)
        {
            var c = s.creature;
            if (c.ragdoll.state != Ragdoll.State.Standing || Pumped(s) || s.shocked) return;
            s.buckleLegs = Has(t, LeftLegParts) ? LeftLegParts : RightLegParts;
            s.buckleUntil = Time.time + 0.35f + 0.6f * k;
            s.coreUntil = Mathf.Max(s.coreUntil, s.buckleUntil);
            foreach (var p in c.ragdoll.parts)
                if (p != null && p.physicBody != null && Has(s.buckleLegs, p.type) && IsLowerSegment(p))
                    p.physicBody.AddForce((c.transform.forward * 0.6f + Vector3.down) * (1.5f + 3f * k), ForceMode.Impulse);   // knee drives down and forward
            if (k > 0.9f && !s.brave) Stagger(s, -c.transform.forward, Effect.StaggerLight, t);   // 0.21: catches himself on the other leg
            if (debugLog) Log($"{WhoIs(s)}'s {(s.buckleLegs == LeftLegParts ? "left" : "right")} knee buckles ({k:0.00})");
        }

        // ---------------- knockout ----------------
        void Knockout(NpcState s, Vector3 dir, bool byPlayer, bool force = false)
        {
            if (!knockouts && !force) return;                                             // 0.34: switch
            var c = s.creature;
            if (s.knockedOut || c.isKilled) return;
            if (!force && !CanDrop49(s)) return;                                          // 0.49: healthy men don't get knocked out
            if (!force && TankShrug(s, "a knockout blow")) return;
            s.knockedOut = true;
            if (knockoutKills) { s.finalKill = true; s.dying = false; s.noGasp = true; }
            s.koLimbs = s.limpParts;
            AllowFallAlways(s, 10f);
            Guard("ko", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
            MakeLimp(s, ArmParts | LegParts);
            Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso, 0.95f, 30f);
            s.eyesShutUntil = float.MaxValue; if (!livingEyes.Contains(s)) livingEyes.Add(s);
            foreach (var side in new[] { Side.Left, Side.Right }) { var h = Hand(c, side); if (h != null && h.grabbedHandle != null) { var hh = h; Guard("drop", () => hh.UnGrab(false)); } }
            SetMelee(s, false, Time.time + 99f);
            if (knockoutKills) s.koDeathAt = Time.time + Random.Range(3.5f, 8f);
            else { s.koWakeAt = Time.time + Random.Range(6f, 12f); Guard("brain", () => c.brain.Stop()); }
            Sfx.Play("NRE_BodyFall", c.ragdoll.rootPart != null ? c.ragdoll.rootPart.transform : c.transform, 0.8f);
            Log(knockoutKills ? $"{WhoIs(s)} is KNOCKED OUT cold - won't wake up" : $"{WhoIs(s)} is KNOCKED OUT cold");
            if (byPlayer) RageMoment("knockout");
        }

        void TickKnockout(NpcState s, float now)
        {
            if (s.knockedOut && s.koWakeAt > 0f && now >= s.koWakeAt)
            {
                // 0.21 (knockouts can't kill): he comes round - groggy, punch-drunk, and back in the fight
                s.koWakeAt = 0f; s.knockedOut = false;
                var cc = s.creature;
                if (cc.isKilled) return;
                s.limpParts = s.koLimbs; s.looseUntil = 0f; s.looseParts = 0; s.looseAmount = 0f;
                Guard("muscles", () => { RestoreMuscles(s); cc.ragdoll.ResetPinForce(); if (s.limpParts != 0) MakeLimp(s, s.limpParts); });
                s.eyesShutUntil = now + 1.5f;
                s.drunk = Mathf.Max(s.drunk, 1.1f);
                SetMelee(s, false, now + 4f);
                Guard("brain", () => cc.brain.Load(cc.data.brainId));
                s.rnasDirty = true;
                Log($"{WhoIs(s)} comes round - groggy and punch-drunk");
                return;
            }
            if (!s.knockedOut || s.koDeathAt <= 0f || now < s.koDeathAt) return;
            s.koDeathAt = 0f;
            var c = s.creature;
            if (!c.isKilled) Guard("kill", () => c.Kill());
            Log($"{WhoIs(s)} died without waking up");
        }

        // ---------------- deep cuts ----------------
        void DisablingCut(NpcState s, RagdollPart part, Vector3 point, Vector3 away, float k)
        {
            var c = s.creature;
            foreach (var side in new[] { Side.Left, Side.Right }) { var h = Hand(c, side); if (h != null && h.grabbedHandle != null && !(h.grabbedHandle is HandleRagdoll)) { var hh = h; Guard("drop", () => hh.UnGrab(false)); } }
            float dur = Mathf.Lerp(4f, 9f, k);
            AddTag(s, Tag.Agony, dur);
            s.agonyUntil = Mathf.Max(s.agonyUntil, Time.time + dur);
            SetMelee(s, false, Time.time + dur);
            QueueClutch(s, part, point, true, 0.1f);
            s.bleedRate += 0.4f * bleedingRate;
            for (int i = 0; i < 3; i++) Guard("blood", () => RageEngineScript.Blood(part.transform));
            Scream(s, true);
            Log($"{WhoIs(s)}: deep cut across the {(point.y < part.transform.position.y ? "belly" : "chest")} - drops everything, out of the fight {dur:0}s");
        }

        // ---------------- hands ----------------
        void BreakHand(NpcState s, RagdollPart hand, Vector3 point, float severity)
        {
            var c = s.creature;
            Side side = Has(hand.type, RagdollPart.Type.LeftHand) ? Side.Left : Side.Right;
            int i = (side == Side.Left ? 0 : (int)Mu.Count) + (int)Mu.HandTendons;
            if (s.muscles[i] >= 1f) return;
            s.muscles[i] = 1f; s.musclesDirty = true;
            if (severity >= boneToughness * 1.8f) Guard("handbone", () => s.boneMeshes.Add(BoneMesh.Attach(hand, point, (point - hand.transform.position).normalized, "RE_Bone_Metacarpal")));
            var h = Hand(c, side);
            if (h != null && h.grabbedHandle != null) Guard("drop", () => h.UnGrab(false));
            Sfx.Play("NRE_BoneSnap_Arm", hand.transform, 0.9f);
            Guard("fingers", () => FingerBones(s, hand, point, severity));
            Transform bone = hand.meshBone != null ? hand.meshBone : hand.bone != null ? hand.bone.mesh : null;
            if (false && bone != null) Guard("handkink", () => HeadDeform.Dent(c, bone, point, Random.onUnitSphere, 0.04f, 0.01f));
            Guard("handpaint", () => SkinPaint.Paint(c, point, 0.035f, new Color(0.32f, 0.08f, 0.24f, 0.7f), TexGen.Bruise()));
            BoneBreakShock(s, hand, severity);
            Log($"{WhoIs(s)}: {side} HAND BROKEN - can't hold anything with it");
        }

        /// Every broken bone: a real scream and a full-body jolt; the arms stop swinging a moment.
        void BoneBreakShock(NpcState s, RagdollPart part, float severity)
        {
            var c = s.creature;
            if (c.isKilled) return;
            Scream(s, true);
            Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Head | RagdollPart.Type.Neck | ArmParts, 0.65f, 0.7f);
            s.coreUntil = Mathf.Max(s.coreUntil, Time.time + 0.9f);
            part.physicBody?.AddForce(Random.onUnitSphere * 2.5f, ForceMode.Impulse);
            c.ragdoll.headPart?.physicBody?.AddForce((Vector3.up * 0.3f - c.transform.forward) * 2f, ForceMode.Impulse);   // head thrown back in the scream
            SetMelee(s, false, Time.time + 1.5f);
            s.pain = 1f;
            AgonyCheck(s, Mathf.Max(1f, severity));
        }

        /// Blood up and out of the mouth (rib shots, lungs).
        void SpitBlood(NpcState s, int amount)
        {
            var mouth = Mouth(s.creature);
            for (int i = 0; i < amount; i++) Guard("blood", () => RageEngineScript.Blood(mouth));
            s.bloodInAirway = true; s.nextCoughTime = Mathf.Min(s.nextCoughTime > 0f ? s.nextCoughTime : float.MaxValue, Time.time + Random.Range(0.8f, 2f));
            Speak(s, "choke");
        }

        // ---------------- impaled: pulling the blade out ----------------
        void TickPullOut(NpcState s, float now)
        {
            var c = s.creature;
            var it = s.impaleItem;
            if (it == null || c.isKilled || Pumped(s) || s.shocked) { s.pullHand = null; return; }
            if (it.mainHandler != null && it.mainHandler.creature != c) { s.pullHand = null; return; }          // you're still holding it
            if (s.pullHand == null)
            {
                if (now - s.impaleSince < 1.2f || now < s.nextPullTry) return;
                Side side = HandUsable(s, Side.Right) ? Side.Right : HandUsable(s, Side.Left) ? Side.Left : (Side)(-1);
                if ((int)side < 0) return;
                var hand = Hand(c, side);
                var handle = it.handles != null && it.handles.Count > 0 ? it.handles[0] : null;
                if (hand == null || handle == null || Vector3.Distance(hand.transform.position, handle.transform.position) > 0.9f) { s.nextPullTry = now + 1f; return; }
                if (hand.grabbedHandle != null) Guard("drop", () => hand.UnGrab(false));
                bool ok = false;
                Guard("grab", () => { hand.Grab(handle, false); ok = hand.grabbedHandle == handle; });
                if (!ok) { s.nextPullTry = now + 2f; return; }
                s.pullHand = hand; s.pullDoneAt = now + Random.Range(1f, 2f);
                Scream(s, false);
                Log($"{WhoIs(s)} grabs the {it.data?.id} in him - trying to pull it out");
                return;
            }
            if (now < s.pullDoneAt) { s.pullHand.physicBody?.AddForce(Random.insideUnitSphere * 1.2f, ForceMode.Impulse); return; }   // straining
            var h2 = s.pullHand; s.pullHand = null;
            Guard("unpen", () => it.FullyUnpenetrate());
            Guard("drop", () => h2.UnGrab(false));
            Guard("toss", () => it.physicBody?.AddForce((it.transform.position - c.transform.position).normalized * 3f + Vector3.up, ForceMode.VelocityChange));
            for (int i = 0; i < 3; i++) if (s.impalePart != null) Guard("blood", () => RageEngineScript.Blood(s.impalePart.transform));
            s.pain = Mathf.Min(1f, s.pain + 0.25f); s.bleedRate += 0.3f * bleedingRate;
            Scream(s, true);
            s.nextPullTry = now + 5f;
            Log($"{WhoIs(s)} PULLS the {it.data?.id} out of himself");
        }

        // ---------------- fight talk / invitations ----------------
        void AnnounceFists(NpcState s)
        {
            if (s.announcedFists) return;
            s.announcedFists = true; s.fistsAt = Time.time;
            s.beckonUntil = Time.time + 1.4f;
            Speak(s, "taunt");
            Log($"{WhoIs(s)} drops into a fighting stance and waves you in");
        }

        /// Some confident NPCs see you barehanded and throw their weapon away to fight you fair.
        void MaybeInviteFistfight(NpcState s, float now)
        {
            var c = s.creature;
            if (s.ally || !fairFights || s.invited || !s.fairFighter || now < s.nextInviteCheck || !s.inCombat) return;
            s.nextInviteCheck = now + 1f;
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) return;
            bool playerBare = (pc.handLeft == null || pc.handLeft.grabbedHandle == null) && (pc.handRight == null || pc.handRight.grabbedHandle == null);
            if (!playerBare || !(RealWeapon(c, Side.Left) || RealWeapon(c, Side.Right)) || s.pain > 0.3f) return;
            var target = PlayerTarget();
            if (target == null || Vector3.Distance(target.position, c.transform.position) > 4f) return;
            s.invited = true;
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                var h = Hand(c, side);
                if (h == null || h.grabbedHandle == null || h.grabbedHandle is HandleRagdoll) continue;
                var it = h.grabbedHandle.item; var hh = h;
                Guard("drop", () => hh.UnGrab(false));
                if (it != null) { s.badWeapon = it; s.badWeaponUntil = now + 60f; Guard("toss", () => it.physicBody?.AddForce(-c.transform.forward * 2f + c.transform.right * 2f + Vector3.up * 2f, ForceMode.VelocityChange)); }
            }
            s.announcedFists = false;
            Log($"{WhoIs(s)} sees you're barehanded - throws his weapon aside: a fair fight");
            s.rnasDirty = true;
        }

        // ---------------- groups ----------------
        static readonly NpcState[] engagedBuf = new NpcState[MaxTracked];

        /// Only the two best-placed fighters engage; the rest hold at range on your flanks/back and swap in.
        void TickGroup(NpcState s, float now)
        {
            var c = s.creature;
            if (c.isKilled || !s.inCombat || c.ragdoll.state != Ragdoll.State.Standing || s.behavior >= Behavior.Surrender) { s.waiting = false; return; }
            var target = PlayerTarget();
            if (target == null) return;
            // rank everyone fighting you by distance; first N attack
            int n = 0;
            for (int i = 0; i < MaxTracked; i++)
            {
                var o = pool[i];
                if (o.active && o.creature != null && !o.creature.isKilled && o.inCombat && o.creature.ragdoll.state == Ragdoll.State.Standing && o.behavior < Behavior.Surrender)
                    engagedBuf[n++] = o;
            }
            if (n <= 2) { s.waiting = false; return; }
            int attackers = 2 + (s.trAggression > 0.8f ? 1 : 0);
            float myD = Vector3.Distance(c.transform.position, target.position);
            int closer = 0;
            for (int i = 0; i < n; i++)
                if (engagedBuf[i] != s && Vector3.Distance(engagedBuf[i].creature.transform.position, target.position) < myD - 0.05f) closer++;
            bool wait = closer >= attackers && !HasTag(s, Tag.Anger);
            if (wait && !s.waiting) Log($"{WhoIs(s)} waits for an opening ({closer} closer)");
            s.waiting = wait;
            if (!wait) return;
            SetMelee(s, false, now + 0.5f);
            // flank slot: spread around you (sides and back), 2.6 m out
            int slot = System.Array.IndexOf(pool, s) % 5;
            Vector3 pf = Player.local != null && Player.local.head != null ? Vector3.ProjectOnPlane(Player.local.head.transform.forward, Vector3.up).normalized : Vector3.forward;
            float ang = new[] { 75f, -75f, 140f, -140f, 180f }[slot];
            Vector3 want = target.position + Quaternion.Euler(0f, ang, 0f) * pf * 2.6f;
            Vector3 to = want - c.transform.position; to.y = 0f;
            if (to.magnitude > 0.6f && now >= s.nextFlankStep) { s.nextFlankStep = now + 0.5f; ShoveBodyQuiet(s, to, 0.45f); }
        }

        /// Knocked down / thrown while angry = gets up and charges straight back.
        void OnGotUpAngry(NpcState s)
        {
            if (s.ally) return;
            if (!(HasTag(s, Tag.Anger) || s.trAggression > 0.7f) || s.cantStand) return;
            s.chargeUntil = Time.time + 4f;
            var c = s.creature;
            Guard("speed", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(ChargeHandler); c.locomotion.globalMoveSpeedMultiplier.Add(ChargeHandler, 1.35f); });
            Scream(s, false);
            Log($"{WhoIs(s)} gets up FURIOUS - charges back at you");
        }

        void TickCharge(NpcState s, float now)
        {
            if (s.chargeUntil <= 0f || now < s.chargeUntil) return;
            s.chargeUntil = 0f;
            var c = s.creature;
            Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Remove(ChargeHandler));
        }

        static readonly object ChargeHandler = new object();

        /// Armed NPCs: sidestep a swing coming at them; step back after being hit to make space.
        void TickArmedFootwork(NpcState s, float now)
        {
            var c = s.creature;
            if (c.isKilled || c.ragdoll.state != Ragdoll.State.Standing || s.shocked || !(RealWeapon(c, Side.Left) || RealWeapon(c, Side.Right))) return;
            var pc = Player.local != null ? Player.local.creature : null;
            var head = c.ragdoll.headPart;
            if (pc == null || head == null || now < s.nextDodge) return;
            foreach (var ph in new[] { pc.handLeft, pc.handRight })
            {
                if (ph == null || ph.grabbedHandle == null || ph.grabbedHandle.item == null || ph.grabbedHandle.item.physicBody == null) continue;
                var it = ph.grabbedHandle.item;
                Vector3 v = it.physicBody.velocity; Vector3 to = c.transform.position + Vector3.up - it.transform.position;
                if (v.magnitude < 4f || to.magnitude > 1.6f || Vector3.Dot(v.normalized, to.normalized) < 0.5f) continue;
                s.nextDodge = now + 1.5f;
                if (Random.value > 0.25f + 0.4f * s.trAwareness) return;
                Vector3 side = Vector3.Cross(Vector3.up, to.normalized) * (Random.value < 0.5f ? -1f : 1f);
                ShoveBodyQuiet(s, side - to.normalized * 0.4f, 1.1f);
                Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.3f, 0.3f);
                Log($"{WhoIs(s)} sidesteps your swing");
                return;
            }
        }

        // ---------------- surprise 1: rage moment ----------------
        static float nextRageMoment;
        static AnimationCurve rageCurve;
        internal static void RageMoment(string why)
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextRageMoment) return;
            nextRageMoment = now + 20f;
            Guard("ragemoment", () =>
            {
                if (TimeManager.slowMotionState != TimeManager.SlowMotionState.Disabled) return;    // the player's own slow-mo wins
                if (rageCurve == null) rageCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                TimeManager.SetSlowMotion(true, 0.3f, rageCurve, null, true, false);
                rageEnd = Time.realtimeSinceStartup + 0.7f;
            });
            var p = PlayerTarget();
            if (p != null) Sfx.PlayAt("NRE_HeavyImpact", p.position, 1f);
            Log($"RAGE MOMENT ({why})");
        }
        static float rageEnd;
        static void TickRageMoment()
        {
            if (rageEnd <= 0f || Time.realtimeSinceStartup < rageEnd) return;
            rageEnd = 0f;
            Guard("ragemoment", () => TimeManager.StopSlowMotion());
        }

        // ---------------- surprise 2: old scars ----------------
        void ShowOldScars(NpcState s)
        {
            var rec = s.identity;
            var c = s.creature;
            if (rec == null || rec.scars == 0 || c.ragdoll == null) return;
            int painted = 0;
            foreach (var p in c.ragdoll.parts)
            {
                if (p == null || (rec.scars & (int)p.type) == 0) continue;
                Vector3 at = p.transform.position + Random.insideUnitSphere * 0.03f;
                Guard("scar", () => { if (SkinPaint.Paint(c, at, 0.03f, new Color(0.78f, 0.62f, 0.58f, 0.7f), TexGen.Bruise())) painted++; });                    // pale healed scar
                Guard("scar", () => SkinPaint.Paint(c, at + Random.insideUnitSphere * 0.02f, 0.02f, new Color(0.45f, 0.3f, 0.3f, 0.5f), TexGen.Bruise()));     // its darker line
                if (Has(p.type, LegParts) && (rec.brokenLegs != 0)) s.legWounded = true;
            }
            if (rec.brokenLegs != 0) Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Add(InjuryHandler, 0.8f));   // the old limp
            s.pointAtUntil = Time.time + 2f;
            Log($"{rec.name} still carries the scars from last time ({painted} marks{(rec.brokenLegs != 0 ? ", walks with a limp" : "")})");
        }

        void TickPointAt(NpcState s, float now)
        {
            var c = s.creature;
            var ik = c.ragdoll.ik;
            if (s.pointAtUntil <= 0f || ik == null) return;
            var target = PlayerTarget();
            if (now >= s.pointAtUntil || target == null || c.isKilled)
            {
                s.pointAtUntil = 0f;
                if (!(s.clutching && s.clutchSide == Side.Right) && s.punchPhase == 0) { ik.SetHandAnchor(Side.Right, null); ik.SetHandState(Side.Right, false, false); }
                return;
            }
            if (Dead(s, Side.Right) || RealWeapon(c, Side.Right)) return;
            if (s.pointAnchor == null) s.pointAnchor = new GameObject("RageEngine_Point").transform;
            var chest = ChestPart(c);
            Vector3 from = chest != null ? chest.transform.position + Vector3.up * 0.15f : c.transform.position + Vector3.up * 1.4f;
            s.pointAnchor.position = from + (target.position - from).normalized * 0.6f;
            s.pointAnchor.rotation = Quaternion.LookRotation(target.position - from, Vector3.up);
            ik.SetHandAnchor(Side.Right, s.pointAnchor); ik.SetHandState(Side.Right, true, true);
        }
    }
}

// Rage Engine 0.54 - smarter defense, combat exhaustion, recovering after a stagger.
//
// Everything here steers the GAME'S OWN fighting AI (its blocks, parries, dodges, ripostes and attacks) instead of
// replacing it, and it only adds to what 0.52 already does (vanilla parry tuning in Gore22, weapon proficiency in
// Rage43, the step-back dodge in Rage47).
//
// DEFENSE BEATS: every 1.2-2.4 s in a fight an NPC picks how he'll defend the next exchange, from what's coming
//   (an overhead chop, a thrust, a side swing), what he's holding (shield, two-hander, dagger, nothing), how tired and
//   hurt he is, how good he is and his personality. Six ways: steady guard, high tight guard, low wide guard, evasive
//   (the game's dodges and sidesteps switched on), counter-ready (more and quicker ripostes) and backing off a step.
//   He won't pick the same one three times running. Sifu-style: the situation picks the move, not a dice roll.
// PARRY -> COUNTER: when he parries you (the game's parry event), a skilled, fresh NPC hits back a moment later -
//   but only if nothing interrupted him in between (no hit taken, not staggered, not already swinging, still standing).
// GUARD HITS: a hard blow on his guard knocks it aside in the direction it came from (shoulders turn, an overhead
//   blow sags the knees) and his defense is shaky for half a second. Light blows just rock him.
// EXHAUSTION: swings, blocks, dodges and hits taken add up; resting brings it back. Tired = slower, weaker guard and
//   slower swings; very tired = he slumps and gasps between exchanges; spent = he has to catch his breath. How fast
//   depends on personality, skill, injuries and the Style setting.
// RECOVERY: after a stagger or getting knocked down he takes a moment (shorter when skilled, longer when tired or
//   hurt) before attacking again - so an attack he'd lined up before the stagger never fires late, and nothing
//   doubles up - then re-reads the fight and picks a new defense.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        [ModOptionCategory("NPCs", 2), ModOptionOrder(44)]
        [ModOption("Smarter defense", "NPCs choose how to defend from what's coming at them: high or low guard, sidestep, back off, or parry and hit back. Uses the game's own blocks, dodges and ripostes.", defaultValueIndex = 1)]
        public static bool defense54 = true;

        [ModOptionCategory("NPCs", 2), ModOptionOrder(45)]
        [ModOption("Combat exhaustion", "Swinging, blocking, dodging and taking hits tires NPCs: weaker guard, slower swings, gasping between exchanges. Resting brings it back.", defaultValueIndex = 1)]
        public static bool exhaustion54 = true;

        static readonly string[] Profiles54 = { "steady guard", "high guard", "low wide guard", "evasive", "counter-ready", "backing off" };
        static float nextCombatTick54;

        // ================================================================== the game's modules for this NPC
        /// Finds (once per brain) the game's melee / defense / hit-reaction / detection / movement modules and saves their values.
        static bool Mods54(NpcState s)
        {
            var c = s.creature;
            var inst = c != null && c.brain != null ? c.brain.instance : null;
            if (inst == null) return false;
            if (inst == s.brainInst54) return true;
            CombatRestore54(s);
            s.brainInst54 = inst;
            Guard("mods54", () =>
            {
                s.melee54 = inst.GetModule<BrainModuleMelee>(false);
                s.def54 = inst.GetModule<BrainModuleDefense>(false);
                s.hit54 = inst.GetModule<BrainModuleHitReaction>(false);
            });
            Guard("mods54.ai", () => { s.det54 = inst.GetModule<BrainModuleDetection>(false); s.move54 = inst.GetModule<BrainModuleMove>(false); });
            if (s.def54 != null) Guard("def54.save", () =>
            {
                var d = s.def54;
                s.defO54 = new[] { d.defenseMaxDistance, d.heldDefenseMinRadius, d.heldDefenseMaxRadius, d.dynamicHeightOffset, d.parryHorizontalMaxAngle,
                                   d.shieldRiposteChance, d.riposteBlockRequirement, d.minMaxDodgeCooldown.x, d.minMaxDodgeCooldown.y, d.dodgeEnabled ? 1f : 0f, d.sideDodgeEnabled ? 1f : 0f };
            });
            if (s.melee54 != null) Guard("melee54.save", () =>
            {
                var m = s.melee54;
                s.meleeO54 = new[] { m.recoilNothingRiposteChance.x, m.recoilNothingRiposteChance.y, m.minMaxRiposteDelay.x, m.minMaxRiposteDelay.y, m.riposteSpeedAdditiveModifier };
            });
            if (s.det54 != null) Guard("det54.save", () =>
            {
                var d = s.det54;
                s.detO54 = new[] { d.sightDetectionHorizontalFov, d.sightDetectionVerticalFov, d.hearMaxDistance, d.canHear ? 1f : 0f,
                                   d.crouchingMultiplier, d.enemyInFOVIncrease, d.alertednessLossPerSec, d.acquireSharedTargetDelay };   // 0.56: + the stealth values
            });
            s.profile54 = 0; s.percKey54 = null;
            return true;
        }

        /// Puts every value we changed on the game's modules back (Release, or the brain was swapped).
        static void CombatRestore54(NpcState s)
        {
            if (s.def54 != null && s.defO54 != null) Guard("def54.restore", () =>
            {
                var d = s.def54; var o = s.defO54;
                d.defenseMaxDistance = o[0]; d.heldDefenseMinRadius = o[1]; d.heldDefenseMaxRadius = o[2]; d.dynamicHeightOffset = o[3]; d.parryHorizontalMaxAngle = o[4];
                d.shieldRiposteChance = o[5]; d.riposteBlockRequirement = o[6]; d.minMaxDodgeCooldown = new Vector2(o[7], o[8]); d.dodgeEnabled = o[9] > 0.5f; d.sideDodgeEnabled = o[10] > 0.5f;
            });
            if (s.melee54 != null && s.meleeO54 != null) Guard("melee54.restore", () =>
            {
                var m = s.melee54; var o = s.meleeO54;
                m.recoilNothingRiposteChance = new Vector2(o[0], o[1]); m.minMaxRiposteDelay = new Vector2(o[2], o[3]); m.riposteSpeedAdditiveModifier = o[4];
            });
            if (s.det54 != null && s.detO54 != null) Guard("det54.restore", () =>
            {
                var d = s.det54; var o = s.detO54;
                d.sightDetectionHorizontalFov = o[0]; d.sightDetectionVerticalFov = o[1]; d.hearMaxDistance = o[2]; d.canHear = o[3] > 0.5f;
                if (o.Length >= 8) { d.crouchingMultiplier = o[4]; d.enemyInFOVIncrease = o[5]; d.alertednessLossPerSec = o[6]; d.acquireSharedTargetDelay = o[7]; }
            });
            if (s.move54 != null && s.walking54) Guard("move54.stop", () => s.move54.StopMove());
            s.walking54 = false;
            s.brainInst54 = null; s.melee54 = null; s.def54 = null; s.hit54 = null; s.det54 = null; s.move54 = null;
            s.defO54 = s.meleeO54 = s.detO54 = null;
        }

        /// Release: everything back the way the game had it.
        static void CombatOnRelease54(NpcState s) => CombatRestore54(s);

        // ================================================================== exhaustion
        static float StyleCost54() => style == "brute" ? 0.7f : style == "light" ? 1.3f : style == "realism" ? 1.2f : 1f;
        static float StyleRest54() => style == "brute" ? 1.3f : style == "light" ? 0.75f : style == "realism" ? 0.85f : 1f;

        static void Exert54(NpcState s, float amount)
        {
            s.lastActive54 = Time.time;
            if (!exhaustion54) return;
            var p = s.personality != null ? s.personality.type : PersonalityType.Calm;
            float pers = p == PersonalityType.Aggressive ? 1.15f : p == PersonalityType.Nervous ? 1.1f : p == PersonalityType.Cautious ? 0.9f : 1f;
            float cost = amount * pers * Mathf.Lerp(1.25f, 0.75f, Mathf.Clamp01(s.prof43)) * StyleCost54() * (1f + 0.6f * Mathf.Clamp01(s.pain)) * (s.drunk > 0.3f ? 1.2f : 1f);
            s.exert54 = Mathf.Clamp01(s.exert54 + cost);
        }

        /// Defense quality multiplier (fed into the existing vanilla-parry tuning).
        internal static float ExertFit54(NpcState s)
        {
            float f = exhaustion54 ? 1f - 0.5f * s.exert54 * s.exert54 : 1f;
            if (Time.time < s.guardBreakUntil54) f *= 0.55f;
            return f;
        }
        /// Swing speed multiplier (fed into the existing proficiency tempo).
        internal static float ExertAnim54(NpcState s) => (exhaustion54 ? 1f - 0.14f * s.exert54 : 1f) * (Time.time < s.counterBoostUntil54 ? 1.12f : 1f);
        /// Pause-between-attacks multiplier (fed into the existing proficiency tempo).
        internal static float ExertDelay54(NpcState s) => (exhaustion54 ? 1f + 0.9f * s.exert54 * s.exert54 : 1f) * (Time.time < s.recoverUntil54 ? 1.8f : 1f);

        static void TickExertion54(NpcState s, float now, bool fighting)
        {
            var c = s.creature;
            float dt = Mathf.Clamp(now - s.lastExertAt54, 0f, 0.5f); s.lastExertAt54 = now;
            // what he did since the last look
            if (s.melee54 != null)
            {
                float t = 0f; var m = s.melee54; Guard("exert.atk", () => t = m.lastAttackStartTime);
                if (s.lastAtk54 <= 0f) s.lastAtk54 = Mathf.Max(t, 0.001f);                  // first look: the value may be from before he was tracked
                else if (t > s.lastAtk54 + 0.05f) { s.lastAtk54 = t; s.atkCount54++; Exert54(s, 0.09f); }
            }
            bool dodging = false; Guard("exert.dodge", () => dodging = c.brain.isDodging);
            if (dodging && !s.dodgeWas54) Exert54(s, 0.1f);
            s.dodgeWas54 = dodging;
            if (s.hitsTaken > s.hitsSeen54) { Exert54(s, 0.04f * (s.hitsTaken - s.hitsSeen54)); s.hitsSeen54 = s.hitsTaken; }
            // rest
            var p = s.personality != null ? s.personality.type : PersonalityType.Calm;
            float pers = p == PersonalityType.Calm ? 1.2f : p == PersonalityType.Cautious ? 1.1f : p == PersonalityType.Aggressive ? 0.85f : p == PersonalityType.Nervous ? 0.9f : 1f;
            float rate = now - s.lastActive54 < 1.5f ? 0.015f : fighting ? 0.1f : 0.22f;
            s.exert54 = Mathf.Max(0f, s.exert54 - rate * pers * Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(s.prof43)) * StyleRest54() * dt);
            if (!exhaustion54) { s.exert54 = 0f; return; }
            // you can see and hear it
            if (s.exert54 > 0.7f && now >= s.nextWinded54 && c.ragdoll.state == Ragdoll.State.Standing && now - s.lastActive54 > 0.8f && s.punchPhase == 0)
            {
                s.nextWinded54 = now + Random.Range(2.5f, 4.5f);
                Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.15f + 0.2f * s.exert54, 1.2f);   // shoulders drop, head hangs
                var mouth = Mouth(c); if (mouth != null) Sfx.Play(s.exert54 > 0.9f ? "NRE_BreathCritical" : "NRE_BreathHeavy", mouth, 0.55f, s.voicePitch * FemaleLift56(c));
            }
            if (s.exert54 > 0.92f && now >= s.nextGassed54)
            {
                s.nextGassed54 = now + 4f;
                if (!s.meleeOff || s.meleeOffUntil < now + 1f) SetMelee(s, false, now + 1f);  // has to catch his breath
                if (debugLog) Log($"{WhoIs(s)} is spent - catching his breath");
            }
        }

        // ================================================================== reading the fight
        /// What's coming at him: 0 nothing, 1 overhead, 2 thrust, 3 side swing.
        static int Incoming54(Creature c)
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) return 0;
            var chest = ChestPart(c); Vector3 at = chest != null ? chest.transform.position : c.transform.position + Vector3.up * 1.2f;
            int kind = 0; float best = 3f;
            foreach (var h in new[] { pc.handLeft, pc.handRight })
            {
                if (h == null) continue;
                Vector3 p = h.transform.position; Vector3 v = h.physicBody != null ? h.physicBody.velocity : Vector3.zero;
                var it = h.grabbedHandle != null ? h.grabbedHandle.item : null;
                if (it != null && it.physicBody != null) { v = it.physicBody.velocity; p = it.transform.position; }
                float sp = v.magnitude;
                if (sp < best || (p - at).sqrMagnitude > 2.5f * 2.5f) continue;
                best = sp; Vector3 dir = v / sp; Vector3 to = (at - p).normalized;
                kind = dir.y < -0.55f ? 1 : Vector3.Dot(dir, to) > 0.8f ? 2 : 3;
            }
            return kind;
        }

        static int Stance54(Creature c)
        {
            int st = -1;
            Guard("stance54", () => { var m = c.brain.instance?.GetModule<BrainModuleStance>(false); if (m != null) st = (int)m.stance; });
            return st;    // BrainModuleStance.Stance: 1 fists, 2 shield, 3 one-hand, 4 two-hand, 5 dual, 9 pole
        }

        /// Picks how he defends the next exchange.
        void Beat54(NpcState s, float now)
        {
            var c = s.creature;
            int inc = Incoming54(c), st = Stance54(c);
            float e = s.exert54, k = Mathf.Clamp01(s.prof43);
            var p = s.personality != null ? s.personality.type : PersonalityType.Calm;
            bool recentHits = now - s.lastHitTime < 2f, armed = HasRealWeapon42(s);
            var w = new float[6];
            w[0] = 1f + 0.6f * e + (k < 0.35f ? 0.4f : 0f);
            w[1] = 0.6f + (inc == 1 ? 1.2f : 0f) + (st == 2 ? 0.5f : 0f) + (p == PersonalityType.Cautious || p == PersonalityType.Calm ? 0.4f : 0f);
            w[2] = 0.6f + (inc == 3 ? 1f : 0f) + (st == 4 || st == 9 ? 0.6f : 0f);
            w[3] = e < 0.6f ? (inc == 2 ? 1.2f : 0.2f) + Mathf.Max(0f, k - 0.3f) * 1.5f + (!armed || st == 1 || st == 5 ? 0.5f : 0f) + (p == PersonalityType.Nervous ? 0.4f : 0f) : 0f;
            w[4] = e < 0.7f && armed ? Mathf.Max(0f, k - 0.35f) * 2f + (p == PersonalityType.Aggressive || p == PersonalityType.Confident ? 0.6f : 0f) + 0.5f * s.trAggression : 0f;
            w[5] = (recentHits ? 0.8f : 0f) + (e > 0.6f ? 0.8f * e : 0f) + 0.6f * Mathf.Clamp01(s.pain) + (p == PersonalityType.Cautious || p == PersonalityType.Nervous ? 0.3f : 0f);
            if (s.repeat54 >= 2) w[s.profile54] *= 0.2f;                                         // not the same thing three times running
            float sum = 0f; for (int i = 0; i < 6; i++) { w[i] = w[i] * w[i]; sum += w[i]; }
            float r = Random.value * sum; int pick = 0;
            for (int i = 0; i < 6; i++) { r -= w[i]; if (r <= 0f) { pick = i; break; } }
            s.repeat54 = pick == s.profile54 ? s.repeat54 + 1 : 0;
            if (pick != s.profile54 && debugLog) Log($"{WhoIs(s)}: {Profiles54[pick]} (incoming {(inc == 1 ? "overhead" : inc == 2 ? "thrust" : inc == 3 ? "swing" : "none")}, tired {e:0.00})");
            s.profile54 = pick;
            ApplyProfile54(s, pick, now);
        }

        /// Sets the game's guard / dodge / riposte values for a defense, always from the saved originals.
        static void ApplyProfile54(NpcState s, int pick, float now)
        {
            float e = s.exert54;
            if (s.def54 != null && s.defO54 != null) Guard("def54.apply", () =>
            {
                var d = s.def54; var o = s.defO54;
                d.defenseMaxDistance = o[0] * (pick == 5 ? 1.2f : 1f);
                d.heldDefenseMinRadius = o[1] * (pick == 1 ? 0.85f : 1f);
                d.heldDefenseMaxRadius = o[2] * (pick == 1 ? 0.85f : pick == 2 ? 1.2f : 1f);
                d.dynamicHeightOffset = o[3] + (pick == 1 ? 0.08f : pick == 2 ? -0.08f : 0f);
                d.parryHorizontalMaxAngle = o[4] * (pick == 1 ? 1.15f : pick == 2 ? 1.3f : 1f);
                d.shieldRiposteChance = Mathf.Clamp01(o[5] * (pick == 4 ? 1.5f : 1f));
                d.riposteBlockRequirement = o[6] * (pick == 4 ? 0.7f : 1f);
                bool evasive = pick == 3;
                d.dodgeEnabled = evasive || o[9] > 0.5f;
                d.sideDodgeEnabled = evasive || o[10] > 0.5f;
                d.minMaxDodgeCooldown = evasive ? new Vector2(o[7] * 0.5f, o[8] * 0.5f) : new Vector2(o[7], o[8]);
                if (evasive) d.nextAllowedDodgeTime = Mathf.Min(d.nextAllowedDodgeTime, now);
            });
            if (s.melee54 != null && s.meleeO54 != null) Guard("melee54.apply", () =>
            {
                var m = s.melee54; var o = s.meleeO54;
                float ch = pick == 4 ? 1.6f * (1f - 0.5f * e) : 1f;
                m.recoilNothingRiposteChance = new Vector2(Mathf.Clamp01(o[0] * ch), Mathf.Clamp01(o[1] * ch));
                m.minMaxRiposteDelay = pick == 4 ? new Vector2(o[2] * 0.6f, o[3] * 0.6f) : new Vector2(o[2], o[3]);
                m.riposteSpeedAdditiveModifier = o[4] + (pick == 4 ? 0.1f : 0f);
            });
            if (pick == 5)
            {   // back off a step before re-engaging
                var pc = Player.local != null ? Player.local.creature : null;
                if (pc != null) ShoveBodyQuiet(s, Vector3.ProjectOnPlane(s.creature.transform.position - pc.transform.position, Vector3.up).normalized, 0.6f);
                if (!s.meleeOff || s.meleeOffUntil < now + 0.5f) SetMelee(s, false, now + 0.5f);
            }
        }

        // ================================================================== parries (game event)
        void OnParry54(Creature parried, Item parriedItem, Creature parrying, Item parryingItem, CollisionInstance hit)
        {
            if (!modEnabled || OrgansOnly || !defense54) return;
            float now = Time.time;
            // he parried an attack: maybe hit back, and the blow pushes his guard around
            if (parrying != null && !parrying.isPlayer && TryGet(parrying, out var d) && d.life54 == LifeOf54(parrying))
            {
                Exert54(d, 0.05f);
                Vector3 v = Vector3.zero;
                Guard("parry.v", () => { if (parriedItem != null && parriedItem.physicBody != null) v = parriedItem.physicBody.velocity; });
                Guard("parry.guard", () => GuardHit54(d, v, now));
                if (parried != null && parried.factionId != parrying.factionId) MaybeCounter54(d, now);
            }
            // his attack got parried: recoil, nothing lined up survives, rethink
            if (parried != null && !parried.isPlayer && TryGet(parried, out var a) && a.life54 == LifeOf54(parried))
            {
                Exert54(a, 0.07f);
                float k = Mathf.Clamp01(a.prof43);
                a.recoverUntil54 = now + Mathf.Lerp(0.9f, 0.45f, k) * (1f + 0.5f * a.exert54);
                a.counterAt54 = 0f;
                a.nextBeat54 = a.recoverUntil54;
                if (debugLog) Log($"{WhoIs(a)}: attack parried - recovering {a.recoverUntil54 - now:0.00}s");
            }
        }

        /// A blow on his guard: hard ones knock it aside the way they came, light ones rock him.
        void GuardHit54(NpcState s, Vector3 v, float now)
        {
            var c = s.creature;
            if (c.ragdoll.state != Ragdoll.State.Standing) return;
            float sp = v.magnitude;
            if (sp < 2.5f) return;
            Vector3 dir = Vector3.ProjectOnPlane(v, Vector3.up); dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : -c.transform.forward;
            float hold = Mathf.Lerp(0.7f, 1.3f, Mathf.Clamp01(s.prof43)) * (1f - 0.4f * s.exert54) * Tough33(s);
            if (sp > 5.5f / Mathf.Max(0.5f, hold))
            {   // guard broken: turned with the blow, shaky defense for a moment
                ShoveBodyQuiet(s, dir, Mathf.Clamp(sp * 0.09f, 0.3f, 0.9f));
                Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.LeftArm | RagdollPart.Type.RightArm, 0.35f, 0.45f);
                if (v.y < -0.5f * sp) Loosen(s, RagdollPart.Type.LeftLeg | RagdollPart.Type.RightLeg, 0.25f, 0.4f);   // an overhead blow sags the knees
                s.guardBreakUntil54 = now + 0.5f; s.nextDefense = 0f;                       // re-tune the parry right away
                s.counterAt54 = 0f;
                Exert54(s, 0.06f);
                if (debugLog) Log($"{WhoIs(s)}: guard knocked aside ({sp:0.0} m/s)");
            }
            else ShoveBodyQuiet(s, dir, Mathf.Clamp(sp * 0.03f, 0.08f, 0.25f));
        }

        void MaybeCounter54(NpcState s, float now)
        {
            if (now - s.lastCounter54 < 1.5f || s.counterAt54 > 0f || now < s.guardBreakUntil54 || !HasRealWeapon42(s)) return;
            float k = Mathf.Clamp01(s.prof43);
            var p = s.personality != null ? s.personality.type : PersonalityType.Calm;
            float chance = (0.2f + 0.55f * k) * (p == PersonalityType.Aggressive || p == PersonalityType.Confident ? 1.2f : p == PersonalityType.Cautious ? 0.8f : 1f)
                           * (exhaustion54 ? 1f - s.exert54 : 1f) * (s.profile54 == 4 ? 1.4f : 1f);
            if (Random.value > chance) return;
            s.counterAt54 = now + Mathf.Lerp(0.35f, 0.12f, k) + (exhaustion54 ? 0.2f * s.exert54 : 0f);
            s.counterHit54 = s.lastHitTime;
        }

        /// The counter fires only if nothing got in the way since the parry.
        void TryCounter54(NpcState s, float now)
        {
            var c = s.creature;
            bool late = now - s.counterAt54 > 1f;
            s.counterAt54 = 0f;
            if (late) return;
            bool busy = false, staggered = false;
            Guard("counter.state", () => { busy = c.brain.isAttacking; staggered = c.brain.currentStagger != Brain.Stagger.None; });
            if (busy || staggered || s.lastHitTime > s.counterHit54 + 0.001f || c.ragdoll.state != Ragdoll.State.Standing || now < s.recoverUntil54
                || s.meleeOff || s.surrendered || s.knockedOut || s.melee54 == null) return;
            Transform target = null;
            Guard("counter.target", () => target = s.melee54.targetTransform);
            if (target == null) target = PlayerTarget();
            if (target == null || (target.position - c.transform.position).sqrMagnitude > 3f * 3f) return;
            var m = s.melee54; var tt = target;
            bool ok = false;
            Guard("counter.attack", () => { m.AttackTarget(tt); ok = true; });
            if (!ok) return;
            s.lastCounter54 = now; s.counterBoostUntil54 = now + 0.7f;
            Exert54(s, 0.06f);
            Log($"{WhoIs(s)}: parried you and hit back");
        }

        // ================================================================== stagger -> recover -> reassess
        void TickRecovery54(NpcState s, float now)
        {
            var c = s.creature;
            int st = 0; Guard("recover.stagger", () => st = (int)c.brain.currentStagger);
            bool down = c.ragdoll.state != Ragdoll.State.Standing;
            if ((st != 0 && s.staggerWas54 == 0) || (down && !s.downWas54))
            {   // staggered or knocked over: whatever he had lined up is off
                s.recovering54 = true; s.counterAt54 = 0f;
            }
            s.staggerWas54 = st; s.downWas54 = down;
            if (s.recovering54 && st == 0 && !down)
            {
                s.recovering54 = false;
                float rec = Mathf.Lerp(0.8f, 0.35f, Mathf.Clamp01(s.prof43)) * (1f + 0.6f * s.exert54) * (1f + 0.5f * Mathf.Clamp01(s.pain));
                s.recoverUntil54 = now + rec;
                if (!s.meleeOff || s.meleeOffUntil < now + rec) SetMelee(s, false, now + rec);   // no stale or doubled-up attack straight out of the stagger
                s.nextBeat54 = now + rec;                                                         // then re-read the fight
                if (debugLog) Log($"{WhoIs(s)}: back on balance - {rec:0.00}s before he attacks again");
            }
        }

        // ================================================================== tick (10 Hz, tracked NPCs only)
        void TickCombat54()
        {
            float now = Time.time;
            if (now < nextCombatTick54) return;
            nextCombatTick54 = now + 0.1f;
            var pc = Player.local != null ? Player.local.creature : null;
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active || s.creature == null || s.life54 != LifeOf54(s.creature)) continue;
                var c = s.creature;
                if (c.isKilled || c.ragdoll == null) continue;
                if (!Mods54(s)) continue;
                bool fighting = s.inCombat || (pc != null && (c.transform.position - pc.transform.position).sqrMagnitude < 4f * 4f && !s.ally);
                TickExertion54(s, now, fighting);
                if (s.guardBreakUntil54 > 0f && now >= s.guardBreakUntil54) { s.guardBreakUntil54 = 0f; s.nextDefense = 0f; }
                if (!defense54) { if (s.profile54 != 0) { s.profile54 = 0; s.repeat54 = 0; ApplyProfile54(s, 0, now); } s.counterAt54 = 0f; continue; }   // switched off: the game's own values back
                TickRecovery54(s, now);
                if (s.counterAt54 > 0f && now >= s.counterAt54) TryCounter54(s, now);
                if (fighting && now >= s.nextBeat54 && now >= s.recoverUntil54 && c.ragdoll.state == Ragdoll.State.Standing && !s.surrendered)
                {
                    s.nextBeat54 = now + Random.Range(1.2f, 2.4f);
                    Beat54(s, now);
                }
                else if (!fighting && s.profile54 != 0 && now >= s.nextBeat54) { s.profile54 = 0; s.repeat54 = 0; ApplyProfile54(s, 0, now); }
            }
        }
    }
}

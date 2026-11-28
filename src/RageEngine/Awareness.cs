// Rage Engine 0.19 - senses, memory, personality traits, relationships, surrender, idle life.
//
// STIMULI: screams, deaths, metal clashes, body falls, gunshots and explosions are broadcast with a loudness (radius).
//   Every NPC that can hear it (hearing personality, blind NPCs hear better) remembers WHERE and WHEN, looks toward it,
//   and - if it's sudden and close - FLINCHES: head and shoulders jerk away, a hand comes up. Repeats fade (habituation).
//   A near-miss (your weapon or fist whipping past their head) makes them flinch too.
// MEMORY: last place they SAW you (field of view + line of sight, not blind) and last thing they HEARD. Lost sight of
//   you: they keep looking where you were, then search. Old information fades.
// EYES: real eye bones turn toward what they're looking at (you, a sound, your last known spot, idle glances), with
//   small saccades; clamped; blind / shut eyes don't track.
// IDLE: when not fighting, weight shifts, look-arounds (at other people too), more slumped when tired.
// TRAITS: confidence, aggression, awareness, loyalty, patience, risk - from the personality plus randomness. They tune
//   flinching, slipping, punching rhythm, weapon retrieval risk, surrender, and how they take an ally's death.
// RELATIONSHIPS: people who fight side by side remember each other (saved in the roster). Seeing a friend - or a
//   loyal ally - get hurt or killed makes them angry (or, for the timid, afraid).
// SURRENDER: badly hurt + scared + not confident = they give up: weapons dropped, hands up (or out / or covering the
//   head - varies), no walking, no swinging. Hit them again and they either break and run or fight in desperation;
//   leave them alone long enough and they pull themselves together.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal enum Stim : byte { Impact, Scream, Death, Clash, Gunshot, Explosion, NearMiss, Noise }   // 0.54: Noise = the Noisemaker

    public sealed partial class RageEngineScript
    {
        // ---------------- traits ----------------
        static void RollTraits(NpcState s)
        {
            var p = s.personality.type;
            float R(float a, float b) => Random.Range(a, b);
            s.trConfidence = Mathf.Clamp01((p == PersonalityType.Confident ? 0.75f : p == PersonalityType.Nervous ? 0.25f : p == PersonalityType.Aggressive ? 0.65f : 0.5f) + R(-0.2f, 0.2f));
            s.trAggression = Mathf.Clamp01((p == PersonalityType.Aggressive ? 0.8f : p == PersonalityType.Cautious ? 0.3f : p == PersonalityType.Nervous ? 0.35f : 0.5f) + R(-0.2f, 0.2f));
            s.trAwareness = Mathf.Clamp01((p == PersonalityType.Cautious ? 0.75f : p == PersonalityType.Nervous ? 0.7f : 0.5f) + R(-0.2f, 0.2f));
            s.trLoyalty = Mathf.Clamp01(R(0.2f, 0.9f));
            s.trPatience = Mathf.Clamp01((p == PersonalityType.Calm ? 0.75f : p == PersonalityType.Aggressive ? 0.25f : 0.5f) + R(-0.2f, 0.2f));
            s.trRisk = Mathf.Clamp01((p == PersonalityType.Aggressive || p == PersonalityType.Unpredictable ? 0.7f : p == PersonalityType.Cautious || p == PersonalityType.Nervous ? 0.25f : 0.5f) + R(-0.2f, 0.2f));
            if (s.brave) { s.trConfidence = Mathf.Min(1f, s.trConfidence + 0.2f); s.trRisk = Mathf.Min(1f, s.trRisk + 0.15f); }
            s.voicePitch = Random.Range(0.9f, 1.1f) * (s.creature.name.IndexOf("Female", System.StringComparison.OrdinalIgnoreCase) >= 0 ? 1.04f : 1f);
            s.guardStyle = Random.Range(0, 3);
            s.punchPrefJab = Random.Range(0.25f, 0.6f);
            s.idlePhase = Random.Range(0f, 10f);
        }

        // ---------------- stimulus bus ----------------
        /// Something loud happened at pos. radius = how far it can be heard (metres).
        internal static void Emit(Vector3 pos, float radius, Stim kind, Creature source = null, int tag = 0)   // 0.54: tag = which faction's noisemaker (0 none)
        {
            if (instance == null || !modEnabled) return;
            float now = Time.time;
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = instance.pool[i];
                if (!s.active || s.creature == null || s.creature == source || s.creature.isKilled) continue;
                var head = s.creature.ragdoll != null ? s.creature.ragdoll.headPart : null;
                Vector3 ear = head != null ? head.transform.position : s.creature.transform.position;
                float hearing = (s.personality != null ? s.personality.hearingMult : 1f) * (HasTag(s, Tag.Blind) ? 1.5f : 1f) * Hear54();   // 0.54: NPC hearing setting
                float d = Vector3.Distance(ear, pos);
                float r = radius * hearing;                                                    // 0.55: faction items are style only - every NPC hears them the same
                if (d > r) continue;
                float intensity = 1f - d / r;
                s.heardPos = pos; s.heardAt = now; s.heardKind = kind; s.heardTag54 = tag;
                s.lookAt = pos; s.lookUntil = now + 1.5f + intensity;
                instance.Startle(s, pos, intensity, kind, source);
            }
        }

        void Startle(NpcState s, Vector3 from, float intensity, Stim kind, Creature source)
        {
            var c = s.creature;
            float now = Time.time;
            // relationships: an ally / friend dying or screaming nearby
            if (source != null && (kind == Stim.Death || kind == Stim.Scream) && source.factionId == c.factionId && Vector3.Distance(from, c.transform.position) < 10f)
            {
                bool friend = TryGet(source, out var other) && IsFriend(s, other);
                if (friend || s.trLoyalty > 0.6f)
                {
                    if (s.trConfidence > 0.4f || s.brave) { AddTag(s, Tag.Anger, kind == Stim.Death ? 15f : 6f, kind == Stim.Death ? 2f : 1f); if (kind == Stim.Death) Log($"{WhoIs(s)} saw {(friend ? "friend " : "ally ")}{(other != null ? WhoIs(other) : source.name)} die - FURIOUS"); }
                    else { AddTag(s, Tag.Fear, kind == Stim.Death ? 12f : 5f, kind == Stim.Death ? 2f : 1f); if (kind == Stim.Death) Log($"{WhoIs(s)} saw {(friend ? "friend " : "ally ")}{(other != null ? WhoIs(other) : source.name)} die - shaken"); }
                }
            }
            // flinch: sudden + close, fading with repetition, scaled by nerve
            s.habituation = Mathf.Max(0f, s.habituation - (now - s.lastStartle) * 0.12f);
            s.lastStartle = now;
            float sudden = kind == Stim.Explosion || kind == Stim.Gunshot ? 1.3f : kind == Stim.NearMiss ? 1.1f : kind == Stim.Clash ? 0.7f : kind == Stim.Death ? 0.6f : 0.5f;
            float amp = intensity * sudden * (1f - s.habituation) * (1.3f - 0.6f * s.trConfidence);
            s.habituation = Mathf.Min(0.85f, s.habituation + 0.3f * amp);
            if (amp > 0.18f) Flinch(s, from, Mathf.Clamp01(amp));
            s.rnasDirty = true;
        }

        /// Head and shoulders jerk away from 'from'; a hand comes up on big ones. Doesn't cancel what they're doing.
        void Flinch(NpcState s, Vector3 from, float amp)
        {
            var c = s.creature;
            if (c.ragdoll.state != Ragdoll.State.Standing || s.punchPhase != 0 || Pumped(s) || s.shocked || s.knockedOut) return;
            var head = c.ragdoll.headPart;
            if (head == null) return;
            Vector3 away = head.transform.position - from; away.y *= 0.3f;
            if (away.sqrMagnitude < 1e-4f) away = -c.transform.forward;
            away.Normalize();
            head.physicBody?.AddForce((away + Vector3.down * 0.25f) * (1.2f + 3f * amp), ForceMode.Impulse);
            var chest = ChestPart(c);
            chest?.physicBody?.AddForce(away * (0.8f + 2.2f * amp), ForceMode.Impulse);
            Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso, 0.3f + 0.3f * amp, 0.25f + 0.2f * amp);
            s.coreUntil = Mathf.Max(s.coreUntil, Time.time + 0.4f);
            if (amp > 0.6f && !RealWeapon(c, Side.Left) && !RealWeapon(c, Side.Right) && s.guardUntil <= 0f) RaiseGuard(s, head);
            if (amp > 0.85f) SetMelee(s, false, Time.time + 0.4f);
            if (debugLog && amp > 0.4f) Log($"{WhoIs(s)} flinches ({amp:0.00})");
        }

        // ---------------- memory + looking ----------------
        static readonly RaycastHit[] sightHits = new RaycastHit[8];

        void TickAwareness(NpcState s, float dt)
        {
            var c = s.creature;
            float now = Time.time;
            var head = c.ragdoll.headPart;
            var pc = Player.local != null ? Player.local.creature : null;
            if (head == null || pc == null || c.isKilled) return;
            var target = PlayerTarget();
            Vector3 eye = head.transform.position;

            // sight (every 0.3 s): in front, in range, not blind, nothing solid in between
            if (now >= s.nextSight && target != null)
            {
                s.nextSight = now + 0.3f;
                bool canSee = !HasTag(s, Tag.Blind) && !HasTag(s, Tag.EyesShut);
                Vector3 to = target.position - eye;
                float d = to.magnitude;
                float range = Mathf.Lerp(12f, 28f, s.trAwareness) * (s.blindEyes != 0 ? 0.6f : 1f);
                Vector3 fwd = c.centerEyes != null ? c.centerEyes.forward : c.transform.forward;
                bool inView = canSee && d < range && (d < 1.5f || Vector3.Angle(fwd, to) < SightHalf54());   // 0.54: NPC field of view setting
                if (inView)
                {
                    int n = Physics.RaycastNonAlloc(eye, to / Mathf.Max(d, 0.01f), sightHits, d - 0.2f, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n && inView; i++)
                    {
                        var col = sightHits[i].collider;
                        if (col == null || col.attachedRigidbody != null || col.GetComponentInParent<Creature>() != null) continue;
                        inView = false;                                                 // a wall in the way
                    }
                }
                if (inView) { if (now - s.seenAt > 4f && s.seenAt > 0f && debugLog) Log($"{WhoIs(s)} spots you again"); s.seenPos = target.position; s.seenAt = now; }
                else if (s.seenAt > 0f && now - s.seenAt > 0.3f && !s.lostSight && now - s.seenAt < 1f) { s.lostSight = true; if (debugLog) Log($"{WhoIs(s)} lost sight of you - checking where you were"); }
                if (inView) s.lostSight = false;
            }

            // near miss: your weapon / fist whipping past their head without touching
            if (now >= s.nextNearMiss)
                foreach (var ph in new[] { pc.handLeft, pc.handRight })
                {
                    if (ph == null || ph.physicBody == null) continue;
                    Vector3 v = ph.physicBody.velocity; Vector3 p = ph.transform.position;
                    if (ph.grabbedHandle != null && ph.grabbedHandle.item != null && ph.grabbedHandle.item.physicBody != null) { v = ph.grabbedHandle.item.physicBody.velocity; p = ph.grabbedHandle.item.transform.position; }
                    if (v.magnitude < 4f || Vector3.Distance(p, eye) > 0.55f || now - s.lastHitTime < 0.4f) continue;
                    s.nextNearMiss = now + 0.8f;
                    Startle(s, p, 1f, Stim.NearMiss, null);
                    break;
                }

            // what to look at: you (seen) > a fresh sound > where you were > idle glances
            Vector3 look; bool combat = target != null && now - s.seenAt < 1f && Vector3.Distance(eye, target.position) < 6f;
            if (now < s.lookUntil) look = s.lookAt;
            else if (target != null && now - s.seenAt < 0.6f) look = target.position;
            else if (now - s.heardAt < 4f) look = s.heardPos;
            else if (s.seenAt > 0f && now - s.seenAt < 8f)
            {
                // searching around the last known spot
                look = s.seenPos + new Vector3(Mathf.Sin(now * 1.3f + s.idlePhase), 0f, Mathf.Cos(now * 0.9f + s.idlePhase)) * 1.5f;
            }
            else look = IdleLook(s, eye, now);
            s.gazeTarget = look;
            s.inCombat = combat;

            // idle life
            if (!combat && c.ragdoll.state == Ragdoll.State.Standing && s.behavior == Behavior.Normal && now >= s.nextIdle)
            {
                s.nextIdle = now + Random.Range(2.5f, 6f) * (0.7f + s.trPatience * 0.6f);
                Vector3 side = c.transform.right * (Random.value < 0.5f ? -1f : 1f);
                ShoveBodyQuiet(s, side, Random.Range(0.08f, 0.2f));             // weight shift
                if (s.fatigue > 0.5f) Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.25f * s.fatigue, 1.5f);   // slump
            }
            // familiarity: people fighting side by side get to know each other
            if (now >= s.nextFriendCheck && s.identity != null && combat)
            {
                s.nextFriendCheck = now + 5f;
                for (int i = 0; i < MaxTracked; i++)
                {
                    var o = pool[i];
                    if (o == s || !o.active || o.creature == null || o.identity == null || o.creature.isKilled || o.creature.factionId != c.factionId) continue;
                    if (Vector3.Distance(o.creature.transform.position, c.transform.position) > 10f) continue;
                    MakeFriends(s, o);
                }
            }
        }

        Vector3 IdleLook(NpcState s, Vector3 eye, float now)
        {
            if (now >= s.nextIdleLook)
            {
                s.nextIdleLook = now + Random.Range(1.5f, 4f);
                // half the time at someone nearby, otherwise somewhere around
                NpcState pick = null;
                if (Random.value < 0.5f)
                    for (int i = 0; i < MaxTracked; i++)
                    {
                        var o = pool[i];
                        if (o == s || !o.active || o.creature == null || o.creature.ragdoll == null || o.creature.ragdoll.headPart == null) continue;
                        if (Vector3.Distance(o.creature.transform.position, s.creature.transform.position) < 8f) { pick = o; break; }
                    }
                s.idleLookPos = pick != null ? pick.creature.ragdoll.headPart.transform.position
                    : eye + Quaternion.Euler(Random.Range(-15f, 10f), Random.Range(-70f, 70f), 0f) * s.creature.transform.forward * 4f;
            }
            return s.idleLookPos;
        }

        // ---------------- eyes (LateUpdate) ----------------
        void LateUpdateGaze()
        {
            float now = Time.time;
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active || s.creature == null || s.creature.isKilled || s.creature.allEyes == null) continue;
                var c = s.creature;
                if (s.eyeRest == null || s.eyeRest.Length != c.allEyes.Count) InitEyes(s);
                if (now >= s.nextSaccade) { s.nextSaccade = now + Random.Range(0.25f, 1.1f); s.saccade = Random.insideUnitSphere * 0.04f; }
                for (int e = 0; e < c.allEyes.Count; e++)
                {
                    var eye = c.allEyes[e];
                    if (eye == null) continue;
                    eye.transform.localRotation = s.eyeRest[e];
                    if ((s.blindEyes & (1 << e)) != 0 || now < s.eyesShutUntil) continue;          // dead / shut eyes don't track
                    Vector3 gaze = eye.transform.rotation * s.eyeAxis[e];
                    Vector3 want = (s.gazeTarget + s.saccade - eye.transform.position);
                    if (want.sqrMagnitude < 1e-4f) continue;
                    want.Normalize();
                    float ang = Vector3.Angle(gaze, want);
                    if (ang < 0.5f) continue;
                    Vector3 clamped = Vector3.RotateTowards(gaze, want, Mathf.Deg2Rad * Mathf.Min(ang, 28f), 0f);
                    s.eyeAim[e] = Vector3.Slerp(s.eyeAim[e].sqrMagnitude > 0.5f ? s.eyeAim[e] : gaze, clamped, 1f - Mathf.Exp(-18f * Time.deltaTime));
                    eye.transform.rotation = Quaternion.FromToRotation(gaze, s.eyeAim[e]) * eye.transform.rotation;
                }
            }
        }

        static void InitEyes(NpcState s)
        {
            var c = s.creature;
            int n = c.allEyes.Count;
            s.eyeRest = new Quaternion[n]; s.eyeAxis = new Vector3[n]; s.eyeAim = new Vector3[n];
            Vector3 fwd = c.centerEyes != null ? c.centerEyes.forward : c.transform.forward;
            Vector3[] axes = { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left };
            for (int e = 0; e < n; e++)
            {
                var eye = c.allEyes[e];
                if (eye == null) continue;
                s.eyeRest[e] = eye.transform.localRotation;
                float best = -2f;
                foreach (var a in axes) { float d = Vector3.Dot(eye.transform.rotation * a, fwd); if (d > best) { best = d; s.eyeAxis[e] = a; } }
            }
        }

        static void RestoreEyes(NpcState s)
        {
            var c = s.creature;
            if (c == null || c.allEyes == null || s.eyeRest == null) return;
            for (int e = 0; e < c.allEyes.Count && e < s.eyeRest.Length; e++) if (c.allEyes[e] != null) c.allEyes[e].transform.localRotation = s.eyeRest[e];
            s.eyeRest = null;
        }

        // ---------------- relationships ----------------
        static bool IsFriend(NpcState a, NpcState b)
            => a != null && b != null && a.identity != null && b.identity != null && !string.IsNullOrEmpty(a.identity.friends) && a.identity.friends.Contains(b.identity.id);

        static void MakeFriends(NpcState a, NpcState b)
        {
            if (IsFriend(a, b)) return;
            AddFriend(a.identity, b.identity.id); AddFriend(b.identity, a.identity.id);
            rosterDirty = true;
            Log($"{a.identity.name} and {b.identity.name} fought side by side - they know each other now");
        }

        static void AddFriend(NpcRecord r, string id)
        {
            var list = new List<string>(string.IsNullOrEmpty(r.friends) ? new string[0] : r.friends.Split(','));
            if (list.Contains(id)) return;
            list.Add(id);
            if (list.Count > 8) list.RemoveAt(0);
            r.friends = string.Join(",", list.ToArray());
        }

        /// Tells nearby allies that s just got hurt (they get angry or scared depending on bond and nerve).
        void AlliesSeeHurt(NpcState s, float severity)
        {
            if (severity < 0.6f || s.creature == null) return;
            var c = s.creature;
            for (int i = 0; i < MaxTracked; i++)
            {
                var o = pool[i];
                if (o == s || !o.active || o.creature == null || o.creature.isKilled || o.creature.factionId != c.factionId) continue;
                if (Vector3.Distance(o.creature.transform.position, c.transform.position) > 6f) continue;
                bool friend = IsFriend(o, s);
                if (!friend && o.trLoyalty < 0.6f) continue;
                if (o.trConfidence > 0.4f || o.brave) AddTag(o, Tag.Anger, 6f, friend ? 1f : 0.5f); else AddTag(o, Tag.Fear, 5f, 0.5f);
            }
        }

        // ---------------- surrender ----------------
        bool WantsToSurrender(NpcState s)
        {
            if (s.brave || Pumped(s) || s.surrenderBroken || HasTag(s, Tag.Anger) || s.bioNoQuit) return false;
            float injuries = Mathf.Clamp01((CountBits((int)s.brokenParts) * 0.25f) + (s.ribsBroken > 0 ? 0.2f : 0f) + (HasTag(s, Tag.LeftArmUnavailable) || HasTag(s, Tag.RightArmUnavailable) ? 0.25f : 0f) + (s.cantStand ? 0.1f : 0f));
            // 0.21: someone who just put his fists up has chosen to fight - it takes real damage to make him quit,
            // and pain alone never does it
            bool fighting = s.fairFighter || s.stanceOn || Time.time - s.fistsAt < 30f;
            if (fighting && injuries < 0.5f) return false;
            if (injuries < 0.2f && TagIntensity(s, Tag.Fear) < 0.6f) return false;
            if (Time.time - s.trackedAt < 10f) return false;
            float score = TagIntensity(s, Tag.Fear) * 0.25f + Mathf.Clamp01(s.pain * s.painSensitivity) * 0.3f + injuries * 0.5f
                          - s.trConfidence * 0.45f - s.trRisk * 0.25f;
            return score > 0.4f;
        }

        static int CountBits(int v) { int n = 0; while (v != 0) { n += v & 1; v >>= 1; } return n; }

        void EnterSurrender(NpcState s)
        {
            var c = s.creature;
            s.surrenderSince = Time.time; s.surrenderPose = Random.Range(0, 3);
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                var h = Hand(c, side);
                if (h != null && h.grabbedHandle != null && !(h.grabbedHandle is HandleRagdoll)) { var hh = h; Guard("drop", () => hh.UnGrab(false)); }
            }
            DropPhantom(s); ReleaseGuard(s); ReleaseClutch(s);
            Scream(s, false);
            Log($"{WhoIs(s)} SURRENDERS ({(s.surrenderPose == 0 ? "hands up" : s.surrenderPose == 1 ? "hands out, begging" : "covering up")})");
        }

        void TickSurrender(NpcState s, float now)
        {
            var c = s.creature;
            var ik = c.ragdoll.ik;
            var head = c.ragdoll.headPart;
            Guard("rnas-move", () => c.locomotion.allowMove = false);
            SetMelee(s, false, now + 1f);
            if (ik == null || head == null) return;
            if (s.surrenderL == null) { s.surrenderL = new GameObject("RageEngine_SurL").transform; s.surrenderR = new GameObject("RageEngine_SurR").transform; }
            Vector3 fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized, right = Vector3.Cross(Vector3.up, fwd);
            float shake = Mathf.Sin(now * 9f) * 0.01f;
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                if (Dead(s, side)) continue;
                float sg = side == Side.Left ? -1f : 1f;
                var a = side == Side.Left ? s.surrenderL : s.surrenderR;
                Vector3 h = head.transform.position;
                a.position = s.surrenderPose == 0 ? h + Vector3.up * 0.25f + right * sg * 0.28f + fwd * 0.1f
                           : s.surrenderPose == 1 ? h - Vector3.up * 0.3f + right * sg * 0.25f + fwd * 0.4f
                           : h + Vector3.up * 0.08f + right * sg * 0.08f + fwd * 0.05f;
                a.position += Vector3.up * shake;
                a.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                ik.SetHandAnchor(side, a); ik.SetHandState(side, true, true);
            }
            // left alone long enough: pulls himself together
            var target = PlayerTarget();
            float d = target != null ? Vector3.Distance(target.position, head.transform.position) : 99f;
            if (d > 7f) { if (s.aloneSince <= 0f) s.aloneSince = now; } else s.aloneSince = 0f;
            if (s.aloneSince > 0f && now - s.aloneSince > 12f) { s.surrendered = false; s.surrenderBroken = true; s.rnasDirty = true; Log($"{WhoIs(s)} pulls himself together"); }
        }

        void ExitSurrender(NpcState s)
        {
            var ik = s.creature != null && s.creature.ragdoll != null ? s.creature.ragdoll.ik : null;
            if (ik != null) foreach (var side in new[] { Side.Left, Side.Right }) { ik.SetHandAnchor(side, null); ik.SetHandState(side, false, false); }
        }

        /// Hit while surrendering: break and run, or fight in desperation.
        void SurrenderBetrayed(NpcState s)
        {
            if (!s.surrendered) return;
            s.surrendered = false; s.surrenderBroken = true; s.rnasDirty = true;
            if (Random.value < 0.5f + 0.3f * (1f - s.trConfidence)) { Panic(s, 20f); Log($"{WhoIs(s)} was hit while surrendering - RUNS"); }
            else { AddTag(s, Tag.Anger, 20f, 3f); s.brave = true; Log($"{WhoIs(s)} was hit while surrendering - fights in desperation"); }
        }
    
        /// 0.19: drives a pain/grimace face shape (if the head mesh has one) from pain and fear.
        void LateUpdateFaces()
        {
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active || s.faceSmr == null || s.faceShape < 0) continue;
                float w = s.creature.isKilled ? 0f : Mathf.Clamp01(s.pain * s.painSensitivity + TagIntensity(s, Tag.Fear) * 0.15f) * 100f;
                s.faceSmr.SetBlendShapeWeight(s.faceShape, Mathf.Lerp(s.faceSmr.GetBlendShapeWeight(s.faceShape), w, 0.1f));
            }
        }
    }
}

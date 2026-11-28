// Rage Engine 0.50
//   LIGHTNING    a real strike leaves a Lichtenberg scar (the branching "lightning flower") where it went in, a track of
//                them down the body, and a burn where it went out (a foot).
//   FIRE         burning NPCs try to put themselves out - patting, staggering, dropping to roll. Most manage it; slow,
//                weak, drunk or badly hurt ones don't. Burns leave char on the skin. Burned long enough (or charred by the
//                game's fire) the body collapses into a pile of ash.
//   GRAVITY      fling a man hard enough and the landing does it: bones break where he hit, blood on the wall/floor,
//                and fast enough he bursts. Floating NPCs flail and scream.
//   BRAIN        head hits add up. A damaged brain is slower and dumber: pauses, forgets to attack, stumbles, confused face.
//   CONCUSSION   one huge head blow: a minute or two of drunk, dazed fighting.
//   THROAT PUNCH a solid hit to the throat: head snaps, both hands to the throat, gagging, can't fight for a few seconds.
//   TEETH        a real crack sound and more blood from the mouth.
//   WAVE MUSIC   Rage Engine waves get battle music (CC0 tracks; volume in the Rage tab).
//   SUPER WAVES  "Rage - Super Soldiers" waves: everyone spawns already injected with a super serum.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ================================================================== settings
        [ModOptionCategory("Gore", 1), ModOptionOrder(20)]
        [ModOption("Lightning scars", "Lightning leaves the branching scar where it went in and a burn where it went out.", defaultValueIndex = 1)]
        public static bool lightningScars50 = true;

        [ModOptionCategory("Gore", 1), ModOptionOrder(21)]
        [ModOption("Burn to ash", "Bodies burned long enough collapse into a pile of ash.", defaultValueIndex = 1)]
        public static bool burnToAsh50 = true;

        [ModOptionCategory("NPCs", 2), ModOptionOrder(30)]
        [ModOption("Put out fires", "Burning NPCs try to put themselves out. Most manage it; slow, weak or badly hurt ones don't.", defaultValueIndex = 1)]
        public static bool putOutFire50 = true;

        [ModOptionCategory("NPCs", 2), ModOptionOrder(31)]
        [ModOption("Brain damage & concussions", "Head hits add up: a damaged brain fights slower and dumber. One huge blow = a concussion (drunk, dazed).", defaultValueIndex = 1)]
        public static bool brain50 = true;

        public static ModOptionString[] musicVols50 = { new ModOptionString("Off", null, "0"), new ModOptionString("Quiet", null, "0.25"), new ModOptionString("Normal", null, "0.45"), new ModOptionString("Loud", null, "0.7") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(40)]
        [ModOption("Wave music", "Battle music while a Rage Engine wave is running.", valueSourceName = nameof(musicVols50), defaultValueIndex = 2)]
        public static string waveMusicOpt50 = "0.45";
        static float waveMusic50 => float.TryParse(waveMusicOpt50, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0.45f;

        // ================================================================== Realism style: everything on, real pain
        internal static bool RealismOn => style == "realism";
        static void Realism50()
        {
            desperation = "3"; injuryEffects = "3"; rageMode = "all"; goreSource = "rage";
            ownBlood = bonesOutOpt = faceCaveOpt = knockouts = bruiseMarks = modSfx = modVoices = true;
            ripOrgans49 = punchThrough49 = taunts49 = fearVoices49 = true;
            lightningScars50 = burnToAsh50 = putOutFire50 = brain50 = true;
            Guard("realism.ui", () =>
            {
                SetOptionUi44("Desperation", "3"); SetOptionUi44("Injury effects", "3"); SetOptionUi44("Mode", "all"); SetOptionUi44("Gore & blood from", "rage");
                foreach (var n in new[] { "Mod blood spray", "Bones through skin", "Face cave-in & skull bits", "Knockouts & passing out", "Bruise marks", "Mod sound effects", "Mod voices",
                                          "Rip organs out by hand", "Punch through chest", "Taunts", "Fear & surrender voices", "Lightning scars", "Burn to ash", "Put out fires", "Brain damage & concussions" })
                    SetOptionUi44(n, true);
            });
            Debug.Log("[RageEngine] Realism style: every setting on, injury effects heavy, desperation intense, real pain");
        }

        // ================================================================== hits (from Rage42Hit)
        void Hit50(NpcState s, RagdollPart part, CollisionInstance hit, float severity)
        {
            var c = s.creature;
            if (c.isKilled || part == null) return;
            var t = part.type; var dt = hit.damageStruct.damageType; float now = Time.time;
            if (dt == DamageType.Energy) { s.energyAt50 = now; s.energyPt50 = hit.contactPoint; s.energyPart50 = part; s.energyBig50 = severity >= 0.5f; }
            if (RealismOn && severity >= 0.3f)                                                    // real pain: every solid hit really hurts
            {
                s.pain = Mathf.Min(1f, s.pain + 0.12f * Mathf.Clamp(severity, 0.3f, 2.5f));
                if (severity >= 1.2f) AddTag(s, Tag.Agony, 3f, 0.7f);
            }
            bool head = Has(t, RagdollPart.Type.Head);
            if (brain50 && head)
            {
                float add = dt == DamageType.Blunt ? 0.08f * Mathf.Clamp(severity, 0.2f, 3f) : dt == DamageType.Pierce ? 0.3f * Mathf.Clamp(severity, 0.5f, 2f) : 0.03f * severity;
                add /= Mathf.Max(0.7f, Tough33(s));
                s.brain50 = Mathf.Min(1f, s.brain50 + add);
                if (dt == DamageType.Blunt && severity >= 1.4f && now >= s.concussUntil50)
                {
                    s.concussUntil50 = now + Random.Range(60f, 120f);
                    s.drunk = Mathf.Max(s.drunk, 1.0f); s.daze = Mathf.Max(s.daze, 1.2f);
                    SetMelee(s, false, now + 2.5f);
                    Speak(s, "pain");
                    Log($"{WhoIs(s)}: CONCUSSED ({severity:0.0})");
                }
            }
            Guard("jaw51", () => Jaw51(s, part, hit, severity));
            if (severity >= 0.6f && !Has(t, RagdollPart.Type.Head)) { s.wounds51++; s.lastWoundPart51 = part; }
            if (throatPunch51 && Has(t, RagdollPart.Type.Neck) && dt == DamageType.Blunt && severity >= 0.45f && now >= s.throatPunchAt50 + 4f && s.throatCutUntil <= 0f)
                ThroatPunch50(s, part, hit, severity);
        }

        void ThroatPunch50(NpcState s, RagdollPart neck, CollisionInstance hit, float severity)
        {
            var c = s.creature; float now = Time.time;
            s.throatPunchAt50 = now;
            Vector3 dir = hit.impactVelocity.sqrMagnitude > 0.01f ? hit.impactVelocity.normalized : -c.transform.forward;
            ShoveBodyQuiet(s, Vector3.ProjectOnPlane(dir, Vector3.up).normalized, Mathf.Clamp(0.6f + severity * 0.4f, 0.6f, 1.6f));
            var head = c.ragdoll.headPart;
            if (head != null && head.physicBody != null) head.physicBody.velocity += (dir + Vector3.up * 0.4f).normalized * Mathf.Clamp(2.2f * severity, 1.2f, 4.5f);   // the head snaps back
            SetMelee(s, false, now + Random.Range(2.5f, 4.5f));
            StartClutch(s, neck, hit.contactPoint, null, false, severity >= 0.9f || Random.value < 0.4f);
            if (s.clutching) s.clutchUntil = now + Random.Range(2.5f, 4f);
            Speak(s, "choke");
            s.fatigue = Mathf.Max(s.fatigue, 0.9f);
            s.pain = Mathf.Min(1f, s.pain + 0.5f);
            if (severity >= 1.1f && !ExternalGore) { var m = Mouth(c); if (m != null) Guard("spit", () => Spray28(m.position, c.transform.forward + Vector3.down * 0.3f, new Color(0.5f, 0.03f, 0.03f), 12, 1.2f)); }
            Sfx.Play("NRE_Punch", neck.transform, 1f, 0.8f);
            Log($"{WhoIs(s)}: THROAT PUNCH - gagging");
        }

        /// Teeth knocked out (Gore25/Gore26): the crack and more blood.
        internal void TeethFx50(NpcState s, Transform mouth, int n)
        {
            var c = s.creature; if (mouth == null) return;
            Sfx.Play("NRE_ToothCrack", mouth, 1f, Random.Range(0.95f, 1.1f));
            if (ExternalGore) return;
            Guard("toothblood", () => Spray28(mouth.position, c.transform.forward + Vector3.down * 0.35f, new Color(0.55f, 0.03f, 0.03f), 10 + n * 6, 1.6f));
            for (int i = 0; i < Mathf.Min(3, 1 + n / 2); i++) Guard("blood", () => RageEngineScript.Blood(mouth));
            Stamp(c, mouth.position - Vector3.up * 0.04f, 0.045f, "blood_drip", 0.95f);
            Stamp(c, mouth.position - Vector3.up * 0.09f, 0.06f, "blood_drip", 0.85f);
            s.bleedRate += 0.05f * n * bleedingRate;
        }

        // ================================================================== per NPC (from Tick43, every ~0.1 s)
        void Tick50(NpcState s, float now)
        {
            var c = s.creature;
            Guard("fire50", () => Fire50(s, now));
            if (slams51) Guard("fling50", () => Fling50(s, now));
            if (c.isKilled) return;
            // ---- lightning: the moment the game's electrocution starts
            bool zapped = s.shocked || StatusNamed53(c, "Electrocute") != null || now - s.energyAt50 < 0.3f && s.energyBig50;      // 0.53: status by name + big energy hits
            if (zapped && !s.shockScarred50) { s.shockScarred50 = true; s.shockEnd50 = now; Guard("scar", () => LightningScar50(s)); }
            if (zapped) s.shockEnd50 = now;
            if (!zapped && s.shockScarred50 && now - s.shockEnd50 > 4f) s.shockScarred50 = false;
            // ---- brain damage / concussion: a dumber, slower fighter
            if (brain50 && (s.brain50 > 0.12f || now < s.concussUntil50))
            {
                bool conc = now < s.concussUntil50;
                float floor = Mathf.Max(s.brain50 * 0.6f, conc ? 0.55f : 0f);
                if (s.drunk < floor) s.drunk = floor;
                if (now >= s.nextDumb50)
                {
                    s.nextDumb50 = now + Random.Range(2.5f, 5f);
                    float k = Mathf.Max(s.brain50, conc ? 0.5f : 0f);
                    if (Random.value < k * 0.6f && c.ragdoll.state == Ragdoll.State.Standing && !s.impaled49)
                    {
                        SetMelee(s, false, now + Random.Range(1.2f, 2.8f) * (0.6f + k));
                        Guard("face", () => c.brain.instance?.GetModule<BrainModuleExpression>(false)?.SetFaceExpression(FaceAnimator.Expression.Confusion, 0, null));
                        if (Random.value < 0.5f) ShoveBodyQuiet(s, Vector3.ProjectOnPlane(Random.onUnitSphere, Vector3.up).normalized, 0.35f);
                        if (Random.value < 0.3f) Speak(s, "pain");
                    }
                }
            }
        }

        // ================================================================== lightning scars
        void LightningScar50(NpcState s)
        {
            if (!lightningScars50 || ExternalGore) return;
            var c = s.creature;
            RagdollPart entryPart = Time.time - s.energyAt50 < 1.5f && s.energyPart50 != null ? s.energyPart50 : (Random.value < 0.5f ? c.ragdoll.headPart : ChestPart(c));
            Vector3 entry = Time.time - s.energyAt50 < 1.5f ? s.energyPt50 : (entryPart != null ? entryPart.transform.position : c.transform.position + Vector3.up * 1.5f);
            Side legSide = Random.value < 0.5f ? Side.Left : Side.Right;
            var foot = PartOf(c, legSide == Side.Left ? RagdollPart.Type.LeftFoot : RagdollPart.Type.RightFoot);
            var leg = PartOf(c, legSide == Side.Left ? RagdollPart.Type.LeftLeg : RagdollPart.Type.RightLeg);
            var chest = ChestPart(c);
            var path = new List<Vector3> { entry };
            if (chest != null) path.Add(chest.transform.position + c.transform.forward * 0.1f + c.transform.right * (legSide == Side.Left ? -0.06f : 0.06f));
            var hips = PartOf(c, RagdollPart.Type.Torso);
            if (hips != null && hips != chest) path.Add(hips.transform.position + c.transform.forward * 0.1f);
            if (leg != null) path.Add(leg.transform.position + c.transform.forward * 0.06f);
            Vector3 exit = foot != null ? foot.transform.position : c.transform.position;
            Stamp(c, entry, 0.16f, "lichtenberg", 1f);
            for (int i = 1; i < path.Count; i++)
            {
                Stamp(c, path[i], Random.Range(0.11f, 0.15f), "lichtenberg", 0.95f);
                Stamp(c, Vector3.Lerp(path[i - 1], path[i], 0.5f), 0.1f, "lichtenberg", 0.9f);
            }
            Stamp(c, exit, 0.08f, "char", 0.95f);
            Stamp(c, entry, 0.05f, "char", 0.9f);
            Log($"{WhoIs(s)}: lightning scar ({entryPart?.type} -> {legSide} foot)");
        }

        // ================================================================== fire
        static Burning BurningOf(Creature c)
        {
            var list = c.Statuses;
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++) if (list[i] is Burning b) return b;
            return null;
        }

        void Fire50(NpcState s, float now)
        {
            var c = s.creature;
            object b = StatusNamed53(c, "Burning");                                   // 0.53: by type name - works on this game version
            bool lit = b != null && Get53(b, "isIgnited", true);
            if (lit)
            {
                if (s.fireAt50 <= 0f) s.fireAt50 = now;
                s.burnTime50 += 0.1f;
                if (!ExternalGore && now >= s.nextChar50 && s.charMarks50 < 16)
                {
                    s.nextChar50 = now + Random.Range(0.6f, 1.3f);
                    var parts = c.ragdoll.parts; var rp = parts[Random.Range(0, parts.Count)];
                    if (rp != null) { Stamp(c, rp.transform.position + Random.insideUnitSphere * 0.05f, Random.Range(0.09f, 0.16f), "char", 0.95f); s.charMarks50++; }
                }
            }
            if (c.isKilled)
            {
                if (burnToAsh50 && !ExternalGore && !s.ash50)
                {
                    float charred = Get53(b, "CharAmount", 0f);
                    if (charred >= 0.97f || s.burnTime50 >= 14f) { s.ash50 = true; Guard("ash", () => Ash50(c)); }
                }
                return;
            }
            if (!lit)
            {
                if (s.fireAt50 > 0f) { s.fireAt50 = 0f; s.fireDecided50 = false; if (s.despPose43 == 6) s.despUntil43 = now; }
                return;
            }
            if (!putOutFire50 || s.knockedOut || s.passedOut || s.impaled49) return;
            // every few seconds he gets another go at it: quick, strong, healthy men usually manage; the weak, drunk,
            // blinded and badly hurt often don't - and the fire keeps sticking to them
            if (!s.fireDecided50 || now >= s.fireOutAt50 + 0.5f && !s.fireWins50)
            {
                s.fireDecided50 = true;
                float chance = 0.5f + 0.3f * HpFrac(c) + (s.will44 ? 0.1f : 0f) + (Tank(s) ? 0.1f : 0f) - Mathf.Min(0.4f, s.drunk * 0.3f) - (s.blind42 ? 0.2f : 0f) - Mathf.Min(0.3f, s.brain50 * 0.4f);
                s.fireWins50 = Random.value < Mathf.Clamp(chance, 0.15f, 0.9f);
                s.fireOutAt50 = now + Random.Range(2f, 4f) / Mathf.Max(0.5f, HpFrac(c) + 0.3f);
                s.fireRoll50 = s.fireWins50 && Random.value < 0.3f;
                FearVoice49(s, true);
                Log($"{WhoIs(s)}: ON FIRE - {(s.fireWins50 ? (s.fireRoll50 ? "drops and rolls" : "beats it out") : "can't get it out")}");
            }
            if (s.fireRoll50)
            {
                if (c.ragdoll.state == Ragdoll.State.Standing) { AllowFall(s, 3f); Guard("drop", () => c.ragdoll.SetState(Ragdoll.State.Destabilized)); }
                var ch = ChestPart(c); if (ch != null && ch.physicBody != null) ch.physicBody.angularVelocity += c.transform.forward * 0.6f;
            }
            else if (c.ragdoll.state == Ragdoll.State.Standing)
            {
                if (s.despUntil43 < now + 0.2f) Gesture43(s, 6, 0.8f);                      // beating at the flames
                if (Random.value < 0.35f) ShoveBodyQuiet(s, Vector3.ProjectOnPlane(Random.onUnitSphere, Vector3.up).normalized, 0.6f);   // moving, staggering about
                if (Random.value < 0.1f) SetMelee(s, false, now + 0.6f);
            }
            if (now >= s.nextFearVoice49) FearVoice49(s);
            if (s.fireWins50 && now >= s.fireOutAt50)
            {
                Guard("putout", () => ClearStatus53(c, b));
                s.fireAt50 = 0f; s.fireDecided50 = false;
                if (s.despPose43 == 6) s.despUntil43 = now;
                Log($"{WhoIs(s)}: put the fire out");
            }
        }

        static Material ashMat50, ashDark50;
        static void Ash50(Creature c)
        {
            if (ashMat50 == null) { ashMat50 = Mats.Make(new Color(0.32f, 0.31f, 0.3f), 0.05f); ashDark50 = Mats.Make(new Color(0.1f, 0.09f, 0.085f), 0.05f); }
            var parts = c.ragdoll.parts;
            Vector3 floor = c.transform.position;
            if (Physics.Raycast(c.ragdoll.rootPart.transform.position + Vector3.up * 0.5f, Vector3.down, out var gh, 4f, ~0, QueryTriggerInteraction.Ignore)) floor = gh.point;
            foreach (var rp in parts)
            {
                if (rp == null) continue;
                int n = rp.type == RagdollPart.Type.Torso || rp.type == RagdollPart.Type.Head ? 7 : 3;
                for (int i = 0; i < n; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "RageEngine_Ash";
                    go.transform.position = rp.transform.position + Random.insideUnitSphere * 0.06f;
                    go.transform.rotation = Random.rotation;
                    go.transform.localScale = Vector3.one * Random.Range(0.03f, 0.07f);
                    go.GetComponent<Renderer>().sharedMaterial = Random.value < 0.6f ? ashMat50 : ashDark50;
                    var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.05f; rb.drag = 2.5f; rb.velocity = Random.insideUnitSphere * 0.4f;
                    Object.Destroy(go, Random.Range(18f, 30f));
                }
            }
            var pile = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(pile.GetComponent<Collider>());
            pile.name = "RageEngine_AshPile"; pile.transform.position = floor + Vector3.up * 0.03f;
            pile.transform.localScale = new Vector3(0.9f, 0.08f, 1.5f); pile.transform.rotation = Quaternion.Euler(0f, c.transform.eulerAngles.y, 0f);
            pile.GetComponent<Renderer>().sharedMaterial = ashMat50;
            Object.Destroy(pile, 90f);
            Sfx.Play("NRE_BodyFall", c.ragdoll.rootPart.transform, 0.6f, 0.7f);
            Guard("ash.despawn", () => c.Despawn(0f));
            Debug.Log($"[RageEngine] {c.name}: BURNED TO ASH");
        }

        // ================================================================== gravity: flung bodies and the landing
        void Fling50(NpcState s, float now)
        {
            var c = s.creature;
            var root = c.ragdoll.rootPart;
            if (root == null || root.physicBody == null) return;
            if (c.ragdoll.handlers != null && c.ragdoll.handlers.Count > 0) { s.flingPeak50 = 0f; return; }
            float v = root.physicBody.velocity.magnitude;
            if (v > 8f) { s.flingPeak50 = Mathf.Max(s.flingPeak50, v); s.flingAt50 = now; s.flingDir50 = root.physicBody.velocity / v; }
            else if (s.flingPeak50 > 0f)
            {
                if (now - s.flingAt50 > 0.25f) { s.flingPeak50 = 0f; return; }
                if (v < s.flingPeak50 * 0.4f)                                           // stopped dead: he hit something
                {
                    float peak = s.flingPeak50; s.flingPeak50 = 0f;
                    Splat50(s, peak, s.flingDir50);
                }
            }
            // floating: flail and scream
            if (!c.isKilled && now >= s.nextFloat50)
            {
                s.nextFloat50 = now + 0.5f;
                bool floating = false;
                try { var l = c.Statuses; if (l != null) for (int i = 0; i < l.Count; i++) if (l[i] != null && l[i].GetType().Name == "Floating") { floating = true; break; } } catch { }
                if (floating)
                {
                    foreach (var rp in c.ragdoll.parts)
                        if (rp != null && rp.physicBody != null && Has(rp.type, ArmParts | LegParts)) rp.physicBody.velocity += Random.insideUnitSphere * 1.6f;
                    if (Random.value < 0.35f) FearVoice49(s);
                }
            }
        }

        void Splat50(NpcState s, float speed, Vector3 dir)
        {
            var c = s.creature;
            var root = c.ragdoll.rootPart;
            Vector3 at = root.transform.position;
            Vector3 surf = at + dir * 0.3f; Vector3 n = -dir;
            if (Physics.Raycast(at - dir * 0.2f, dir, out var hh, 1.5f, ~0, QueryTriggerInteraction.Ignore)) { surf = hh.point; n = hh.normal; }
            Sfx.Play("NRE_BodyFall", root.transform, 1f, 0.85f);
            Sfx.Play("NRE_BoneCrunch", root.transform, Mathf.Clamp01(speed / 14f));
            if (!ExternalGore)
            {
                Guard("wall", () => GroundBlood(surf + n * 0.3f, "blood_splatter", Mathf.Clamp(speed / 12f, 0.5f, 1.4f), c));
                for (int i = 0; i < Mathf.Clamp((int)(speed / 4f), 1, 5); i++) Guard("blood", () => RageEngineScript.Blood(root.transform));
            }
            // break what hit first: the parts closest to the surface
            int breaks = speed >= 16f ? 4 : speed >= 12f ? 2 : speed >= 10f ? 1 : 0;
            var list = new List<RagdollPart>(c.ragdoll.parts);
            list.Sort((a, b) => (a.transform.position - surf).sqrMagnitude.CompareTo((b.transform.position - surf).sqrMagnitude));
            foreach (var rp in list)
            {
                if (breaks <= 0) break;
                if (rp == null || !Has(rp.type, ArmParts | LegParts)) continue;
                var p = rp; Guard("fracture", () => Fracture(s, p, p.transform.position, 2f, speed >= 14f)); breaks--;
            }
            if (!c.isKilled)
            {
                if (speed >= 18f) { s.finalKill = true; s.dying = false; Guard("kill", () => c.Kill()); }
                else Guard("dmg", () => c.Damage(speed * 4f));
            }
            if (speed >= 18f && !ExternalGore) { Guard("burst", () => SpawnChunks(at, n, 10, 4f, 1.1f, c)); Log($"{c.name}: SPLATTERED ({speed:0.0} m/s)"); }
            else Log($"{c.name}: slammed at {speed:0.0} m/s");
            AddTag(s, Tag.Agony, 4f, 0.8f);
        }

        // ================================================================== wave music + super soldier waves
        static readonly List<AudioClip> waveClips50 = new List<AudioClip>();
        static AudioSource waveSrc50;
        static float waveVol50, nextWaveCheck50;
        static bool waveOn50;
        static readonly HashSet<Creature> superDone50 = new HashSet<Creature>();

        void TickWaveMusic50()
        {
            float now = Time.unscaledTime;
            if (now >= nextWaveCheck50)
            {
                nextWaveCheck50 = now + 0.5f;
                bool running = false, super = false;
                var list = WaveSpawner.instances;
                if (list != null)
                    foreach (var ws in list)
                    {
                        if (ws == null || !ws.isRunning || ws.currentLoadedData == null) continue;
                        string id = ws.currentLoadedData.id ?? "";
                        if (!id.StartsWith("Rage_")) continue;
                        running = true;
                        if (id.StartsWith("Rage_Super") && ws.spawnedCreatures != null)
                        {
                            super = true;
                            foreach (var cr in ws.spawnedCreatures)
                                if (cr != null && !cr.isKilled && superDone50.Add(cr))
                                {
                                    int comp = RandomSuperSerum(); var cc = cr;
                                    pendingSoldiers.Add(new PendingSoldier { c = cc, at = Time.time + 1.0f, comp = comp });
                                    Debug.Log($"[RageEngine] super wave: {cr.name} gets {CompName[comp]}");
                                }
                        }
                    }
                if (!super && superDone50.Count > 0) superDone50.RemoveWhere(x => x == null || x.isKilled);
                if (running && !waveOn50 && waveMusic50 > 0f)
                {
                    RequestAudio28();
                    if (waveClips50.Count > 0)
                    {
                        if (waveSrc50 == null)
                        {
                            var go = new GameObject("RageEngine_WaveMusic"); Object.DontDestroyOnLoad(go);
                            waveSrc50 = go.AddComponent<AudioSource>(); waveSrc50.loop = true; waveSrc50.spatialBlend = 0f; waveSrc50.playOnAwake = false;
                        }
                        waveSrc50.clip = waveClips50[Random.Range(0, waveClips50.Count)];
                        waveSrc50.volume = 0f; waveVol50 = 0f; waveSrc50.Play(); waveOn50 = true;
                        Log($"wave music: {waveSrc50.clip.name}");
                    }
                }
                else if (!running && waveOn50) waveOn50 = false;
            }
            if (waveSrc50 != null && waveSrc50.isPlaying)
            {
                float target = waveOn50 ? waveMusic50 : 0f;
                waveVol50 = Mathf.MoveTowards(waveVol50, target, Time.unscaledDeltaTime * (waveOn50 ? 0.25f : 0.15f));
                waveSrc50.volume = waveVol50;
                if (!waveOn50 && waveVol50 <= 0.001f) waveSrc50.Stop();
            }
        }
    }
}

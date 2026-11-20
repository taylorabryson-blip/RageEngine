// Nomad Rage Engine 0.22 - PRESSURE POINTS: critical injury zones + one functional body state.
//
// Two layers:
//  1) FUNCTIONAL STATE (always on): every injury system (broken bones, cut muscles, dead arms, ribs, pain, zones)
//     feeds ONE picture of what the body can still do - and the body does only that:
//       one leg out           -> knee buckles for good, hobbles at 40% speed, can't run
//       both legs out         -> goes down and can't get up
//       a leg + an arm out    -> the body gives up: sinks to the ground, fights from there only if you come close
//       an arm out            -> dead arm, drops what's in it (cradles it)
//       both arms out         -> drops everything, stops fighting, backs away
//  2) PRESSURE POINTS ZONES (optional, Mod Options > Rage Engine: Pressure Points):
//       upper torso, lower torso, abdomen, neck, each shoulder, each arm, each leg build up damage per hit
//       (repeated hits to a tired zone count more - progressive fatigue). Stages: impaired -> disabled -> critical.
//       Severity sets the thresholds (Mild / Severe / Extreme). Persistence: Temporary (heals), Encounter, or
//       Persistent (named NPCs come back still carrying it).
//     Critical condition: the NPC knows it - stops fighting, tries to get away, gasps and wheezes.
//     Smooth transitions: going down sinks through the knees over ~0.7 s instead of snapping into ragdoll.
//     Weapon handling: hurt shoulders/arms make the weapon sag and sometimes slip out of the hand.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal enum PZ { UpperTorso, LowerTorso, Abdomen, Neck, LeftShoulder, RightShoulder, LeftArm, RightArm, LeftLeg, RightLeg, Count }

    public sealed partial class RageEngineScript
    {
        const string CP = "Rage Engine: Pressure Points";
        public static ModOptionString[] ppSeverityValues =
        {
            new ModOptionString("Mild - gradual impairment", null, "Mild"),
            new ModOptionString("Severe - major impairment", null, "Severe"),
            new ModOptionString("Extreme - critical consequences", null, "Extreme"),
        };
        public static ModOptionString[] ppPersistenceValues =
        {
            new ModOptionString("Temporary - heals over time", null, "Temporary"),
            new ModOptionString("Entire encounter", null, "Encounter"),
            new ModOptionString("Persistent - named NPCs keep it", null, "Persistent"),
        };

        public static bool ppEnabled = true;

        public static string ppSeverity = "Severe";

        public static string ppPersistence = "Encounter";

        public static bool ppSmooth = true;

        static readonly object PPHandler = new object();
        static readonly string[] ZoneName = { "upper torso", "lower torso", "abdomen", "neck", "left shoulder", "right shoulder", "left arm", "right arm", "left leg", "right leg" };

        static int SevIdx => ppSeverity == "Mild" ? 0 : ppSeverity == "Extreme" ? 2 : 1;
        static float ImpairAt => SevIdx == 0 ? 0.9f : SevIdx == 1 ? 0.7f : 0.5f;
        static float DisableAt => SevIdx == 0 ? 99f : SevIdx == 1 ? 1.6f : 1.1f;
        static float CriticalAt => SevIdx == 0 ? 99f : SevIdx == 1 ? 2.6f : 1.8f;

        static PZ ZoneOf(Creature c, RagdollPart part, Vector3 point)
        {
            var t = part.type;
            if (Has(t, RagdollPart.Type.Head | RagdollPart.Type.Neck)) return PZ.Neck;
            if (Has(t, LeftLegParts)) return PZ.LeftLeg;
            if (Has(t, RightLegParts)) return PZ.RightLeg;
            var chest = ChestPart(c);
            if (Has(t, LeftArmParts | RightArmParts))
            {
                bool left = Has(t, LeftArmParts);
                bool shoulder = chest != null && t != RagdollPart.Type.LeftHand && t != RagdollPart.Type.RightHand && Vector3.Distance(point, chest.transform.position) < 0.3f;
                return shoulder ? (left ? PZ.LeftShoulder : PZ.RightShoulder) : (left ? PZ.LeftArm : PZ.RightArm);
            }
            // torso: high = upper torso; low front = abdomen; low back/sides = lower torso
            var root = c.ragdoll.rootPart;
            float lo = root != null ? root.transform.position.y : c.transform.position.y + 0.95f;
            float hi = chest != null ? chest.transform.position.y + 0.15f : lo + 0.5f;
            float h = Mathf.InverseLerp(lo, hi, point.y);
            if (h > 0.55f) return PZ.UpperTorso;
            Vector3 centre = root != null ? root.transform.position : c.transform.position;
            return Vector3.Dot(Vector3.ProjectOnPlane(point - centre, Vector3.up), c.transform.forward) > 0.02f ? PZ.Abdomen : PZ.LowerTorso;
        }

        /// Every hit: add damage to the zone it landed in (progressive fatigue: a tired zone takes more).
        void PressureHit(NpcState s, RagdollPart part, Vector3 point, DamageType type, float severity)
        {
            if (!ppEnabled || s.creature.isKilled) return;
            var z = ZoneOf(s.creature, part, point);
            int i = (int)z;
            float now = Time.time;
            float typeMul = type == DamageType.Pierce ? 1.15f : type == DamageType.Slash ? 1f : 0.7f;
            float fatigue = 1f + Mathf.Clamp01(s.ppFatigue[i]) * 0.8f;                        // repeated damage to the same place
            float add = Mathf.Clamp(severity, 0f, 3f) * typeMul * fatigue * 0.55f / Mathf.Max(0.6f, s.toughness);
            if (add < 0.03f) return;
            s.ppZone[i] += add;
            s.ppFatigue[i] = Mathf.Min(1.5f, s.ppFatigue[i] + 0.35f);
            s.ppLastHit[i] = now;
            int st = StageOf(s.ppZone[i], z);
            if (st > s.ppStage[i])
            {
                Log($"{WhoIs(s)}: PRESSURE POINT {ZoneName[i]} -> {(st == 3 ? "CRITICAL" : st == 2 ? "DISABLED" : "impaired")} ({s.ppZone[i]:0.00})");
                s.ppStage[i] = st;
                s.ppDirty = true;
                if (st >= 2) Guard("scream", () => Scream(s, st >= 3));
            }
        }

        static int StageOf(float v, PZ z)
        {
            bool vital = z == PZ.Neck || z == PZ.Abdomen || z == PZ.UpperTorso;
            if (vital && v >= CriticalAt) return 3;
            if (v >= DisableAt) return 2;
            if (v >= ImpairAt) return 1;
            return 0;
        }

        /// 0.1 s tick: merge every injury system into one functional state and make the body obey it.
        void TickPressure(NpcState s, float now)
        {
            var c = s.creature;
            if (c.isKilled || s.knockedOut || s.shocked) return;
            float dt = s.ppLastTick > 0f ? Mathf.Min(0.5f, now - s.ppLastTick) : 0.1f;
            s.ppLastTick = now;

            // ---- zones recover (temporary) / fatigue fades
            for (int i = 0; i < (int)PZ.Count; i++)
            {
                s.ppFatigue[i] = Mathf.Max(0f, s.ppFatigue[i] - dt * 0.08f);
                if (ppPersistence == "Temporary" && now - s.ppLastHit[i] > 8f && s.ppZone[i] > 0f)
                {
                    s.ppZone[i] = Mathf.Max(0f, s.ppZone[i] - dt * 0.03f);
                    int st = StageOf(s.ppZone[i], (PZ)i);
                    if (st < s.ppStage[i] && s.ppZone[i] < (st + 1 == 1 ? ImpairAt : st + 1 == 2 ? DisableAt : CriticalAt) * 0.85f)
                    { s.ppStage[i] = st; s.ppDirty = true; Log($"{WhoIs(s)}: {ZoneName[i]} recovering"); }
                }
            }

            // ---- one functional picture from every system
            bool lArmOut = Dead(s, Side.Left) || Has(s.brokenParts, LeftArmParts) || Stage(s, PZ.LeftArm) >= 2 || Stage(s, PZ.LeftShoulder) >= 2;
            bool rArmOut = Dead(s, Side.Right) || Has(s.brokenParts, RightArmParts) || Stage(s, PZ.RightArm) >= 2 || Stage(s, PZ.RightShoulder) >= 2;
            bool lLegOut = Has(s.brokenParts, LeftLegParts) || Has(s.limpParts, LeftLegParts) || Stage(s, PZ.LeftLeg) >= 2 || LegStrength(s, Side.Left) < 0.25f;
            bool rLegOut = Has(s.brokenParts, RightLegParts) || Has(s.limpParts, RightLegParts) || Stage(s, PZ.RightLeg) >= 2 || LegStrength(s, Side.Right) < 0.25f;
            int armsOut = (lArmOut ? 1 : 0) + (rArmOut ? 1 : 0), legsOut = (lLegOut ? 1 : 0) + (rLegOut ? 1 : 0);
            int torso = Mathf.Max(Stage(s, PZ.UpperTorso), Stage(s, PZ.LowerTorso), Stage(s, PZ.Abdomen));
            int disabledZones = 0; for (int i = 0; i < (int)PZ.Count; i++) if (s.ppStage[i] >= 2) disabledZones++;
            bool critical = Stage(s, PZ.Neck) >= 3 || Stage(s, PZ.Abdomen) >= 3 || Stage(s, PZ.UpperTorso) >= 3
                            || (SevIdx == 2 && (Stage(s, PZ.Neck) >= 2 || Stage(s, PZ.Abdomen) >= 2))
                            || disabledZones >= 4 || (armsOut + legsOut >= 3 && !Pumped(s));

            // arms: a disabled arm becomes a dead arm (drops its weapon, hangs) - reuse the existing system
            if (lArmOut && !Dead(s, Side.Left) && Stage(s, PZ.LeftArm) + Stage(s, PZ.LeftShoulder) >= 2) KillArm(s, Side.Left);
            if (rArmOut && !Dead(s, Side.Right) && Stage(s, PZ.RightArm) + Stage(s, PZ.RightShoulder) >= 2) KillArm(s, Side.Right);

            // legs: one out = permanent buckle; both out, or a leg + an arm, or critical = goes down and stays down
            if (lLegOut) { s.buckleLegs |= LeftLegParts; s.buckleUntil = float.MaxValue; }
            if (rLegOut) { s.buckleLegs |= RightLegParts; s.buckleUntil = float.MaxValue; }
            bool goDown = legsOut >= 2 || (legsOut >= 1 && armsOut >= 1 && SevIdx >= 1) || (legsOut >= 1 && torso >= 2)
                          || (Stage(s, PZ.LowerTorso) >= 3) || (critical && SevIdx >= 1);
            if (Pumped(s)) goDown = legsOut >= 2;                                              // adrenaline keeps them up on one leg
            if (goDown && !s.cantStand)
            {
                s.cantStand = true;
                Log($"{WhoIs(s)}: body gives out - legs {legsOut}/2 gone, arms {armsOut}/2 gone, torso {torso} - goes down for good");
                Sink(s, now);
            }

            // speed from the whole picture
            float speed = 1f;
            if (legsOut == 1) speed = Mathf.Min(speed, 0.4f);
            speed = Mathf.Min(speed, Stage(s, PZ.LeftLeg) == 1 || Stage(s, PZ.RightLeg) == 1 ? 0.75f : 1f);
            speed = Mathf.Min(speed, torso >= 2 ? 0.5f : torso == 1 ? 0.78f : 1f);
            if (critical) speed = Mathf.Min(speed, 0.45f);
            if (Mathf.Abs(speed - s.ppSpeed) > 0.01f)
            {
                s.ppSpeed = speed;
                Guard("speed", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(PPHandler); if (speed < 0.99f) c.locomotion.globalMoveSpeedMultiplier.Add(PPHandler, speed); });
            }

            // balance: lower torso / legs cap how steady they can be
            float balCap = Stage(s, PZ.LowerTorso) >= 2 ? 0.4f : Stage(s, PZ.LowerTorso) == 1 || legsOut == 1 ? 0.65f : 1f;
            if (s.balance > balCap) s.balance = balCap;

            // upper torso: arms weaker (can't put power behind swings); neck: head lolls
            if (Stage(s, PZ.UpperTorso) >= 1 || Stage(s, PZ.LeftShoulder) == 1) s.weakArmLUntil = Mathf.Max(s.weakArmLUntil, now + 0.3f);
            if (Stage(s, PZ.UpperTorso) >= 1 || Stage(s, PZ.RightShoulder) == 1) s.weakArmRUntil = Mathf.Max(s.weakArmRUntil, now + 0.3f);
            if (Stage(s, PZ.Neck) >= 1) Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.35f + 0.2f * Stage(s, PZ.Neck), 0.3f);

            // abdomen: doubles over holding it
            if (Stage(s, PZ.Abdomen) >= 1 && !s.clutching && now >= s.ppNextClutch && c.ragdoll.state == Ragdoll.State.Standing)
            {
                s.ppNextClutch = now + Random.Range(6f, 12f);
                var chest = ChestPart(c);
                if (chest != null) Guard("clutch", () => StartClutch(s, chest, chest.transform.position - Vector3.up * 0.25f + c.transform.forward * 0.12f, null, false, false));
                s.coreUntil = Mathf.Max(s.coreUntil, now + 1.2f);
            }

            // weapon handling: hurt shoulders / arms make the weapon sag, and sometimes it slips
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                int imp = Mathf.Max(Stage(s, side == Side.Left ? PZ.LeftShoulder : PZ.RightShoulder), Stage(s, side == Side.Left ? PZ.LeftArm : PZ.RightArm));
                if (imp < 1 || Dead(s, side) || !RealWeapon(c, side)) continue;
                var h = Hand(c, side);
                var it = h != null && h.grabbedHandle != null ? h.grabbedHandle.item : null;
                if (it == null || it.physicBody == null) continue;
                it.physicBody.AddForce(Vector3.down * (4f * imp), ForceMode.Acceleration);          // the weapon droops
                if (now >= s.ppNextFumble)
                {
                    s.ppNextFumble = now + Random.Range(3f, 6f);
                    if (Random.value < 0.12f * imp * (Pumped(s) ? 0.3f : 1f))
                    {
                        Guard("ungrab", () => h.UnGrab(false));
                        Log($"{WhoIs(s)}: {side} arm too hurt - weapon slips out of his hand");
                    }
                }
            }

            // critical condition: he knows it
            if (critical && !s.ppCritical)
            {
                s.ppCritical = true;
                Log($"{WhoIs(s)} is in CRITICAL CONDITION - and knows it");
                Guard("face", () => c.brain.instance.GetModule<BrainModuleExpression>(false)?.SetFaceExpression(FaceAnimator.Expression.Fear, 0, null));
                if (!Pumped(s)) { s.fleeing = true; s.rnasDirty = true; }
            }
            if (s.ppCritical && !Pumped(s)) SetMelee(s, false, now + 0.5f);
            else if ((armsOut >= 1 && legsOut >= 1) || armsOut >= 2)
            {
                // hobbled: only swings if you come right up to him
                var pc = PlayerTarget();
                bool close = pc != null && Vector3.Distance(pc.position, c.transform.position) < 1.3f;
                if (!close) SetMelee(s, false, now + 0.4f);
            }

            // audio: laboured breathing when hurt, wheezing gasps when critical
            int worst = 0; for (int i = 0; i < (int)PZ.Count; i++) worst = Mathf.Max(worst, s.ppStage[i]);
            if (s.ribsBroken > 0) worst = Mathf.Max(worst, 1);
            if ((worst >= 1 || s.ppCritical) && now >= s.ppNextBreath)
            {
                bool crit = s.ppCritical || worst >= 3;
                s.ppNextBreath = now + (crit ? Random.Range(1.4f, 2.4f) : Random.Range(2.6f, 4.2f));
                // 0.25: the game's own recorded voice (muffled pain / choking), not synthesized breathing
                if (crit || Random.value < 0.35f) Speak(s, crit && Random.value < 0.4f ? "choke" : "muffled");
            }
            s.ppDirty = false;
        }

        static int Stage(NpcState s, PZ z) => s.ppStage[(int)z];

        /// Smooth emergency transition: the legs give out over ~0.7 s (knees first), then the ragdoll takes over.
        void Sink(NpcState s, float now)
        {
            var c = s.creature;
            if (c.ragdoll.state != Ragdoll.State.Standing) return;
            if (!ppSmooth) { AllowFallAlways(s, 3f); Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized)); return; }
            s.ppSinkStart = now; s.ppSinkUntil = now + 0.7f;
            Speak(s, "falling");
        }

        void TickSink(NpcState s, float now)
        {
            if (s.ppSinkUntil <= 0f || now < s.ppSinkUntil) return;
            s.ppSinkUntil = 0f;
            var c = s.creature;
            if (c.isKilled || c.ragdoll.state != Ragdoll.State.Standing) return;
            AllowFallAlways(s, 3f);
            Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
            ShoveBodyQuiet(s, -c.transform.forward, 0.3f);
        }

        /// 0..1 how far through the sink we are (Muscles.PartMuscle eases the legs off with it).
        static float SinkT(NpcState s) => s.ppSinkUntil > 0f ? Mathf.Clamp01((Time.time - s.ppSinkStart) / Mathf.Max(0.05f, s.ppSinkUntil - s.ppSinkStart)) : 0f;

        /// Persistent mode: the disabled zones of a named NPC who escaped.
        static int DisabledZoneMask(NpcState s)
        {
            int m = 0; for (int i = 0; i < (int)PZ.Count; i++) if (s.ppStage[i] >= 2) m |= 1 << i;
            return m;
        }

        void ApplyPersistentZones(NpcState s, NpcRecord rec)
        {
            if (!ppEnabled || ppPersistence != "Persistent" || rec == null || rec.ppZones == 0) return;
            for (int i = 0; i < (int)PZ.Count; i++)
                if ((rec.ppZones & (1 << i)) != 0) { s.ppZone[i] = Mathf.Max(s.ppZone[i], ImpairAt); s.ppStage[i] = 1; }
            Log($"{rec.name} still carries old injuries (pressure points {rec.ppZones})");
        }

        static void ClearPressure(NpcState s)
        {
            for (int i = 0; i < (int)PZ.Count; i++) { s.ppZone[i] = 0f; s.ppStage[i] = 0; s.ppFatigue[i] = 0f; s.ppLastHit[i] = -100f; }
            s.ppCritical = false; s.ppDirty = false; s.ppSpeed = 1f; s.ppLastTick = 0f; s.ppNextClutch = s.ppNextFumble = s.ppNextBreath = 0f;
            s.ppSinkStart = s.ppSinkUntil = 0f;
        }
    }
}

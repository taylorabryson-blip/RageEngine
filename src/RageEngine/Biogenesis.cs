// Nomad Rage Engine 0.24 - BIOGENESIS: injectable fictional compounds (ailments) and the X-SERIES super serums.
//
// DEVICES (item book: "Biogenesis" + "Biogenesis X-Series"): modelled syringes / injectors / vials built from parts
//   (glass barrel, plunger + stopper, needle hub, needle, label, liquid). Each device is a real game item underneath
//   (the throwing-dagger's pierce physics = the needle) dressed with our models; its hidden blade sits along the needle.
//   INJECTION: the needle has to actually go in (the game registers a PIERCE hit from that item on an NPC). Then the
//   plunger animates down over ~0.8 s and the liquid drains; the dose delivered = how far the plunger got before the
//   needle came out. The device is then EMPTY (no re-use, no double-trigger). Either hand.
//   REFILL: touch the needle of an EMPTY syringe to a full vial -> it draws that compound (the vial empties).
// AILMENTS (fictional): Neurostatic, Verdant Syndrome, Cryo-Sleep, Adrenal Surge, Hallucinatory Syndrome, Motor
//   Shutdown. They stack where compatible, have duration/intensity settings and wear off (NPC recovery setting).
// X-SERIES: X-01 Night Soldier, X-02 Hero X (+ Super Serum Syndrome outcomes), X-03 Adrenal Overload (+ Last Stand),
//   X-04 Titan, X-05 Phantom, X-06 Phoenix, X-07 Ravager, X-08 Overmind, X-09 Colossus. Visual identity: glowing eyes,
//   a light particle aura, optional slight size increase (through the game's own Creature.SetHeight).
// EVERYTHING reuses the existing systems: toughness, pain sensitivity, balance, drunk (coordination), knockout,
//   defense tuning, muscles (strength), the game's damage multipliers (durability) and Creature.Heal (regeneration).
//   Modifiers are applied as reversible multipliers, so nothing is permanently overwritten.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal enum Comp
    {
        Neurostatic, Verdant, CryoSleep, AdrenalSurge, Hallucinatory, MotorShutdown,
        NightSoldier, HeroX, AdrenalOverload, Titan, Phantom, Phoenix, Ravager, Overmind, Colossus, Count
    }

    public sealed partial class RageEngineScript
    {
        const string CB = "Rage Engine: Serums", CX = "Rage Engine: Super Serums";
        internal static readonly string[] CompKey = { "Neurostatic", "Verdant", "CryoSleep", "AdrenalSurge", "Hallucinatory", "MotorShutdown",
            "X01_NightSoldier", "X02_HeroX", "X03_AdrenalOverload", "X04_Titan", "X05_Phantom", "X06_Phoenix", "X07_Ravager", "X08_Overmind", "X09_Colossus" };
        internal static readonly string[] CompName = { "Neurostatic", "Verdant Syndrome", "Cryo-Sleep", "Adrenal Surge", "Hallucinatory Syndrome", "Motor Shutdown",
            "Night Soldier", "Hero X", "Adrenal Overload", "Titan", "Phantom", "Phoenix", "Ravager", "Overmind", "Colossus" };
        internal static readonly Color[] CompLiquid =
        {
            new Color(0.2f, 0.9f, 1f), new Color(0.35f, 0.8f, 0.15f), new Color(0.6f, 0.85f, 1f), new Color(1f, 0.45f, 0.05f), new Color(0.95f, 0.2f, 0.85f), new Color(0.75f, 0.75f, 0.78f),
            new Color(0.55f, 0.02f, 0.04f), new Color(1f, 0.8f, 0.2f), new Color(1f, 0.35f, 0.05f), new Color(1f, 0.85f, 0.1f), new Color(0.35f, 0.65f, 1f),
            new Color(0.2f, 1f, 0.35f), new Color(0.75f, 0f, 0.12f), new Color(0.6f, 0.2f, 1f), new Color(0.75f, 0.9f, 1f),
        };
        // X-series identity: eyes / aura (index = comp - NightSoldier)
        static readonly Color[] XEyes = { new Color(1f, 0.05f, 0.05f), Color.white, new Color(1f, 0.5f, 0f), new Color(1f, 0.95f, 0.1f), new Color(0.2f, 0.5f, 1f), new Color(0.1f, 1f, 0.2f), new Color(0.85f, 0f, 0.15f), new Color(0.65f, 0.1f, 1f), new Color(0.8f, 0.95f, 1f) };
        static readonly Color[] XAura = { new Color(0.45f, 0f, 0.02f), new Color(1f, 0.78f, 0.2f), new Color(1f, 0.3f, 0.05f), new Color(1f, 0.55f, 0.05f), new Color(0.55f, 0.8f, 1f), new Color(0.15f, 0.9f, 0.2f), new Color(0.75f, 0f, 0.1f), new Color(0.55f, 0.1f, 0.9f), new Color(0.3f, 0.5f, 1f) };

        internal static bool IsSerum(Comp c) => c >= Comp.NightSoldier;

        // ---------------------------------------------------------------- settings
        [ModOptionCategory(CB, 6), ModOptionOrder(0)] [ModOption("Serums", "Syringes and vials: the serums and the super serums.", defaultValueIndex = 1)]
        public static bool bioEnabled = true;
        public static bool bioNeuro = true;
        public static bool bioVerdant = true;
        public static bool bioCryo = true;
        public static bool bioSurge = true;
        public static bool bioHalluc = true;
        public static bool bioMotor = true;
        public static float bioDuration = 1f;
        public static float bioIntensity = 1f;
        public static bool bioVisuals = true;
        public static bool bioRecovery = true;
        [ModOptionCategory(CB, 6), ModOptionOrder(11)] [ModOption("Status indicators", "A small coloured marker above an affected NPC's head.", defaultValueIndex = 1)] public static bool bioIndicators = true;

        public static bool xNight = true;
        public static bool xHero = true;
        public static bool xAdrenal = true;
        public static bool xTitan = true;
        public static bool xPhantom = true;
        public static bool xPhoenix = true;
        public static bool xRavager = true;
        public static bool xOvermind = true;
        public static bool xColossus = true;
        public static float xStrength = 1f;
        public static float xRegen = 1f;
        public static bool xLastStand = true;
        public static float xLastStandAt = 0.2f;
        public static bool xVisuals = true;
        public static bool xScaling = false;
        [ModOptionCategory(CX, 7), ModOptionOrder(18)] [ModOption("Enhanced NPCs in waves", "Chance a spawned NPC already carries a super serum.", defaultValueIndex = 0)] [ModOptionSlider, ModOptionFloatValues(0f, 0.5f, 0.05f)]
        public static float xSpawnChance = 0f;

        static bool CompAllowed(Comp c)
        {
            if (!bioEnabled) return false;
            switch (c)
            {
                case Comp.Neurostatic: return bioNeuro; case Comp.Verdant: return bioVerdant; case Comp.CryoSleep: return bioCryo;
                case Comp.AdrenalSurge: return bioSurge; case Comp.Hallucinatory: return bioHalluc; case Comp.MotorShutdown: return bioMotor;
                case Comp.NightSoldier: return xNight; case Comp.HeroX: return xHero; case Comp.AdrenalOverload: return xAdrenal;
                case Comp.Titan: return xTitan; case Comp.Phantom: return xPhantom; case Comp.Phoenix: return xPhoenix;
                case Comp.Ravager: return xRavager; case Comp.Overmind: return xOvermind; case Comp.Colossus: return xColossus;
            }
            return false;
        }

        // ---------------------------------------------------------------- per-NPC effects
        internal sealed class BioFx { public Comp comp; public float start, dur, inten, dose; public int stage; public string outcome = ""; public bool used; }

        /// Apply a compound to an NPC (from an injection, or a wave spawn). dose 0..1 = how much of the syringe went in.
        void ApplyCompound(NpcState s, Comp comp, float dose, Vector3 site)
        {
            var c = s.creature;
            if (c == null || c.isKilled || !CompAllowed(comp) || dose < 0.15f) return;
            bool serum = IsSerum(comp);
            if (serum) s.bioFx.RemoveAll(f => IsSerum(f.comp));                          // one X-serum at a time
            s.bioFx.RemoveAll(f => f.comp == comp);                                        // re-dosing refreshes
            // incompatible pairs: stimulants cancel sleep, sleep cancels surge
            if (comp == Comp.AdrenalSurge || comp == Comp.AdrenalOverload) s.bioFx.RemoveAll(f => f.comp == Comp.CryoSleep);
            if (comp == Comp.CryoSleep) s.bioFx.RemoveAll(f => f.comp == Comp.AdrenalSurge);
            float baseDur = serum ? 600f : comp == Comp.CryoSleep ? 50f : comp == Comp.AdrenalSurge ? 45f : 75f;
            var fx = new BioFx { comp = comp, start = Time.time, dur = baseDur * bioDuration * Mathf.Lerp(0.6f, 1f, dose), inten = bioIntensity * dose * (serum ? xStrength : 1f), dose = dose };
            if (comp == Comp.HeroX) fx.outcome = RollSyndrome();
            s.bioFx.Add(fx);
            s.bioDirty = true;
            Log($"{WhoIs(s)} INJECTED: {CompName[(int)comp]} (dose {dose:0.00}{(fx.outcome != "" ? ", " + fx.outcome : "")})");
            Research(s, comp, fx.outcome, dose);
            MarkInjected(s, CompLiquid[(int)comp], site);
            // the moment of injection: flinch at the site, a sharp breath, a glance at the arm
            if (!serum || comp == Comp.HeroX) Guard("bio.react", () => Flinch(s, site, 0.6f));
            if (serum) { Sfx.Play("NRE_SerumSurge", c.transform, 0.9f); if (c.ragdoll.headPart != null) Guard("bio.surge", () => Flinch(s, c.ragdoll.headPart.transform.position + c.transform.forward, 1f)); }
        }

        // 0.25.3: serums always work - no rejection, no partial/unstable/mutation rolls.
        static string RollSyndrome() => "SUCCESS";

        // aggregated multipliers (applied as ratios, so they come off cleanly)
        static readonly object BioHandler = new object(), BioDmgHandler = new object();

        void TickBio(NpcState s, float now)
        {
            var c = s.creature;
            if (s.bioFx.Count == 0 && !s.bioActive) return;
            if (c.isKilled) { ClearBio(s); return; }
            float dt = s.bioLastTick > 0f ? Mathf.Min(0.5f, now - s.bioLastTick) : 0.1f;
            s.bioLastTick = now;

            float tough = 1f, pain = 1f, balance = 1f, speed = 1f, dmgTaken = 1f, react = 1f, painCap = 1f, regen = 0f, height = 1f;
            float strength = 1f, flinch = 1f; bool noQuit = false, angry = false;
            Comp? xs = null;

            for (int i = s.bioFx.Count - 1; i >= 0; i--)
            {
                var f = s.bioFx[i];
                float t = now - f.start, k = f.inten;
                if (t > f.dur && (bioRecovery || IsSerum(f.comp))) { EndFx(s, f); s.bioFx.RemoveAt(i); continue; }
                float ramp = Mathf.Clamp01(t / 4f);                                      // effects build up over a few seconds
                switch (f.comp)
                {
                    case Comp.Neurostatic:
                        s.drunk = Mathf.Max(s.drunk, 0.8f * k * ramp); react *= 1f - 0.5f * k * ramp; balance *= 1f - 0.4f * k * ramp;
                        if (Random.value < 0.04f * k) Convulse(s, 0.12f);
                        break;
                    case Comp.Verdant:
                        speed *= 1f - 0.35f * k * ramp; s.fatigue = Mathf.Max(s.fatigue, 0.6f * k * ramp);
                        s.weakArmLUntil = Mathf.Max(s.weakArmLUntil, now + 0.3f); s.weakArmRUntil = Mathf.Max(s.weakArmRUntil, now + 0.3f);
                        if (bioVisuals && now >= s.bioNextPaint) { s.bioNextPaint = now + 1.6f; PaintAround(s, new Color(0.25f, 0.55f, 0.12f, 0.35f), 0.03f + Mathf.Min(0.07f, t * 0.002f)); }
                        if (Random.value < 0.01f) Speak(s, "choke");
                        break;
                    case Comp.CryoSleep:
                        speed *= 1f - 0.45f * ramp; react *= 0.5f;
                        if (bioVisuals && now >= s.bioNextPaint) { s.bioNextPaint = now + 2f; PaintAround(s, new Color(0.7f, 0.85f, 1f, 0.25f), 0.05f); }
                        if (f.stage == 0 && t > 8f / Mathf.Max(0.5f, k)) { f.stage = 1; Sleep(s, Mathf.Max(8f, f.dur - t)); }
                        if (f.stage == 1 && !s.knockedOut) { f.stage = 2; f.dur = t; }          // woke up = done
                        break;
                    case Comp.AdrenalSurge:
                        if (t < f.dur * 0.75f)
                        {
                            angry = true; painCap = Mathf.Min(painCap, 0.35f); speed *= 1f + 0.2f * k;
                            if (Random.value < 0.05f) ShoveBodyQuiet(s, Quaternion.Euler(0f, Random.Range(-120f, 120f), 0f) * c.transform.forward, 0.6f * k);   // erratic
                        }
                        else { speed *= 0.7f; s.fatigue = Mathf.Max(s.fatigue, 0.8f); }        // the crash
                        break;
                    case Comp.Hallucinatory:
                        react *= 0.75f;
                        if (now >= s.bioNextHalluc)
                        {
                            s.bioNextHalluc = now + Random.Range(1.5f, 4f) / Mathf.Max(0.5f, k);
                            float r = Random.value;
                            Vector3 phantom = c.transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(1f, 3f) + Vector3.up * 1.5f;
                            if (r < 0.35f) Guard("halluc", () => Flinch(s, phantom, 0.8f));                 // flinches at nothing
                            else if (r < 0.6f) { ShoveBodyQuiet(s, c.transform.position - phantom, 0.5f); }  // backs away from it
                            else if (r < 0.8f) SetMelee(s, false, now + 1.2f);                              // loses the thread
                            else Scream(s, false);
                            Guard("face", () => c.brain.instance.GetModule<BrainModuleExpression>(false)?.SetFaceExpression(FaceAnimator.Expression.Confusion, 0, null));
                        }
                        break;
                    case Comp.MotorShutdown:
                        s.weakArmLUntil = Mathf.Max(s.weakArmLUntil, now + 0.3f); s.weakArmRUntil = Mathf.Max(s.weakArmRUntil, now + 0.3f);
                        if (t > 3f) { s.buckleLegs |= LegParts; s.buckleUntil = Mathf.Max(s.buckleUntil, now + 0.3f); }
                        if (t > 6f / Mathf.Max(0.5f, k) && f.stage == 0 && !s.cantStand) { f.stage = 1; s.cantStand = true; s.bioSetCantStand = true; Sink(s, now); }
                        speed *= 0.5f;
                        break;
                    default:
                        xs = f.comp;
                        SerumTick(s, f, t, k, dt, ref tough, ref pain, ref balance, ref speed, ref dmgTaken, ref react, ref painCap, ref regen, ref height, ref strength, ref flinch, ref noQuit, ref angry);
                        break;
                }
            }

            // ---- apply the aggregate (ratios against what we applied last time)
            ApplyRatio(ref s.toughness, ref s.bioTough, tough);
            ApplyRatio(ref s.painSensitivity, ref s.bioPain, pain);
            ApplyRatio(ref s.balanceStability, ref s.bioBalance, balance);
            s.bioReact = react; s.bioStrength = strength; s.bioFlinch = flinch; s.bioNoQuit = noQuit;
            if (painCap < 1f) s.pain = Mathf.Min(s.pain, painCap);
            if (angry) AddTag(s, Tag.Anger, 1f, 1f);
            if (Mathf.Abs(speed - s.bioSpeed) > 0.01f)
            {
                s.bioSpeed = speed;
                Guard("speed", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(BioHandler); if (Mathf.Abs(speed - 1f) > 0.01f) c.locomotion.globalMoveSpeedMultiplier.Add(BioHandler, speed); });
            }
            if (Mathf.Abs(dmgTaken - s.bioDmg) > 0.01f)
            {
                s.bioDmg = dmgTaken;
                Guard("durability", () => { c.damageMultipliers.Remove(BioDmgHandler); if (dmgTaken < 0.99f) c.damageMultipliers[BioDmgHandler] = dmgTaken; });
            }
            if (regen > 0f && c.currentHealth < c.maxHealth) Guard("regen", () => c.Heal(regen * dt));
            if (regen > 0f) s.bleedRate = Mathf.Max(0f, s.bleedRate - 0.05f * regen * dt);
            // size (game's own height system, so rig, colliders and grips stay consistent)
            if (xScaling && Mathf.Abs(height - s.bioHeight) > 0.005f)
            {
                if (s.bioBaseHeight <= 0f) Guard("height", () => s.bioBaseHeight = c.GetHeight());
                if (s.bioBaseHeight > 0f) { float h = s.bioBaseHeight * height; s.bioHeight = height; Guard("height", () => c.SetHeight(h)); }
            }
            s.bioActive = s.bioFx.Count > 0;
            Guard("bio.visual", () => UpdateBioVisuals(s, xs));
            if (!s.bioActive) ClearBio(s);
        }

        static void ApplyRatio(ref float field, ref float applied, float want)
        {
            if (applied <= 0f) applied = 1f;
            if (Mathf.Abs(want - applied) < 0.001f) return;
            field = field / applied * want;
            applied = want;
        }

        void SerumTick(NpcState s, BioFx f, float t, float k, float dt, ref float tough, ref float pain, ref float balance, ref float speed, ref float dmgTaken,
                       ref float react, ref float painCap, ref float regen, ref float height, ref float strength, ref float flinch, ref bool noQuit, ref bool angry)
        {
            var c = s.creature;
            float now = Time.time;
            switch (f.comp)
            {
                case Comp.NightSoldier:
                    painCap = 0f; pain *= 0.2f; strength *= 1f + 0.35f * k; tough *= 1f + 0.5f * k; dmgTaken *= 1f - 0.25f * Mathf.Min(1f, k);
                    regen += 0.6f * xRegen; flinch *= 0.5f; balance *= 1.5f; noQuit = true;
                    break;
                case Comp.HeroX:
                {
                    string o = f.outcome;
                    float q = o == "PARTIAL" ? 0.5f : o == "MUTATION" ? 1.4f : 1f;
                    regen += 3f * xRegen * q; strength *= 1f + 0.7f * k * q; tough *= 1f + 0.8f * k * q; dmgTaken *= 1f - 0.4f * Mathf.Min(1f, k * q);
                    react *= 1f + 0.5f * q; painCap = Mathf.Min(painCap, 0.3f); flinch *= 0.25f; balance *= 1.6f; s.drunk = 0f;
                    if (o == "UNSTABLE" && Random.value < 0.015f) { Convulse(s, 0.7f); SetMelee(s, false, now + 1.5f); Log($"{WhoIs(s)}: unstable serum - seizure"); }
                    if (o == "MUTATION") { height = Mathf.Max(height, 1.1f); angry = true; if (Random.value < 0.01f) Convulse(s, 0.4f); }
                    // limb restoration (function, not severed limbs - see COMPATIBILITY)
                    if (now >= s.bioNextRestore) { s.bioNextRestore = now + 5f / q; RestoreOneLimb(s); }
                    break;
                }
                case Comp.AdrenalOverload:
                    angry = true; speed *= 1f + 0.35f * Mathf.Min(1.5f, k); strength *= 1f + 0.5f * k; flinch *= 0.4f; pain *= 0.3f; tough *= 1f + 0.6f * k; noQuit = true;
                    if (xLastStand && !f.used && c.maxHealth > 0f && c.currentHealth / c.maxHealth <= xLastStandAt)
                    {
                        f.used = true; s.bioLastStandUntil = now + 15f;
                        Scream(s, true); RageMoment("Last Stand");
                        Log($"{WhoIs(s)}: LAST STAND ({c.currentHealth:0}/{c.maxHealth:0} HP)");
                    }
                    if (now < s.bioLastStandUntil) { painCap = 0f; flinch *= 0.25f; speed *= 1.15f; s.rushUntil = Mathf.Max(s.rushUntil, now + 0.3f); }
                    break;
                case Comp.Titan:
                    strength *= 1f + 1f * k; dmgTaken *= 1f - 0.5f * Mathf.Min(1f, k); tough *= 1f + 1f * k; speed *= 0.85f; flinch *= 0.3f; height = Mathf.Max(height, 1.08f); balance *= 2f;
                    break;
                case Comp.Phantom:
                    speed *= 1f + 0.3f * k; react *= 1f + 0.8f * k; flinch *= 0.6f;
                    break;
                case Comp.Phoenix:
                    regen += 1.5f * xRegen;
                    if (!f.used && c.maxHealth > 0f && c.currentHealth / c.maxHealth <= 0.1f)
                    {
                        f.used = true;
                        Guard("rebirth", () => c.Heal(c.maxHealth * 0.6f));
                        s.pain = 0f; s.bleedRate = 0f; s.rushUntil = now + 4f;
                        Scream(s, true); RageMoment("Phoenix rebirth");
                        Log($"{WhoIs(s)}: PHOENIX - second wind");
                    }
                    break;
                case Comp.Ravager:
                    angry = true; strength *= 1f + 0.6f * k; painCap = Mathf.Min(painCap, 0.2f); s.bleedRate *= 0.97f; flinch *= 0.5f; noQuit = true;
                    if (s.nextPunch > now + 0.4f) s.nextPunch = now + 0.4f;
                    break;
                case Comp.Overmind:
                    react *= 1f + 0.6f * k; balance *= 1.3f;
                    // allies close by fight smarter (reaction / defence boost)
                    if (Random.value < 0.1f)
                        for (int i = 0; i < MaxTracked; i++)
                        {
                            var o = pool[i];
                            if (o == s || !o.active || o.creature == null || o.creature.isKilled) continue;
                            if ((o.creature.transform.position - c.transform.position).sqrMagnitude < 64f) o.bioAllyBoostUntil = now + 2f;
                        }
                    break;
                case Comp.Colossus:
                    dmgTaken *= 1f - 0.6f * Mathf.Min(1f, k); flinch *= 0.1f; strength *= 1f + 0.5f * k; tough *= 1f + 1.5f * k; speed *= 0.8f; height = Mathf.Max(height, 1.12f); balance *= 3f; noQuit = true;
                    break;
            }
        }

        /// Hero X: restores FUNCTION to one injured limb (broken bone knits, dead arm returns, cut muscles heal).
        /// A limb that has been cut off cannot be re-attached (Nomad has no un-slice API) - the stump is sealed instead.
        void RestoreOneLimb(NpcState s)
        {
            var c = s.creature;
            foreach (var limb in new[] { LeftArmParts, RightArmParts, LeftLegParts, RightLegParts })
            {
                bool broken = Has(s.brokenParts, limb), limp = Has(s.limpParts, limb);
                bool armDead = limb == LeftArmParts ? s.leftArmDead : limb == RightArmParts ? s.rightArmDead : false;
                bool sliced = false;
                foreach (var p in c.ragdoll.parts) if (p != null && Has(limb, p.type) && p.isSliced) sliced = true;
                if (sliced)
                {
                    if ((s.bioSealed & (int)limb) == 0) { s.bioSealed |= (int)limb; s.bleedRate *= 0.3f; Log($"{WhoIs(s)}: severed {limb} - stump sealed (a cut-off limb can't be re-attached in Nomad)"); return; }
                    continue;
                }
                if (!broken && !limp && !armDead) continue;
                s.brokenParts &= ~limb; s.limpParts &= ~limb;
                if (limb == LeftArmParts) s.leftArmDead = false; else if (limb == RightArmParts) s.rightArmDead = false;
                for (int side = 0; side < 2; side++) for (int m = 0; m < (int)Mu.Count; m++) s.muscles[side * (int)Mu.Count + m] *= 0.5f;
                s.musclesDirty = true;
                if (Has(limb, LegParts)) { s.buckleLegs &= ~limb; if (s.cantStand && !s.spineBroken && !Has(s.brokenParts, LegParts)) { s.cantStand = false; } }
                Guard("pins", () => { RestoreMuscles(s); c.ragdoll.ResetPinForce(); if (s.limpParts != 0) MakeLimp(s, s.limpParts); });
                Log($"{WhoIs(s)}: Hero X restored the {limb} ({(broken ? "bone knitted" : armDead ? "arm back" : "muscles healed")})");
                return;
            }
        }

        void Sleep(NpcState s, float seconds)
        {
            var c = s.creature;
            if (s.knockedOut) return;
            s.knockedOut = true; s.koWakeAt = Time.time + seconds; s.koLimbs = s.limpParts; s.eyesShutUntil = float.MaxValue;
            Guard("brain", () => c.brain.Stop());
            AllowFall(s, 3f); Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
            Log($"{WhoIs(s)} slips UNCONSCIOUS ({seconds:0} s)");
        }

        void EndFx(NpcState s, BioFx f)
        {
            Log($"{WhoIs(s)}: {CompName[(int)f.comp]} wears off");
            if (s.bioSetCantStand && (f.comp == Comp.MotorShutdown || f.comp == Comp.HeroX))
            {
                bool realInjury = s.spineBroken || (Has(s.brokenParts, LeftLegParts) && Has(s.brokenParts, RightLegParts));
                if (!realInjury) s.cantStand = false;
                s.bioSetCantStand = false;
            }
        }

        void ClearBio(NpcState s)
        {
            var c = s.creature;
            s.bioFx.Clear();
            ApplyRatio(ref s.toughness, ref s.bioTough, 1f); ApplyRatio(ref s.painSensitivity, ref s.bioPain, 1f); ApplyRatio(ref s.balanceStability, ref s.bioBalance, 1f);
            s.bioReact = s.bioStrength = s.bioFlinch = 1f; s.bioNoQuit = false; s.bioActive = false;
            if (c != null)
            {
                Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Remove(BioHandler));
                Guard("durability", () => c.damageMultipliers.Remove(BioDmgHandler));
                if (s.bioBaseHeight > 0f && Mathf.Abs(s.bioHeight - 1f) > 0.005f && !c.isKilled) { float h = s.bioBaseHeight; Guard("height", () => c.SetHeight(h)); }
            }
            s.bioSpeed = s.bioDmg = s.bioHeight = 1f;
            RemoveBioVisuals(s);
        }

        void PaintAround(NpcState s, Color col, float r)
        {
            var c = s.creature;
            var parts = c.ragdoll.parts;
            var p = s.bioSite != null ? s.bioSite : parts[Random.Range(0, parts.Count)];
            if (p == null) return;
            Vector3 at = (s.bioSite != null ? p.transform.TransformPoint(s.bioSiteLocal) : p.transform.position) + Random.insideUnitSphere * r * 1.5f;
            Guard("bio.paint", () => SkinPaint.Paint(c, at, r, col, TexGen.Bruise(), false, Random.Range(0f, 360f)));
        }

        // ---------------------------------------------------------------- visuals: eyes, aura, indicator
        static Material glowMat, auraMat;
        static Texture2D softDot;
        static Material GlowMat(Color col)
        {
            var sh = (Application.platform == RuntimePlatform.Android ? null : SkinPaint.BlendShader);     // 0.28.3: UI shader on meshes/particles crashed the Quest
            if (sh == null) return Mats.Make(col, 0.9f);
            var m = new Material(sh) { color = col, renderQueue = 3100 };
            if (softDot == null) softDot = SoftDot();
            m.mainTexture = softDot;
            return m;
        }
        /// Unlit solid colour (reads as a glow on Quest without post-processing).
        static Material SolidGlow(Color col)
        {
            var sh = (Application.platform == RuntimePlatform.Android ? null : SkinPaint.BlendShader);     // 0.28.3: UI shader on meshes/particles crashed the Quest
            if (sh == null) return Mats.Make(col, 0.9f);
            return new Material(sh) { color = new Color(col.r, col.g, col.b, 1f), mainTexture = Texture2D.whiteTexture, renderQueue = 3050 };
        }

        static Texture2D SoftDot()
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                float a = Mathf.Clamp01(1f - d); a *= a;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            return t;
        }

        void UpdateBioVisuals(NpcState s, Comp? x)
        {
            var c = s.creature;
            // status indicator: the colour of the newest compound, floating above the head
            if (false && bioIndicators && bioVisuals && s.bioFx.Count > 0 && c.ragdoll.headPart != null)   // 0.25.3: replaced by Serum25 status marker
            {
                var latest = s.bioFx[s.bioFx.Count - 1].comp;
                if (s.bioIndicator == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(go.GetComponent<Collider>());
                    go.name = "RageEngine_BioIndicator";
                    go.transform.SetParent(c.ragdoll.headPart.transform, false);
                    go.transform.localScale = Vector3.one * 0.06f;
                    s.bioIndicator = go;
                }
                if (s.bioIndicatorComp != (int)latest) { s.bioIndicatorComp = (int)latest; s.bioIndicator.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(CompLiquid[(int)latest]); }
                var cam = Camera.main;
                s.bioIndicator.transform.position = c.ragdoll.headPart.transform.position + Vector3.up * 0.3f;
                if (cam != null) s.bioIndicator.transform.rotation = Quaternion.LookRotation(s.bioIndicator.transform.position - cam.transform.position);
            }
            else if (s.bioIndicator != null) { Object.Destroy(s.bioIndicator); s.bioIndicator = null; s.bioIndicatorComp = -1; }

            // X-Series identity: glowing eyes + aura
            bool wantX = x.HasValue && xVisuals && bioVisuals;
            int xi = wantX ? (int)x.Value - (int)Comp.NightSoldier : -1;
            if (xi == s.bioXVisual) return;
            RemoveXVisuals(s);
            if (!wantX) return;
            s.bioXVisual = xi;
            Color eye = XEyes[xi], aura = XAura[xi];
            // iris colour: Serum25 eye tint (all serums, toxins and custom serums)
            if (c.allEyes != null)
                foreach (var e in c.allEyes)
                {
                    if (e == null) continue;
                    var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.Destroy(g.GetComponent<Collider>());
                    g.name = "RageEngine_EyeGlow";
                    g.transform.SetParent(e.transform, false);
                    g.transform.position = e.transform.position + (c.centerEyes != null ? c.centerEyes.forward : c.transform.forward) * 0.012f;
                    var ls = e.transform.lossyScale;
                    g.transform.localScale = new Vector3(0.035f / Mathf.Max(0.001f, ls.x), 0.035f / Mathf.Max(0.001f, ls.y), 0.035f / Mathf.Max(0.001f, ls.z));
                    g.transform.localScale *= 0.45f;
                    g.GetComponent<MeshRenderer>().sharedMaterial = SolidGlow(eye);
                    s.bioVisualObjs.Add(g);
                }
            Guard("bio.aura", () => AddAura(s, aura));
        }

        /// Separate method: if particles were ever unavailable, only the aura is lost.
        static void AddAura(NpcState s, Color aura)
        {
            var c = s.creature;
            var chest = ChestPart(c);
            if (chest != null && Application.platform != RuntimePlatform.Android && SkinPaint.BlendShader != null)
            {
                var go = new GameObject("RageEngine_Aura");
                go.transform.SetParent(chest.transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main; main.startLifetime = 0.9f; main.startSpeed = 0.12f; main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
                main.startColor = new Color(aura.r, aura.g, aura.b, 0.5f); main.maxParticles = 28; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -0.05f;
                var em = ps.emission; em.rateOverTime = 16f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.32f;
                var col = ps.colorOverLifetime; col.enabled = true;
                var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(aura, 0f), new GradientColorKey(aura, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.3f), new GradientAlphaKey(0f, 1f) });
                col.color = gr;
                var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = GlowMat(Color.white);
                s.bioVisualObjs.Add(go);
            }
        }

        static void RemoveXVisuals(NpcState s)
        {
            foreach (var o in s.bioVisualObjs) if (o != null) Object.Destroy(o);
            s.bioVisualObjs.Clear();
            if (s.bioXVisual >= 0 && s.creature != null)
            {
                // iris restored by Serum25 eye tint
            }
            s.bioXVisual = -1;
        }

        static void RemoveBioVisuals(NpcState s)
        {
            RemoveXVisuals(s);
            if (s.bioIndicator != null) { Object.Destroy(s.bioIndicator); s.bioIndicator = null; }
            s.bioIndicatorComp = -1;
        }

        // ---------------------------------------------------------------- research database
        static void Research(NpcState s, Comp comp, string outcome, float dose)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(RosterPath);
                string p = System.IO.Path.Combine(dir, "RageEngine_research.txt");
                string subject = s.identity != null ? s.identity.name : s.creature.name;
                System.IO.File.AppendAllText(p, $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}|{subject}|{CompName[(int)comp]}|{(outcome == "" ? "administered" : outcome)}|dose {dose:0.00}|hp {s.creature.maxHealth:0}\n");
                if (s.identity != null && IsSerum(comp)) { s.identity.serum = CompKey[(int)comp] + (outcome != "" ? ":" + outcome : ""); rosterDirty = true; }
            }
            catch (System.Exception e) { Log("research log: " + e.Message); }
        }

        /// Waves: some NPCs arrive already enhanced (Mod Options > X-Series > Enhanced NPCs in waves).
        void MaybeSpawnEnhanced(NpcState s)
        {
            if (!bioEnabled || xSpawnChance <= 0f || Random.value > xSpawnChance) return;
            var pool9 = new List<Comp>();
            for (var x = Comp.NightSoldier; x < Comp.Count; x++) if (CompAllowed(x)) pool9.Add(x);
            if (pool9.Count == 0) return;
            ApplyCompound(s, pool9[Random.Range(0, pool9.Count)], 1f, s.creature.transform.position);
        }
    }
}

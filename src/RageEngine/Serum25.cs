// Rage Engine 0.25.3 - serums that just work, and you can SEE them working.
//   EYE COLOUR    every serum, super serum, custom serum, toxin and the Loyalty Serum tints the NPC's eyes while it
//                 is in him (super serum > custom serum > toxin > serum > loyalty). It goes back when it wears off.
//   STATUS MARK   a small glowing marker above his head in the same colour, pulsing while it works. The moment a
//                 needle (or an oiled blade) gets it into him the marker flares and a puff of that colour comes off
//                 the injection site. Option: Serums > Status indicators.
//   REGENERATION  Night Soldier, Hero X and Phoenix really regenerate: step by step the spine, broken legs and arms,
//                 ribs, bleeding, jaw/nose/eyes heal (a green flash at the spot), then the blood and bruises clear
//                 and he gets back up. While a regenerating serum is in him his spine can't be broken.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static readonly Color AllyEyes = new Color(0.3f, 0.75f, 1f);
        static readonly Dictionary<int, Material> puffMats = new Dictionary<int, Material>();
        static readonly Color RegenGreen = new Color(0.35f, 1f, 0.45f);

        // ------------------------------------------------------------------ injection moment
        void MarkInjected(NpcState s, Color col, Vector3 site)
        {
            if (s == null || s.creature == null) return;
            col.a = 1f;
            s.injectColor = col;
            s.injectFlashUntil = Time.time + 1.2f;
            if (bioIndicators) Guard("inject.puff", () => Puff(site, col, 14, 0.35f));
        }

        /// One-shot puff of glowing particles (cleans itself up).
        static void Puff(Vector3 at, Color col, int count, float speed)
        {
            if (Application.platform == RuntimePlatform.Android) return;          // 0.28.3: no runtime particles on Quest (crash)
            var go = new GameObject("RageEngine_Puff");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = 0.3f; main.loop = false; main.startLifetime = 0.7f; main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f); main.startColor = new Color(1f, 1f, 1f, 0.85f);
            main.maxParticles = count; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -0.02f;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.04f;
            // 0.26: the colour is in the material too - on shaders that ignore particle colour the puff came out WHITE
            int pk = Mathf.RoundToInt(col.r * 15f) | Mathf.RoundToInt(col.g * 15f) << 4 | Mathf.RoundToInt(col.b * 15f) << 8;
            if (!puffMats.TryGetValue(pk, out var pm) || pm == null) puffMats[pk] = pm = GlowMat(new Color(col.r, col.g, col.b, 1f));
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = pm;
            ps.Emit(count);
            Object.Destroy(go, 2f);
        }

        // ------------------------------------------------------------------ what is in him right now
        static bool ActiveTint(NpcState s, float now, out Color col)
        {
            col = Color.white;
            Comp? x = null, other = null;
            foreach (var f in s.bioFx) { if (IsSerum(f.comp)) x = f.comp; else other = f.comp; }
            if (x.HasValue) { col = XEyes[(int)x.Value - (int)Comp.NightSoldier]; return true; }
            if (s.labUntil > now && !string.IsNullOrEmpty(s.labSerum)) { col = s.labColor.a > 0f ? s.labColor : Color.gray; return true; }
            if (s.tox >= 0 && s.tox < ToxColor.Length) { col = ToxColor[s.tox]; return true; }
            if (other.HasValue) { col = CompLiquid[(int)other.Value]; return true; }
            if (s.ally) { col = AllyEyes; return true; }
            return false;
        }

        internal static bool RegenActive(NpcState s)
        {
            if (s == null || xRegen <= 0f) return false;
            foreach (var f in s.bioFx) if (f.comp == Comp.NightSoldier || f.comp == Comp.HeroX || f.comp == Comp.Phoenix) return true;
            return false;
        }

        // ------------------------------------------------------------------ 0.1 s tick
        void TickSerum25(NpcState s, float now)
        {
            var c = s.creature;
            if (c == null) return;
            bool has = ActiveTint(s, now, out var col);
            if (c.isKilled) has = has && s.ally;                                           // a dead man's eyes go back
            // ---- eye colour
            float key = has ? Mathf.Round(col.r * 255f) * 65536f + Mathf.Round(col.g * 255f) * 256f + Mathf.Round(col.b * 255f) : -2f;
            if (key != s.eyeTintKey)
            {
                s.eyeTintKey = key;
                var cc = col; var orig = s.irisOrig;
                Guard("eyetint", () => c.SetColor(has ? new Color(cc.r, cc.g, cc.b, 1f) : orig, Creature.ColorModifier.EyesIris, true));
            }
            // ---- status marker
            bool flash = now < s.injectFlashUntil;
            bool want = bioIndicators && !c.isKilled && (has || flash) && c.ragdoll.headPart != null;
            if (!want) { if (s.fxMark != null) { var m0 = s.fxMark.GetComponent<MeshRenderer>().sharedMaterial; if (m0 != null) Object.Destroy(m0); Object.Destroy(s.fxMark); s.fxMark = null; } }
            else
            {
                Color mc = flash ? s.injectColor : col;
                if (s.fxMark == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(go.GetComponent<Collider>());
                    go.name = "RageEngine_StatusMark";
                    s.fxMark = go;
                }
                var mr = s.fxMark.GetComponent<MeshRenderer>();
                if (mr.sharedMaterial == null || mr.sharedMaterial.color != new Color(mc.r, mc.g, mc.b, mr.sharedMaterial.color.a)) { var old = mr.sharedMaterial; mr.sharedMaterial = GlowMat(mc); if (old != null) Object.Destroy(old); }
                float pulse = flash ? 1.6f + 1.2f * (s.injectFlashUntil - now) : 1f + 0.22f * Mathf.Sin(now * 7f);
                if (now < s.regenGlowUntil) pulse *= 1.3f;
                s.fxMark.transform.position = c.ragdoll.headPart.transform.position + Vector3.up * 0.32f;
                s.fxMark.transform.localScale = Vector3.one * 0.055f * pulse;
                var cam = Camera.main;
                if (cam != null) s.fxMark.transform.rotation = Quaternion.LookRotation(s.fxMark.transform.position - cam.transform.position);
            }
            // ---- regeneration
            if (!c.isKilled && RegenActive(s)) TickRegen25(s, now);
        }

        // ------------------------------------------------------------------ regeneration
        void TickRegen25(NpcState s, float now)
        {
            if (now < s.nextRegenStep) return;
            s.nextRegenStep = now + Mathf.Clamp(2.5f / Mathf.Max(0.3f, xRegen), 1f, 8f);
            var c = s.creature;
            var chest = ChestPart(c);
            Vector3 at = chest != null ? chest.transform.position : c.transform.position;
            string healed = null;

            if (s.spineBroken || s.paralysed)
            {
                s.spineBroken = s.paralysed = false;
                s.limpParts &= s.brokenParts;                                               // only genuinely broken limbs stay limp
                Guard("regen.spine", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(InjuryHandler); RestoreMuscles(s); c.ragdoll.ResetPinForce(); if (s.limpParts != 0) MakeLimp(s, s.limpParts); });
                if (s.meleeOff) SetMelee(s, true, 0f);
                healed = "spine";
            }
            else if (s.brokenParts != 0 || s.limpParts != 0 || s.leftArmDead || s.rightArmDead)
            {
                var before = s.brokenParts | s.limpParts;
                bool la = s.leftArmDead, ra = s.rightArmDead;
                RestoreOneLimb(s);
                if ((s.brokenParts | s.limpParts) != before || la != s.leftArmDead || ra != s.rightArmDead)
                {
                    healed = "a broken limb";
                    var p = AnyPartOf(c, before & ~(s.brokenParts | s.limpParts));
                    if (p != null) at = p.transform.position;
                }
                else { s.brokenParts = 0; s.limpParts = 0; s.leftArmDead = s.rightArmDead = false; Guard("regen.pins", () => { RestoreMuscles(s); c.ragdoll.ResetPinForce(); }); healed = "his limbs"; }
            }
            else if (s.ribsBroken > 0) { s.ribsBroken = Mathf.Max(0, s.ribsBroken - 2); healed = "ribs"; Guard("regen.rib", () => c.locomotion.globalMoveSpeedMultiplier.Remove(RibHandler)); }
            else if (s.bleedRate > 0.02f || s.legWounded || s.throatCutUntil > now || s.gutImpaled)
            {
                s.bleedRate = 0f; s.bleedAccum = 0f; s.legWounded = false; s.throatCutUntil = 0f; s.gutImpaled = false; s.bloodInAirway = false;
                Guard("regen.wounds", () => c.locomotion.globalMoveSpeedMultiplier.Remove(WoundHandler));
                healed = "the wounds close";
            }
            else if (s.jawBroken || s.noseBroken || s.gouged != 0 || HasBlackEye(s))
            {
                if (s.jawBroken && c.jaw != null) c.jaw.localRotation = s.jawRest;
                s.jawBroken = s.jawTorn = false;
                Guard("regen.jaw", () => { var speak = c.brain.instance.GetModule<BrainModuleSpeak>(false); if (speak != null) speak.moveMouth = true; });
                s.noseBroken = false;
                if (s.gouged != 0) Guard("regen.eyes", () => RestoreGouged(s));
                System.Array.Clear(s.blackEye, 0, s.blackEye.Length);
                Guard("regen.face", () => HeadDeform.Restore(c));
                if (c.ragdoll.headPart != null) at = c.ragdoll.headPart.transform.position;
                healed = "the face";
            }
            else if (!s.regenClean)
            {
                // fully healed: the blood and bruises clear
                s.regenClean = true;
                Guard("regen.decals", () => { if (c.revealDecals != null) foreach (var d in c.revealDecals) if (d != null) d.Reset(); });
                Guard("regen.skin", () => SkinPaint.Restore(c));
                foreach (var b in s.bruises) if (b.go != null) Object.Destroy(b.go);
                s.bruises.Clear(); s.bruiseSpots.Clear(); s.woundSpots.Clear(); s.swellCount = 0;
                ClearKnots(s);
                foreach (var g in s.boneMeshes) if (g != null) Object.Destroy(g);
                s.boneMeshes.Clear();
                healed = "the blood and bruises";
            }

            if (healed != null)
            {
                if (healed != "the blood and bruises") s.regenClean = false;
                s.pain = Mathf.Min(s.pain, 0.2f); s.shock = 0f; s.shockDeathAt = 0f; s.daze = 0f;
                s.regenGlowUntil = now + 0.8f;
                if (bioIndicators) Guard("regen.puff", () => Puff(at, RegenGreen, 18, 0.25f));
                Log($"{WhoIs(s)}: REGENERATES - {healed}");
            }

            // a regenerating body gets back up
            bool clearRules = !s.spineBroken && !Has(s.brokenParts, LegParts);
            if (clearRules && (s.cantStand || s.bioSetCantStand) && !s.knockedOut && s.tox < 0) { s.cantStand = false; s.bioSetCantStand = false; s.buckleLegs = 0; }
            if (clearRules && !s.cantStand && !s.knockedOut && s.knockedOutUntil <= 0f && c.ragdoll.state != Ragdoll.State.Standing && s.tox < 0)
            {
                s.stayDown = 0f;
                Guard("regen.getup", () => { SetCancelGetUp(s, false); c.locomotion.allowMove = true; if (c.ragdoll.state == Ragdoll.State.Inert) c.ragdoll.SetState(Ragdoll.State.Destabilized); });
            }
        }

        static bool HasBlackEye(NpcState s) { foreach (var e in s.blackEye) if (e > 0) return true; return false; }

        static RagdollPart AnyPartOf(Creature c, RagdollPart.Type t)
        {
            if (t == 0 || c.ragdoll == null) return null;
            foreach (var p in c.ragdoll.parts) if (p != null && Has(t, p.type)) return p;
            return null;
        }

        /// From Gore: a regenerating body's spine doesn't break - it cracks and he shrugs it off.
        bool SpineShrug(NpcState s, RagdollPart part, Vector3 point)
        {
            if (!RegenActive(s)) return false;
            if (Time.time < s.nextSpineShrug) return true;
            s.nextSpineShrug = Time.time + 1.5f;
            Sfx.PlayAt("NRE_BoneCrunch", point, 0.7f);
            s.pain = Mathf.Min(1f, s.pain + 0.3f);
            Guard("shrug", () => Stagger(s, part.transform.position - point, BrainModuleHitReaction.PushBehaviour.Effect.StaggerMedium, part.type));
            if (bioIndicators) Guard("shrug.puff", () => Puff(point, RegenGreen, 10, 0.2f));
            Log($"{WhoIs(s)}: spine-breaking blow - the serum holds it together");
            return true;
        }
    }
}

// Rage Engine 0.26 - more gore you can actually see.
//   TEETH          easier to knock out, up to 12, they skitter on the floor (new sound).
//   FINGER BONES   a broken hand shows it: 2-4 thin finger bones stick out of the knuckles and fingers, split skin.
//   RIBS           a rib-breaking body shot leaves 2-3 rib ends through the skin, torn flesh around them.
//   FACE CAVE-IN   blunt hits to the head add up (alive or dead): every solid hit dents the real head mesh a little
//                  deeper where it landed. Three stages - swollen and split, caved in (cheekbone/orbit collapses,
//                  teeth go), pulp. New textures and crunch sounds for each.
//   BLOOD SPRAY    optional Rage Engine blood spray instead of the game's effect (for when other blood mods turn the
//                  game's spray white). Gore > "Rage Engine blood spray".
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        [ModOptionCategory(C4, 4), ModOptionOrder(31)]
        [ModOption("Face cave-in", "Blunt hits to the head add up and really dent the face in (mesh deformation), in three stages.", defaultValueIndex = 1)]
        public static bool faceCave = true;
        [ModOptionCategory(C4, 4), ModOptionOrder(32)]
        [ModOption("Bones through the skin", "Finger bones out of broken hands, rib ends out of the chest.", defaultValueIndex = 1)]
        public static bool bonesOut = true;
        [ModOptionCategory(C4, 4), ModOptionOrder(33)]
        [ModOption("Rage Engine blood spray", "ON: the mod's own dark red spray instead of the game's blood effect. Turn ON if blood sprays come out white with other blood mods installed.", defaultValueIndex = 0)]
        public static bool ownBlood = false;

        static Material boneMat26, bloodMat26;
        static Material BoneMat26() => boneMat26 != null ? boneMat26 : boneMat26 = Mats.MakeTextured(TexGen.Bone(), TexGen.BoneNormal(), Color.white, 0.4f, false);

        /// A bone end sticking out of the body. The model's jagged end is at its origin, +Y = out of the break.
        GameObject PokeBone(NpcState s, RagdollPart part, Vector3 basePos, Vector3 dir, string model, float outLen, Vector3 scale)
        {
            var m = BoneModels.Get(model);
            if (m == null || part == null) return null;
            var go = new GameObject("RageEngine_BoneOut");
            go.transform.SetParent(part.transform, true);
            go.transform.position = basePos + dir.normalized * outLen;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var ls = part.transform.lossyScale;
            go.transform.localScale = new Vector3(scale.x / Mathf.Max(0.01f, ls.x), scale.y / Mathf.Max(0.01f, ls.y), scale.z / Mathf.Max(0.01f, ls.z));
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = BoneMat26(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.boneMeshes.Add(go);
            return go;
        }

        /// From BreakHand: individual finger bones through the skin.
        void FingerBones(NpcState s, RagdollPart handPart, Vector3 point, float severity)
        {
            if (!bonesOut || s.fingersOut >= 8) return;
            var c = s.creature;
            var hand = handPart as RagdollHand;
            Vector3 centre = handPart.transform.position;
            var spots = new List<Vector3>(); var axes = new List<Vector3>();
            if (hand != null && hand.fingers != null)
                foreach (var f in hand.fingers)
                {
                    if (f == null || f.tip == null) continue;
                    Vector3 tip = f.tip.position;
                    if ((tip - centre).sqrMagnitude < 0.0004f) continue;
                    spots.Add(Vector3.Lerp(centre, tip, Random.Range(0.45f, 0.8f))); axes.Add((tip - centre).normalized);
                }
            if (spots.Count == 0)
                for (int i = 0; i < 4; i++) { Vector3 a = (handPart.transform.forward + handPart.transform.right * (i - 1.5f) * 0.25f).normalized; spots.Add(centre + a * 0.07f); axes.Add(a); }
            int n = Mathf.Clamp(Random.Range(2, 4) + (severity > boneToughness * 1.8f ? 1 : 0), 2, spots.Count);
            for (int k = 0; k < n; k++)
            {
                int i = Random.Range(0, spots.Count);
                Vector3 at = spots[i], axis = axes[i];
                spots.RemoveAt(i); axes.RemoveAt(i);
                Vector3 side = Vector3.Cross(axis, Random.onUnitSphere).normalized;
                Vector3 dir = (side + axis * Random.Range(-0.3f, 0.6f)).normalized;
                PokeBone(s, handPart, at, dir, "RE_Bone_Metacarpal", Random.Range(0.012f, 0.024f), new Vector3(0.6f, 0.55f, 0.6f));
                Stamp(c, at, 0.02f, "knuckle_split", 0.95f);
                s.fingersOut++;
            }
            Stamp(c, point, 0.035f, "open_fracture", 0.9f);
            Stamp(c, centre, 0.04f, "blood_drip", 0.85f);
            Blood(handPart.transform);
            Sfx.Play("NRE_FingerSnap", handPart.transform, 1f);
            s.bleedRate += 0.08f * bleedingRate;
            Log($"{WhoIs(s)}: {n} finger bones through the skin");
        }

        /// From BluntTorso when a rib goes: rib ends through the chest wall.
        void RibsPoke(NpcState s, RagdollPart part, Vector3 point, float severity)
        {
            if (!bonesOut || s.ribsOut >= 3) return;
            var c = s.creature;
            s.ribsOut++;
            Vector3 outward = Vector3.ProjectOnPlane(point - part.transform.position, c.transform.up);
            outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : c.transform.forward;
            Vector3 skin = point;
            if (SkinAt(c, point, 0.08f, out var sp, out var sn, out _)) { skin = sp; outward = (outward + sn).normalized; }
            Vector3 up = c.transform.up, along = Vector3.Cross(up, outward).normalized;
            int n = severity >= boneToughness * 2f ? 3 : 2;
            for (int i = 0; i < n; i++)
            {
                Vector3 at = skin + up * ((i - (n - 1) * 0.5f) * 0.032f) + along * Random.Range(-0.01f, 0.01f);
                Vector3 dir = (outward + along * Random.Range(-0.5f, 0.5f) + up * Random.Range(-0.25f, 0.25f)).normalized;
                PokeBone(s, part, at - outward * 0.004f, dir, "RE_Bone_Rib", Random.Range(0.022f, 0.04f), Vector3.one);
            }
            Stamp(c, skin, 0.06f, "rib_wound", 0.95f);
            Stamp(c, skin - up * 0.05f, 0.05f, "blood_drip", 0.9f);
            Stamp(c, skin, 0.09f, "hematoma", 0.7f);
            Blood(part.transform); Blood(part.transform);
            Sfx.PlayAt("NRE_GoreTear", skin, 0.8f);
            s.bleedRate += 0.2f * bleedingRate;
            Log($"{WhoIs(s)}: {n} ribs through the chest wall");
        }

        /// From HandleHit: every blunt hit to the head. Accumulates and dents the real mesh, three stages.
        void FaceCave(NpcState s, RagdollPart head, Vector3 point, float severity, CollisionInstance hit)
        {
            if (!faceCave || severity < 0.5f) return;
            var c = s.creature;
            float now = Time.time;
            s.faceDmg += Mathf.Min(severity, 2.5f);
            if (now < s.nextFaceCave || severity < 0.7f || s.faceDmg < 1.5f) return;
            s.nextFaceCave = now + 0.25f;
            Transform bone = head.meshBone != null ? head.meshBone : head.bone != null ? head.bone.mesh : null;
            if (bone == null) return;
            Vector3 inward = hit.impactVelocity.sqrMagnitude > 0.1f ? hit.impactVelocity.normalized : (head.transform.position - point).normalized;
            inward = (inward + (head.transform.position - point).normalized).normalized;
            // every solid hit: a little deeper where it landed (total depth is capped by the stage logic below)
            if (s.faceDepth < 0.045f)
            {
                float depth = Mathf.Clamp(0.0025f + 0.0015f * s.faceDmg, 0.003f, 0.01f) * Mathf.Clamp(severity, 0.7f, 1.6f);
                s.faceDepth += depth;
                Guard("cave", () => HeadDeform.Dent(c, bone, point, inward, 0.05f, depth));
            }
            if (bloodyFace(s)) Sfx.Play("NRE_WetPunch", head.transform, 0.8f);
            // 0.28: keep going and the whole head goes
            if (!s.headCrushed && !Tank(s) && (s.faceDmg >= 14f || (s.faceStage >= 2 && severity >= 2.2f)))
            {
                Vector3 cdir = hit.impactVelocity.sqrMagnitude > 0.1f ? hit.impactVelocity.normalized : inward;
                CrushHead(s, head, point, cdir, Mathf.Max(severity, headCrushForce));
                return;
            }
            int stage = s.faceDmg >= 9f ? 3 : s.faceDmg >= 5f ? 2 : s.faceDmg >= 2.5f ? 1 : 0;
            if (stage <= s.faceStage) return;
            s.faceStage = stage;
            var ce = c.centerEyes;
            Vector3 fwd = ce != null ? ce.forward : head.transform.forward;
            if (stage == 1)
            {
                Stamp(c, point, 0.05f, "hematoma", 0.85f); Stamp(c, point, 0.03f, "split_skin", 0.95f);
                // 0.28.3: no swelling lump next to the dent
                Blood(head.transform);
                Log($"{WhoIs(s)}: face swollen and split open");
            }
            else if (stage == 2)
            {
                Guard("cave2", () => HeadDeform.Dent(c, bone, point, inward, 0.075f, 0.014f));
                s.faceDepth += 0.014f;
                Stamp(c, point, 0.07f, "crush_wound", 0.95f); Stamp(c, point, 0.09f, "face_pulp", 0.8f); Stamp(c, point - Vector3.up * 0.05f, 0.05f, "blood_drip", 0.9f);
                Sfx.Play("NRE_FaceCrush", head.transform, 1f);
                if (ce != null)
                {
                    Vector3 mouth = ce.position - ce.up * 0.1f + ce.forward * 0.05f;
                    int n = Random.Range(2, 5); s.teethLost += n;
                    SpawnGoreModels(mouth, fwd + Vector3.up * 0.2f, "RE_Tooth", n, ToothMat(), 1f, c);
                    Sfx.PlayAt("NRE_Teeth", mouth, 0.7f);
                }
                for (int i = 0; i < 2; i++) Blood(head.transform);
                var pt = point; Guard("ground", () => GroundBlood(pt, "blood_ground_splat", 0.3f, c));
                s.bleedRate += 0.25f * bleedingRate;
                if (!c.isKilled) { s.daze = Mathf.Max(s.daze, 1f); s.pain = 1f; }
                Log($"{WhoIs(s)}: FACE CAVED IN");
                RageMoment("face caved in");
            }
            else
            {
                Guard("cave3", () => { HeadDeform.Dent(c, bone, point, inward, 0.09f, 0.016f); if (ce != null) HeadDeform.Dent(c, bone, ce.position + ce.forward * 0.05f, -ce.forward, 0.07f, 0.012f); });
                s.faceDepth += 0.016f;
                Stamp(c, point, 0.1f, "face_pulp", 1f); Stamp(c, point + Random.onUnitSphere * 0.04f, 0.07f, "torn_flesh", 0.95f); Stamp(c, point, 0.06f, "open_fracture", 0.9f);
                Sfx.Play("NRE_FaceCrush", head.transform, 1f, 0.85f);
                SpawnGoreModels(point, -inward + Vector3.up * 0.3f, "RE_Skull_Frag", 2, BoneMat26(), 0.8f, c);
                for (int i = 0; i < 3; i++) Blood(head.transform);
                var pt = point; Guard("ground", () => GroundBlood(pt, "blood_pool", 0.35f, c));
                s.bleedRate += 0.4f * bleedingRate;
                Log($"{WhoIs(s)}: face beaten to pulp");
            }
        }

        static bool bloodyFace(NpcState s) => s.faceStage >= 1 || s.noseBroken || s.teethLost > 0;

        // ------------------------------------------------------------------ blood spray
        /// Blood at a body part: the game's effect, or the mod's own spray (option / fallback).
        internal static void Blood(Transform at)
        {
            if (at == null) return;
            if (ownBlood) Guard("ownblood", () => BloodBurst(at.position, Vector3.up, 16));
            else Effects.Blood(at);
        }

        static Texture2D bloodDot;
        internal static void BloodBurst(Vector3 at, Vector3 dir, int count)
        {
            var sh = SkinPaint.BlendShader;
            if (sh == null) return;
            if (bloodMat26 == null)
            {
                if (bloodDot == null) bloodDot = SoftDot();
                bloodMat26 = new Material(sh) { color = new Color(0.42f, 0.01f, 0.02f, 1f), mainTexture = bloodDot, renderQueue = 3050 };
            }
            var go = new GameObject("RageEngine_BloodBurst");
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir : Vector3.up);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = 0.3f; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f); main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.04f);
            main.startColor = new Color(0.42f, 0.01f, 0.02f, 0.95f);
            main.maxParticles = count; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 1.2f;
            var em = ps.emission; em.rateOverTime = 0f;
            var shp = ps.shape; shp.shapeType = ParticleSystemShapeType.Cone; shp.angle = 35f; shp.radius = 0.015f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = bloodMat26;
            ps.Emit(count);
            Object.Destroy(go, 1.5f);
        }
    }
}

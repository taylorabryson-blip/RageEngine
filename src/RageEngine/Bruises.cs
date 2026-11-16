// Nomad Rage Engine - bruising. Blunt hits (punches, kicks, maces, shield bashes, thrown objects) leave a bruise on the
// skin where they landed:
//   - fresh hits show red, darken to purple after a few seconds, and settle to a deep purple-blue
//   - hitting the same spot again makes the bruise bigger and darker; bruises follow the body part as it moves
//   - faces bruise too (cheeks, brows, jaw)
// The bruise is a thin swelling patch on the skin, drawn with the game's own skin shader recoloured (works on Quest).
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal sealed class Bruise
    {
        public GameObject go;
        public RagdollPart part;
        public float born, size;
        public int stage = -1;
        public MeshRenderer renderer;
    }

    public sealed partial class RageEngineScript
    {
        const int MaxBruisesPerNpc = 10;
        static Mesh bruiseMesh;
        static Material[] bruiseMats;
        static readonly Color[] BruiseStages =
        {
            new Color(0.62f, 0.16f, 0.16f),   // fresh: red
            new Color(0.45f, 0.14f, 0.30f),   // purple
            new Color(0.27f, 0.12f, 0.30f),   // deep purple
            new Color(0.20f, 0.13f, 0.28f),   // dark purple-blue
        };
        static readonly float[] StageAge = { 0f, 4f, 12f, 30f };

        void AddBruise(NpcState s, RagdollPart part, Vector3 point, Vector3 normal, float severity)
        {
            if (!bruising || part == null || bruiseIntensity <= 0f) return;
            float sev = severity * bruiseIntensity;
            if (sev < 0.25f) return;

            // outward surface normal at the hit
            Vector3 outward = point - part.transform.position;
            if (Vector3.Dot(normal, outward) < 0f) normal = -normal;
            if (normal.sqrMagnitude < 0.01f) normal = outward;
            normal.Normalize();

            // on the real skin: nearest skin vertex of the live mesh; under clothes/armour -> no visible bruise
            if (!SkinAt(s.creature, point, 0.07f, out var skinP, out var skinN, out bool covered) || covered) return;
            point = skinP; normal = skinN;

            // 0.17: painted INTO the skin texture - no geometry. A fresh bruise is reddish-purple; more hits there
            // deepen it (each stamp blends over the last). Only if painting isn't possible on this device do we fall
            // back to the flat decal below.
            bool staged = false;
            Guard("bruisestage", () => staged = goreTextures && BruiseStage(s, part, point, normal, sev));   // 0.22: classified stages + swelling
            if (staged) { s.bruisesPainted++; return; }
            float strength = Mathf.Clamp(0.25f + 0.25f * sev, 0.25f, 0.7f);
            Color tone = Color.Lerp(new Color(0.55f, 0.16f, 0.22f), new Color(0.28f, 0.12f, 0.32f), Mathf.Clamp01(sev - 0.6f));
            tone.a = strength;
            bool paintedOk = false;
            Guard("skinpaint", () => paintedOk = SkinPaint.Paint(s.creature, point, Mathf.Clamp(0.022f * (0.8f + sev), 0.02f, 0.06f), tone, TexGen.Bruise()));
            if (paintedOk) { s.bruisesPainted++; if (debugLog && s.bruisesPainted <= 3) Log($"{s.creature.name} bruise painted into the skin on {part.type}"); return; }
            return;                                                               // 0.28.3: no geometry patches (they read as 3D welts on Quest)

            // same spot again: grow + darken
            foreach (var b in s.bruises)
            {
                if (b.go == null || b.part != part) continue;
                if ((b.go.transform.position - point).sqrMagnitude < 0.06f * 0.06f)
                {
                    b.size = Mathf.Min(b.size + 0.25f * Mathf.Min(sev, 2f), 2.4f);
                    b.born -= 5f;                                         // looks older / darker
                    ApplySize(b);
                    return;
                }
            }

            if (s.bruises.Count >= MaxBruisesPerNpc)
            {
                var oldest = s.bruises[0];
                if (oldest.go != null) Object.Destroy(oldest.go);
                s.bruises.RemoveAt(0);
            }

            if (bruiseMesh == null) bruiseMesh = BuildBruiseMesh();
            if (bruiseMats == null) bruiseMats = MakeBruiseMaterials(part);

            var go = new GameObject("RageEngine_Bruise");
            go.transform.SetParent(part.transform, true);
            go.transform.position = point + normal * 0.0015f;                // a flat patch lying on the skin
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.AddComponent<MeshFilter>().sharedMesh = bruiseMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            var nb = new Bruise { go = go, part = part, born = Time.time, size = Mathf.Clamp(0.6f + 0.35f * sev, 0.6f, 1.6f), renderer = r };
            ApplySize(nb);
            SetStage(nb, 0);
            s.bruises.Add(nb);
            Log($"{s.creature.name} bruise on {part.type} size {nb.size:0.0}");
        }

        static void ApplySize(Bruise b)
        {
            float d = 0.032f * b.size;                               // ~3 cm across at size 1 (a real bruise, not a welt)
            b.go.transform.localScale = new Vector3(d / Mathf.Max(0.01f, b.go.transform.parent.lossyScale.x),
                                                     1f / Mathf.Max(0.01f, b.go.transform.parent.lossyScale.y),
                                                     d * Random.Range(0.75f, 1.1f) / Mathf.Max(0.01f, b.go.transform.parent.lossyScale.z));
        }

        static void SetStage(Bruise b, int stage)
        {
            if (b.stage == stage || bruiseMats == null) return;
            b.stage = stage;
            b.renderer.sharedMaterial = bruiseMats[stage];
        }

        void TickBruises(NpcState s)
        {
            if (s.bruises.Count == 0) return;
            float now = Time.time;
            for (int i = s.bruises.Count - 1; i >= 0; i--)
            {
                var b = s.bruises[i];
                if (b.go == null || b.part == null || b.part.isSliced) { if (b.go != null) Object.Destroy(b.go); s.bruises.RemoveAt(i); continue; }
                float age = (now - b.born) * Mathf.Lerp(1f, 1.6f, Mathf.InverseLerp(0.6f, 2.4f, b.size));
                int stage = 0;
                for (int k = StageAge.Length - 1; k >= 0; k--) if (age >= StageAge[k]) { stage = k; break; }
                SetStage(b, stage);
            }
        }

        /// Skin material clone, recoloured per stage (every colour property, textures removed, matte).
        static Material[] MakeBruiseMaterials(RagdollPart part)
        {
            Material src = null;
            var rend = part.ragdoll.creature.GetComponentInChildren<SkinnedMeshRenderer>();
            if (rend != null) src = rend.sharedMaterial;
            var mats = new Material[BruiseStages.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = Mats.MakeTextured(TexGen.Bruise(), null, BruiseStages[i], 0.2f, true);   // soft blotchy patch, transparent edges
            }
            return mats;
        }

        /// Soft dome patch (flattened hemisphere-ish disc) with a ragged edge.
        static Mesh BuildBruiseMesh()
        {
            // flat disc (no thickness) - the texture's soft alpha makes the edges blend into the skin
            const int segs = 24;
            var fv = new List<Vector3> { Vector3.zero }; var fuv = new List<Vector2> { new Vector2(0.5f, 0.5f) }; var ft = new List<int>();
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                fv.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
                fuv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < segs; i++) { ft.Add(0); ft.Add(1 + (i + 1) % segs); ft.Add(1 + i); }
            var flat = new Mesh { name = "RageEngine_BruiseDecal" };
            flat.SetVertices(fv); flat.SetUVs(0, fuv); flat.SetTriangles(ft, 0);
            flat.RecalculateNormals(); flat.RecalculateBounds();
            return flat;
        }

        static Mesh BuildBruiseDome()
        {
            const int seg = 14, rings = 4;
            var v = new List<Vector3>(); var tri = new List<int>(); var rnd = new System.Random(3);
            v.Add(new Vector3(0f, 1f, 0f));
            for (int r = 1; r <= rings; r++)
            {
                float t = r / (float)rings;
                float rad = Mathf.Sin(t * Mathf.PI * 0.5f) * 0.5f;
                float y = Mathf.Cos(t * Mathf.PI * 0.5f);
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    float edge = r == rings ? 0.8f + 0.35f * (float)rnd.NextDouble() : 1f;
                    v.Add(new Vector3(Mathf.Cos(a) * rad * edge, y, Mathf.Sin(a) * rad * edge));
                }
            }
            for (int i = 0; i < seg; i++) { tri.Add(0); tri.Add(1 + (i + 1) % seg); tri.Add(1 + i); }
            for (int r = 1; r < rings; r++)
                for (int i = 0; i < seg; i++)
                {
                    int a = 1 + (r - 1) * seg + i, b = 1 + (r - 1) * seg + (i + 1) % seg;
                    int c = a + seg, d = b + seg;
                    tri.Add(a); tri.Add(b); tri.Add(c);
                    tri.Add(b); tri.Add(d); tri.Add(c);
                }
            var uv = new List<Vector2>();
            foreach (var p in v) uv.Add(new Vector2(p.x + 0.5f, p.z + 0.5f));
            var m = new Mesh { name = "RageEngine_Bruise" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}

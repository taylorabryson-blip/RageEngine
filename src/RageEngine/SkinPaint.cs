// Rage Engine 0.17 - injuries painted INTO the skin texture (no extra geometry).
// Each NPC's skin material (body, head, hands) gets its own copy of its colour texture on the GPU (a RenderTexture -
// works even though the game's textures are compressed and unreadable). Bruises, black eyes and discolouration are
// stamped into that copy at the exact UV under the hit, using the bruise texture's soft alpha. The original texture is
// put back when the NPC despawns. If the device can't do it (no blend shader / no UVs), the old flat decal is used.
// Swelling uses REAL mesh deformation (HeadDeform.Dent with a negative depth pushes the skin outward).
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal static class SkinPaint
    {
        sealed class Painted { public Material mat; public string prop; public Texture original; public RenderTexture rt; }
        static readonly Dictionary<Creature, List<Painted>> painted = new Dictionary<Creature, List<Painted>>();
        static readonly Dictionary<SkinnedMeshRenderer, float> uvPerMetre = new Dictionary<SkinnedMeshRenderer, float>();
        static readonly string[] AlbedoNames = { "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_AlbedoMap", "_Diffuse" };
        static Material stamp;
        static bool stampTried, loggedProps;
        static readonly List<Vector3> v = new List<Vector3>();
        static readonly List<Vector2> uv = new List<Vector2>();
        static readonly List<int> tris = new List<int>();
        static readonly Dictionary<SkinnedMeshRenderer, Mesh> bake = new Dictionary<SkinnedMeshRenderer, Mesh>();
        static int MaxPaintedNpcs => RageEngineScript.PerfPainted;

        static Material Stamp()
        {
            if (stampTried) return stamp;
            stampTried = true;
            foreach (var n in new[] { "UI/Default", "Sprites/Default", "Unlit/Transparent", "Legacy Shaders/Particles/Alpha Blended", "Mobile/Particles/Alpha Blended" })
            {
                var sh = Shader.Find(n);
                if (sh != null) { stamp = new Material(sh); RageEngineScript.Log($"skin paint: using blend shader '{n}'"); break; }
            }
            if (stamp == null) RageEngineScript.Log("skin paint: no blend shader on this device - bruises fall back to flat decals");
            return stamp;
        }

        /// Paints a soft blob into the skin texture under 'point'. radius in metres; col.a = strength.
        /// Returns false if it couldn't (caller falls back).
        // 0.56: paint jobs asked for inside the physics step (hit callbacks) are done right after it, in the frame update -
        // render textures and Blits never run inside the physics step on the Quest.
        static readonly List<(Creature c, Vector3 p, float r, Color col, Texture2D b, bool cloth, float rot, Transform anchor, Vector3 local)> later = new List<(Creature, Vector3, float, Color, Texture2D, bool, float, Transform, Vector3)>();
        public static void Flush()
        {
            if (later.Count == 0) return;
            var jobs = later.ToArray(); later.Clear();
            foreach (var j in jobs)
            {
                if (j.c == null) continue;
                Vector3 at = j.anchor != null ? j.anchor.TransformPoint(j.local) : j.p;                  // the body moved since the hit
                try { PaintNow(j.c, at, j.r, j.col, j.b, j.cloth, j.rot); } catch (System.Exception e) { RageEngineScript.Log("skin paint (later): " + e.Message); }
            }
        }

        public static bool Paint(Creature c, Vector3 point, float radius, Color col, Texture2D brush, bool cloth = false, float rot = 0f)
        {
            if (c == null || brush == null || Stamp() == null) return false;
            if (Time.inFixedTimeStep)
            {
                if (later.Count >= 48) return false;
                Transform anchor = null; Vector3 local = point; float best = float.MaxValue;
                if (c.ragdoll != null && c.ragdoll.parts != null)
                    foreach (var part in c.ragdoll.parts) { if (part == null) continue; float d = (part.transform.position - point).sqrMagnitude; if (d < best) { best = d; anchor = part.transform; } }
                if (anchor != null) local = anchor.InverseTransformPoint(point);
                later.Add((c, point, radius, col, brush, cloth, rot, anchor, local));
                return true;
            }
            return PaintNow(c, point, radius, col, brush, cloth, rot);
        }

        static bool PaintNow(Creature c, Vector3 point, float radius, Color col, Texture2D brush, bool cloth, float rot)
        {
            if (c == null || brush == null || Stamp() == null) return false;
            if (!RageEngineScript.bruiseMarks && brush == TexGen.Bruise()) return false;          // 0.34: bruise circles off by default
            if (!painted.ContainsKey(c) && painted.Count >= MaxPaintedNpcs) return false;
            SkinnedMeshRenderer best = null; Vector2 bestUv = default; float bestD = 0.06f * 0.06f; Mesh bestMesh = null;
            foreach (var r in c.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || (cloth ? !LooksLikeCloth(r) : !LooksLikeSkin(r))) continue;
                if (!bake.TryGetValue(r, out var m) || m == null) { m = new Mesh(); bake[r] = m; }
                r.BakeMesh(m);
                m.GetVertices(v); m.GetUVs(0, uv);
                if (uv.Count != v.Count || uv.Count == 0) continue;
                Vector3 local = r.transform.InverseTransformPoint(point);
                for (int i = 0; i < v.Count; i++)
                {
                    float d = (v[i] - local).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = r; bestUv = uv[i]; bestMesh = m; }
                }
            }
            if (best == null) return false;
            bestMesh.GetVertices(v); bestMesh.GetUVs(0, uv);
            var p = Target(c, best);
            if (p == null) return false;
            float scale = UvScale(best, bestMesh);
            float ru = Mathf.Clamp(radius * scale, 0.004f, 0.12f);
            Blob(p.rt, bestUv, ru, col, brush, rot);
            return true;
        }

        static bool LooksLikeCloth(SkinnedMeshRenderer r)
        {
            string n = r.name.ToLowerInvariant();
            if (n.Contains("hair") || n.Contains("beard") || n.Contains("eye") || n.Contains("lash") || n.Contains("mouth") || n.Contains("vfx")) return false;
            return !LooksLikeSkin(r) || n.Contains("civilian") || n.Contains("cloth") || n.Contains("legs") || n.Contains("feet");
        }

        /// 0.19: does this NPC's head have facial blend shapes (pain / squint / frown)? Logged once; used if found.
        static bool loggedShapes;
        public static void FindFace(RageEngineScript.NpcState s)
        {
            var c = s.creature;
            foreach (var r in c.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r == null || r.sharedMesh == null || r.name.ToLowerInvariant().IndexOf("head") < 0) continue;
                var m = r.sharedMesh;
                if (!loggedShapes)
                {
                    loggedShapes = true;
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < m.blendShapeCount && i < 60; i++) sb.Append(m.GetBlendShapeName(i)).Append(' ');
                    RageEngineScript.Log($"face blend shapes on {r.name}: {(m.blendShapeCount == 0 ? "NONE" : sb.ToString())}");
                }
                for (int i = 0; i < m.blendShapeCount; i++)
                {
                    string n = m.GetBlendShapeName(i).ToLowerInvariant();
                    if (n.Contains("pain") || n.Contains("hurt") || n.Contains("frown") || n.Contains("squint") || n.Contains("angry") || n.Contains("grimace"))
                    { s.faceSmr = r; s.faceShape = i; return; }
                }
            }
        }

        static bool LooksLikeSkin(SkinnedMeshRenderer r)
        {
            string n = r.name.ToLowerInvariant();
            if (n.Contains("hair") || n.Contains("beard") || n.Contains("eye") || n.Contains("lash") || n.Contains("mouth") || n.Contains("vfx")) return false;
            if (n.Contains("civilian") || n.Contains("cloth") || n.Contains("armor") || n.Contains("legs") || n.Contains("feet")) return false;
            return n.Contains("head") || n.Contains("chest") || n.Contains("body") || n.Contains("hand") || n.Contains("skin");
        }

        /// This renderer's skin material, switched to an NPC-owned copy of its colour texture.
        static Painted Target(Creature c, SkinnedMeshRenderer r)
        {
            var mat = r.sharedMaterial;
            if (mat == null) return null;
            if (!painted.TryGetValue(c, out var list)) { list = new List<Painted>(); painted[c] = list; }
            foreach (var x in list) if (x.mat == mat) return x;
            if (!mat.name.Contains("(Instance)")) { mat = r.material; }                // never paint a material other NPCs share
            string prop = null; Texture tex = null;
            foreach (var n in AlbedoNames) if (mat.HasProperty(n) && mat.GetTexture(n) != null) { prop = n; tex = mat.GetTexture(n); break; }
            if (!loggedProps)
            {
                loggedProps = true;
                var sb = new System.Text.StringBuilder();
                foreach (var n in mat.GetTexturePropertyNames()) sb.Append(n).Append(' ');
                RageEngineScript.Log($"skin material '{mat.name}' shader '{mat.shader?.name}' textures: {sb}-> painting into {(prop ?? "NOTHING")}");
            }
            if (prop == null) return null;
            int w = Mathf.Min(tex.width, 1024), h = Mathf.Min(tex.height, 1024);
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "RageEngine_Skin", wrapMode = TextureWrapMode.Clamp };
            rt.Create();
            Graphics.Blit(tex, rt);
            var pnt = new Painted { mat = mat, prop = prop, original = tex, rt = rt };
            mat.SetTexture(prop, rt);
            list.Add(pnt);
            return pnt;
        }

        /// UV units per metre on this mesh (averaged over a sample of its triangles).
        static float UvScale(SkinnedMeshRenderer r, Mesh baked)
        {
            if (uvPerMetre.TryGetValue(r, out var s)) return s;
            baked.GetTriangles(tris, 0);
            float sum = 0f; int n = 0;
            var lossy = r.transform.lossyScale.x;
            for (int t = 0; t + 2 < tris.Count && n < 400; t += Mathf.Max(3, (tris.Count / 400) / 3 * 3))
            {
                int a = tris[t], b = tris[t + 1];
                float wd = Vector3.Distance(v[a], v[b]) * lossy;
                if (wd < 1e-5f) continue;
                sum += Vector2.Distance(uv[a], uv[b]) / wd; n++;
            }
            s = n > 0 ? sum / n : 1.5f;
            uvPerMetre[r] = s;
            return s;
        }

        /// 0.22: the alpha-blend shader skin painting uses (also for blood decals on the ground).
        internal static Shader BlendShader { get { var m = Stamp(); return m != null ? m.shader : null; } }

        static void Blob(RenderTexture rt, Vector2 at, float r, Color col, Texture2D brush, float rot = 0f)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadOrtho();
            stamp.mainTexture = brush;
            stamp.color = Color.white;
            stamp.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(col);
            float cs = Mathf.Cos(rot * Mathf.Deg2Rad) * r, sn = Mathf.Sin(rot * Mathf.Deg2Rad) * r;     // 0.22: rotated stamps
            GL.TexCoord2(0, 0); GL.Vertex3(at.x - cs + sn, at.y - sn - cs, 0);
            GL.TexCoord2(0, 1); GL.Vertex3(at.x - cs - sn, at.y - sn + cs, 0);
            GL.TexCoord2(1, 1); GL.Vertex3(at.x + cs - sn, at.y + sn + cs, 0);
            GL.TexCoord2(1, 0); GL.Vertex3(at.x + cs + sn, at.y + sn - cs, 0);
            GL.End();
            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        /// Puts every original skin texture back (despawn / re-use).
        public static void Restore(Creature c)
        {
            if (c == null || !painted.TryGetValue(c, out var list)) return;
            foreach (var p in list)
            {
                if (p.mat != null) p.mat.SetTexture(p.prop, p.original);
                if (p.rt != null) { p.rt.Release(); Object.Destroy(p.rt); }
            }
            painted.Remove(c);
            foreach (var r in c.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                uvPerMetre.Remove(r);
                if (r != null && bake.TryGetValue(r, out var m)) { if (m != null) Object.Destroy(m); bake.Remove(r); }
            }
        }

        /// One-time check whether this NPC's meshes can be deformed (logged for diagnosis).
        static bool loggedReadable;
        public static void LogReadable(Creature c)
        {
            if (loggedReadable || c == null) return;
            loggedReadable = true;
            var sb = new System.Text.StringBuilder();
            foreach (var r in c.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (r != null && r.sharedMesh != null && LooksLikeSkin(r)) sb.Append(r.name).Append(r.sharedMesh.isReadable ? "=readable " : "=LOCKED ");
            RageEngineScript.Log("mesh deformation check: " + sb);
        }
    }
}

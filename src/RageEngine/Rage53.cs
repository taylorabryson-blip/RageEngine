// Rage Engine 0.53 - back to what worked.
//   * NPC footing = the 0.22-0.25 rule (only cheap knockdowns are blocked, at the moment they happen; after 6 tries in
//     3 s the fall is let through). Nothing ever re-stands an NPC later, so no more NPCs frozen in the air.
//   * Status effects read by type name (fire = "Burning", lightning = "Electrocute"), so they work on this game version.
//   * Super soldiers: glowing eyes (emissive, visible on Quest) and a coloured light aura.
//   * Glass: shatters into a spray of jagged shards from the impact point; it takes a real hit to break.
using System.Collections.Generic;
using System.Reflection;
using ThunderRoad;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ================================================================== statuses by name (robust across game versions)
        internal static object StatusNamed53(Creature c, string typeName)
        {
            try
            {
                var l = c.Statuses; if (l == null) return null;
                for (int i = 0; i < l.Count; i++) { var st = l[i]; if (st != null && st.GetType().Name.Contains(typeName)) return st; }
            }
            catch { }
            return null;
        }
        static readonly Dictionary<(System.Type, string), MemberInfo> members53 = new Dictionary<(System.Type, string), MemberInfo>();
        static MemberInfo Member53(System.Type t, string n)
        {
            if (members53.TryGetValue((t, n), out var m)) return m;
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
            m = (MemberInfo)t.GetField(n, F) ?? t.GetProperty(n, F);
            return members53[(t, n)] = m;
        }
        internal static T Get53<T>(object o, string n, T fallback)
        {
            if (o == null) return fallback;
            try
            {
                var m = Member53(o.GetType(), n);
                object v = m is FieldInfo f ? f.GetValue(o) : m is PropertyInfo p ? p.GetValue(o) : null;
                return v is T t ? t : fallback;
            }
            catch { return fallback; }
        }
        internal static void Set53(object o, string n, object val)
        {
            if (o == null) return;
            try { var m = Member53(o.GetType(), n); if (m is FieldInfo f) f.SetValue(o, val); else if (m is PropertyInfo p && p.CanWrite) p.SetValue(o, val); } catch { }
        }
        internal static void ClearStatus53(Creature c, object st)
        {
            if (st == null) return;
            Set53(st, "Heat", 0f);
            var data = Get53<StatusData>(st, "data", null);
            if (data != null) Guard("status.clear", () => c.Clear(data));
        }

        // ================================================================== super soldier glow that shows on Quest
        static readonly Dictionary<int, Material> emissive53 = new Dictionary<int, Material>();
        internal static Material Emissive53(Color col)
        {
            int key = Mathf.RoundToInt(col.r * 255) | Mathf.RoundToInt(col.g * 255) << 8 | Mathf.RoundToInt(col.b * 255) << 16;
            if (emissive53.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Mats.Make(col, 0.9f)) { name = "RageEngine_Emissive" };
            try { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; m.SetColor("_EmissionColor", col * 3.5f); } catch { }
            return emissive53[key] = m;
        }
        /// called from UpdateBioVisuals: brighter eye glows + a coloured light around him (particles crash the Quest)
        internal static void AuraLight53(NpcState s, Color aura, Color eye)
        {
            var c = s.creature;
            var chest = ChestPart(c);
            if (chest != null)
            {
                var go = new GameObject("RageEngine_AuraLight"); go.transform.SetParent(chest.transform, false);
                var L = go.AddComponent<Light>(); L.type = LightType.Point; L.color = aura; L.range = 2.2f; L.intensity = 1.6f; L.shadows = LightShadows.None;
                go.AddComponent<AuraPulse53>();
                s.bioVisualObjs.Add(go);
            }
            var head = c.ragdoll.headPart;
            if (head != null)
            {
                var go = new GameObject("RageEngine_EyeLight"); go.transform.SetParent(head.transform, false);
                go.transform.position = (c.centerEyes != null ? c.centerEyes.position + c.centerEyes.forward * 0.05f : head.transform.position + c.transform.forward * 0.12f);
                var L = go.AddComponent<Light>(); L.type = LightType.Point; L.color = eye; L.range = 0.45f; L.intensity = 2.2f; L.shadows = LightShadows.None;
                s.bioVisualObjs.Add(go);
            }
        }

        // ================================================================== glass that actually shatters
        static Material glassShard53;
        internal static void Shatter53(Transform pane, Vector3 at, Vector3 vel, Material mat)
        {
            if (glassShard53 == null)
            {
                glassShard53 = new Material(Mats.Make(new Color(0.78f, 0.9f, 0.95f), 0.97f)) { name = "RageEngine_GlassShard" };
                try { glassShard53.EnableKeyword("_EMISSION"); glassShard53.SetColor("_EmissionColor", new Color(0.12f, 0.16f, 0.18f)); } catch { }
            }
            Vector3 size = pane.localScale;
            int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int a0 = thin == 0 ? 1 : 0, a1 = thin == 2 ? 1 : 2;
            Vector3 ax0 = pane.rotation * Axis53(a0), ax1 = pane.rotation * Axis53(a1), nrm = pane.rotation * Axis53(thin);
            float h0 = size[a0] * 0.5f, h1 = size[a1] * 0.5f;
            Vector3 c0 = pane.position;
            Vector3 rel = at - c0;
            float ix = Mathf.Clamp(Vector3.Dot(rel, ax0), -h0, h0), iy = Mathf.Clamp(Vector3.Dot(rel, ax1), -h1, h1);
            // radial crack pattern: rings x sectors around the impact, small near it, big at the frame
            int sectors = 11; float maxR = Mathf.Sqrt(h0 * h0 + h1 * h1) * 2f;
            float[] rings = { 0f, 0.05f, 0.13f, 0.26f, 0.45f, 0.75f, maxR };
            float[] ang = new float[sectors + 1];
            for (int k = 0; k <= sectors; k++) ang[k] = (k == sectors ? ang[0] + Mathf.PI * 2f : k * Mathf.PI * 2f / sectors + Random.Range(-0.18f, 0.18f));
            int made = 0;
            Vector3 dir = vel.sqrMagnitude > 0.01f ? vel.normalized : -nrm;
            if (Vector3.Dot(dir, nrm) < 0f) nrm = -nrm;
            for (int r = 0; r < rings.Length - 1; r++)
                for (int k = 0; k < sectors; k++)
                {
                    if (made > 60) break;
                    var poly = new List<Vector2>();
                    float r0 = rings[r] * Random.Range(0.9f, 1.1f), r1 = rings[r + 1] * Random.Range(0.9f, 1.1f);
                    foreach (var (rr, aa) in new[] { (r0, ang[k]), (r1, ang[k]), (r1, ang[k + 1]), (r0, ang[k + 1]) })
                        poly.Add(new Vector2(Mathf.Clamp(ix + Mathf.Cos(aa) * rr, -h0, h0), Mathf.Clamp(iy + Mathf.Sin(aa) * rr, -h1, h1)));
                    float area = Mathf.Abs((poly[2].x - poly[0].x) * (poly[3].y - poly[1].y) - (poly[3].x - poly[1].x) * (poly[2].y - poly[0].y)) * 0.5f;
                    if (area < 0.00015f) continue;
                    var cen = (poly[0] + poly[1] + poly[2] + poly[3]) * 0.25f;
                    var go = new GameObject("RageEngine_Shard");
                    go.transform.position = c0 + ax0 * cen.x + ax1 * cen.y;
                    go.transform.rotation = Quaternion.LookRotation(nrm, ax1);
                    var mesh = ShardMesh53(poly, cen, ax0, ax1, go.transform, 0.006f);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = glassShard53; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(Mathf.Max(0.02f, mesh.bounds.size.x), Mathf.Max(0.02f, mesh.bounds.size.y), 0.012f); bc.center = mesh.bounds.center;
                    var rb = go.AddComponent<Rigidbody>(); rb.mass = Mathf.Clamp(area * 8f, 0.02f, 0.6f);
                    float near = 1f - Mathf.Clamp01(cen.magnitude / 0.8f);
                    rb.velocity = dir * Random.Range(1.2f, 3.8f) * (0.4f + near) + Random.insideUnitSphere * 0.8f;
                    rb.angularVelocity = Random.insideUnitSphere * 9f;
                    Object.Destroy(go, Random.Range(14f, 22f));
                    made++;
                }
            // fine glitter: a burst of tiny chips
            for (int i = 0; i < 16; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "RageEngine_GlassChip";
                Object.Destroy(g.GetComponent<Collider>());
                g.transform.position = at + Random.insideUnitSphere * 0.05f; g.transform.rotation = Random.rotation; g.transform.localScale = Vector3.one * Random.Range(0.006f, 0.014f);
                g.GetComponent<Renderer>().sharedMaterial = glassShard53;
                var rb = g.AddComponent<Rigidbody>(); rb.mass = 0.01f; rb.velocity = (dir + Random.insideUnitSphere * 0.9f) * Random.Range(1.5f, 4.5f);
                Object.Destroy(g, Random.Range(1.5f, 3f));
            }
            Sfx.PlayAt("NRE_GlassBreak", at, 1f);
            Sfx.PlayAt("NRE_GlassBreak", at + dir * 0.6f, 0.6f);
        }
        static Vector3 Axis53(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        static Mesh ShardMesh53(List<Vector2> poly, Vector2 cen, Vector3 ax0, Vector3 ax1, Transform t, float thick)
        {
            // polygon in the pane plane, relative to the shard centre, extruded both ways (front, back, edges)
            var v = new List<Vector3>(); var tri = new List<int>();
            int n = poly.Count;
            Vector3 ToLocal(Vector2 p, float z) { Vector3 w = ax0 * (p.x - cen.x) + ax1 * (p.y - cen.y); var l = Quaternion.Inverse(t.rotation) * w; l.z = z; return l; }
            for (int i = 0; i < n; i++) v.Add(ToLocal(poly[i], -thick * 0.5f));
            for (int i = 0; i < n; i++) v.Add(ToLocal(poly[i], thick * 0.5f));
            for (int i = 1; i + 1 < n; i++) { tri.Add(0); tri.Add(i); tri.Add(i + 1); tri.Add(n); tri.Add(n + i + 1); tri.Add(n + i); }
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; tri.Add(i); tri.Add(n + i); tri.Add(j); tri.Add(j); tri.Add(n + i); tri.Add(n + j); }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }

    /// slow breathing pulse on the aura light
    public sealed class AuraPulse53 : MonoBehaviour
    {
        Light L; float baseI, ph;
        void Awake() { L = GetComponent<Light>(); baseI = L != null ? L.intensity : 1f; ph = Random.value * 6f; }
        void Update() { if (L != null) L.intensity = baseI * (0.75f + 0.25f * Mathf.Sin(Time.time * 2.2f + ph)); }
    }
}

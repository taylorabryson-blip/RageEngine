// Rage Engine 0.41 - vomiting you can see and hear on Quest: solid chunks out of the mouth (lit game material, no
// particle shaders), a puddle that spreads where it lands, real recorded retching/vomiting (CC0, see SOUND_CREDITS.md).
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static Mesh blob41; static readonly List<GameObject> puddles41 = new List<GameObject>();
        static readonly Color VomitCol = new Color(0.58f, 0.5f, 0.18f), VomitDark = new Color(0.42f, 0.36f, 0.12f);

        static Mesh Blob41()
        {
            if (blob41 != null) return blob41;
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); blob41 = g.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(g);
            return blob41;
        }

        /// One heave: a burst of chunks and liquid out of the mouth.
        static void Spurt41(Vector3 mouth, Vector3 dir, Creature c, bool first)
        {
            int n = perfMode == "p" ? 6 : 11;
            var mat = Mats.Make(VomitCol, 0.75f); var mat2 = Mats.Make(VomitDark, 0.6f);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("RageEngine_Vomit");
                go.transform.position = mouth + dir * 0.09f + Random.insideUnitSphere * 0.02f;
                float sz = Random.Range(0.012f, 0.03f);
                go.transform.localScale = new Vector3(sz, sz * Random.Range(0.6f, 1f), sz);
                go.AddComponent<MeshFilter>().sharedMesh = Blob41();
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = (i % 3 == 0) ? mat2 : mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var vb = go.AddComponent<VomitBlob44>();                                     // 0.44: lands on the floor, never on his body
                vb.vel = (dir + Random.insideUnitSphere * 0.25f).normalized * Random.Range(1.4f, 2.8f);
            }
            if (FloorBelow44(mouth + dir * 0.35f, out var hit))
                Puddle41(hit.point + Vector3.ProjectOnPlane(dir, Vector3.up).normalized * Random.Range(0.15f, 0.4f), hit.normal, first);
        }

        static void Puddle41(Vector3 at, Vector3 normal, bool fresh)
        {
            GameObject pud = null;
            foreach (var p in puddles41) if (p != null && (p.transform.position - at).sqrMagnitude < 0.5f * 0.5f) { pud = p; break; }
            if (pud == null)
            {
                pud = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pud.name = "RageEngine_VomitPuddle";
                Object.Destroy(pud.GetComponent<Collider>());
                pud.GetComponent<MeshRenderer>().sharedMaterial = Mats.Make(VomitCol, 0.9f);
                pud.transform.position = at + normal * 0.004f; pud.transform.up = normal;
                pud.transform.localScale = new Vector3(0.12f, 0.003f, 0.1f);
                puddles41.Add(pud); Object.Destroy(pud, 90f);
                int cap = perfMode == "p" ? 4 : 10;
                while (puddles41.Count > cap) { if (puddles41[0] != null) Object.Destroy(puddles41[0]); puddles41.RemoveAt(0); }
                Sfx.PlayAt("NRE_VomitSplat", at, 0.8f);
            }
            var s = pud.transform.localScale;
            pud.transform.localScale = new Vector3(Mathf.Min(0.75f, s.x + 0.09f), s.y, Mathf.Min(0.6f, s.z + 0.07f));
        }
    }
}

// Rage Engine 0.13 - anatomy.
//   - Real bones: open fractures use real human bone meshes (BodyParts3D, CT-derived, CC BY-SA 2.1 JP) shipped in the
//     mod's Models folder: humerus (upper arm), radius + ulna (forearm), femur (thigh), tibia + fibula (shin), clavicle.
//     Each has a jagged spiral break and marrow, and sticks out of the wound the way a compound fracture does: the
//     broken end through the skin, the shaft angled back into the limb along the bone's natural line.
//   - Skin surface: everything that sits on the body (bruises, torn craters, bone exits) is placed on the NPC's real
//     skin, found from the live skinned mesh (nearest skin vertex + its normal), not on the physics collider (which sits
//     inside the body - that's why 0.12's bruises floated off the face as welts). Covered by clothes -> no bruise.
using System.Collections.Generic;
using System.Globalization;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal static class BoneModels
    {
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        internal static Mesh Get(string name) => meshes.TryGetValue(name, out var m) ? m : null;

        public static void Load(string modFolder)
        {
            if (string.IsNullOrEmpty(modFolder)) return;
            string dir = System.IO.Path.Combine(modFolder, "Models");
            if (!System.IO.Directory.Exists(dir)) { Debug.LogWarning("[RageEngine] no Models folder - bones fall back to the simple shard"); return; }
            foreach (var f in System.IO.Directory.GetFiles(dir, "*.obj"))
            {
                try
                {
                    var m = ParseObj(System.IO.File.ReadAllLines(f));
                    m.name = System.IO.Path.GetFileNameWithoutExtension(f);
                    meshes[m.name] = m;
                }
                catch (System.Exception e) { Debug.LogWarning("[RageEngine] bone model " + f + ": " + e.Message); }
            }
            Debug.Log("[RageEngine] bone models loaded: " + string.Join(", ", meshes.Keys));
        }

        static Mesh ParseObj(string[] lines)
        {
            var v = new List<Vector3>(); var vt = new List<Vector2>();
            var outV = new List<Vector3>(); var outUV = new List<Vector2>(); var tri = new List<int>();
            var map = new Dictionary<long, int>();
            var ci = CultureInfo.InvariantCulture;
            foreach (var raw in lines)
            {
                if (raw.Length < 2) continue;
                var p = raw.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p[0] == "v") v.Add(new Vector3(float.Parse(p[1], ci), float.Parse(p[2], ci), float.Parse(p[3], ci)));
                else if (p[0] == "vt") vt.Add(new Vector2(float.Parse(p[1], ci), float.Parse(p[2], ci)));
                else if (p[0] == "f")
                {
                    var idx = new int[p.Length - 1];
                    for (int k = 1; k < p.Length; k++)
                    {
                        var s = p[k].Split('/');
                        int vi = int.Parse(s[0]) - 1, ti = s.Length > 1 && s[1].Length > 0 ? int.Parse(s[1]) - 1 : vi;
                        long key = ((long)vi << 32) | (uint)ti;
                        if (!map.TryGetValue(key, out int o))
                        {
                            o = outV.Count; map[key] = o;
                            outV.Add(v[vi]); outUV.Add(ti < vt.Count ? vt[ti] : Vector2.zero);
                        }
                        idx[k - 1] = o;
                    }
                    for (int k = 1; k + 1 < idx.Length; k++) { tri.Add(idx[0]); tri.Add(idx[k]); tri.Add(idx[k + 1]); }
                }
            }
            var m = new Mesh();
            if (outV.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(outV); m.SetUVs(0, outUV); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// The right bone for a body part (by its name in the rig and its type).
        public static Mesh For(RagdollPart part)
        {
            if (meshes.Count == 0) return null;
            string n = part.name.ToLowerInvariant();
            var t = part.type;
            string key = null;
            if (RageEngineScript.Has(t, RagdollPart.Type.LeftArm | RagdollPart.Type.RightArm))
                key = n.Contains("fore") || n.Contains("lower") || n.Contains("elbow") ? "RE_Bone_Forearm" : "RE_Bone_Humerus";
            else if (RageEngineScript.Has(t, RagdollPart.Type.LeftLeg | RagdollPart.Type.RightLeg))
                key = n.Contains("upleg") || n.Contains("thigh") || n.Contains("upper") ? "RE_Bone_Femur"
                    : n.Contains("calf") || n.Contains("shin") || n.Contains("lower") || n.Contains("knee") ? "RE_Bone_Shin"
                    : (part.parentPart != null && RageEngineScript.Has(part.parentPart.type, RagdollPart.Type.LeftLeg | RagdollPart.Type.RightLeg) ? "RE_Bone_Shin" : "RE_Bone_Femur");
            else if (RageEngineScript.Has(t, RagdollPart.Type.Torso | RagdollPart.Type.Neck)) key = "RE_Bone_Clavicle";
            if (key == null) return null;
            // arms: a forearm part's parent is the upper arm
            if (key == "RE_Bone_Humerus" && part.parentPart != null && RageEngineScript.Has(part.parentPart.type, RagdollPart.Type.LeftArm | RagdollPart.Type.RightArm))
                key = "RE_Bone_Forearm";
            meshes.TryGetValue(key, out var m);
            RageEngineScript.Log($"bone model for {part.name} ({t}): {key}");
            return m;
        }
    }

    public sealed partial class RageEngineScript
    {
        static readonly List<Vector3> bakeV = new List<Vector3>(), bakeN = new List<Vector3>();
        static readonly Dictionary<Creature, SkinnedMeshRenderer[]> skinCache = new Dictionary<Creature, SkinnedMeshRenderer[]>();
        static readonly Dictionary<SkinnedMeshRenderer, Mesh> bakeCache = new Dictionary<SkinnedMeshRenderer, Mesh>();
        static bool loggedRenderers;

        /// Nearest point on the NPC's real skin (and its outward normal). covered = the nearest surface there is
        /// clothing/armour, not skin. Returns false if nothing usable is within maxDist.
        internal static bool SkinAt(Creature c, Vector3 near, float maxDist, out Vector3 pos, out Vector3 normal, out bool covered)
        {
            pos = near; normal = Vector3.up; covered = false;
            if (c == null) return false;
            if (!skinCache.TryGetValue(c, out var rends) || rends == null)
            {
                rends = c.GetComponentsInChildren<SkinnedMeshRenderer>();
                skinCache[c] = rends;
                if (!loggedRenderers)
                {
                    loggedRenderers = true;
                    var sb = new System.Text.StringBuilder();
                    foreach (var r in rends) sb.Append(r.name).Append('[').Append(r.sharedMaterial != null ? r.sharedMaterial.name : "-").Append(IsSkin(r) ? ",skin" : "").Append("] ");
                    Log("NPC renderers: " + sb);
                }
            }
            float best = float.MaxValue; bool bestSkin = false; Vector3 bp = near, bn = Vector3.up;
            float bestSkinD = float.MaxValue; Vector3 sp = near, sn = Vector3.up;
            foreach (var r in rends)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!bakeCache.TryGetValue(r, out var mesh) || mesh == null) { mesh = new Mesh(); bakeCache[r] = mesh; }
                r.BakeMesh(mesh);
                mesh.GetVertices(bakeV); mesh.GetNormals(bakeN);
                var tr = r.transform;
                Vector3 local = tr.InverseTransformPoint(near);
                bool skin = IsSkin(r);
                for (int i = 0; i < bakeV.Count; i++)
                {
                    float d = (bakeV[i] - local).sqrMagnitude;
                    if (d < best) { best = d; bestSkin = skin; bp = tr.TransformPoint(bakeV[i]); bn = i < bakeN.Count ? tr.TransformDirection(bakeN[i]) : Vector3.up; }
                    if (skin && d < bestSkinD) { bestSkinD = d; sp = tr.TransformPoint(bakeV[i]); sn = i < bakeN.Count ? tr.TransformDirection(bakeN[i]) : Vector3.up; }
                }
            }
            if (bestSkinD == float.MaxValue) { pos = bp; normal = bn.normalized; covered = !bestSkin; return Mathf.Sqrt(best) <= maxDist; }
            pos = sp; normal = sn.normalized;
            covered = !bestSkin && Mathf.Sqrt(bestSkinD) - Mathf.Sqrt(best) > 0.012f;      // clothing sits over the skin here
            return Mathf.Sqrt(bestSkinD) <= maxDist;
        }

        static bool IsSkin(SkinnedMeshRenderer r)
        {
            string n = r.name.ToLowerInvariant();
            if (n.Contains("hair") || n.Contains("beard") || n.Contains("eye") || n.Contains("brow") || n.Contains("lash") || n.Contains("teeth") || n.Contains("tongue")) return false;
            if (n.Contains("body") || n.Contains("head") || n.Contains("face") || n.Contains("skin") || n.Contains("hand") || n.Contains("naked")) return true;
            var m = r.sharedMaterial;
            if (m != null)
            {
                string mn = (m.name + " " + (m.shader != null ? m.shader.name : "")).ToLowerInvariant();
                if (mn.Contains("skin") || mn.Contains("body") || mn.Contains("head")) return true;
            }
            return false;
        }

        static void ForgetSkin(Creature c)
        {
            if (c == null || !skinCache.TryGetValue(c, out var rends)) return;
            if (rends != null)
                foreach (var r in rends) if (r != null && bakeCache.TryGetValue(r, out var m)) { if (m != null) Object.Destroy(m); bakeCache.Remove(r); }
            skinCache.Remove(c);
        }

        /// Direction of the limb this part belongs to (from this joint toward the next one).
        internal static Vector3 LimbAxis(RagdollPart part)
        {
            var rag = part.ragdoll;
            if (rag != null && rag.parts != null)
                foreach (var p in rag.parts)
                    if (p != null && p.parentPart == part)
                    {
                        Vector3 d = p.transform.position - part.transform.position;
                        if (d.sqrMagnitude > 1e-4f) return d.normalized;
                    }
            if (part.parentPart != null)
            {
                Vector3 d = part.transform.position - part.parentPart.transform.position;
                if (d.sqrMagnitude > 1e-4f) return d.normalized;
            }
            return part.transform.up;
        }
    }
}

// Rage Engine 0.15 - skull crushing by real mesh deformation. 0.10-0.14 squashed the head's bone scale, which
// deformed the whole head and could stick on a re-used (pooled) NPC. Now the NPC gets its own copy of its skinned
// mesh, and only the vertices skinned to the head are pushed in around the impact - a dent shaped by the blow, with
// a jagged rim - while skull chunks and gore fly out. The original mesh is always put back when the NPC despawns.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal static class HeadDeform
    {
        sealed class Swap { public SkinnedMeshRenderer r; public Mesh original, copy; }
        static readonly Dictionary<Creature, List<Swap>> swaps = new Dictionary<Creature, List<Swap>>();
        static bool loggedUnreadable;

        /// Caves the head in around point. dir = direction of the blow (into the head). radius/depth in metres.
        /// Returns how many renderers were deformed.
        public static int Dent(Creature c, Transform headBone, Vector3 point, Vector3 dir, float radius, float depth)
        {
            if (c == null || headBone == null) return 0;
            int done = 0;
            if (!swaps.TryGetValue(c, out var list)) { list = new List<Swap>(); swaps[c] = list; }
            foreach (var r in c.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r == null || r.sharedMesh == null) continue;
                var bones = r.bones;
                int hi = -1;
                for (int i = 0; i < bones.Length; i++) if (bones[i] == headBone) { hi = i; break; }
                if (hi < 0) continue;
                var swap = list.Find(x => x.r == r);
                Mesh src = swap != null ? swap.copy : r.sharedMesh;
                if (!src.isReadable)
                {
                    if (!loggedUnreadable) { loggedUnreadable = true; RageEngineScript.Log($"head mesh {src.name} is not readable - dent skipped (chunks only)"); }
                    continue;
                }
                var bw = src.boneWeights;
                var bind = src.bindposes;
                if (bw == null || bw.Length != src.vertexCount || hi >= bind.Length) continue;

                // world -> head bone local -> mesh (bind) space
                Matrix4x4 toMesh = bind[hi].inverse * headBone.worldToLocalMatrix;
                Vector3 p = toMesh.MultiplyPoint3x4(point);
                Vector3 d = toMesh.MultiplyVector(dir);
                float scale = d.magnitude;                                      // world metres -> mesh units
                if (scale < 1e-6f) continue;
                d /= scale;
                float rad = radius * scale, dep = depth * scale;

                if (swap == null)
                {
                    var copy = Object.Instantiate(r.sharedMesh);
                    copy.name = r.sharedMesh.name + "_RageEngineCrushed";
                    swap = new Swap { r = r, original = r.sharedMesh, copy = copy };
                    list.Add(swap);
                    src = copy;
                }
                var v = src.vertices;
                int moved = 0;
                for (int i = 0; i < v.Length; i++)
                {
                    float w = HeadWeight(bw[i], hi);
                    if (w < 0.2f) continue;
                    float dist = Vector3.Distance(v[i], p);
                    if (dist > rad) continue;
                    float f = 1f - dist / rad;
                    f = f * f * (3f - 2f * f);                                  // smooth bowl
                    // jagged rim: broken skull plates don't cave smoothly
                    float jag = 0.75f + 0.5f * Mathf.PerlinNoise(v[i].x * 90f / scale, v[i].z * 90f / scale);
                    v[i] += d * (dep * f * jag * w);
                    moved++;
                }
                if (moved == 0) continue;
                src.vertices = v;
                src.RecalculateNormals();
                src.RecalculateBounds();
                r.sharedMesh = src;
                done++;
            }
            return done;
        }

        static float HeadWeight(BoneWeight b, int hi)
        {
            float w = 0f;
            if (b.boneIndex0 == hi) w += b.weight0;
            if (b.boneIndex1 == hi) w += b.weight1;
            if (b.boneIndex2 == hi) w += b.weight2;
            if (b.boneIndex3 == hi) w += b.weight3;
            return w;
        }

        /// Puts the creature's original meshes back (despawn / re-use).
        public static void Restore(Creature c)
        {
            if (c == null || !swaps.TryGetValue(c, out var list)) return;
            foreach (var sw in list)
            {
                if (sw.r != null && sw.original != null) sw.r.sharedMesh = sw.original;
                if (sw.copy != null) Object.Destroy(sw.copy);
            }
            swaps.Remove(c);
        }
    }
}

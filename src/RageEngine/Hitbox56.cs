// Rage Engine 0.56 - Rage Engine items get their OWN hitboxes.
// The item's vanilla model was already hidden; now its vanilla colliders are switched off too and new ones are built from
// our model: the length of the model is cut into slices and each slice gets a box that fits it (so a curved khopesh or a
// sabre has a curved hitbox, a trident has prongs, a mace has its head). The model is NOT stretched to fit anything any
// more: it is placed at its real size with its own grip in your hand. The game's damage points are moved to match: the
// stab point to our tip, the cutting points along our edge, and stab depth to our blade's length.
// Treasures and the two rare finds get one box per part.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static readonly string[] GripParts56 = { "_Wrap", "_Grip", "_Pole", "_Wood", "_Bronze" };

        /// What we changed on the base item, so a pooled item that comes back as something else gets it all back.
        sealed class Swap56
        {
            public readonly List<Collider> vanilla = new List<Collider>();
            public readonly Dictionary<ColliderGroup, List<Collider>> lists = new Dictionary<ColliderGroup, List<Collider>>();
            public readonly List<GameObject> ours = new List<GameObject>();
            public readonly List<(Damager d, Vector3 lp, Quaternion lr, float pen, Collider only)> dmg = new List<(Damager, Vector3, Quaternion, float, Collider)>();
        }

        static Swap56 Remember56(Item it)
        {
            var sw = new Swap56();
            foreach (var cg in it.colliderGroups) if (cg != null && cg.colliders != null) { sw.lists[cg] = new List<Collider>(cg.colliders); foreach (var c in cg.colliders) if (c != null && c.enabled) sw.vanilla.Add(c); }
            if (it.collisionHandlers != null) foreach (var ch in it.collisionHandlers) if (ch != null && ch.damagers != null) foreach (var d in ch.damagers) if (d != null) sw.dmg.Add((d, d.transform.localPosition, d.transform.localRotation, d.penetrationLength, d.colliderOnly));
            return sw;
        }

        /// Every second: keep the base's colliders off (the game may switch colliders back on, e.g. after a holster).
        static void KeepOff56(Swap56 sw) { foreach (var c in sw.vanilla) if (c != null && c.enabled) c.enabled = false; }

        static void Restore56(Swap56 sw)
        {
            if (sw == null) return;
            foreach (var g in sw.ours) if (g != null) Object.Destroy(g);
            foreach (var kv in sw.lists) if (kv.Key != null && kv.Key.colliders != null) { kv.Key.colliders.Clear(); kv.Key.colliders.AddRange(kv.Value); }
            foreach (var c in sw.vanilla) if (c != null) c.enabled = true;
            foreach (var x in sw.dmg) if (x.d != null) { x.d.transform.localPosition = x.lp; x.d.transform.localRotation = x.lr; x.d.penetrationLength = x.pen; x.d.colliderOnly = x.only; }
        }

        /// All our model's vertices in root space (root has no scale relative to the world).
        static List<Vector3> RootVerts56(Transform root, string onlySuffix = null)
        {
            var l = new List<Vector3>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (onlySuffix != null && !mf.name.EndsWith(onlySuffix)) continue;
                var vs = mf.sharedMesh.vertices; var t = mf.transform;
                for (int i = 0; i < vs.Length; i += (vs.Length > 3000 ? 3 : 1)) l.Add(root.InverseTransformPoint(t.TransformPoint(vs[i])));
            }
            return l;
        }

        static bool BoundsOf56(List<Vector3> v, out Bounds b)
        {
            b = default; if (v == null || v.Count == 0) return false;
            b = new Bounds(v[0], Vector3.zero); foreach (var p in v) b.Encapsulate(p); return true;
        }

        static Handle MainHandle56(Item it) => it.mainHandleRight != null ? it.mainHandleRight : (it.handles != null && it.handles.Count > 0 ? it.handles[0] : null);

        /// Weapons: our grip on the game's grip, blade pointing the way the base's blade pointed, edge the same side,
        /// real size; then our own hitboxes and damage points. False = couldn't (the caller falls back to the old fit).
        static bool RebuildBlade56(Item it, Arm55 a)
        {
            var root = a.root; var h = MainHandle56(it);
            if (h == null || it.colliderGroups == null || it.colliderGroups.Count == 0) return false;
            Transform g = h.transform; Vector3 gp = g.position, ax = g.up;
            // which way along the grip is the blade, and which side is the edge: from the base's colliders (as 0.55 did)
            var corners = ItemCorners55(it); if (corners.Count == 0) return false;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var c in corners) { float t = Vector3.Dot(c - gp, ax); lo = Mathf.Min(lo, t); hi = Mathf.Max(hi, t); }
            var tip = Deep55(it.transform, "Pierce");
            bool flip = tip != null ? Vector3.Dot(tip.position - gp, ax) < 0f : -lo > hi;
            if (flip) { ax = -ax; float t0 = lo; lo = -hi; hi = -t0; }
            Vector3 width = EdgeDir56(corners, gp, ax, lo, hi, g);
            // our grip: the wrap / grip / pole part, in model space
            Bounds gripB = default; bool haveGrip = false;
            foreach (var suf in GripParts56) { if (BoundsOf56(RootVerts56(root, suf), out gripB)) { haveGrip = true; break; } }
            if (!BoundsOf56(RootVerts56(root), out var all)) return false;
            if (!haveGrip) gripB = new Bounds(new Vector3(0f, 0f, all.min.z + 0.08f), Vector3.one * 0.05f);
            Vector3 gripLocal = new Vector3(0f, 0f, gripB.center.z);
            // place: real size, our grip centre on the game's grip centre
            var ls = it.transform.lossyScale;
            root.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));
            root.rotation = Quaternion.LookRotation(ax, width);
            root.position = gp - root.rotation * gripLocal;
            a.s = 1f;
            // where the base's blade was (along the grip axis) vs where ours is: to move damage points along the blade
            float ourLo = all.min.z - gripLocal.z, ourHi = all.max.z - gripLocal.z;        // relative to the grip centre
            float baseLo = lo, baseHi = hi;
            // ---- hitboxes: slices along the blade
            var verts = RootVerts56(root);
            float len = all.max.z - all.min.z;
            int n = Mathf.Clamp(Mathf.RoundToInt(len / 0.05f), 6, 28);
            var boxes = new List<(Vector3 c, Vector3 size, float z)>();
            for (int k = 0; k < n; k++)
            {
                float z0 = all.min.z + len * k / n, z1 = all.min.z + len * (k + 1) / n;
                float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue; int cnt = 0;
                foreach (var p in verts) { if (p.z < z0 || p.z > z1) continue; cnt++; x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); }
                if (cnt < 3) continue;
                boxes.Add((new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(Mathf.Max(0.008f, x1 - x0), Mathf.Max(0.008f, y1 - y0), (z1 - z0) * 1.02f), (z0 + z1) * 0.5f));
            }
            if (boxes.Count == 0) return false;
            // each slice goes into the base's collider group that covered the same stretch of the weapon (handle / blade)
            var groups = new List<(ColliderGroup g, float lo, float hi, Collider sample)>();
            foreach (var cg in it.colliderGroups)
            {
                if (cg == null || cg.colliders == null || cg.colliders.Count == 0) continue;
                float glo = float.MaxValue, ghi = float.MinValue; Collider sample = null;
                var cl = new List<Vector3>();
                foreach (var col in cg.colliders) { if (col == null) continue; if (sample == null) sample = col; cl.Clear(); Corners55(col, cl); foreach (var c in cl) { float t = Vector3.Dot(c - gp, ax); glo = Mathf.Min(glo, t); ghi = Mathf.Max(ghi, t); } }
                if (sample != null) groups.Add((cg, glo, ghi, sample));
            }
            if (groups.Count == 0) return false;
            var sw = a.swap = Remember56(it);
            var made = new Dictionary<ColliderGroup, List<Collider>>();
            Collider tipBox = null; float tipZ = float.MinValue;
            foreach (var bx in boxes)
            {
                float rel = bx.z - gripLocal.z;                                                      // along the axis from the grip centre
                float frac = Mathf.InverseLerp(ourLo, ourHi, rel), baseT = Mathf.Lerp(baseLo, baseHi, frac);
                var best = groups[0]; float bestD = float.MaxValue;
                foreach (var gr in groups) { float d = baseT < gr.lo ? gr.lo - baseT : baseT > gr.hi ? baseT - gr.hi : 0f; if (d < bestD) { bestD = d; best = gr; } }
                var go = new GameObject("RE_Hitbox");
                go.layer = best.sample.gameObject.layer;
                go.transform.SetParent(best.g.transform, true);
                go.transform.position = root.TransformPoint(bx.c); go.transform.rotation = root.rotation;
                var gl = best.g.transform.lossyScale; go.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, gl.x), 1f / Mathf.Max(0.01f, gl.y), 1f / Mathf.Max(0.01f, gl.z));
                var box = go.AddComponent<BoxCollider>(); box.size = bx.size; box.sharedMaterial = best.sample.sharedMaterial; sw.ours.Add(go);
                if (!made.TryGetValue(best.g, out var list)) made[best.g] = list = new List<Collider>();
                list.Add(box);
                if (bx.z > tipZ) { tipZ = bx.z; tipBox = box; }
            }
            // swap: the base's colliders off, ours in
            foreach (var cg in it.colliderGroups)
            {
                if (cg == null || cg.colliders == null) continue;
                if (!made.TryGetValue(cg, out var ours)) { foreach (var col in cg.colliders) if (col != null) col.enabled = false; continue; }   // a group with none of our slices: off
                foreach (var col in cg.colliders) if (col != null) col.enabled = false;
                cg.colliders.Clear(); cg.colliders.AddRange(ours);
            }
            // ---- damage points: stab point on our tip, cutting points moved along our blade, stab depth = our blade
            float ourBlade = all.max.z - gripB.max.z, baseBlade = Mathf.Max(0.05f, baseHi - 0.12f);
            if (it.collisionHandlers != null)
                foreach (var ch in it.collisionHandlers)
                {
                    if (ch == null || ch.damagers == null) continue;
                    foreach (var dm in ch.damagers)
                    {
                        if (dm == null) continue;
                        var dt = dm.transform;
                        float t = Vector3.Dot(dt.position - gp, ax);
                        Vector3 lateral = (dt.position - gp) - ax * t;
                        float frac = Mathf.InverseLerp(baseLo, baseHi, t);
                        bool pierce = dm.direction == Damager.Direction.Forward && Vector3.Dot(dt.forward, ax) > 0.7f;
                        if (pierce)
                        {
                            dt.position = root.TransformPoint(new Vector3(0f, 0f, all.max.z - 0.01f));
                            dm.penetrationLength = Mathf.Clamp(dm.penetrationLength * (ourBlade / baseBlade), 0.02f, Mathf.Max(0.05f, ourBlade));
                            if (dm.isColliderOnly && tipBox != null) dm.colliderOnly = tipBox;
                        }
                        else dt.position = root.TransformPoint(new Vector3(0f, 0f, gripLocal.z + Mathf.Lerp(ourLo, ourHi, frac))) + lateral;
                        if (dm.colliderGroup != null && made.ContainsKey(dm.colliderGroup) == false && dm.isColliderOnly == false)
                        {
                            // its group lost all its colliders: give it the blade slices (the ones past the grip)
                            var blade = new List<Collider>(); foreach (var kv in made) foreach (var col in kv.Value) if (root.InverseTransformPoint(col.transform.position).z > gripB.max.z) blade.Add(col);
                            if (blade.Count > 0 && dm.colliderGroup.colliders != null) { dm.colliderGroup.colliders.Clear(); dm.colliderGroup.colliders.AddRange(blade); }
                        }
                    }
                }
            Guard("hitbox.com", () => it.ResetCenterOfMass());
            Log($"{a.def.id}: own hitbox - {boxes.Count} slices, {len * 100f:0} cm long, grip at {(gripLocal.z - all.min.z) * 100f:0} cm");
            return true;
        }

        /// The base blade's edge direction (as in 0.55's fit).
        static Vector3 EdgeDir56(List<Vector3> corners, Vector3 gp, Vector3 ax, float lo, float hi, Transform g)
        {
            Vector3 u = Vector3.Cross(ax, Mathf.Abs(Vector3.Dot(ax, Vector3.up)) < 0.9f ? Vector3.up : Vector3.forward).normalized, v = Vector3.Cross(ax, u);
            double sxx = 0, syy = 0, sxy = 0, mx = 0, my = 0; int n = 0;
            foreach (var c in corners)
            {
                float t = Vector3.Dot(c - gp, ax); if (t < lo + (hi - lo) * 0.45f) continue;
                var d = c - gp - ax * t; double x = Vector3.Dot(d, u), y = Vector3.Dot(d, v);
                mx += x; my += y; sxx += x * x; syy += y * y; sxy += x * y; n++;
            }
            Vector3 width = Vector3.ProjectOnPlane(g.forward, ax).normalized;
            if (n > 2)
            {
                mx /= n; my /= n; double cxx = sxx / n - mx * mx, cyy = syy / n - my * my, cxy = sxy / n - mx * my;
                double ang = 0.5 * System.Math.Atan2(2 * cxy, cxx - cyy);
                width = (u * (float)System.Math.Cos(ang) + v * (float)System.Math.Sin(ang)).normalized;
                if (mx * System.Math.Cos(ang) + my * System.Math.Sin(ang) < 0) width = -width;
            }
            if (width.sqrMagnitude < 0.5f) width = Vector3.ProjectOnPlane(Vector3.up, ax).normalized;
            return width;
        }

        /// Treasures and hand tools: real size, centred on the item (tools: our grip part on the game's grip), one box per part.
        static bool RebuildSmall56(Item it, Arm55 a)
        {
            var root = a.root;
            if (it.colliderGroups == null || it.colliderGroups.Count == 0) return false;
            ColliderGroup cg = null; Collider sample = null;
            foreach (var g0 in it.colliderGroups) if (g0 != null && g0.colliders != null && g0.colliders.Count > 0) { cg = g0; foreach (var c in g0.colliders) if (c != null) { sample = c; break; } if (sample != null) break; }
            if (cg == null || sample == null) return false;
            if (!BoundsOf56(RootVerts56(root), out var all)) return false;
            var ls = it.transform.lossyScale;
            root.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));
            root.localRotation = Quaternion.identity; a.s = 1f;
            var h = MainHandle56(it);
            if (a.def.fit == Fit55.Grip && h != null)
            {
                Bounds gb = default; bool got = false;
                foreach (var suf in GripParts56) if (BoundsOf56(RootVerts56(root, suf), out gb)) { got = true; break; }
                Vector3 gl = got ? gb.center : all.center;
                root.position = h.transform.position - root.rotation * gl;
            }
            else
            {
                // centre our model where the base's colliders were
                var corners = ItemCorners55(it);
                Vector3 c0 = it.transform.position; if (corners.Count > 0) { var b = new Bounds(corners[0], Vector3.zero); foreach (var c in corners) b.Encapsulate(c); c0 = b.center; }
                root.position = c0 - root.rotation * all.center;
            }
            var ours = new List<Collider>();
            var sw = a.swap = Remember56(it);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var pv = RootVerts56(root, mf.name); if (!BoundsOf56(pv, out var pb) || pb.size.sqrMagnitude < 1e-6f) continue;
                var go = new GameObject("RE_Hitbox"); go.layer = sample.gameObject.layer;
                go.transform.SetParent(cg.transform, true);
                go.transform.position = root.TransformPoint(pb.center); go.transform.rotation = root.rotation;
                var gs = cg.transform.lossyScale; go.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, gs.x), 1f / Mathf.Max(0.01f, gs.y), 1f / Mathf.Max(0.01f, gs.z));
                var box = go.AddComponent<BoxCollider>(); box.size = Vector3.Max(pb.size, Vector3.one * 0.006f); box.sharedMaterial = sample.sharedMaterial; sw.ours.Add(go);
                ours.Add(box);
            }
            if (ours.Count == 0) { Restore56(sw); a.swap = null; return false; }
            foreach (var g0 in it.colliderGroups) if (g0 != null && g0.colliders != null) { foreach (var c in g0.colliders) if (c != null) c.enabled = false; if (g0 != cg) g0.colliders.Clear(); }
            cg.colliders.Clear(); cg.colliders.AddRange(ours);
            Guard("hitbox.com", () => it.ResetCenterOfMass());
            Log($"{a.def.id}: own hitbox - {ours.Count} boxes, {all.size.x * 100f:0}x{all.size.y * 100f:0}x{all.size.z * 100f:0} cm");
            return true;
        }
    }
}

// Rage Engine 0.42 - maps:
//   LIGHTING   the map is cut into 12 m cells (each cell its own mesh), so every part of the map gets ITS nearby lights -
//              before, one giant mesh per material could only take the few lights the renderer allows per object, so
//              most lamps lit nothing. Lights are brighter and reach further, and the per-object light limit is raised
//              while you're in a map (put back when you leave). Performance mode keeps the old single meshes.
//   GLASS      on Quest (no see-through shader) panes get a frame and light streaks so you can see the glass is there;
//              punch it, swing at it or throw someone through it.
//   TRAIN      "TRAIN" line: a subway train that runs through the station on a loop - pulls in, waits, pulls out. It's
//              solid and heavy: get hit by it and you know about it (so do NPCs).
//   DOORS      saloon batwing doors swing both ways, flap back and creak.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    /// The train: a kinematic body on a loop along X. Cars are children with colliders.
    public sealed class Train42 : MonoBehaviour
    {
        public float xStart, xEnd, xStop, speed = 11f, wait = 10f, gap = 8f, len = 20f;
        Rigidbody rb; float x, v, waitUntil, restartAt; int phase;   // 0 arriving, 1 waiting, 2 leaving, 3 out of sight
        Vector3 basePos; AudioSource hum; float nextHorn;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            basePos = transform.localPosition; x = xStart; v = speed; phase = 0;
            RageEngineScript.Guard("train.hum", () =>
            {
                if (!Sfx.TryClip("NRE_Train", out var clip)) return;
                hum = gameObject.AddComponent<AudioSource>(); hum.clip = clip; hum.loop = true; hum.spatialBlend = 1f; hum.rolloffMode = AudioRolloffMode.Linear;
                hum.minDistance = 4f; hum.maxDistance = 45f; hum.volume = 0f; hum.Play();
            });
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime, now = Time.time;
            float dir = Mathf.Sign(xEnd - xStart);
            switch (phase)
            {
                case 0:     // brake into the station
                {
                    float left = (xStop - x) * dir;
                    v = Mathf.Max(1.2f, Mathf.Min(speed, Mathf.Sqrt(Mathf.Max(0f, 2f * 1.6f * left))));
                    x += dir * v * dt;
                    if ((xStop - x) * dir <= 0.02f) { x = xStop; v = 0f; phase = 1; waitUntil = now + wait; }
                    if (now >= nextHorn && Mathf.Abs(x - xStop) < 40f && Mathf.Abs(x - xStop) > 25f) { nextHorn = now + 30f; Sfx.PlayAt("NRE_TrainHorn", transform.position, 0.9f); }
                    break;
                }
                case 1: if (now >= waitUntil) phase = 2; break;
                case 2:     // pull out and speed up
                    v = Mathf.Min(speed, v + 1.4f * dt); x += dir * v * dt;
                    if ((xEnd - x) * dir <= 0f) { phase = 3; restartAt = now + gap; }
                    break;
                case 3: if (now >= restartAt) { x = xStart; v = speed; phase = 0; if (rb != null) rb.position = transform.parent.TransformPoint(basePos + Vector3.right * (x - basePos.x)); } break;
            }
            var lp = basePos; lp.x = x;
            var wp = transform.parent != null ? transform.parent.TransformPoint(lp) : lp;
            if (rb != null && phase != 3) rb.MovePosition(wp); else transform.position = wp;
            if (hum != null) { hum.volume = Mathf.Clamp01(v / speed) * 0.8f; hum.pitch = 0.75f + 0.35f * Mathf.Clamp01(v / speed); }
        }

        void OnCollisionEnter(Collision c) => Hit(c);
        void OnCollisionStay(Collision c) => Hit(c);
        float nextHit;
        void Hit(Collision col)
        {
            if (v < 2f || col == null || col.collider == null || Time.time < nextHit) return;
            var cr = col.collider.GetComponentInParent<Creature>();
            if (cr == null || cr.isPlayer || cr.isKilled) return;
            nextHit = Time.time + 0.2f;
            Vector3 push = Vector3.right * Mathf.Sign(xEnd - xStart) * v + Vector3.up * 2f;
            RageEngineScript.Guard("train.hit", () =>
            {
                cr.ragdoll.SetState(Ragdoll.State.Destabilized);
                foreach (var p in cr.ragdoll.parts) if (p != null && p.physicBody != null) p.physicBody.AddForce(push * 0.9f, ForceMode.VelocityChange);
                Sfx.PlayAt("NRE_BoneCrunch", col.collider.transform.position, 1f);
                if (v > 6f) cr.Kill(); else cr.Damage(v * 12f);
            });
        }
    }

    public sealed partial class RageEngineScript
    {
        const float Cell42 = 12f;

        /// Adds a box to the per-material, per-cell meshes, cut on the cell grid so each piece lands in its own cell.
        static void AddChunked42(Dictionary<string, MeshAcc> acc, string mat, Vector3 mn, Vector3 mx, float uvs)
        {
            bool chunk = perfMode != "p";
            float cs = chunk ? Cell42 : 1e6f;
            int ix0 = Mathf.FloorToInt(mn.x / cs), ix1 = Mathf.FloorToInt((mx.x - 0.0001f) / cs);
            int iz0 = Mathf.FloorToInt(mn.z / cs), iz1 = Mathf.FloorToInt((mx.z - 0.0001f) / cs);
            if (ix1 - ix0 > 12 || iz1 - iz0 > 12) { ix1 = ix0; iz1 = iz0; }                  // something enormous: leave it whole
            for (int ix = ix0; ix <= ix1; ix++)
                for (int iz = iz0; iz <= iz1; iz++)
                {
                    Vector3 a = mn, b = mx;
                    if (ix1 > ix0) { a.x = Mathf.Max(mn.x, ix * cs); b.x = Mathf.Min(mx.x, (ix + 1) * cs); }
                    if (iz1 > iz0) { a.z = Mathf.Max(mn.z, iz * cs); b.z = Mathf.Min(mx.z, (iz + 1) * cs); }
                    if (b.x - a.x < 0.0001f || b.z - a.z < 0.0001f) continue;
                    var a3 = new MeshAcc[3];
                    for (int q = 0; q < 3; q++) { string ak = mat + "|" + q + "|" + ix + "|" + iz; if (!acc.TryGetValue(ak, out a3[q])) acc[ak] = a3[q] = new MeshAcc(); }
                    AddBox(a3[0], a3[1], a3[2], a, b, uvs);
                }
        }

        // ------------------------------------------------------------------ lights
        static object urpAsset42; static System.Reflection.PropertyInfo urpMaxProp42; static int urpMaxOrig42 = -1;
        void LightPass42(Transform root)
        {
            int n = 0;
            float boost = perfMode == "p" ? 1.1f : 1.35f, reach = perfMode == "p" ? 1.05f : 1.25f;
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (l == null) continue;
                l.intensity *= boost; l.range *= reach; n++;
                l.renderMode = n <= 8 ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
            }
            int fill = 0; Guard("map.fill", () => fill = FillLights42(root));
            string urp = "no URP";
            try
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                if (rp != null)
                {
                    var t = rp.GetType();
                    var mode = t.GetProperty("additionalLightsRenderingMode");
                    var max = t.GetProperty("maxAdditionalLightsCount");
                    string modeName = mode != null ? mode.GetValue(rp, null)?.ToString() : "?";
                    if (max != null && max.CanWrite && modeName != "Disabled")
                    {
                        int cur = (int)max.GetValue(rp, null);
                        if (urpMaxOrig42 < 0) { urpMaxOrig42 = cur; urpAsset42 = rp; urpMaxProp42 = max; }
                        int want = perfMode == "p" ? Mathf.Max(cur, 4) : Mathf.Max(cur, 8);
                        if (want != cur) max.SetValue(rp, want, null);
                        urp = $"URP additional lights {modeName}, per object {cur} -> {want}";
                    }
                    else urp = $"URP additional lights {modeName} (left alone)";
                }
            }
            catch (System.Exception e) { urp = "URP: " + e.Message; }
            Debug.Log($"[RageEngine] map lighting: {n} lights x{boost} brightness x{reach} reach, {fill} fill lights added; {urp}");
        }

        /// Every map: finds each floor (every walkable surface with headroom, stacked floors too) on a 7 m grid and puts a
        /// soft fill light over any spot no map light reaches - no more black corners, stairwells or back rooms.
        int FillLights42(Transform root)
        {
            Physics.SyncTransforms();
            var have = new List<Vector3>();
            foreach (var l in root.GetComponentsInChildren<Light>(true)) if (l != null) have.Add(l.transform.position);
            var cols = root.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0) return 0;
            Bounds b = cols[0].bounds; foreach (var c in cols) if (c != null && !c.isTrigger) b.Encapsulate(c.bounds);
            int max = perfMode == "p" ? 10 : perfMode == "q" ? 36 : 20, added = 0;
            float step = 7f, top = b.max.y + 1f;
            var hits = new RaycastHit[12];
            for (float x = b.min.x + step * 0.5f; x < b.max.x && added < max; x += step)
                for (float z = b.min.z + step * 0.5f; z < b.max.z && added < max; z += step)
                {
                    int n = Physics.RaycastNonAlloc(new Vector3(x, top, z), Vector3.down, hits, top - b.min.y + 1f, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n && added < max; i++)
                    {
                        var h = hits[i];
                        if (h.collider == null || !h.collider.transform.IsChildOf(root) || h.normal.y < 0.8f) continue;
                        // headroom: open floor with a ceiling (or sky) at least 2.4 m up
                        float head = 6f;
                        if (Physics.Raycast(h.point + Vector3.up * 0.1f, Vector3.up, out var up, 6f, ~0, QueryTriggerInteraction.Ignore)) head = up.distance;
                        if (head < 2.4f) continue;
                        Vector3 at = h.point + Vector3.up * Mathf.Min(head - 0.4f, 2.8f);
                        bool lit = false;
                        foreach (var p in have) if ((p - at).sqrMagnitude < 6.5f * 6.5f && Mathf.Abs(p.y - at.y) < 3.5f) { lit = true; break; }
                        if (lit) continue;
                        var go = new GameObject("FillLight"); go.transform.SetParent(root, true); go.transform.position = at;
                        var lt = go.AddComponent<Light>(); lt.type = LightType.Point; lt.shadows = LightShadows.None;
                        lt.color = new Color(1f, 0.93f, 0.82f); lt.range = 8.5f; lt.intensity = 0.75f; lt.renderMode = LightRenderMode.Auto;
                        have.Add(at); added++;
                        FillFixture44(root, at);
                    }
                }
            return added;
        }

        static void LightRestore42()
        {
            if (urpMaxOrig42 < 0 || urpAsset42 == null || urpMaxProp42 == null) return;
            try { urpMaxProp42.SetValue(urpAsset42, urpMaxOrig42, null); } catch { }
            urpMaxOrig42 = -1; urpAsset42 = null; urpMaxProp42 = null;
        }

        // ------------------------------------------------------------------ glass you can see on Quest
        void GlassLook42(GameObject pane, GlassPane26 gp)
        {
            Vector3 size = pane.transform.localScale;
            int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            if (thin == 1) return;                                                          // a skylight: leave it clear
            int along = thin == 0 ? 2 : 0;
            var mat = MapMat26("glass_streak", 0);
            gp.mat = mat;
            // two diagonal streaks of reflected light, in the pane's own (unit cube) space
            for (int k = 0; k < 2; k++)
            {
                var st = new GameObject("Streak"); st.transform.SetParent(pane.transform, false);
                st.AddComponent<MeshFilter>().sharedMesh = Cube26();
                var r = st.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Vector3 ls = Vector3.one; ls[thin] = 0.35f; ls[along] = k == 0 ? 0.06f : 0.03f; ls[1] = 0.85f;
                st.transform.localScale = ls;
                Vector3 lp = Vector3.zero; lp[along] = k == 0 ? -0.18f : 0.02f;
                st.transform.localPosition = lp;
                Vector3 axis = Vector3.zero; axis[thin] = 1f;
                st.transform.localRotation = Quaternion.AngleAxis(28f, axis);
            }
            // a thin frame along the bottom and top edges
            for (int k = 0; k < 2; k++)
            {
                var fr = new GameObject("Frame"); fr.transform.SetParent(pane.transform, false);
                fr.AddComponent<MeshFilter>().sharedMesh = Cube26();
                var r = fr.AddComponent<MeshRenderer>(); r.sharedMaterial = MapMat26("steel", 1); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Vector3 ls = Vector3.one; ls[1] = Mathf.Min(0.04f / Mathf.Max(size.y, 0.05f), 0.2f); ls[thin] = 1.6f;
                fr.transform.localScale = ls;
                fr.transform.localPosition = new Vector3(0f, k == 0 ? -0.5f + ls[1] * 0.5f : 0.5f - ls[1] * 0.5f, 0f);
            }
        }

        // ------------------------------------------------------------------ the train
        /// TRAIN mat xStart xEnd xStop z0 z1 y0 y1 cars speed wait
        void BuildTrain42(Transform root, string[] p, int layer)
        {
            string mat = p[1];
            float xs = PF(p[2]), xe = PF(p[3]), xstop = PF(p[4]), z0 = PF(p[5]), z1 = PF(p[6]), y0 = PF(p[7]), y1 = PF(p[8]);
            int cars = p.Length > 9 ? Mathf.Clamp(int.Parse(p[9]), 1, 6) : 3;
            float speed = p.Length > 10 ? PF(p[10]) : 11f, wait = p.Length > 11 ? PF(p[11]) : 10f;
            const float carLen = 6.4f, carGap = 0.5f;
            float total = cars * carLen + (cars - 1) * carGap;
            var go = new GameObject("Train"); go.layer = layer;
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(xs, 0f, 0f);
            float zc = (z0 + z1) * 0.5f, w = z1 - z0, h = y1 - y0;
            for (int i = 0; i < cars; i++)
            {
                float cx = -total * 0.5f + carLen * 0.5f + i * (carLen + carGap);
                var body = Block(go.transform, "Car", new Vector3(cx, y0 + h * 0.5f, zc), new Vector3(carLen, h, w), mat, true, layer);
                // windows band, doors, roof, the undercarriage
                Block(go.transform, "Windows", new Vector3(cx, y0 + h * 0.62f, zc), new Vector3(carLen * 0.92f, h * 0.26f, w + 0.02f), "train_window", false, layer);
                Block(go.transform, "Roof", new Vector3(cx, y1 + 0.06f, zc), new Vector3(carLen, 0.12f, w * 0.92f), "steel", false, layer);
                Block(go.transform, "Under", new Vector3(cx, y0 - 0.2f, zc), new Vector3(carLen * 0.85f, 0.4f, w * 0.7f), "iron_black", false, layer);
                for (int d = -1; d <= 1; d += 2) Block(go.transform, "Door", new Vector3(cx + d * carLen * 0.22f, y0 + h * 0.45f, zc), new Vector3(0.9f, h * 0.8f, w + 0.03f), "train_door", false, layer);
                if (i == 0 || i == cars - 1)
                {
                    float endX = cx + (i == 0 ? -1f : 1f) * (carLen * 0.5f + 0.01f);
                    var lg = new GameObject("Headlight"); lg.transform.SetParent(go.transform, false); lg.transform.localPosition = new Vector3(endX, y0 + h * 0.3f, zc);
                    var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.95f, 0.8f); l.range = 9f; l.intensity = 1.4f; l.shadows = LightShadows.None;
                }
                var il = new GameObject("CarLight"); il.transform.SetParent(go.transform, false); il.transform.localPosition = new Vector3(cx, y1 - 0.4f, zc);
                var li = il.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(0.85f, 0.95f, 1f); li.range = 5f; li.intensity = 0.9f; li.shadows = LightShadows.None;
            }
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.mass = 30000f;
            var tr = go.AddComponent<Train42>();
            tr.xStart = xs; tr.xEnd = xe; tr.xStop = xstop; tr.speed = speed; tr.wait = wait; tr.len = total; tr.gap = Random.Range(6f, 12f);
            Debug.Log($"[RageEngine] map: train {cars} cars, {xs}->{xe}, stops at {xstop}");
        }
    }
}

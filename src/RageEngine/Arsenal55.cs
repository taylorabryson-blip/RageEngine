// Rage Engine 0.55 - faction weapons, Crystal Hunt valuables and two rare finds.
//
// LOOK: every item is a clone of a vanilla item (its JSON names the vanilla prefab), so the grips, colliders, damage,
// AI handling and physics are the game's own. The vanilla renderers are hidden and our Blender model is put on in their
// place: weapons are fitted along the base weapon's handle axis to the length of its colliders, with the blade plane
// and edge side matched to the base's blade; treasures are matched axis-for-axis to the base treasure's colliders.
// WHERE THEY COME FROM: added at runtime to the game's own loot tables - the Baron's weapon racks, the weapon kits
// of each faction's NPCs (so those NPCs spawn carrying them), and the dungeon treasure tables. The shop code (Shop54)
// also stocks the weapons.
// RARE FINDS: the Hollow Eye Crystal only turns up in Eye dungeons (1 run in 8), the Captain's Compass in any
// dungeon (1 in 20): the roll is made once per dungeon, and the next treasure that appears becomes it.
using System;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RageEngine
{
    public partial class RageEngineScript
    {
        enum Fit55 { Blade, Treasure, Grip }

        sealed class Part55
        {
            public string mesh, tex; public bool v54; public Color col; public float smooth, glow;
            public Part55(string mesh, string tex, Color col, float smooth = 0.5f, bool v54 = false, float glow = 0f) { this.mesh = mesh; this.tex = tex; this.col = col; this.smooth = smooth; this.v54 = v54; this.glow = glow; }
        }

        sealed class Def55
        {
            public string id, faction; public Fit55 fit; public int tier; public Part55[] parts;
            public Def55(string id, string faction, Fit55 fit, int tier, params Part55[] parts) { this.id = id; this.faction = faction; this.fit = fit; this.tier = tier; this.parts = parts; }
        }

        static readonly Color Gold55 = new Color(0.84f, 0.66f, 0.28f), Brass55 = new Color(0.62f, 0.44f, 0.22f), Steel55 = new Color(0.72f, 0.74f, 0.76f),
                              Dark55 = new Color(0.12f, 0.12f, 0.14f), Enamel55 = new Color(0.08f, 0.2f, 0.62f), Violet55 = new Color(0.55f, 0.2f, 0.95f);

        internal const string HollowEyeId55 = "RE_HollowEye_Crystal", CompassId55 = "RE_Compass_Captain";

        static readonly Def55[] Defs55 =
        {
            // ---- weapons
            new Def55("RE_Blackwake_Outlaw", "Outlaws", Fit55.Blade, 2,
                new Part55("RE_Blackwake_Blade", "steel_old.png", Steel55, 0.45f), new Part55("RE_Blackwake_Brass", "bronze.png", Brass55, 0.7f, true), new Part55("RE_Blackwake_Grip", "leather_wrap.png", Dark55, 0.3f, true)),
            new Def55("RE_Briarhook_Wildfolk", "Wildfolk", Fit55.Blade, 2,
                new Part55("RE_Briarhook_Wood", "ash.png", new Color(0.58f, 0.5f, 0.4f), 0.3f), new Part55("RE_Briarhook_Head", "iron.png", Steel55, 0.5f, true), new Part55("RE_Briarhook_Wrap", "leather.png", new Color(0.4f, 0.26f, 0.16f), 0.3f, true)),
            new Def55("RE_KingsOath_Eraden", "Eraden", Fit55.Blade, 3,
                new Part55("RE_KingsOath_Blade", "blade_eu.png", Steel55, 0.85f), new Part55("RE_KingsOath_Gold", "gold.png", Gold55, 0.8f), new Part55("RE_KingsOath_Grip", "leather_blue.png", Enamel55, 0.4f),
                new Part55("RE_KingsOath_Enamel", null, Enamel55, 0.9f), new Part55("RE_KingsOath_Crest", "signet_crest.png", Enamel55, 0.85f)),
            new Def55("RE_Chancellor_Eraden", "Eraden", Fit55.Blade, 3,
                new Part55("RE_Chancellor_Steel", "gunmetal.png", Steel55, 0.7f), new Part55("RE_Chancellor_Gold", "gold.png", Gold55, 0.8f), new Part55("RE_Chancellor_Grip", "leather_blue.png", Enamel55, 0.4f)),
            new Def55("RE_Observer_Eye", "The Eye", Fit55.Blade, 3,
                new Part55("RE_Observer_Blade", "blackblade.png", Dark55, 0.75f), new Part55("RE_Observer_Guard", "gunmetal.png", Dark55, 0.7f), new Part55("RE_Observer_Grip", "leather_wrap.png", Dark55, 0.3f, true),
                new Part55("RE_Observer_Crystal", null, Violet55, 0.95f, false, 2.5f)),
            new Def55("RE_Glaive_Rakta", "Rakta", Fit55.Blade, 3,
                new Part55("RE_Glaive_Pole", "lacquer.png", new Color(0.3f, 0.12f, 0.09f), 0.85f), new Part55("RE_Glaive_Wrap", "ito.png", Dark55, 0.4f), new Part55("RE_Glaive_Iron", "gunmetal.png", Dark55, 0.7f),
                new Part55("RE_Glaive_Gold", "gold.png", Gold55, 0.8f), new Part55("RE_Glaive_Gem", null, new Color(0.85f, 0.06f, 0.08f), 0.95f, false, 2f), new Part55("RE_Glaive_Blade", "steel.png", Steel55, 0.85f)),
            new Def55("RE_AshenTalon_Rakta", "Rakta", Fit55.Blade, 2,
                new Part55("RE_AshenTalon_Blade", "steel.png", Steel55, 0.85f), new Part55("RE_AshenTalon_Iron", "gunmetal.png", Dark55, 0.7f), new Part55("RE_AshenTalon_Gold", "gold.png", Gold55, 0.8f),
                new Part55("RE_AshenTalon_Grip", "ito.png", new Color(0.6f, 0.6f, 0.62f), 0.4f)),
            new Def55("RE_Tidebreaker_Sentari", "Sentari", Fit55.Blade, 3,
                new Part55("RE_Tidebreaker_Pole", "walnut.png", new Color(0.35f, 0.2f, 0.12f), 0.55f), new Part55("RE_Tidebreaker_Brass", "bronze.png", Brass55, 0.7f, true),
                new Part55("RE_Tidebreaker_Prongs", "blade_eu.png", Steel55, 0.85f), new Part55("RE_Tidebreaker_Wrap", "leather_blue.png", Enamel55, 0.4f)),
            new Def55("RE_Azure_Sentari", "Sentari", Fit55.Blade, 3,
                new Part55("RE_Azure_Blade", "blade_eu.png", Steel55, 0.85f), new Part55("RE_Azure_Brass", "bronze.png", Brass55, 0.7f, true), new Part55("RE_Azure_Enamel", null, Enamel55, 0.9f),
                new Part55("RE_Azure_Grip", "rayskin.png", new Color(0.86f, 0.84f, 0.78f), 0.5f)),
            new Def55("RE_Khopesh_KharTib", "Khar-Tib", Fit55.Blade, 3,
                new Part55("RE_Khopesh_Bronze", "bronze_kt.png", new Color(0.69f, 0.45f, 0.24f), 0.7f), new Part55("RE_Khopesh_Gold", "gold.png", Gold55, 0.8f),
                new Part55("RE_Khopesh_Turquoise", null, new Color(0.1f, 0.62f, 0.6f), 0.85f), new Part55("RE_Khopesh_Grip", "leather_wrap.png", Dark55, 0.3f, true)),
            // ---- treasures
            new Def55("RE_Coin_Contraband", "Outlaws", Fit55.Treasure, 3, new Part55("RE_Coin_Face", "coin_contraband.png", Gold55, 0.75f), new Part55("RE_Coin_Rim", "gold.png", Gold55, 0.8f)),
            new Def55("RE_Signet_Royal", "Eraden", Fit55.Treasure, 3, new Part55("RE_Signet_Gold", "gold.png", Gold55, 0.8f), new Part55("RE_Signet_Crest", "signet_crest.png", Enamel55, 0.85f)),
            new Def55("RE_Heartroot_Totem", "Wildfolk", Fit55.Treasure, 3, new Part55("RE_Heartroot_Wood", "walnut.png", new Color(0.35f, 0.2f, 0.12f), 0.4f), new Part55("RE_Heartroot_Seed", null, new Color(0.35f, 1f, 0.45f), 0.7f, false, 2.2f)),
            new Def55("RE_Jade_Relic", "Rakta", Fit55.Treasure, 4, new Part55("RE_Jade_Jade", "jade.png", new Color(0.24f, 0.5f, 0.35f), 0.9f), new Part55("RE_Jade_Grooves", "gold.png", Gold55, 0.8f),
                new Part55("RE_Jade_Cord", "gold.png", Gold55, 0.6f), new Part55("RE_Jade_Tassel", null, new Color(0.55f, 0.06f, 0.06f), 0.2f)),
            new Def55("RE_Pearl_Captain", "Sentari", Fit55.Treasure, 4, new Part55("RE_Pearl_Shell", "shell.png", new Color(0.9f, 0.84f, 0.73f), 0.6f), new Part55("RE_Pearl_Pearl", null, new Color(0.93f, 0.92f, 0.88f), 0.95f, false, 0.35f),
                new Part55("RE_Pearl_Hinge", "bronze.png", Brass55, 0.7f, true)),
            new Def55("RE_Sunstone_Celestial", "Khar-Tib", Fit55.Treasure, 4, new Part55("RE_Sunstone_Bronze", "bronze_kt.png", new Color(0.69f, 0.45f, 0.24f), 0.7f), new Part55("RE_Sunstone_Stone", null, new Color(1f, 0.55f, 0.1f), 0.9f, false, 2.2f)),
            // ---- rare finds (hand tools on the potion base, like the NPC Spawner)
            new Def55(HollowEyeId55, "The Eye", Fit55.Grip, 4, new Part55("RE_HollowEye_Bronze", "gunmetal.png", Dark55, 0.7f), new Part55("RE_HollowEye_Shell", null, Violet55, 0.95f, false, 1.2f),
                new Part55("RE_HollowEye_Core", null, Violet55, 0.9f, false, 3f)),
            new Def55(CompassId55, "Sentari", Fit55.Grip, 4, new Part55("RE_Compass_Brass", "bronze.png", Brass55, 0.75f, true), new Part55("RE_Compass_Card", "compass_card.png", new Color(0.88f, 0.82f, 0.67f), 0.3f),
                new Part55("RE_Compass_NeedleN", null, new Color(0.6f, 0.08f, 0.06f), 0.7f), new Part55("RE_Compass_NeedleS", "gunmetal.png", Dark55, 0.7f)),
        };

        static readonly Dictionary<string, Def55> byId55 = new Dictionary<string, Def55>();
        static Def55 Def55Of(string id)
        {
            if (byId55.Count == 0) foreach (var d in Defs55) byId55[d.id] = d;
            return id != null && byId55.TryGetValue(id, out var r) ? r : null;
        }
        internal static bool IsWeapon55(string id) { var d = Def55Of(id); return d != null && d.fit == Fit55.Blade; }
        internal static bool IsRare55(string id) => id == HollowEyeId55 || id == CompassId55;

        sealed class Arm55
        {
            public Item item; public Def55 def; public Transform root, spin, needle, core; public float seed, s = 1f;
            public float needleAng, needleVel, nextTarget, nextTick, targetDist = -1f, targetAng; public Vector3 targetPos; public bool hasTarget;
            public float heat, nextHit, nextNoise, nextBuzz; public bool overheated; public Material glowMat; public LineRenderer beam; public Light coreLight, hitLight; public AudioSource hum;
        }
        static readonly Dictionary<Item, Arm55> arms55 = new Dictionary<Item, Arm55>();
        static readonly Dictionary<string, Material> mats55 = new Dictionary<string, Material>();
        static float nextArmScan55;
        static readonly bool[] armUse55 = new bool[2];

        static Material Mat55(Part55 p)
        {
            if (mats55.TryGetValue(p.mesh, out var have) && have != null) return have;
            Material m = null;
            Texture2D t = p.tex != null ? GoreTex.MeshTex((p.v54 ? "Items54/" : "Items55/") + p.tex) : null;
            m = t != null ? Mats.MakeTextured(t, null, Color.white, p.smooth, false) : Mats.Make(p.col, p.smooth);
            if (p.glow > 0f)
            {
                m = new Material(m) { name = "RageEngine_Glow55" };
                try { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; m.SetColor("_EmissionColor", p.col * p.glow); } catch { }
            }
            return mats55[p.mesh] = m;
        }

        // ================================================================== the frame
        void TickArsenal55()
        {
            float now = Time.time, dt = Time.deltaTime;
            if (now >= nextArmScan55 && Item.allActive != null)
            {
                nextArmScan55 = now + 1f;
                foreach (var it in Item.allActive)
                {
                    if (it == null || it.data == null || it.data.id == null || !it.data.id.StartsWith("RE_") || arms55.ContainsKey(it)) continue;
                    var d = Def55Of(it.data.id);
                    if (d != null) { var item = it; Guard("arsenal.dress", () => Dress55(item, d)); }
                }
                List<Item> gone = null;
                foreach (var kv in arms55)
                {
                    if (kv.Key == null || kv.Value.root == null) { (gone ??= new List<Item>()).Add(kv.Key); continue; }
                    // items are pooled: if this one came back as something else, give it its own look back
                    if (kv.Key.data != null && kv.Key.data.id != kv.Value.def.id) { var a0 = kv.Value; Guard("arsenal.undress", () => Undress55(a0)); (gone ??= new List<Item>()).Add(kv.Key); }
                }
                if (gone != null) foreach (var g in gone) arms55.Remove(g);
            }
            // held rare finds: the crystal's trigger
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc != null)
                for (int k = 0; k < 2; k++)
                {
                    var h = k == 0 ? pc.handLeft : pc.handRight;
                    var it = h != null && h.grabbedHandle != null ? h.grabbedHandle.item : null;
                    bool use = false;
                    if (it != null && arms55.TryGetValue(it, out var a) && a.def.id == HollowEyeId55)
                    {
                        try { use = h.playerHand.controlHand.usePressed; } catch { }
                        if (use) { var aa = a; var hh = h; Guard("eye.beam", () => Beam55(aa, hh, now, dt)); }
                    }
                    armUse55[k] = use;
                }
            foreach (var kv in arms55)
            {
                var a = kv.Value; if (kv.Key == null || a.root == null) continue;
                if (a.spin != null)                                                                   // Observer's Edge: the crystal turns and bobs in the eye
                {
                    a.spin.localRotation = Quaternion.Euler(0f, 0f, (now * 50f + a.seed * 30f) % 360f) * Quaternion.Euler(0f, (now * 35f) % 360f, 0f);
                    a.spin.localPosition = new Vector3(0f, Mathf.Sin(now * 1.7f + a.seed) * 0.0025f, 0f);
                }
                if (a.def.id == HollowEyeId55) { var aa = a; Guard("eye.idle", () => EyeIdle55(aa, now, dt)); }
                else if (a.def.id == CompassId55) { var aa = a; Guard("compass", () => Compass55(aa, now, dt)); }
            }
            Guard("rare55", () => TickRare55(now));
            // the loot tables: again every 20 s (a catalog refresh would drop them) and straight away when "NPC weapons" changes
            if (now >= nextLoot55) { nextLoot55 = now + 20f; InjectLoot55(); }
        }

        // ================================================================== dressing
        void Dress55(Item it, Def55 d)
        {
            var a = new Arm55 { item = it, def = d, seed = Random.value * 10f };
            arms55[it] = a;
            var root = NewRoot54(it, "RageEngine_" + d.id);
            a.root = root;
            bool any = false;
            Transform needleN = null, needleS = null;
            foreach (var p in d.parts)
            {
                var t = Part54(root, p.mesh, Mat55(p));
                if (t == null) continue;
                any = true;
                if (p.mesh == "RE_Observer_Crystal") { a.spin = t; t.localPosition = new Vector3(0f, 0f, 0.143f); }
                else if (p.mesh == "RE_HollowEye_Core") a.core = t;
                else if (p.mesh == "RE_HollowEye_Shell") { a.glowMat = new Material(Mat55(p)); t.GetComponent<MeshRenderer>().sharedMaterial = a.glowMat; }
                else if (p.mesh == "RE_Compass_NeedleN") needleN = t;
                else if (p.mesh == "RE_Compass_NeedleS") needleS = t;
            }
            if (!any)
            {
                // models missing: give the vanilla look back rather than an invisible item
                foreach (var r in it.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;
                UnityEngine.Object.Destroy(root.gameObject); a.root = null;
                Log($"{d.id}: models not found - showing the base item");
                return;
            }
            if (needleN != null || needleS != null)
            {
                var piv = new GameObject("needle").transform; piv.SetParent(root, false); a.needle = piv;
                if (needleN != null) needleN.SetParent(piv, false);
                if (needleS != null) needleS.SetParent(piv, false);
            }
            if (d.fit == Fit55.Blade) FitBlade55(it, a);
            else if (d.fit == Fit55.Treasure) FitTreasure55(it, a);
            if (a.spin != null)
            {
                // the root is stretched along the blade; the crystal hangs from its own unstretched pivot so it turns true
                var piv = new GameObject("eye").transform; piv.SetParent(it.transform, false);
                piv.position = root.TransformPoint(0f, 0f, 0.143f); piv.rotation = root.rotation;
                var ls = it.transform.lossyScale; piv.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));
                a.spin.SetParent(piv, false); a.spin.localPosition = Vector3.zero;
            }
            if (d.id == HollowEyeId55)
            {
                var lg = new GameObject("glow"); lg.transform.SetParent(root, false); lg.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                var L = lg.AddComponent<Light>(); L.type = LightType.Point; L.color = Violet55; L.range = 0.9f; L.intensity = 0.6f; L.shadows = LightShadows.None; a.coreLight = L;
            }
        }

        static void Undress55(Arm55 a)
        {
            BeamOff55(a);
            if (a.hitLight != null) UnityEngine.Object.Destroy(a.hitLight.gameObject);
            if (a.hum != null) UnityEngine.Object.Destroy(a.hum);
            if (a.spin != null && a.spin.parent != null && a.spin.parent != a.root) UnityEngine.Object.Destroy(a.spin.parent.gameObject);
            if (a.root != null) UnityEngine.Object.Destroy(a.root.gameObject);
            if (a.glowMat != null) UnityEngine.Object.Destroy(a.glowMat);
            if (a.item != null) foreach (var r in a.item.GetComponentsInChildren<MeshRenderer>(true)) if (!r.name.StartsWith("RE_") && !r.name.StartsWith("RageEngine")) r.enabled = true;
        }

        /// The base weapon's colliders, as world-space box corners.
        static void Corners55(Collider col, List<Vector3> into)
        {
            Bounds b;
            if (col is BoxCollider bx) b = new Bounds(bx.center, bx.size);
            else if (col is SphereCollider sp) b = new Bounds(sp.center, Vector3.one * sp.radius * 2f);
            else if (col is CapsuleCollider cp) { var sz = Vector3.one * cp.radius * 2f; sz[Mathf.Clamp(cp.direction, 0, 2)] = Mathf.Max(cp.height, cp.radius * 2f); b = new Bounds(cp.center, sz); }
            else if (col is MeshCollider mc && mc.sharedMesh != null) b = mc.sharedMesh.bounds;
            else return;
            var t = col.transform; Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++) into.Add(t.TransformPoint(c + new Vector3((i & 1) != 0 ? e.x : -e.x, (i & 2) != 0 ? e.y : -e.y, (i & 4) != 0 ? e.z : -e.z)));
        }

        static List<Vector3> ItemCorners55(Item it)
        {
            var l = new List<Vector3>();
            foreach (var col in it.GetComponentsInChildren<Collider>(true)) if (col != null && !col.isTrigger) Corners55(col, l);
            return l;
        }

        /// Our model's extent in the root's own space (before any fitting).
        static Bounds ModelBounds55(Transform root)
        {
            bool first = true; var b = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                mb.center += mf.transform.localPosition;
                if (first) { b = mb; first = false; } else b.Encapsulate(mb);
            }
            return b;
        }

        static Transform Deep55(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var r = Deep55(t.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        /// Weapons: our model's length onto the base's colliders along its handle, blade plane and edge side matched.
        static void FitBlade55(Item it, Arm55 a)
        {
            var root = a.root;
            Handle h = it.mainHandleRight != null ? it.mainHandleRight : (it.handles != null && it.handles.Count > 0 ? it.handles[0] : null);
            Transform g = h != null ? h.transform : it.transform;
            Vector3 gp = g.position, ax = g.up;
            var corners = ItemCorners55(it);
            if (corners.Count == 0) return;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var c in corners) { float t = Vector3.Dot(c - gp, ax); if (t < lo) lo = t; if (t > hi) hi = t; }
            var tip = Deep55(it.transform, "Pierce");
            bool flip = tip != null ? Vector3.Dot(tip.position - gp, ax) < 0f : -lo > hi;
            if (flip) { ax = -ax; float t0 = lo; lo = -hi; hi = -t0; }
            // the blade plane: the widest direction across the far part of the weapon; the edge: the side it bulges to
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
                double ang = 0.5 * Math.Atan2(2 * cxy, cxx - cyy);
                width = (u * (float)Math.Cos(ang) + v * (float)Math.Sin(ang)).normalized;
                if (mx * Math.Cos(ang) + my * Math.Sin(ang) < 0) width = -width;
            }
            if (width.sqrMagnitude < 0.5f) width = Vector3.ProjectOnPlane(Vector3.up, ax).normalized;
            var mb = ModelBounds55(root);
            float s = Mathf.Clamp((hi - lo) / Mathf.Max(0.05f, mb.max.z - mb.min.z), 0.6f, 1.6f);
            root.rotation = Quaternion.LookRotation(ax, width);
            root.position = gp + ax * (lo - mb.min.z * s);
            var ls = it.transform.lossyScale;
            root.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), s / Mathf.Max(0.01f, ls.z));
            a.s = s;
            Log($"{a.def.id}: fitted - {(hi - lo) * 100f:0} cm (x{s:0.00})");
        }

        /// Treasures: longest-to-longest, shortest-to-shortest onto the base treasure's colliders, centred.
        static void FitTreasure55(Item it, Arm55 a)
        {
            var root = a.root;
            var corners = ItemCorners55(it);
            if (corners.Count == 0) return;
            var ib = new Bounds(it.transform.InverseTransformPoint(corners[0]), Vector3.zero);
            foreach (var c in corners) ib.Encapsulate(it.transform.InverseTransformPoint(c));
            var mb = ModelBounds55(root);
            int[] ia = Order55(ib.size), ma = Order55(mb.size);
            var axes = new Vector3[3];
            for (int k = 0; k < 3; k++) { var e = Vector3.zero; e[ia[k]] = 1f; axes[ma[k]] = e; }
            if (Vector3.Dot(Vector3.Cross(axes[0], axes[1]), axes[2]) < 0f) axes[ma[2]] = -axes[ma[2]];       // keep it right-handed (no mirror image)
            var q = Quaternion.LookRotation(axes[2], axes[1]);
            float s = Mathf.Clamp(ib.size[ia[0]] / Mathf.Max(0.005f, mb.size[ma[0]]), 0.5f, 2f);
            root.localRotation = q; root.localScale = Vector3.one * s;
            root.localPosition = ib.center - q * (mb.center * s);
            a.s = s;
        }

        static int[] Order55(Vector3 v)
        {
            var o = new[] { 0, 1, 2 };
            Array.Sort(o, (p, q) => v[q].CompareTo(v[p]));
            return o;
        }

        // ================================================================== Hollow Eye Crystal
        static Material beamMat55;
        static Material BeamMat55()
        {
            if (beamMat55 != null) return beamMat55;
            beamMat55 = new Material(Mats.Make(Violet55, 0.9f)) { name = "RageEngine_Beam55" };
            try { beamMat55.EnableKeyword("_EMISSION"); beamMat55.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; beamMat55.SetColor("_EmissionColor", new Color(0.7f, 0.35f, 1f) * 6f); } catch { }
            return beamMat55;
        }

        /// While the trigger is held: a violet beam from the crystal's tip. It burns what it touches and overheats after
        /// about 3.5 seconds (then it has to cool down).
        void Beam55(Arm55 a, RagdollHand hand, float now, float dt)
        {
            if (a.overheated) { if (now >= a.nextBuzz) { a.nextBuzz = now + 0.4f; Haptics.Pulse(hand.side, 0.1f, 1); } return; }
            a.heat += dt / 3.5f;
            if (a.heat >= 1f)
            {
                a.overheated = true; BeamOff55(a);
                Sfx.Play("NRE_Fuse", a.root, 0.35f, 1.8f);
                Log("Hollow Eye Crystal overheated - cooling down");
                return;
            }
            if (a.beam == null)
            {
                var go = new GameObject("RE_Beam55"); go.transform.SetParent(a.root, false);
                var lr = go.AddComponent<LineRenderer>(); lr.positionCount = 2; lr.useWorldSpace = true; lr.numCapVertices = 3;
                lr.sharedMaterial = BeamMat55(); lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; a.beam = lr;
                var hl = new GameObject("RE_BeamHit55"); hl.transform.SetParent(a.root, false); var L = hl.AddComponent<Light>(); L.type = LightType.Point; L.color = Violet55; L.range = 1.6f; L.shadows = LightShadows.None; a.hitLight = L;
            }
            if (a.hum == null && Sfx.TryClip("NRE_Resonance", out var clip))
            {
                a.hum = a.root.gameObject.AddComponent<AudioSource>(); a.hum.clip = clip; a.hum.loop = true; a.hum.spatialBlend = 1f; a.hum.pitch = 1.9f; a.hum.volume = 0.32f;
                a.hum.minDistance = 0.5f; a.hum.maxDistance = 14f; a.hum.rolloffMode = AudioRolloffMode.Linear;
            }
            if (a.hum != null && !a.hum.isPlaying) a.hum.Play();
            Vector3 from = a.root.TransformPoint(0f, 0.19f, 0f), dir = a.root.up;
            Vector3 end = from + dir * 25f; RaycastHit best = default; bool got = false;
            foreach (var hit in Physics.RaycastAll(from, dir, 25f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(a.item.transform)) continue;
                var rp0 = hit.collider.GetComponentInParent<RagdollPart>();
                if (rp0 != null && rp0.ragdoll != null && rp0.ragdoll.creature != null && rp0.ragdoll.creature.isPlayer) continue;
                if (!got || hit.distance < best.distance) { best = hit; got = true; }
            }
            if (got) end = best.point;
            a.beam.enabled = true;
            a.beam.SetPosition(0, from); a.beam.SetPosition(1, end);
            a.beam.widthMultiplier = 0.011f + 0.004f * Mathf.Sin(now * 47f) + 0.003f * Random.value;
            if (a.hitLight != null) { a.hitLight.enabled = got; a.hitLight.transform.position = end - dir * 0.05f; a.hitLight.intensity = 1.6f + Random.value * 0.8f; }
            if (now >= a.nextBuzz) { a.nextBuzz = now + 0.1f; Haptics.Pulse(hand.side, 0.15f, 1); }
            if (!got) return;
            if (now >= a.nextNoise) { a.nextNoise = now + 0.6f; Emit(end, 10f, Stim.Impact); }
            var rp = best.collider.GetComponentInParent<RagdollPart>();
            var c = rp != null && rp.ragdoll != null ? rp.ragdoll.creature : null;
            if (c != null && !c.isPlayer && !c.isKilled && now >= a.nextHit)
            {
                a.nextHit = now + 0.12f;
                PhysicalNow(c, 1.2f);
                var ds = new DamageStruct { damageType = DamageType.Energy, damage = 4.5f, pushLevel = 0, hitRagdollPart = rp };
                var ci = new CollisionInstance { damageStruct = ds, contactPoint = end, contactNormal = -dir, impactVelocity = dir * 2f };
                float before = -1f, after = -1f;
                Guard("health", () => before = c.currentHealth);
                Guard("eye.damage", () => c.Damage(ci));
                Guard("health", () => after = c.currentHealth);
                if (!c.isKilled && before >= 0f && after >= before - 0.01f) Guard("eye.damage2", () => c.Damage(4.5f));
                if (rp.physicBody != null) rp.physicBody.AddForce(dir * 0.6f, ForceMode.VelocityChange);
                if (TryGet(c, out var s)) { s.pain = Mathf.Min(1f, s.pain + 0.03f); s.lastHitTime = now; s.lastHitDir = dir; }
                if (Random.value < 0.25f) Sfx.Play("NRE_Fuse", rp.transform, 0.18f, 1.6f);
            }
        }

        static void BeamOff55(Arm55 a)
        {
            if (a.beam != null) a.beam.enabled = false;
            if (a.hitLight != null) a.hitLight.enabled = false;
            if (a.hum != null && a.hum.isPlaying) a.hum.Stop();
        }

        /// Every frame: cool down, glow with what's left, beam off when the trigger isn't held.
        void EyeIdle55(Arm55 a, float now, float dt)
        {
            bool held = false;
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc != null)
                for (int k = 0; k < 2; k++)
                {
                    var h = k == 0 ? pc.handLeft : pc.handRight;
                    if (h != null && h.grabbedHandle != null && h.grabbedHandle.item == a.item && armUse55[k]) held = true;
                }
            if (!held || a.overheated)
            {
                BeamOff55(a);
                a.heat = Mathf.Max(0f, a.heat - dt * 0.22f);
                if (a.overheated && a.heat < 0.25f) { a.overheated = false; Sfx.Play("NRE_Resonance", a.root, 0.2f, 2.2f); }
            }
            float ready = a.overheated ? 0.15f : 1f - a.heat * 0.6f;
            if (a.coreLight != null) a.coreLight.intensity = (0.35f + 0.35f * Mathf.Sin(now * 2.2f + a.seed)) * ready + (held ? 1.2f : 0f);
            if (a.glowMat != null)                                                                    // the crystal breathes; dim when spent, bright while it fires
                try { a.glowMat.SetColor("_EmissionColor", Violet55 * ((0.7f + 0.5f * Mathf.Sin(now * 3.1f + a.seed)) * ready + (held && !a.overheated ? 2.2f : 0f))); } catch { }
        }

        // ================================================================== Captain's Compass
        /// The needle swings to the nearest treasure lying about (not held, not in a holder) within 120 m, and ticks
        /// faster the closer you get. With nothing to find it wanders.
        void Compass55(Arm55 a, float now, float dt)
        {
            if (a.needle == null) return;
            if (now >= a.nextTarget)
            {
                a.nextTarget = now + 0.5f; a.hasTarget = false; float best = 120f * 120f;
                Vector3 here = a.root.position;
                if (Item.allActive != null)
                    foreach (var it in Item.allActive)
                    {
                        if (it == null || it == a.item || it.data == null) continue;
                        // 0.56: treasure chests first (they count as much closer), then loose treasure
                        string iid = it.data.id ?? "";
                        bool chest = iid.IndexOf("chest", System.StringComparison.OrdinalIgnoreCase) >= 0 || it.name.IndexOf("chest", System.StringComparison.OrdinalIgnoreCase) >= 0
                                     || iid.IndexOf("coffer", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!chest && it.data.type != ItemData.Type.Valuable) continue;
                        if (it.holder != null || it.IsHanded() || it.isBrokenPiece) continue;
                        float d2 = (it.transform.position - here).sqrMagnitude * (chest ? 0.25f : 1f);
                        if (d2 < best && d2 > 0.04f) { best = d2; a.targetPos = it.transform.position; a.hasTarget = true; }
                    }
                a.targetDist = a.hasTarget ? Mathf.Sqrt(best) : -1f;
            }
            float target;
            if (a.hasTarget) { var d = a.root.InverseTransformPoint(a.targetPos); target = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
            else target = a.needleAng + Mathf.Sin(now * 0.7f + a.seed) * 40f;
            a.needleVel += (Mathf.DeltaAngle(a.needleAng, target) * 22f - a.needleVel * 5.5f) * dt;
            a.needleAng += a.needleVel * dt;
            a.needle.localRotation = Quaternion.Euler(0f, a.needleAng, 0f);
            bool held = a.item.IsHanded();
            if (held && a.hasTarget && now >= a.nextTick)
            {
                a.nextTick = now + Mathf.Lerp(0.22f, 1.6f, Mathf.InverseLerp(2f, 45f, a.targetDist));
                Sfx.Play("NRE_MetalBang", a.root, 0.05f, 2.8f);
            }
        }

        // ================================================================== loot tables
        sealed class Kit55 { public string[] keys; public string[] ids; public Kit55(string[] k, params string[] i) { keys = k; ids = i; } }
        static readonly Kit55[] NpcKits55 =
        {
            new Kit55(new[] { "soldier", "eraden" }, "RE_KingsOath_Eraden", "RE_Chancellor_Eraden"),
            new Kit55(new[] { "cultist", "theeye" }, "RE_Observer_Eye"),
            new Kit55(new[] { "scavenger", "bandit", "outlaw", "pirate" }, "RE_Blackwake_Outlaw"),
            new Kit55(new[] { "tribal", "wildfolk", "druid" }, "RE_Briarhook_Wildfolk"),
            new Kit55(new[] { "rakta", "raike" }, "RE_Glaive_Rakta", "RE_AshenTalon_Rakta"),
            new Kit55(new[] { "sentari", "sentara" }, "RE_Tidebreaker_Sentari", "RE_Azure_Sentari"),
            new Kit55(new[] { "khartib", "khar-tib", "khar_tib" }, "RE_Khopesh_KharTib"),
        };
        static readonly string[] NotKits55 = { "bow", "staff", "small", "dagger", "offhand", "shield", "quiver", "arrow", "shop", "throw" };
        static readonly Dictionary<string, string[]> Treasure55 = new Dictionary<string, string[]>
        {
            { "LootablesT2", new[] { "RE_Coin_Contraband" } },
            { "LootablesT3", new[] { "RE_Coin_Contraband", "RE_Signet_Royal", "RE_Heartroot_Totem" } },
            { "LootablesT4", new[] { "RE_Jade_Relic", "RE_Pearl_Captain", "RE_Sunstone_Celestial", "RE_Signet_Royal" } },
        };
        static bool kitsOn55; static float nextLoot55;

        /// Adds our items to the game's loot tables (and takes the NPC kits out again if "NPC weapons" is set to the
        /// game's own). Safe to repeat: it never adds the same item to a table twice.
        void InjectLoot55()
        {
            List<LootTable> tables = null;
            Guard("loot55.list", () => tables = Catalog.GetDataList<LootTable>());
            if (tables == null) return;
            bool kits = false;                                                                          // 0.56: no Rage Engine weapons in NPC kits (they come back when the weapons are rebuilt)
            int added = 0, removed = 0, kitTables = 0, rackTables = 0, treasureTables = 0;
            foreach (var lt in tables)
            {
                if (lt == null || lt.id == null || lt.levelledDrops == null || lt.levelledDrops.Count == 0) continue;
                string id = lt.id, low = id.ToLowerInvariant();
                var lvl = lt.levelledDrops[0]; if (lvl == null) continue;
                if (lvl.drops == null) lvl.drops = new List<LootTable.Drop>();
                bool changed = false;
                float total = 0f; foreach (var d in lvl.drops) if (d != null && !(d.referenceID ?? "").StartsWith("RE_")) total += d.probabilityWeight;
                if (total <= 0f) total = 1f;
                // treasure
                if (Treasure55.TryGetValue(id, out var tre)) { foreach (var r in tre) changed |= RemoveDrop55(lvl, r, ref removed); treasureTables++; }   // 0.56: treasures only in their own faction dungeons (TickRare55)
                // the Baron's weapon racks
                if (low.StartsWith("shop_weaponrack_t") && low.Length > 17 && char.IsDigit(low[17]))
                {
                    int tier = low[17] - '0';
                    if (tier >= 2) { foreach (var d in Defs55) if (d.fit == Fit55.Blade && (d.tier == tier || (tier >= 3 && d.tier >= 3))) changed |= AddDrop55(lvl, d.id, 1f, ref added); rackTables++; }
                }
                // faction NPC kits (and the generic tiered tables wave NPCs draw from)
                if (low.StartsWith("weapon") && Array.TrueForAll(NotKits55, x => !low.Contains(x)))
                {
                    foreach (var kit in NpcKits55)
                    {
                        if (!Array.Exists(kit.keys, k => low.Contains(k))) continue;
                        foreach (var r in kit.ids)
                            changed |= kits ? AddDrop55(lvl, r, total * 0.12f, ref added) : RemoveDrop55(lvl, r, ref removed);
                        kitTables++;
                    }
                    if (low.Length == 7 && low.StartsWith("weapont") && char.IsDigit(low[6]))
                    {
                        int tier = low[6] - '0';
                        foreach (var d in Defs55)
                            if (d.fit == Fit55.Blade && d.tier == tier) changed |= kits ? AddDrop55(lvl, d.id, total * 0.04f, ref added) : RemoveDrop55(lvl, d.id, ref removed);
                    }
                }
                if (changed) { var t = lt; Guard("loot55.refresh", () => t.OnCatalogRefresh()); }
            }
            kitsOn55 = kits;
            if (added > 0 || removed > 0) Log($"loot: +{added} / -{removed} drops (NPC kit tables {kitTables}, Baron racks {rackTables}, treasure tables {treasureTables})");
        }

        static bool AddDrop55(LootTable.DropLevel lvl, string id, float weight, ref int added)
        {
            foreach (var d in lvl.drops) if (d != null && d.referenceID == id) return false;
            if (Def55Of(id) == null) return false;
            lvl.drops.Add(new LootTable.Drop { reference = LootTable.Drop.Reference.Item, referenceID = id, probabilityWeight = Mathf.Max(0.01f, weight) });
            added++; return true;
        }

        static bool RemoveDrop55(LootTable.DropLevel lvl, string id, ref int removed)
        {
            int n = lvl.drops.RemoveAll(d => d != null && d.referenceID == id);
            removed += n; return n > 0;
        }

        // ================================================================== rare finds
        static bool rareRolled55; static readonly List<string> rareWant55 = new List<string>(); static float rareFrom55, rareUntil55, nextRare55;
        /// 0.56: each faction's own treasure (tag from FactionOf56): 1 Eraden, 2 Outlaws, 3 Wildfolk, 4 The Eye, 5 Rakta, 6 Sentari, 7 Khar-Tib.
        static readonly string[] FactionTreasure56 = { null, "RE_Signet_Royal", "RE_Coin_Contraband", "RE_Heartroot_Totem", null, "RE_Jade_Relic", "RE_Pearl_Captain", "RE_Sunstone_Celestial" };
        static readonly string[] FactionName56 = { "unknown", "Eraden", "Outlaw", "Wildfolk", "Eye", "Rakta", "Sentari", "Khar-Tib" };
        internal static int FactionOf56(Creature c)
        {
            int t = FactionTag54(c);
            if (t != 0 || c == null) return t;
            string n = (FactionName28(c.factionId) ?? "").ToLowerInvariant();
            return n.Contains("khar") || n.Contains("tib") || n.Contains("desert") ? 7 : 0;
        }

        /// Once per dungeon, after its people have spawned: which faction holds it, and what it hides. Its own faction's
        /// treasure (only there - a Khar-Tib sunstone is only ever in a Khar-Tib dungeon), and rarely a rare find.
        /// Each one takes the place of the next loose treasure lying in the level.
        void TickRare55(float now)
        {
            if (now < nextRare55) return;
            nextRare55 = now + 1f;
            if (!InDungeon55()) { rareRolled55 = false; rareFrom55 = 0f; rareWant55.Clear(); return; }
            if (!rareRolled55)
            {
                if (rareFrom55 <= 0f) { rareFrom55 = now + 8f; return; }                                   // let the dungeon's people spawn first
                if (now < rareFrom55) return;
                rareRolled55 = true; rareWant55.Clear();
                var counts = new int[8];
                if (Creature.allActive != null) foreach (var c in Creature.allActive) if (c != null && !c.isPlayer) { int t = FactionOf56(c); if (t > 0 && t < counts.Length) counts[t]++; }
                int top = 0; for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[top]) top = i;
                if (counts[top] == 0) top = 0;
                if (top == 4 && Random.value < 0.125f) rareWant55.Add(HollowEyeId55);
                if (Random.value < 0.05f) rareWant55.Add(CompassId55);
                var own = FactionTreasure56[top];
                if (own != null) { if (Random.value < 0.45f) rareWant55.Add(own); if (Random.value < 0.15f) rareWant55.Add(own); }
                rareUntil55 = now + 900f;
                Log($"dungeon: {FactionName56[top]} - hidden this run: {(rareWant55.Count > 0 ? string.Join(", ", rareWant55) : "nothing special")}");
                return;
            }
            if (rareWant55.Count == 0 || now > rareUntil55 || Item.allActive == null) return;
            foreach (var it in Item.allActive)
            {
                if (it == null || it.data == null || it.data.type != ItemData.Type.Valuable || it.data.id == null || it.data.id.StartsWith("RE_")) continue;
                if (it.holder != null || it.IsHanded() || it.isBrokenPiece) continue;
                string want = rareWant55[0]; rareWant55.RemoveAt(0);
                var data = Catalog.GetData<ItemData>(want, false); if (data == null) { Log($"dungeon find: {want} isn't loaded - skipped"); return; }
                Vector3 p = it.transform.position; Quaternion q = it.transform.rotation; string was = it.data.id;
                Guard("rare55.spawn", () => data.SpawnAsync(n => { if (n != null) Log($"dungeon find: {want} placed where a {was} was"); }, p + Vector3.up * 0.03f, q));
                Guard("rare55.despawn", () => it.Despawn());
                return;
            }
        }

        void Unload55() { rareRolled55 = false; rareWant55.Clear(); rareFrom55 = 0f; arms55.Clear(); }
    }
}

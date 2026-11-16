// Nomad Rage Engine 0.24 - BIOGENESIS devices: syringes, injectors, vials (models, injection, refilling).
//   Item ids (Items/*.json): RE_Bio_<Compound> (filled syringe/injector), RE_Bio_Empty (empty syringe),
//   RE_Vial_<Compound> (glass ampoule). Underneath: a throwing dagger (its pierce = the needle) / a pottery cup (vial).
//   Kinds: 0 disposable syringe (ailments, empty), 1 glass lab syringe (X-01/04/07), 2 specialized injector
//   (X-02/05/08), 3 compact injector (X-03/06/09), 4 vial.
//   Model parts (Models/RE_Dev<kind>_<Part>.obj), origin = needle tip, +Z = towards the tip.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        enum DevState { Full, Injecting, Empty }

        sealed class Device
        {
            public Item item; public int kind; public int comp; public DevState state;
            public Transform root, plunger, liquid, needle;
            public Vector3 plungerStart; public float liquidLen, travel, t0, refillT = -1f;
            public NpcState target; public RagdollPart site; public Vector3 siteLocal;
            public int tox = -1, uses; public float nextOil; public int custom = -1; public int special;          // custom = Serum Lab slot (0-5)                                    // 0.25: toxin oil bottles
        }
        static readonly Dictionary<Item, Device> devices = new Dictionary<Item, Device>();
        static float nextDevScan, nextRefillCheck;

        static int KindFor(int comp)
        {
            if (comp < (int)Comp.NightSoldier) return 0;
            int x = comp - (int)Comp.NightSoldier;
            return x % 3 == 0 ? 1 : x % 3 == 1 ? 2 : 3;
        }

        // liquid length / plunger travel per kind (metres, matching the generated models)
        static readonly float[] LiquidLen = { 0.05f, 0.055f, 0.045f, 0.03f, 0.036f };
        static readonly float[] LiquidZ = { -0.047f, -0.05f, -0.03f, -0.024f, -0.036f };

        static Material GlassMat()
        {
            var sh = (Application.platform == RuntimePlatform.Android ? null : SkinPaint.BlendShader);     // 0.28.3: opaque glass on Quest
            if (sh == null) return Mats.Make(new Color(0.8f, 0.9f, 1f), 0.95f);
            return new Material(sh) { color = new Color(0.85f, 0.93f, 1f, 0.22f), mainTexture = Texture2D.whiteTexture, renderQueue = 3010 };
        }
        static Material glassMat, steelMat, whiteMat, blackMat, hubMat, penMat;
        static readonly Dictionary<string, Material> labelMats = new Dictionary<string, Material>();
        static Material LabelMat(string key)
        {
            if (labelMats.TryGetValue(key, out var m) && m != null) return m;
            var t = GoreTex.MeshTex("Labels/RE_Label_" + key + ".png");
            return labelMats[key] = t != null ? Mats.MakeTextured(t, null, Color.white, 0.3f, false) : Mats.Make(Color.white, 0.3f);
        }

        /// Scan for new device items and dress them.
        void TickDevices()
        {
            float now = Time.time;
            if (now >= nextDevScan)
            {
                nextDevScan = now + 0.5f;
                if (!catalogReported && now > 15f) Guard("catalog", CatalogReport);
                var all = Item.allActive;
                if (all != null)
                    foreach (var it in all)
                    {
                        if (it == null || it.data == null || devices.ContainsKey(it)) continue;
                        string id = it.data.id;
                        if (id == null) continue;
                        int comp = -2; int kind = -1;
                        if (id == "RE_Bio_Empty") { comp = -1; kind = 0; }
                        else if (id.StartsWith("RE_Bio_")) { comp = System.Array.IndexOf(CompKey, id.Substring(7)); if (comp >= 0) kind = KindFor(comp); }
                        else if (id.StartsWith("RE_Vial_")) { comp = System.Array.IndexOf(CompKey, id.Substring(8)); if (comp >= 0) kind = 4; }
                        int tox = -1;
                        if (id.StartsWith("RE_Oil_")) { tox = System.Array.IndexOf(ToxName, id.Substring(7)); if (tox >= 0) { kind = 4; comp = -1; } }
                        int custom = -1;
                        if (id.StartsWith("RE_Custom_") && int.TryParse(id.Substring(10), out int cn) && cn >= 1 && cn <= LabSlots) { custom = cn - 1; kind = 2; comp = -1; LoadRecipes(); }
                        int special = 0;
                        if (id == "RE_Bio_Loyalty") { kind = 2; comp = -1; special = 1; }
                        if (kind < 0) continue;
                        Guard("dev.dress", () => DressDevice(it, kind, comp, tox, custom, special));
                    }
                var gone = new List<Item>();
                foreach (var kv in devices) if (kv.Key == null) gone.Add(kv.Key);
                foreach (var g in gone) devices.Remove(g);
            }
            foreach (var kv in devices)
            {
                var d = kv.Value;
                if (d.item == null || d.root == null) continue;
                if (d.state == DevState.Injecting) Guard("dev.inject", () => TickInjection(d, now));
                if (d.refillT >= 0f) TickRefillAnim(d, now);
            }
            if (now >= nextRefillCheck) { nextRefillCheck = now + 0.1f; Guard("dev.refill", CheckRefills); Guard("dev.oil", CheckOils); }
        }

        // model lengths (needle tip to back end), metres - from the .obj files
        static readonly float[] DevLen = { 0.158f, 0.172f, 0.167f, 0.108f, 0.076f };

        /// 0.25.3 grips: the device model is placed and sized so that where the game puts your hand (the base item's
        /// main handle) is where you'd hold the real thing - the barrel of a syringe, the body of an injector, the middle
        /// of a vial - while the needle still ends at the point that actually pierces.
        static void FitToGrip(Item it, Transform root, int kind)
        {
            Handle h = null;
            try { h = it.GetMainHandle(Side.Right); } catch { }
            if (h == null) h = it.mainHandleRight != null ? it.mainHandleRight : it.mainHandleLeft;
            if (h == null) return;
            Vector3 g = h.transform.position;
            try { var gp = h.GetDefaultAxisPosition(Side.Right); if (gp != Vector3.zero) g = gp; } catch { }
            float L = DevLen[Mathf.Clamp(kind, 0, 4)];
            if (kind == 4)
            {
                // vial / oil: the middle of the ampoule sits in the palm, a bit bigger so it fills the hand
                float k = 1.35f;
                root.localScale *= k;
                root.position = g + root.forward * (L * 0.5f * k);                        // origin = top end, model runs along -Z
                return;
            }
            // syringe / injector: needle tip stays at the blade tip; stretch along the length so the hand lands on the barrel
            Vector3 tip = root.position;
            float D = Vector3.Dot(tip - g, root.forward);
            if (D < 0.05f) return;
            float frac = kind <= 1 ? 0.82f : 0.7f;                                          // fingers at the flange, thumb by the plunger
            float sz = Mathf.Clamp(D / (frac * L), 0.8f, 2.2f);
            float sxy = Mathf.Clamp(Mathf.Sqrt(sz), 0.9f, 1.45f);
            var s0 = root.localScale;
            root.localScale = new Vector3(s0.x * sxy, s0.y * sxy, s0.z * sz);
        }

        void DressDevice(Item it, int kind, int comp, int tox = -1, int custom = -1, int special = 0)
        {
            bool full = comp >= 0 || tox >= 0 || special > 0 || (custom >= 0 && recipes[custom] != null);
            var d = new Device { item = it, kind = kind, comp = comp, state = full ? DevState.Full : DevState.Empty, tox = tox, uses = tox >= 0 ? 3 : 0, custom = custom, special = special };
            // 0.25: measure the base item before hiding it (vials/oils sit on a potion bottle now)
            Bounds bb = default; bool anyB = false;
            foreach (var r in it.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.name.StartsWith("RageEngine_")) continue;
                if (!anyB) { bb = r.bounds; anyB = true; } else bb.Encapsulate(r.bounds);
            }
            if (glassMat == null)
            {
                glassMat = GlassMat(); steelMat = Mats.Make(new Color(0.78f, 0.8f, 0.83f), 0.85f); whiteMat = Mats.Make(new Color(0.93f, 0.93f, 0.92f), 0.4f);
                blackMat = Mats.Make(new Color(0.08f, 0.08f, 0.09f), 0.3f); hubMat = Mats.Make(new Color(0.95f, 0.55f, 0.15f), 0.6f); penMat = Mats.Make(new Color(0.18f, 0.18f, 0.2f), 0.5f);
            }
            foreach (var r in it.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var root = new GameObject("RageEngine_Device").transform;
            var ls = it.transform.lossyScale;
            if (kind == 4)
            {
                root.SetParent(it.transform, false);
                Vector3 upV = it.transform.up;
                root.position = anyB ? bb.center + upV * (Mathf.Abs(upV.x) * bb.extents.x + Mathf.Abs(upV.y) * bb.extents.y + Mathf.Abs(upV.z) * bb.extents.z) * 0.85f
                                     : it.transform.position + upV * 0.035f;                     // origin = top of the ampoule
                root.rotation = Quaternion.LookRotation(it.transform.up, it.transform.forward);          // vial stands along the cup's up
            }
            else
            {
                Vector3 fwd = (it.flyDirRef != null ? it.flyDirRef : it.transform).forward;
                Bounds b = default; bool any = false;
                foreach (var r in it.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r.name.StartsWith("RageEngine_")) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                Vector3 tip = any ? b.center + fwd * (Mathf.Abs(fwd.x) * b.extents.x + Mathf.Abs(fwd.y) * b.extents.y + Mathf.Abs(fwd.z) * b.extents.z) : it.transform.position + fwd * 0.1f;
                root.SetParent(it.transform, true);
                root.position = tip;
                Vector3 up = Vector3.ProjectOnPlane(it.transform.up, fwd); if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(it.transform.right, fwd);
                root.rotation = Quaternion.LookRotation(fwd, up.normalized);
            }
            root.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));
            Guard("grip", () => FitToGrip(it, root, kind));                                  // 0.25.3: the hand holds the barrel / the vial
            d.root = root;
            string K = "RE_Dev" + kind + "_";
            string labelKey = special == 1 ? "Loyalty" : custom >= 0 ? "Custom_" + (custom + 1) : tox >= 0 ? "Oil_" + ToxName[tox] : comp >= 0 ? CompKey[comp] : "Empty";
            Color liquidCol = special == 1 ? new Color(0.3f, 0.75f, 1f) : custom >= 0 ? (recipes[custom] != null ? recipes[custom].color : Color.white) : tox >= 0 ? ToxColor[tox] : comp >= 0 ? CompLiquid[comp] : Color.white;
            Part(root, K + "Body", kind >= 2 && kind <= 3 ? penMat : glassMat);
            Part(root, K + "Window", glassMat);
            Part(root, K + "Metal", steelMat);
            Part(root, K + "Cap", comp >= 0 || tox >= 0 || custom >= 0 || special > 0 ? Mats.Make(liquidCol, 0.5f) : hubMat);
            Part(root, K + "Hub", kind == 1 ? steelMat : hubMat);
            Part(root, K + "Label", LabelMat(labelKey));
            var needle = Part(root, K + "Needle", steelMat);
            d.needle = needle;
            if (kind <= 1)
            {
                var pl = new GameObject("RageEngine_Plunger").transform; pl.SetParent(root, false);
                Part(pl, K + "Plunger", kind == 1 ? steelMat : whiteMat);
                Part(pl, K + "Stopper", blackMat);
                d.plunger = pl; d.plungerStart = pl.localPosition;
            }
            if (kind >= 2 && kind <= 3 && needle != null) needle.localPosition = new Vector3(0f, 0f, -0.012f);    // injector needle hidden until it fires
            // liquid: unit cylinder (0 .. -1 on Z) scaled to the fill length, front end fixed
            var liq = Part(root, "RE_Dev_Liquid", comp >= 0 || tox >= 0 || custom >= 0 || special > 0 ? Mats.Make(liquidCol, 0.9f) : glassMat);
            d.liquidLen = LiquidLen[kind]; d.travel = d.liquidLen;
            if (liq != null)
            {
                float rad = kind == 4 ? 1.6f : kind == 3 ? 0.9f : 1f;
                liq.localPosition = new Vector3(0f, 0f, LiquidZ[kind]);
                liq.localScale = new Vector3(rad, rad, d.state == DevState.Full ? d.liquidLen : 0.0001f);
                d.liquid = liq;
                if (d.state != DevState.Full && d.plunger != null) d.plunger.localPosition = d.plungerStart + Vector3.forward * d.travel;
            }
            devices[it] = d;
            Log($"device ready: {it.data.id} ({(tox >= 0 ? "toxin oil" : kind == 4 ? "vial" : kind == 0 ? "syringe" : kind == 1 ? "glass syringe" : kind == 2 ? "injector" : "compact injector")})");
        }

        static Transform Part(Transform parent, string model, Material mat)
        {
            var mesh = BoneModels.Get(model);
            if (mesh == null) return null;
            var go = new GameObject("RageEngine_" + model);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// From HandleHit: a pierce from a FULL device starts the injection (the needle is in the body).
        void DeviceHit(NpcState s, CollisionInstance hit, RagdollPart part)
        {
            if (hit.damageStruct.damageType != DamageType.Pierce) return;
            var col = hit.sourceCollider;
            var it = col != null ? col.GetComponentInParent<Item>() : null;
            if (it == null || !devices.TryGetValue(it, out var d) || d.kind == 4 || d.state != DevState.Full || (d.comp < 0 && d.special == 0 && (d.custom < 0 || recipes[d.custom] == null))) return;
            d.state = DevState.Injecting; d.t0 = Time.time; d.target = s; d.site = part; d.siteLocal = part.transform.InverseTransformPoint(hit.contactPoint);
            Sfx.Play("NRE_Inject", it.transform, 1f);
            s.bioSite = part; s.bioSiteLocal = d.siteLocal;
            if (d.custom >= 0 || d.special > 0 || !IsSerum((Comp)d.comp) || d.comp == (int)Comp.HeroX)
            {
                StartClutch(s, part, hit.contactPoint, null, false, false);
                if (s.clutching) s.clutchUntil = Time.time + 1.5f;
            }
            Log($"{WhoIs(s)}: needle in ({part.type}) - injecting {(d.special == 1 ? "Loyalty Serum" : d.custom >= 0 ? "custom serum " + recipes[d.custom].name : CompName[d.comp])}");
        }

        void TickInjection(Device d, float now)
        {
            float p = Mathf.Clamp01((now - d.t0) / 0.8f);
            if (d.kind >= 2 && d.needle != null) d.needle.localPosition = Vector3.Lerp(new Vector3(0f, 0f, -0.012f), Vector3.zero, Mathf.Clamp01(p * 6f));
            bool stillIn = d.item.isPenetrating;
            if (!stillIn && now - d.t0 > 0.12f) p = Mathf.Min(p, (now - d.t0) / 0.8f);
            SetFill(d, 1f - p);
            if (p >= 1f || (!stillIn && now - d.t0 > 0.12f))
            {
                float dose = p;
                d.state = DevState.Empty;
                var s = d.target; int comp = d.comp;
                d.comp = -1;
                if (s != null && s.active && s.creature != null && dose > 0.05f)
                {
                    Vector3 site = d.site != null ? d.site.transform.TransformPoint(d.siteLocal) : s.creature.transform.position;
                    if (d.special == 1) ApplyLoyalty(s, dose);
                    else if (d.custom >= 0) ApplyCustom(s, d.custom, dose, d.site, site);
                    else ApplyCompound(s, (Comp)comp, dose, site);
                }
                Log($"injection {(dose >= 0.99f ? "complete" : $"interrupted at {dose * 100f:0}%")} - device empty");
            }
        }

        static void SetFill(Device d, float f)
        {
            if (d.liquid != null) { var sc = d.liquid.localScale; sc.z = Mathf.Max(0.0001f, d.liquidLen * f); d.liquid.localScale = sc; }
            if (d.plunger != null) d.plunger.localPosition = d.plungerStart + Vector3.forward * d.travel * (1f - f);
        }

        /// An EMPTY syringe whose needle touches a vial with liquid draws it up.
        static void CheckRefills()
        {
            foreach (var kv in devices)
            {
                var d = kv.Value;
                if (d.kind > 1 || d.state != DevState.Empty || d.root == null || d.refillT >= 0f) continue;
                Vector3 tip = d.root.position;
                foreach (var kv2 in devices)
                {
                    var v = kv2.Value;
                    if (v.kind != 4 || v.comp < 0 || v.root == null) continue;
                    if ((v.root.position - tip).sqrMagnitude > 0.05f * 0.05f) continue;
                    d.comp = v.comp; v.comp = -1; v.state = DevState.Empty;
                    SetFill(v, 0f);
                    if (d.liquid != null) d.liquid.GetComponent<MeshRenderer>().sharedMaterial = Mats.Make(CompLiquid[d.comp], 0.9f);
                    d.refillT = Time.time;
                    Log($"syringe drew up {CompName[d.comp]} from a vial");
                    break;
                }
            }
        }

        /// 0.25: TOXIN OILS. Touch the mouth of a toxin-oil bottle to the blade of a weapon you're holding and it's
        /// coated (the same coating as the toxin daggers / menu buttons: 6 hits). A bottle has 3 coats in it.
        static void CheckOils()
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) return;
            float now = Time.time;
            foreach (var kv in devices)
            {
                var d = kv.Value;
                if (d.tox < 0 || d.uses <= 0 || d.root == null || now < d.nextOil) continue;
                Vector3 mouth = d.root.position;
                foreach (var h in new[] { pc.handLeft, pc.handRight })
                {
                    var w = h != null && h.grabbedHandle != null ? h.grabbedHandle.item : null;
                    if (w == null || w == d.item || devices.ContainsKey(w) || w.data == null || w.data.type != ItemData.Type.Weapon) continue;
                    bool touch = false;
                    foreach (var col in w.GetComponentsInChildren<Collider>())
                    {
                        if (col == null || col.isTrigger || !col.enabled) continue;
                        if ((col.ClosestPoint(mouth) - mouth).sqrMagnitude < 0.045f * 0.045f) { touch = true; break; }
                    }
                    if (!touch) continue;
                    CoatItem(w, (Tox)d.tox, 6);
                    d.uses--; d.nextOil = now + 1.2f;
                    SetFill(d, d.uses / 3f);
                    if (d.uses <= 0) d.state = DevState.Empty;
                    Sfx.Play("NRE_Coat", w.transform, 0.8f);
                    Log($"coated {w.data.id} with {ToxName[d.tox]} oil ({d.uses} coats left)");
                    break;
                }
            }
        }

        static void TickRefillAnim(Device d, float now)
        {
            float p = Mathf.Clamp01((now - d.refillT) / 0.6f);
            SetFill(d, p);
            if (p >= 1f) { d.refillT = -1f; d.state = DevState.Full; }
        }
    }
}

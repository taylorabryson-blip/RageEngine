// Rage Engine 0.17 - metal heat. Hard metal-on-metal clashes heat a weapon up. The game already throws sparks on metal
// hits; Rage Engine adds the HEAT: Cold -> Warming (faint glow) -> Hot (orange glow, extra hiss) -> Cooling (fades
// back to normal). The glow is the material's own emission on a per-item material copy; the original is restored once
// it has cooled. Only metal-on-metal (by the game's physics material ids) counts.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        sealed class HeatState { public float heat; public List<Material> mats; public List<Color> orig; public Item item; public float lastHiss; }
        static readonly Dictionary<Item, HeatState> heat = new Dictionary<Item, HeatState>();
        static readonly HashSet<CollisionHandler> heatHooked = new HashSet<CollisionHandler>();
        static readonly List<Item> heatScratch = new List<Item>();
        static float nextHeatHook;
        static readonly Color HeatColor = new Color(1f, 0.38f, 0.08f);

        static void TickHeat()
        {
            float now = Time.time;
            // hook weapons we haven't hooked yet (cheap: every 2 s, weapons only)
            if (now >= nextHeatHook)
            {
                nextHeatHook = now + 2f;
                var list = Item.allActive;
                if (list != null)
                    for (int i = 0; i < list.Count && heatHooked.Count < 300; i++)
                    {
                        var it = list[i];
                        if (it == null || it.data == null || it.data.type != ItemData.Type.Weapon || phantoms.Contains(it)) continue;
                        var ch = it.mainCollisionHandler;
                        if (ch == null || heatHooked.Contains(ch)) continue;
                        heatHooked.Add(ch);
                        var item = it;
                        ch.OnCollisionStartEvent += hit => QueueMetalHit56(item, hit);
                    }
            }
            // 0.56: the hits recorded during physics are handled here, outside the physics step
            if (metalQ56.Count > 0) { var q = new List<(Item, float, Vector3, bool)>(metalQ56); metalQ56.Clear(); foreach (var x in q) OnMetalHit56(x.Item1, x.Item2, x.Item3, x.Item4); }
            // cool down + glow (0.5 s cadence)
            heatScratch.Clear();
            foreach (var kv in heat) heatScratch.Add(kv.Key);
            foreach (var it in heatScratch)
            {
                var h = heat[it];
                if (it == null || h.item == null) { heat.Remove(it); continue; }
                h.heat = Mathf.Max(0f, h.heat - 0.5f * 0.035f);                 // ~30 s from hot to cold
                Glow(h);
                if (h.heat <= 0f) { Unglow(h); heat.Remove(it); }
            }
        }

        static readonly List<(Item, float, Vector3, bool)> metalQ56 = new List<(Item, float, Vector3, bool)>();
        /// In the physics callback: only note it (0.56 - nothing that touches materials runs inside the physics step).
        static void QueueMetalHit56(Item item, CollisionInstance hit)
        {
            if (!modEnabled || item == null || hit == null || metalQ56.Count > 32) return;
            try { if (IsMetal(hit.sourceMaterial) && IsMetal(hit.targetMaterial)) metalQ56.Add((item, hit.impactVelocity.magnitude, hit.contactPoint, true)); } catch { }
        }

        static void OnMetalHit56(Item item, float speed, Vector3 point, bool metal)
        {
            if (!modEnabled || item == null) return;
            try
            {
                if (speed < 2.5f) return;
                if (!heat.TryGetValue(item, out var h)) { h = new HeatState { item = item }; heat[item] = h; }
                float before = h.heat;
                if (speed > 4f) Emit(point, 12f, Stim.Clash);
                h.heat = Mathf.Min(1.5f, h.heat + 0.035f * speed);
                if (before < 0.4f && h.heat >= 0.4f) Log($"{item.data?.id} is warming up");
                if (before < 1f && h.heat >= 1f) Log($"{item.data?.id} is HOT");
                if (h.heat >= 1f && Time.time - h.lastHiss > 1.5f) { h.lastHiss = Time.time; Sfx.PlayAt("NRE_Swoosh", point, 0.35f); }
                Glow(h);
            }
            catch { }
        }

        static bool IsMetal(MaterialData m) => m != null && m.id != null && m.id.IndexOf("Metal", System.StringComparison.OrdinalIgnoreCase) >= 0;

        static void Glow(HeatState h)
        {
            if (Application.platform == RuntimePlatform.Android) return;                         // 0.56: no runtime material copies / keywords on the Quest (crash)
            if (h.heat < 0.15f) { Unglow(h); return; }
            if (h.mats == null)
            {
                h.mats = new List<Material>(); h.orig = new List<Color>();
                foreach (var r in h.item.GetComponentsInChildren<MeshRenderer>())
                    foreach (var m in r.materials) if (m != null && m.HasProperty("_EmissionColor")) { h.orig.Add(m.GetColor("_EmissionColor")); m.EnableKeyword("_EMISSION"); h.mats.Add(m); }
            }
            float k = Mathf.Clamp01((h.heat - 0.15f) / 1.2f);
            Color e = HeatColor * (k * k * 2.2f);
            for (int i = 0; i < h.mats.Count; i++) if (h.mats[i] != null) h.mats[i].SetColor("_EmissionColor", h.orig[i] + e);
        }

        static void Unglow(HeatState h)
        {
            if (h.mats == null) return;
            for (int i = 0; i < h.mats.Count; i++) if (h.mats[i] != null) h.mats[i].SetColor("_EmissionColor", h.orig[i]);
        }
    }
}

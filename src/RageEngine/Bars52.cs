// Rage Engine 0.52: NPC health bars - floating over heads, always facing you.
//   Health bars: Off / Enemies / Everyone / Only when hurt
//   Bar style:   Classic (framed, damage chip) / Slim (thin line) / Segmented (10 blocks) / Souls (wide, yellow chip lag)
//   Bar color:   By health (green -> red) / Red / Orange / Blue / White
//   Bar size:    Small / Normal / Large
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        public static ModOptionString[] barShowValues = { new ModOptionString("Off", null, "off"), new ModOptionString("Only when hurt", null, "hurt"), new ModOptionString("Enemies", null, "enemies"), new ModOptionString("Everyone", null, "all") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(60)]
        [ModOption("Health bars", "Health bars over NPCs' heads.", valueSourceName = nameof(barShowValues), defaultValueIndex = 0)]
        public static string barShow52 = "off";

        public static ModOptionString[] barStyleValues = { new ModOptionString("Classic", null, "classic"), new ModOptionString("Slim", null, "slim"), new ModOptionString("Segmented", null, "seg"), new ModOptionString("Souls", null, "souls") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(61)]
        [ModOption("Bar style", "Classic: framed with a damage chip. Slim: a thin line. Segmented: 10 blocks. Souls: wide, with a slow yellow damage trail.", valueSourceName = nameof(barStyleValues), defaultValueIndex = 0)]
        public static string barStyle52 = "classic";

        public static ModOptionString[] barColorValues = { new ModOptionString("By health", null, "hp"), new ModOptionString("Red", null, "red"), new ModOptionString("Orange", null, "orange"), new ModOptionString("Blue", null, "blue"), new ModOptionString("White", null, "white") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(62)]
        [ModOption("Bar color", "By health fades green to red as they get hurt.", valueSourceName = nameof(barColorValues), defaultValueIndex = 0)]
        public static string barColor52 = "hp";

        public static ModOptionString[] barSizeValues = { new ModOptionString("Small", null, "0.7"), new ModOptionString("Normal", null, "1"), new ModOptionString("Large", null, "1.4") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(63)]
        [ModOption("Bar size", "", valueSourceName = nameof(barSizeValues), defaultValueIndex = 1)]
        public static string barSize52 = "1";

        sealed class Bar52
        {
            public GameObject root; public Transform frame, bg, chip, fill; public readonly List<Transform> ticks = new List<Transform>();
            public string style, size; public float chipHp = 1f, shownHp = 1f, deadAt = -1f, lastHp = 1f, hurtAt = -100f; public int life;   // 0.54: whose bar
            public MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        }
        static readonly Dictionary<Creature, Bar52> bars52 = new Dictionary<Creature, Bar52>();
        static Material barMat52;

        static Material BarMat52()
        {
            if (barMat52 != null) return barMat52;
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null || !sh.isSupported) sh = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            barMat52 = new Material(sh) { name = "RageEngine_Bar", renderQueue = 3100 };
            return barMat52;
        }

        static void Tint52(Transform t, MaterialPropertyBlock mpb, Color c)
        {
            var r = t.GetComponent<Renderer>(); if (r == null) return;
            r.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", c); mpb.SetColor("_Color", c); r.SetPropertyBlock(mpb);
        }

        static Transform Quad52(Transform parent, string name)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = name;
            Object.Destroy(q.GetComponent<Collider>());
            var r = q.GetComponent<Renderer>(); r.sharedMaterial = BarMat52(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            q.transform.SetParent(parent, false);
            return q.transform;
        }

        static void Build52(Bar52 b)
        {
            if (b.root != null) Object.Destroy(b.root);
            b.ticks.Clear();
            b.style = barStyle52; b.size = barSize52;
            b.root = new GameObject("RageEngine_HealthBar");
            var t = b.root.transform;
            b.frame = Quad52(t, "frame"); b.bg = Quad52(t, "bg"); b.chip = Quad52(t, "chip"); b.fill = Quad52(t, "fill");
            if (b.style == "seg") for (int i = 1; i < 10; i++) b.ticks.Add(Quad52(t, "tick"));
        }

        static void Dims52(string style, out float w, out float h, out float pad)
        {
            switch (style)
            {
                case "slim": w = 0.42f; h = 0.014f; pad = 0.004f; break;
                case "seg": w = 0.46f; h = 0.05f; pad = 0.008f; break;
                case "souls": w = 0.62f; h = 0.034f; pad = 0.006f; break;
                default: w = 0.46f; h = 0.055f; pad = 0.009f; break;
            }
        }

        static Color FillColor52(float hp)
        {
            switch (barColor52)
            {
                case "red": return new Color(0.78f, 0.06f, 0.06f);
                case "orange": return new Color(0.95f, 0.45f, 0.05f);
                case "blue": return new Color(0.15f, 0.45f, 0.95f);
                case "white": return new Color(0.92f, 0.92f, 0.92f);
                default: return Color.HSVToRGB(Mathf.Lerp(0f, 0.33f, Mathf.Clamp01(hp * 1.15f - 0.05f)), 0.85f, 0.85f);
            }
        }

        void LateUpdateBars52()
        {
            if (barShow52 == "off" || !modEnabled)
            {
                if (bars52.Count > 0) { foreach (var kv in bars52) if (kv.Value.root != null) Object.Destroy(kv.Value.root); bars52.Clear(); }
                return;
            }
            Transform cam = Player.local != null && Player.local.head != null ? Player.local.head.transform : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null) return;
            float now = Time.time, dt = Time.deltaTime;
            float.TryParse(barSize52, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float k); if (k <= 0f) k = 1f;
            // tracked NPCs
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active || s.creature == null) continue;
                var c = s.creature;
                if (bars52.TryGetValue(c, out var old) && old.life != s.life54) { if (old.root != null) Object.Destroy(old.root); bars52.Remove(c); }   // 0.54: a new person gets a new, full bar
                if (!bars52.TryGetValue(c, out var b)) { b = new Bar52 { life = s.life54 }; bars52[c] = b; }
                if (b.root == null || b.style != barStyle52 || b.size != barSize52) Build52(b);
                float hp = c.isKilled ? 0f : Mathf.Clamp01(HpFrac(c));
                if (hp < b.lastHp - 0.001f) b.hurtAt = now;
                b.lastHp = hp;
                if (c.isKilled) { if (b.deadAt < 0f) b.deadAt = now; } else b.deadAt = -1f;
                var head = c.ragdoll != null ? c.ragdoll.headPart : null;
                Vector3 pos = (head != null ? head.transform.position : c.transform.position + Vector3.up * 1.7f) + Vector3.up * 0.32f * Mathf.Lerp(1f, k, 0.5f);
                float dist = Vector3.Distance(cam.position, pos);
                bool show = dist < 22f && (b.deadAt < 0f || now - b.deadAt < 1.2f);
                if (barShow52 == "enemies" && s.ally) show = false;
                if (barShow52 == "hurt" && hp > 0.995f && now - b.hurtAt > 6f) show = false;
                if (b.root.activeSelf != show) b.root.SetActive(show);
                if (!show) continue;
                // animate: fill eases, chip trails (souls: slow and yellow)
                b.shownHp = Mathf.MoveTowards(b.shownHp, hp, dt * 2.5f);
                bool souls = b.style == "souls";
                if (b.chipHp < b.shownHp) b.chipHp = b.shownHp;
                else if (now - b.hurtAt > (souls ? 0.8f : 0.35f)) b.chipHp = Mathf.MoveTowards(b.chipHp, b.shownHp, dt * (souls ? 0.35f : 0.9f));
                Dims52(b.style, out float w, out float h, out float pad);
                var t = b.root.transform;
                t.position = pos;
                t.rotation = Quaternion.LookRotation(pos - cam.position, Vector3.up);
                float sc = k * Mathf.Clamp(dist / 4f, 0.8f, 2.2f);                       // readable far away, not huge close up
                t.localScale = Vector3.one * sc;
                b.frame.localPosition = new Vector3(0f, 0f, 0.002f); b.frame.localScale = new Vector3(w + pad * 2f, h + pad * 2f, 1f);
                b.bg.localPosition = new Vector3(0f, 0f, 0.0015f); b.bg.localScale = new Vector3(w, h, 1f);
                b.chip.localScale = new Vector3(Mathf.Max(0.0001f, w * b.chipHp), h, 1f); b.chip.localPosition = new Vector3(-w * 0.5f + w * b.chipHp * 0.5f, 0f, 0.001f);
                b.fill.localScale = new Vector3(Mathf.Max(0.0001f, w * b.shownHp), h, 1f); b.fill.localPosition = new Vector3(-w * 0.5f + w * b.shownHp * 0.5f, 0f, 0.0005f);
                for (int j = 0; j < b.ticks.Count; j++) { b.ticks[j].localScale = new Vector3(0.004f, h, 1f); b.ticks[j].localPosition = new Vector3(-w * 0.5f + w * (j + 1) / 10f, 0f, 0f); }
                float fade = b.deadAt > 0f ? Mathf.Clamp01(1f - (now - b.deadAt) / 1.2f) : 1f;
                Tint52(b.frame, b.mpb, b.style == "slim" ? new Color(0f, 0f, 0f, 0.6f * fade) : new Color(0.02f, 0.02f, 0.02f, 0.85f * fade));
                Tint52(b.bg, b.mpb, new Color(0.12f, 0.03f, 0.03f, 0.9f * fade));
                Tint52(b.chip, b.mpb, souls ? new Color(0.95f, 0.75f, 0.15f, fade) : new Color(0.95f, 0.95f, 0.95f, 0.85f * fade));
                Tint52(b.fill, b.mpb, FillColor52(b.shownHp) * new Color(1f, 1f, 1f, fade));
                foreach (var tk in b.ticks) Tint52(tk, b.mpb, new Color(0.02f, 0.02f, 0.02f, fade));
            }
            // clean up bars whose NPC is gone or no longer tracked
            if (Time.frameCount % 30 == 0)
            {
                List<Creature> gone = null;
                foreach (var kv in bars52)
                    if (kv.Key == null || !byCreature.ContainsKey(kv.Key)) { if (kv.Value.root != null) Object.Destroy(kv.Value.root); (gone ??= new List<Creature>()).Add(kv.Key); }
                if (gone != null) foreach (var g in gone) bars52.Remove(g);
            }
        }
    }
}

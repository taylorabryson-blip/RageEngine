// Rage Engine 0.54 - two new items (Item book: Utilities).
//
// NOISEMAKER - a beat-up wind-up alarm clock. Pull the trigger while holding it: it winds (a few ticks) and rings for
//   six seconds - the hammer hammers between the bells, the clock buzzes in your hand and dances on a table. Throw it
//   and it rings when it lands. While it rings, every NPC within earshot hears a noise there (the "Noises draw NPCs"
//   setting decides whether idle ones walk over to check). Other button / trigger again: stops it.
// NPC SCANNER - a rugged handheld field scanner. Point the top at an NPC and pull the trigger: the screen shows what
//   Rage Engine knows about him - vitals (HP, pain, stamina, bleeding, what he's doing), injuries (every broken bone,
//   gouged eye, broken jaw, concussion, burns...) and his mind (morale, fear / anger, whether he's seen or heard you,
//   how he's defending, skill, hidden knife). It only READS the NPC's existing state - it never adds or changes
//   anything. The bottom line shows the body's life number and the state's generation, so you can watch the 0.54
//   ownership fix at work. Other button: next page. Updates twice a second while you hold it.
using System;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const string Glyphs54 = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,:%/-+()'!?#=<>_*[]|~";
        static readonly byte[] Font54 = {0, 0, 0, 0, 0, 0, 0, 14, 17, 17, 31, 17, 17, 17, 30, 17, 17, 30, 17, 17, 30, 14, 17, 16, 16, 16, 17, 14, 28, 18, 17, 17, 17, 18, 28, 31, 16, 16, 30, 16, 16, 31, 31, 16, 16, 30, 16, 16, 16, 14, 17, 16, 23, 17, 17, 15, 17, 17, 17, 31, 17, 17, 17, 14, 4, 4, 4, 4, 4, 14, 7, 2, 2, 2, 2, 18, 12, 17, 18, 20, 24, 20, 18, 17, 16, 16, 16, 16, 16, 16, 31, 17, 27, 21, 21, 17, 17, 17, 17, 17, 25, 21, 19, 17, 17, 14, 17, 17, 17, 17, 17, 14, 30, 17, 17, 30, 16, 16, 16, 14, 17, 17, 17, 21, 18, 13, 30, 17, 17, 30, 20, 18, 17, 15, 16, 16, 14, 1, 1, 30, 31, 4, 4, 4, 4, 4, 4, 17, 17, 17, 17, 17, 17, 14, 17, 17, 17, 17, 17, 10, 4, 17, 17, 17, 21, 21, 21, 10, 17, 17, 10, 4, 10, 17, 17, 17, 17, 10, 4, 4, 4, 4, 31, 1, 2, 4, 8, 16, 31, 14, 17, 19, 21, 25, 17, 14, 4, 12, 4, 4, 4, 4, 14, 14, 17, 1, 2, 4, 8, 31, 31, 2, 4, 2, 1, 17, 14, 2, 6, 10, 18, 31, 2, 2, 31, 16, 30, 1, 1, 17, 14, 6, 8, 16, 30, 17, 17, 14, 31, 1, 2, 4, 8, 8, 8, 14, 17, 17, 14, 17, 17, 14, 14, 17, 17, 15, 1, 2, 12, 0, 0, 0, 0, 0, 12, 12, 0, 0, 0, 0, 12, 4, 8, 0, 12, 12, 0, 12, 12, 0, 24, 25, 2, 4, 8, 19, 3, 0, 1, 2, 4, 8, 16, 0, 0, 0, 0, 31, 0, 0, 0, 0, 4, 4, 31, 4, 4, 0, 2, 4, 8, 8, 8, 4, 2, 8, 4, 2, 2, 2, 4, 8, 4, 4, 8, 0, 0, 0, 0, 4, 4, 4, 4, 4, 0, 4, 14, 17, 1, 2, 4, 0, 4, 31, 31, 31, 31, 31, 31, 31, 0, 0, 31, 0, 31, 0, 0, 2, 4, 8, 16, 8, 4, 2, 8, 4, 2, 1, 2, 4, 8, 0, 0, 0, 0, 0, 0, 31, 0, 4, 21, 14, 21, 4, 0, 14, 8, 8, 8, 8, 8, 14, 14, 2, 2, 2, 2, 2, 14, 4, 4, 4, 4, 4, 4, 4, 0, 0, 8, 21, 2, 0, 0};

        // ================================================================== shared
        static float nextItemScan54;
        readonly bool[] itemUse54 = new bool[2], itemAlt54 = new bool[2];

        static Material Tex54(string file, Color fallback, float smooth)
        {
            var t = GoreTex.MeshTex("Items54/" + file);
            return t != null ? Mats.MakeTextured(t, null, Color.white, smooth, false) : Mats.Make(fallback, smooth);
        }

        static Transform Part54(Transform root, string mesh, Material mat)
        {
            var mm = BoneModels.Get(mesh); if (mm == null) return null;
            var g = new GameObject(mesh); g.transform.SetParent(root, false);
            g.AddComponent<MeshFilter>().sharedMesh = mm;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        static Transform NewRoot54(Item it, string name)
        {
            foreach (var r in it.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            var root = new GameObject(name).transform; root.SetParent(it.transform, false);
            root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity;
            return root;
        }

        void TickItems54()
        {
            float now = Time.time;
            // dress them as soon as they exist (from the book, a shelf, wherever), not only once held
            if (now >= nextItemScan54 && Item.allActive != null)
            {
                nextItemScan54 = now + 1f;
                foreach (var it in Item.allActive)
                {
                    if (it == null || it.data == null || it.data.id == null) continue;
                    if (it.data.id == "RE_Noisemaker" && !clocks54.ContainsKey(it)) { var i2 = it; Guard("noise.dress", () => DressClock54(i2)); }
                    else if (it.data.id == "RE_Scanner" && !scanners54.ContainsKey(it)) { var i2 = it; Guard("scan.dress", () => DressScanner54(i2)); }
                }
                List<Item> gone = null;
                foreach (var kv in clocks54) if (kv.Key == null) (gone ??= new List<Item>()).Add(kv.Key);
                foreach (var kv in scanners54) if (kv.Key == null) (gone ??= new List<Item>()).Add(kv.Key);
                if (gone != null) foreach (var g in gone) { clocks54.Remove(g); scanners54.Remove(g); }
            }
            // held: trigger / other button
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc != null)
                for (int k = 0; k < 2; k++)
                {
                    var h = k == 0 ? pc.handLeft : pc.handRight;
                    var it = h != null && h.grabbedHandle != null ? h.grabbedHandle.item : null;
                    bool use = false, alt = false;
                    if (it != null && it.data != null && (it.data.id == "RE_Noisemaker" || it.data.id == "RE_Scanner"))
                    {
                        try { var ch = h.playerHand.controlHand; use = ch.usePressed; alt = ch.alternateUsePressed; } catch { }
                        bool useDown = use && !itemUse54[k], altDown = alt && !itemAlt54[k];
                        if (clocks54.TryGetValue(it, out var ck)) { if (useDown) { if (now < ck.ringUntil) StopClock54(ck); else RingClock54(ck, 6f, true); } else if (altDown) StopClock54(ck); if (now < ck.ringUntil && now >= ck.startAt && now >= ck.nextBuzz) { ck.nextBuzz = now + 0.15f; Haptics.Pulse(h.side, 0.25f, 1); } }
                        if (scanners54.TryGetValue(it, out var sc)) { if (useDown) { var hh = h; Guard("scan.scan", () => DoScan54(sc, hh)); } if (altDown) { sc.page = (sc.page + 1) % 3; DrawScan54(sc); Haptics.Pulse(h.side, 0.2f, 1); } sc.held = true; sc.heldAt = now; }
                    }
                    itemUse54[k] = use; itemAlt54[k] = alt;
                }
            foreach (var kv in clocks54) if (kv.Key != null) { var ck = kv.Value; Guard("noise.tick", () => TickClock54(ck, now)); }
            foreach (var kv in scanners54) if (kv.Key != null) { var sc = kv.Value; Guard("scan.tick", () => TickScanner54(sc, now)); }
        }

        // ================================================================== NOISEMAKER
        sealed class Clock54 { public Item it; public Transform model, hammer; public AudioSource src; public float startAt = -1f, ringUntil = -1f, nextEmit, speed, nextBuzz, nextDance; }
        static readonly Dictionary<Item, Clock54> clocks54 = new Dictionary<Item, Clock54>();
        static readonly Vector3 HammerPivot54 = new Vector3(0f, 0.0325f, 0.0035f);   // printed by re_items54.py

        static void DressClock54(Item it)
        {
            var k = new Clock54 { it = it };
            clocks54[it] = k;
            var root = NewRoot54(it, "RageEngine_Noisemaker"); k.model = root;
            var brass = Tex54("noise_brass.png", new Color(0.75f, 0.58f, 0.27f), 0.65f);
            bool any = Part54(root, "RE_Noise_Body", Tex54("noise_body.png", new Color(0.58f, 0.09f, 0.08f), 0.6f)) != null;
            Part54(root, "RE_Noise_Brass", brass);
            Part54(root, "RE_Noise_Face", Tex54("noise_face.png", new Color(0.9f, 0.86f, 0.76f), 0.3f));
            Part54(root, "RE_Noise_Hands", Mats.Make(new Color(0.03f, 0.03f, 0.03f), 0.5f));
            Part54(root, "RE_Noise_Alarm", Mats.Make(new Color(0.6f, 0.06f, 0.05f), 0.5f));
            var piv = new GameObject("HammerPivot").transform; piv.SetParent(root, false); piv.localPosition = HammerPivot54;
            k.hammer = Part54(piv, "RE_Noise_Hammer", brass) != null ? piv : null;
            if (!any)
            {   // models missing: a plain stand-in so the item still works
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, false); g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); g.transform.localScale = new Vector3(0.076f, 0.016f, 0.076f);
                g.GetComponent<Renderer>().sharedMaterial = Mats.Make(new Color(0.58f, 0.09f, 0.08f), 0.6f);
            }
        }

        static void RingClock54(Clock54 k, float seconds, bool wound)
        {
            float now = Time.time;
            k.startAt = now + (wound ? 0.7f : 0f); k.ringUntil = k.startAt + seconds; k.nextEmit = k.startAt;
            if (wound) Sfx.Play("NRE_ClockWind", k.it.transform, 0.8f);
            Log($"Noisemaker: ringing for {seconds:0} s");
        }

        static void StopClock54(Clock54 k)
        {
            k.ringUntil = -1f; k.startAt = -1f;
            if (k.src != null) k.src.Stop();
            if (k.hammer != null) k.hammer.localRotation = Quaternion.identity;
            if (k.model != null) k.model.localPosition = Vector3.zero;
        }

        void TickClock54(Clock54 k, float now)
        {
            var it = k.it; var pb = it.physicBody;
            bool held = it.IsHanded();
            // thrown: rings when it lands
            float sp = pb != null ? pb.velocity.magnitude : 0f;
            if (!held && now >= k.ringUntil && k.speed > 3.5f && sp < k.speed * 0.45f)
            {
                Sfx.Play("NRE_MetalBang", it.transform, 0.45f, 1.4f);
                RingClock54(k, 4f, false);
            }
            k.speed = held ? 0f : sp;
            bool ringing = now >= k.startAt && now < k.ringUntil && k.startAt >= 0f;
            if (!ringing)
            {
                if (k.src != null && k.src.isPlaying) StopClock54(k);
                return;
            }
            // the bell
            if (k.src == null && Sfx.TryClip("NRE_AlarmRing", out var clip))
            {
                k.src = it.gameObject.AddComponent<AudioSource>();
                k.src.clip = clip; k.src.loop = true; k.src.spatialBlend = 1f; k.src.rolloffMode = AudioRolloffMode.Linear;
                k.src.minDistance = 1f; k.src.maxDistance = 30f; k.src.dopplerLevel = 0f;
            }
            if (k.src != null)
            {
                k.src.volume = Mathf.Clamp01(sfxVolume * 0.9f);
                if (!k.src.isPlaying) k.src.Play();
            }
            // everyone in earshot hears it (hearing is separate from sight: they still have to see you)
            if (now >= k.nextEmit) { k.nextEmit = now + 0.8f; Emit(it.transform.position, 18f, Stim.Noise); }
            // the hammer, the buzz, and a free-standing clock walking across the table
            if (k.hammer != null) k.hammer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(now * Mathf.PI * 2f * 12f) * 30f);
            if (k.model != null) k.model.localPosition = Random.insideUnitSphere * 0.0007f;
            if (!held && pb != null && now >= k.nextDance)
            {
                k.nextDance = now + 0.1f;
                Vector2 r = Random.insideUnitCircle * 0.004f;
                Guard("noise.dance", () => pb.AddForce(new Vector3(r.x, 0.002f, r.y), ForceMode.Impulse));
            }
        }

        // ================================================================== NPC SCANNER
        sealed class Scan54 { public Item it; public Transform model; public Texture2D tex; public Color32[] px; public Creature target; public int life, page; public float nextDraw, heldAt; public bool held, idle = true; }
        static readonly Dictionary<Item, Scan54> scanners54 = new Dictionary<Item, Scan54>();
        const int LcdW54 = 256, LcdH54 = 160;
        static Color32[] lcdBg54;
        static readonly Color32 Fg54 = new Color32(156, 245, 168, 255), Dim54 = new Color32(78, 138, 92, 255), Warn54 = new Color32(250, 196, 92, 255), Bad54 = new Color32(255, 104, 88, 255), Bg54 = new Color32(9, 20, 13, 255);

        static void DressScanner54(Item it)
        {
            var sc = new Scan54 { it = it };
            scanners54[it] = sc;
            var root = NewRoot54(it, "RageEngine_Scanner"); sc.model = root;
            bool any = Part54(root, "RE_Scan_Shell", Tex54("scanner_shell.png", new Color(0.84f, 0.38f, 0.09f), 0.45f)) != null;
            Part54(root, "RE_Scan_Rubber", Tex54("scanner_rubber.png", new Color(0.13f, 0.13f, 0.13f), 0.2f));
            Part54(root, "RE_Scan_Keys", Tex54("scanner_keys.png", new Color(0.12f, 0.12f, 0.13f), 0.4f));
            Part54(root, "RE_Scan_Lens", Emissive53(new Color(0.9f, 0.08f, 0.05f)));
            sc.tex = new Texture2D(LcdW54, LcdH54, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "RageEngine_ScannerLCD" };
            sc.px = new Color32[LcdW54 * LcdH54];
            var mat = Mats.MakeTextured(sc.tex, null, Color.white, 0.85f, false);
            try { mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; mat.SetTexture("_EmissionMap", sc.tex); mat.SetColor("_EmissionColor", Color.white * 0.9f); } catch { }
            var screen = Part54(root, "RE_Scan_Screen", mat);
            if (screen != null)
            {
                var lg = new GameObject("Backlight"); lg.transform.SetParent(screen, false); lg.transform.localPosition = new Vector3(0f, 0.055f, -0.03f);
                var L = lg.AddComponent<Light>(); L.type = LightType.Point; L.color = new Color(0.7f, 0.95f, 0.75f); L.range = 0.25f; L.intensity = 0.5f; L.shadows = LightShadows.None;
            }
            if (!any)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, false); g.transform.localScale = new Vector3(0.074f, 0.155f, 0.03f);
                g.GetComponent<Renderer>().sharedMaterial = Mats.Make(new Color(0.84f, 0.38f, 0.09f), 0.45f);
            }
            DrawScan54(sc);
        }

        void TickScanner54(Scan54 sc, float now)
        {
            bool held = sc.it.IsHanded();
            if (held && sc.model != null)
            {   // screen turned toward your face (yaw around the grip), like the NPC Spawner
                var head = Player.local != null && Player.local.head != null ? Player.local.head.transform : null;
                if (head != null)
                {
                    Vector3 up = sc.it.transform.up;
                    Vector3 toFace = Vector3.ProjectOnPlane(head.position - sc.it.transform.position, up);
                    if (toFace.sqrMagnitude > 1e-4f) sc.model.rotation = Quaternion.LookRotation(-toFace.normalized, up);
                }
            }
            if (sc.target != null && held && now >= sc.nextDraw) DrawScan54(sc);
        }

        void DoScan54(Scan54 sc, RagdollHand h)
        {
            var it = sc.it; var pc = Player.local != null ? Player.local.creature : null;
            Vector3 dir = it.transform.up, from = it.transform.position;
            if (pc != null && Vector3.Dot(dir, pc.transform.position - from) > 0f && Player.local.head != null) dir = Player.local.head.transform.forward;   // held upside down: use your gaze
            Creature found = null; float best = 16f;
            var hits = Physics.SphereCastAll(from + dir * 0.12f, 0.15f, dir, 15f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var c = hit.collider != null ? hit.collider.GetComponentInParent<Creature>() : null;
                if (c == null || c.isPlayer || hit.distance >= best) continue;
                best = hit.distance; found = c;
            }
            sc.target = found; sc.life = found != null ? LifeOf54(found) : 0; sc.idle = false;
            Sfx.Play("NRE_ScanBeep", it.transform, 0.7f, found != null ? 1f : 0.7f);
            Haptics.Pulse(h.side, found != null ? 0.5f : 0.2f, found != null ? 2 : 1);
            DrawScan54(sc);
            if (found != null) Log($"Scanner: {found.name}");
        }

        // ---------------------------------------------------------------- the screen (twin of tools/items54 lcd.py)
        static void LcdBg54(Color32[] px)
        {
            if (lcdBg54 == null)
            {
                lcdBg54 = new Color32[LcdW54 * LcdH54];
                for (int y = 0; y < LcdH54; y++)
                    for (int x = 0; x < LcdW54; x++)
                    {
                        float k = (y % 2 == 1) ? 0.82f : 1f;                                    // LCD rows
                        float vx = Mathf.Abs(x - LcdW54 / 2f) / (LcdW54 / 2f), vy = Mathf.Abs(y - LcdH54 / 2f) / (LcdH54 / 2f);
                        float v = (1f - 0.25f * (vx * vx + vy * vy)) * k;                       // vignette
                        lcdBg54[(LcdH54 - 1 - y) * LcdW54 + x] = new Color32((byte)(Bg54.r * v), (byte)(Bg54.g * v), (byte)(Bg54.b * v), 255);
                    }
            }
            Array.Copy(lcdBg54, px, px.Length);
        }

        static void Rect54(Color32[] px, int x0, int y0, int x1, int y1, Color32 c)
        {
            x0 = Mathf.Max(0, x0); y0 = Mathf.Max(0, y0); x1 = Mathf.Min(LcdW54, x1); y1 = Mathf.Min(LcdH54, y1);
            for (int y = y0; y < y1; y++) { int row = (LcdH54 - 1 - y) * LcdW54; for (int x = x0; x < x1; x++) px[row + x] = c; }
        }

        static void Text54(Color32[] px, int x, int y, string s, Color32 c, int sc = 2)
        {
            if (string.IsNullOrEmpty(s)) return;
            s = s.ToUpperInvariant();
            foreach (char ch in s)
            {
                int gi = Glyphs54.IndexOf(ch); if (gi < 0) gi = Glyphs54.IndexOf('?');
                for (int j = 0; j < 7; j++)
                {
                    int bits = Font54[gi * 7 + j];
                    for (int i = 0; i < 5; i++)
                        if ((bits >> (4 - i) & 1) != 0) Rect54(px, x + i * sc, y + j * sc, x + i * sc + sc, y + j * sc + sc, c);
                }
                x += 6 * sc;
                if (x > LcdW54) break;
            }
        }

        static void Bar54(Color32[] px, int x, int y, int w, float f, Color32 c)
        {
            Rect54(px, x, y, x + w, y + 10, Dim54); Rect54(px, x + 1, y + 1, x + w - 1, y + 9, Bg54);
            Rect54(px, x + 2, y + 2, x + 2 + (int)((w - 4) * Mathf.Clamp01(f)), y + 8, c);
        }

        static string Fit54(string s) => s == null ? "" : s.Length > 21 ? s.Substring(0, 21) : s;

        void DrawScan54(Scan54 sc)
        {
            if (sc.tex == null) return;
            float now = Time.time; sc.nextDraw = now + 0.5f;
            var px = sc.px; LcdBg54(px);
            var lines = new List<(string t, Color32 c, float bar, string label)>();
            string title;
            var c = sc.target;
            if (sc.idle) { title = "RE SCANNER"; lines.Add(("", Fg54, -1f, null)); lines.Add(("AIM AT SOMEONE", Fg54, -1f, null)); lines.Add(("AND PULL THE", Fg54, -1f, null)); lines.Add(("TRIGGER", Fg54, -1f, null)); lines.Add(("", Fg54, -1f, null)); lines.Add(("OTHER BUTTON:", Dim54, -1f, null)); lines.Add(("NEXT PAGE", Dim54, -1f, null)); }
            else if (c == null) { title = "NO TARGET"; lines.Add(("", Fg54, -1f, null)); lines.Add(("NOBODY THERE", Warn54, -1f, null)); lines.Add(("AIM THE TOP OF THE", Dim54, -1f, null)); lines.Add(("SCANNER AT AN NPC", Dim54, -1f, null)); }
            else if (!Same54(c, sc.life)) { title = "SIGNAL LOST"; lines.Add(("", Fg54, -1f, null)); lines.Add(("THAT BODY IS", Warn54, -1f, null)); lines.Add(("SOMEBODY ELSE NOW", Warn54, -1f, null)); lines.Add(("SCAN AGAIN", Dim54, -1f, null)); sc.target = null; }
            else
            {
                TryGet(c, out var s);
                title = sc.page == 0 ? "VITALS" : sc.page == 1 ? "INJURIES" : "MIND";
                if (sc.page == 0) PageVitals54(c, s, now, lines);
                else if (sc.page == 1) PageInjuries54(c, s, now, lines);
                else PageMind54(c, s, now, lines);
            }
            Rect54(px, 0, 0, LcdW54, 18, Fg54); Text54(px, 4, 2, title, Bg54);
            if (c != null && !sc.idle && title != "SIGNAL LOST") Text54(px, LcdW54 - 4 - 3 * 12, 2, (sc.page + 1) + "/3", Bg54);
            int y = 22;
            foreach (var (t, col, bar, label) in lines)
            {
                if (y > LcdH54 - 14) break;
                if (bar >= 0f) { Text54(px, 4, y, label, Dim54); Bar54(px, 92, y + 2, 104, bar, col); Text54(px, 204, y, Mathf.RoundToInt(bar * 100f) + "%", col); }
                else Text54(px, 4, y, Fit54(t), col);
                y += 17;
            }
            sc.tex.SetPixels32(px); sc.tex.Apply(false);
        }

        static void Line54(List<(string, Color32, float, string)> l, string t, Color32 c) => l.Add((t, c, -1f, null));

        void PageVitals54(Creature c, NpcState s, float now, List<(string, Color32, float, string)> l)
        {
            Line54(l, s != null ? WhoIs(s) : c.name, Fg54);
            if (s != null && s.personality != null) Line54(l, s.personality.type + " " + ProfName(s.prof43), Dim54); else Line54(l, "NOT TRACKED", Dim54);
            float hp = c.isKilled ? 0f : HpFrac(c);
            l.Add(("", hp > 0.5f ? Fg54 : hp > 0.2f ? Warn54 : Bad54, hp, "HP"));
            if (s == null) return;
            l.Add(("", s.pain > 0.6f ? Bad54 : s.pain > 0.25f ? Warn54 : Fg54, Mathf.Clamp01(s.pain), "PAIN"));
            if (exhaustion54) { float st = 1f - s.exert54; l.Add(("", st > 0.5f ? Fg54 : st > 0.2f ? Warn54 : Bad54, st, "STAMINA")); }
            Line54(l, s.bleedRate > 0.05f ? $"BLEEDING {s.bleedRate:0.0}/S" : "NOT BLEEDING", s.bleedRate > 0.05f ? Warn54 : Dim54);
            string state = c.isKilled ? "DECEASED" : s.surrendered ? "SURRENDERED" : s.knockedOut || s.passedOut ? "KNOCKED OUT" : s.inCombat ? "FIGHTING"
                         : s.pat54 == Check54 || s.pat54 == Look54 ? "CHECKING A NOISE" : s.walking54 ? "ON PATROL" : "IDLE";
            Line54(l, "STATE: " + state, c.isKilled ? Bad54 : Fg54);
            Line54(l, $"LIFE {s.life54}  GEN {s.gen54}", Dim54);
        }

        static readonly (RagdollPart.Type t, string n)[] PartNames54 = {
            (RagdollPart.Type.Head, "SKULL"), (RagdollPart.Type.Neck, "NECK"), (RagdollPart.Type.Torso, "CHEST"),
            (RagdollPart.Type.LeftArm, "LEFT ARM"), (RagdollPart.Type.RightArm, "RIGHT ARM"), (RagdollPart.Type.LeftHand, "LEFT HAND"), (RagdollPart.Type.RightHand, "RIGHT HAND"),
            (RagdollPart.Type.LeftLeg, "LEFT LEG"), (RagdollPart.Type.RightLeg, "RIGHT LEG"), (RagdollPart.Type.LeftFoot, "LEFT FOOT"), (RagdollPart.Type.RightFoot, "RIGHT FOOT") };

        void PageInjuries54(Creature c, NpcState s, float now, List<(string, Color32, float, string)> l)
        {
            if (s == null) { Line54(l, "NO DATA", Dim54); return; }
            var all = new List<(string, Color32)>();
            foreach (var (t, n) in PartNames54) if ((s.brokenParts & t) != 0) all.Add((n + " BROKEN", Bad54));
            if (s.headCrushed) all.Add(("SKULL CRUSHED", Bad54));
            if (s.spineBroken) all.Add(("SPINE BROKEN", Bad54));
            if (s.jawTorn) all.Add(("JAW TORN LOOSE", Bad54)); else if (s.jawBroken) all.Add(("JAW BROKEN", Bad54));
            int eyes = 0; for (int i = 0; i < 8; i++) if ((s.gouged & (1 << i)) != 0) eyes++;
            if (eyes > 0) all.Add((eyes > 1 ? "BOTH EYES GOUGED" : "EYE GOUGED", Bad54));
            if (s.throatCutUntil > now) all.Add(("THROAT CUT", Bad54));
            if (s.gutsOut44 || s.gutImpaled) all.Add(("GUTS OUT", Bad54));
            if (s.lostLeg) all.Add(("LEG GONE", Bad54)); else if (s.dismembered) all.Add(("DISMEMBERED", Bad54));
            if (s.ribsBroken > 0) all.Add(($"{s.ribsBroken} RIB{(s.ribsBroken > 1 ? "S" : "")} BROKEN", Warn54));
            if (s.concussUntil50 > now) all.Add(($"CONCUSSED {s.concussUntil50 - now:0}S", Warn54));
            else if (s.brain50 > 0.5f) all.Add(("BRAIN DAMAGE", Warn54));
            if (s.noseBroken) all.Add(("NOSE BROKEN", Warn54));
            if (s.teethLost > 0) all.Add(($"TEETH LOST: {s.teethLost}", Dim54));
            if (s.charMarks50 > 0) all.Add(("BURNS", Warn54));
            if (s.shockScarred50) all.Add(("LIGHTNING SCARS", Dim54));
            if (HasTag(s, Tag.Blind)) all.Add(("BLIND", Bad54));
            if (all.Count == 0) { Line54(l, "NO INJURIES", Fg54); return; }
            for (int i = 0; i < all.Count && i < 8; i++)
            {
                if (i == 7 && all.Count > 8) { Line54(l, $"+{all.Count - 7} MORE", Dim54); break; }
                Line54(l, all[i].Item1, all[i].Item2);
            }
        }

        void PageMind54(Creature c, NpcState s, float now, List<(string, Color32, float, string)> l)
        {
            if (s == null) { Line54(l, "NO DATA", Dim54); return; }
            float mo = Mathf.Clamp01(s.morale51);
            Line54(l, $"MORALE {Mathf.RoundToInt(mo * 100f)}%", mo > 0.6f ? Fg54 : mo > 0.3f ? Warn54 : Bad54);
            string mood = (HasTag(s, Tag.Anger) ? "ANGRY  " : "") + (HasTag(s, Tag.Fear) ? "AFRAID" : "");
            Line54(l, mood.Length > 0 ? mood.Trim() : "CALM", mood.Length > 0 ? Bad54 : Fg54);
            Line54(l, s.seenAt > 0f ? $"SAW YOU {now - s.seenAt:0}S AGO" : "HASN'T SEEN YOU", Fg54);
            if (now - s.heardAt < 10f && s.heardAt > 0f) Line54(l, s.heardKind == Stim.Noise ? "HEARD A NOISE" : "HEARD " + s.heardKind.ToString(), Dim54);
            if (defense54) Line54(l, "DEFENSE: " + Profiles54[Mathf.Clamp(s.profile54, 0, Profiles54.Length - 1)], Fg54);
            Line54(l, $"SKILL: {ProfName(s.prof43)} {Mathf.RoundToInt(s.prof43 * 100f)}", Dim54);
            if (s.hasKnife && !s.knifeOut) Line54(l, "HIDDEN KNIFE", Warn54);
            if (s.identity != null && s.identity.escapes > 0) Line54(l, $"ESCAPED YOU {s.identity.escapes}X", Warn54);
            else if (s.brave) Line54(l, "BRAVE", Dim54);
        }
    }
}

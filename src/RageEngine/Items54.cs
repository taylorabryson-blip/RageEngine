// Rage Engine 0.54 - medieval items (Item book: Utilities; also stocked in the Crystal Hunt shop, see Shop54.cs).
//
// FACTION NOISEMAKERS - one per faction, made the way that faction would make it. Pull the trigger while holding it
//   to sound it; throw it and it sounds when it lands. While it sounds, NPCs within earshot hear a noise there (the
//   "Noises draw NPCs" setting decides whether idle ones walk over). NPCs of the faction it belongs to know their own
//   signal: they hear it from farther away, come straight to it (running, a close guess) and search longer. It never
//   tells anyone where YOU are - they still have to see you. Other button: silence it.
//     Eraden hand bell       - the garrison's alarm bell. Rings for 4 s, the clapper swings.          Eraden Kingdom
//     Outlaw coin pouch      - a purse of stolen coin. A rattle of coins nobody in a pirate crew ignores. Outlaws
//     Wildfolk bone rattle   - painted hide on a bone handle, beads and teeth. A ritual shake.         Wildfolk
//     Eye resonance fork     - a bronze ritual fork with a glowing gem. Rings long and carries far.   The Eye
// SEER'S MIRROR - an arched bronze hand mirror. Point its top at an NPC and pull the trigger: the dark glass shows
//   what Rage Engine knows about him - Vitals (health, pain, breath, bleeding, what he's doing), Wounds (every broken
//   bone, gouged eye, broken jaw, concussion, burns...) and Mind (morale, anger / fear, whether he's seen or heard you,
//   how he's guarding, his skill, a hidden knife). It only READS what the mod already tracks; it never adds or changes
//   anything. The last line of Vitals shows the body's life number and the state's thread (generation), so you can
//   watch the 0.54 ownership fix work. Other button: turn the page. Updates twice a second while held.
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

        /// The ids of every item this file dresses.
        internal static readonly string[] NoiseIds54 = { "RE_Bell_Eraden", "RE_Pouch_Outlaw", "RE_Rattle_Wildfolk", "RE_Fork_Eye" };
        internal const string MirrorId54 = "RE_SeerMirror";

        void TickItems54()
        {
            float now = Time.time;
            // dress them as soon as they exist (book, shop shelf, wherever), not only once held
            if (now >= nextItemScan54 && Item.allActive != null)
            {
                nextItemScan54 = now + 1f;
                foreach (var it in Item.allActive)
                {
                    if (it == null || it.data == null || it.data.id == null || !it.data.id.StartsWith("RE_")) continue;
                    int kind = Array.IndexOf(NoiseIds54, it.data.id);
                    if (kind >= 0 && !makers54.ContainsKey(it)) { var i2 = it; Guard("noise.dress", () => DressNoise54(i2, kind)); }
                    else if (it.data.id == MirrorId54 && !mirrors54.ContainsKey(it)) { var i2 = it; Guard("mirror.dress", () => DressMirror54(i2)); }
                }
                List<Item> gone = null;
                foreach (var kv in makers54) if (kv.Key == null) (gone ??= new List<Item>()).Add(kv.Key);
                foreach (var kv in mirrors54) if (kv.Key == null) (gone ??= new List<Item>()).Add(kv.Key);
                if (gone != null) foreach (var g in gone) { makers54.Remove(g); mirrors54.Remove(g); }
            }
            // held: trigger / other button
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc != null)
                for (int k = 0; k < 2; k++)
                {
                    var h = k == 0 ? pc.handLeft : pc.handRight;
                    var it = h != null && h.grabbedHandle != null ? h.grabbedHandle.item : null;
                    bool use = false, alt = false;
                    if (it != null && (makers54.ContainsKey(it) || mirrors54.ContainsKey(it)))
                    {
                        try { var ch = h.playerHand.controlHand; use = ch.usePressed; alt = ch.alternateUsePressed; } catch { }
                        bool useDown = use && !itemUse54[k], altDown = alt && !itemAlt54[k];
                        if (makers54.TryGetValue(it, out var n))
                        {
                            if (useDown) { if (now < n.until && Kinds54[n.kind].loop) StopNoise54(n); else SoundNoise54(n, Kinds54[n.kind].useSeconds); }
                            else if (altDown) StopNoise54(n);
                            if (now < n.until && now >= n.nextBuzz) { n.nextBuzz = now + 0.15f; Haptics.Pulse(h.side, 0.2f, 1); }
                        }
                        if (mirrors54.TryGetValue(it, out var m))
                        {
                            if (useDown) { var hh = h; Guard("mirror.look", () => Gaze54(m, hh)); }
                            if (altDown) { m.page = (m.page + 1) % 3; DrawMirror54(m); Haptics.Pulse(h.side, 0.2f, 1); }
                        }
                    }
                    itemUse54[k] = use; itemAlt54[k] = alt;
                }
            foreach (var kv in makers54) if (kv.Key != null) { var n = kv.Value; Guard("noise.tick", () => TickNoise54(n, now)); }
            foreach (var kv in mirrors54) if (kv.Key != null) { var m = kv.Value; Guard("mirror.tick", () => TickMirror54(m, now)); }
        }

        // ================================================================== FACTION NOISEMAKERS
        sealed class Kind54 { public string name, heard, sound; public bool loop; public float useSeconds, landSeconds, radius; public int tag; public string[] faction; }
        static readonly Kind54[] Kinds54 = {
            new Kind54 { name = "Eraden hand bell", heard = "Heard a bell", sound = "NRE_HandBell", loop = true, useSeconds = 4f, landSeconds = 2.5f, radius = 20f, tag = 1, faction = new[] { "eraden", "kingdom", "knight" } },
            new Kind54 { name = "Outlaw coin pouch", heard = "Heard coins", sound = "NRE_Coins", loop = false, useSeconds = 1.6f, landSeconds = 1.6f, radius = 13f, tag = 2, faction = new[] { "outlaw", "bandit", "pirate" } },
            new Kind54 { name = "Wildfolk bone rattle", heard = "Heard a rattle", sound = "NRE_Rattle", loop = true, useSeconds = 3f, landSeconds = 1.5f, radius = 15f, tag = 3, faction = new[] { "wildfolk", "wild", "druid" } },
            new Kind54 { name = "Eye resonance fork", heard = "Heard a ringing", sound = "NRE_Resonance", loop = false, useSeconds = 6f, landSeconds = 4f, radius = 24f, tag = 4, faction = new[] { "eye", "cult" } } };

        sealed class Noise54 { public Item it; public int kind; public Transform model, swing; public Light glow; public AudioSource src; public float startAt = -1f, until = -1f, nextEmit, speed, nextBuzz, nextDance; }
        static readonly Dictionary<Item, Noise54> makers54 = new Dictionary<Item, Noise54>();
        static readonly Dictionary<int, int> factionTag54 = new Dictionary<int, int>();

        /// Which noisemaker faction (1 Eraden, 2 Outlaws, 3 Wildfolk, 4 Eye, 0 none) an NPC belongs to, from the game's faction name.
        internal static int FactionTag54(Creature c)
        {
            if (c == null) return 0;
            int id = c.factionId;
            if (factionTag54.TryGetValue(id, out int t)) return t;
            string name = (FactionName28(id) ?? "").ToLowerInvariant();
            t = 0;
            for (int k = 0; k < Kinds54.Length && t == 0; k++) foreach (var f in Kinds54[k].faction) if (name.Contains(f)) { t = Kinds54[k].tag; break; }
            factionTag54[id] = t;
            return t;
        }

        static void DressNoise54(Item it, int kind)
        {
            var n = new Noise54 { it = it, kind = kind };
            noises54[it] = n;
            var root = NewRoot54(it, "RageEngine_" + NoiseIds54[kind]); n.model = root;
            bool any;
            switch (kind)
            {
                case 0:
                {
                    var bronze = Tex54("bronze.png", new Color(0.6f, 0.42f, 0.22f), 0.7f);
                    any = Part54(root, "RE_Bell_Body", bronze) != null;
                    Part54(root, "RE_Bell_Handle", Tex54("wood.png", new Color(0.36f, 0.23f, 0.13f), 0.35f));
                    var piv = new GameObject("ClapperPivot").transform; piv.SetParent(root, false); piv.localPosition = new Vector3(0f, -0.0545f, 0f);   // printed by re_items54.py
                    n.swing = Part54(piv, "RE_Bell_Clapper", Tex54("iron.png", new Color(0.27f, 0.27f, 0.28f), 0.4f)) != null ? piv : null;
                    break;
                }
                case 1:
                    any = Part54(root, "RE_Pouch_Leather", Tex54("leather.png", new Color(0.38f, 0.24f, 0.15f), 0.3f)) != null;
                    Part54(root, "RE_Pouch_Rope", Tex54("rope.png", new Color(0.69f, 0.59f, 0.41f), 0.2f));
                    Part54(root, "RE_Pouch_Coins", Tex54("coin.png", new Color(0.81f, 0.63f, 0.23f), 0.8f));
                    break;
                case 2:
                    any = Part54(root, "RE_Rattle_Hide", Tex54("hide.png", new Color(0.77f, 0.67f, 0.5f), 0.25f)) != null;
                    Part54(root, "RE_Rattle_Bone", Tex54("bone.png", new Color(0.84f, 0.8f, 0.7f), 0.4f));
                    Part54(root, "RE_Rattle_Lace", Tex54("leather.png", new Color(0.38f, 0.24f, 0.15f), 0.3f));
                    Part54(root, "RE_Rattle_Beads", Mats.Make(new Color(0.62f, 0.12f, 0.08f), 0.65f));
                    break;
                default:
                {
                    any = Part54(root, "RE_Fork_Bronze", Tex54("bronze.png", new Color(0.6f, 0.42f, 0.22f), 0.7f)) != null;
                    Part54(root, "RE_Fork_Grip", Tex54("leather_wrap.png", new Color(0.28f, 0.18f, 0.11f), 0.25f));
                    var gem = Part54(root, "RE_Fork_Gem", Emissive53(new Color(0.45f, 0.2f, 0.95f)));
                    if (gem != null)
                    {
                        var lg = new GameObject("GemLight"); lg.transform.SetParent(gem, false); lg.transform.localPosition = new Vector3(0f, 0.056f, -0.01f);
                        var L = lg.AddComponent<Light>(); L.type = LightType.Point; L.color = new Color(0.55f, 0.3f, 1f); L.range = 0.35f; L.intensity = 0.4f; L.shadows = LightShadows.None;
                        n.glow = L;
                    }
                    n.swing = root;
                    break;
                }
            }
            if (!any)
            {   // models missing: a plain stand-in so the item still works
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, false); g.transform.localScale = Vector3.one * 0.07f;
                g.GetComponent<Renderer>().sharedMaterial = Mats.Make(new Color(0.6f, 0.42f, 0.22f), 0.6f);
            }
        }

        static void SoundNoise54(Noise54 n, float seconds)
        {
            float now = Time.time;
            n.startAt = now; n.until = now + seconds; n.nextEmit = now;
            var k = Kinds54[n.kind];
            if (n.src == null && Sfx.TryClip(k.sound, out var clip))
            {
                n.src = n.it.gameObject.AddComponent<AudioSource>();
                n.src.clip = clip; n.src.loop = k.loop; n.src.spatialBlend = 1f; n.src.rolloffMode = AudioRolloffMode.Linear;
                n.src.minDistance = 1f; n.src.maxDistance = Mathf.Max(25f, k.radius + 6f); n.src.dopplerLevel = 0f;
            }
            if (n.src != null) { n.src.volume = Mathf.Clamp01(sfxVolume * 0.9f); n.src.pitch = Random.Range(0.96f, 1.04f); n.src.Stop(); n.src.Play(); }
            if (debugLog) Log($"{k.name}: sounding for {seconds:0.0} s");
        }

        static void StopNoise54(Noise54 n)
        {
            n.until = -1f; n.startAt = -1f;
            if (n.src != null) n.src.Stop();
            if (n.swing != null && n.kind == 0) n.swing.localRotation = Quaternion.identity;
            if (n.model != null) { n.model.localPosition = Vector3.zero; n.model.localScale = Vector3.one; if (n.kind != 3) n.model.localRotation = Quaternion.identity; }
            if (n.glow != null) n.glow.intensity = 0.4f;
        }

        void TickNoise54(Noise54 n, float now)
        {
            var it = n.it; var pb = it.physicBody; var k = Kinds54[n.kind];
            bool held = it.IsHanded();
            // thrown: sounds when it lands
            float sp = pb != null ? pb.velocity.magnitude : 0f;
            if (!held && n.speed > 3.5f && sp < n.speed * 0.45f) SoundNoise54(n, k.landSeconds);
            n.speed = held ? 0f : sp;
            bool sounding = n.startAt >= 0f && now < n.until;
            if (!sounding) { if (n.startAt >= 0f) StopNoise54(n); return; }
            // everyone in earshot hears it; its own faction knows the signal (hearing is separate from sight)
            if (now >= n.nextEmit) { n.nextEmit = now + 0.8f; Emit(it.transform.position, k.radius, Stim.Noise, null, k.tag); }
            float t = now - n.startAt;
            switch (n.kind)
            {
                case 0: if (n.swing != null) n.swing.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f * 4.5f) * 24f); break;   // the clapper
                case 1: if (n.model != null) { float b = Mathf.Abs(Mathf.Sin(t * 18f)) * Mathf.Exp(-t * 1.5f); n.model.localScale = new Vector3(1f + 0.05f * b, 1f - 0.07f * b, 1f + 0.05f * b); } break;   // coins shifting in the purse
                case 2: if (n.model != null) n.model.localRotation = Quaternion.Euler(Mathf.Sin(t * 31f) * 7f, 0f, Mathf.Sin(t * 23f) * 9f); break;   // the shake
                default:
                    if (n.model != null) n.model.localPosition = Random.insideUnitSphere * 0.0006f * Mathf.Exp(-t * 0.4f);                              // the fork hums
                    if (n.glow != null) n.glow.intensity = 0.4f + 1.6f * Mathf.Exp(-t * 0.5f) * (0.75f + 0.25f * Mathf.Sin(t * 20f));
                    break;
            }
            if (!held && pb != null && n.kind == 0 && now >= n.nextDance)
            {   // a bell ringing on the floor rocks
                n.nextDance = now + 0.12f;
                Vector2 r = Random.insideUnitCircle * 0.003f;
                Guard("noise.dance", () => pb.AddForce(new Vector3(r.x, 0.001f, r.y), ForceMode.Impulse));
            }
        }

        // ================================================================== SEER'S MIRROR
        sealed class Mirror54 { public Item it; public Transform model; public Texture2D tex; public Color32[] px; public Creature target; public int life, page; public float nextDraw; public bool idle = true; }
        static readonly Dictionary<Item, Mirror54> mirrors54 = new Dictionary<Item, Mirror54>();
        const int MirW54 = 256, MirH54 = 320, CellW54 = 24, CellH54 = 30, Cols54 = 16;
        static readonly int[] Adv54 = { 6, 5, 7, 18, 12, 19, 14, 4, 6, 6, 11, 11, 5, 10, 5, 15, 13, 8, 11, 12, 11, 11, 12, 10, 12, 12, 5, 6, 11, 11, 11, 10, 17, 14, 14, 15, 16, 13, 12, 16, 17, 8, 9, 15, 12, 20, 16, 16, 13, 16, 14, 12, 14, 16, 14, 21, 14, 14, 13, 8, 15, 8, 17, 17, 9, 11, 12, 11, 13, 11, 8, 12, 13, 7, 6, 12, 6, 19, 13, 12, 13, 12, 10, 10, 8, 13, 11, 17, 11, 11, 11, 7, 5, 7, 11 };   // seer_font.png (Lora), printed by items54/seer.py
        static Color32[] glassBg54, fontPx54; static int fontW54, fontH54;
        static readonly Color32 Gold54 = new Color32(236, 206, 140, 255), Pale54 = new Color32(196, 220, 230, 255), DimS54 = new Color32(128, 150, 160, 255),
                                 WarnS54 = new Color32(240, 170, 90, 255), BadS54 = new Color32(240, 96, 80, 255);

        static void DressMirror54(Item it)
        {
            var m = new Mirror54 { it = it };
            mirrors54[it] = m;
            var root = NewRoot54(it, "RageEngine_SeerMirror"); m.model = root;
            bool any = Part54(root, "RE_Mirror_Frame", Tex54("bronze.png", new Color(0.6f, 0.42f, 0.22f), 0.7f)) != null;
            Part54(root, "RE_Mirror_Grip", Tex54("leather_wrap.png", new Color(0.28f, 0.18f, 0.11f), 0.25f));
            m.tex = new Texture2D(MirW54, MirH54, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "RageEngine_SeerGlass" };
            m.px = new Color32[MirW54 * MirH54];
            var mat = Mats.MakeTextured(m.tex, null, Color.white, 0.95f, false);
            try { mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; mat.SetTexture("_EmissionMap", m.tex); mat.SetColor("_EmissionColor", Color.white * 0.85f); } catch { }
            var glass = Part54(root, "RE_Mirror_Glass", mat);
            if (glass != null)
            {
                var lg = new GameObject("SeerLight"); lg.transform.SetParent(glass, false); lg.transform.localPosition = new Vector3(0f, 0.1f, -0.04f);
                var L = lg.AddComponent<Light>(); L.type = LightType.Point; L.color = new Color(0.6f, 0.8f, 0.95f); L.range = 0.3f; L.intensity = 0.45f; L.shadows = LightShadows.None;
            }
            if (!any)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, false); g.transform.localScale = new Vector3(0.08f, 0.1f, 0.01f); g.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                g.GetComponent<Renderer>().sharedMaterial = Mats.Make(new Color(0.6f, 0.42f, 0.22f), 0.6f);
            }
            instance?.DrawMirror54(m);
        }

        void TickMirror54(Mirror54 m, float now)
        {
            bool held = m.it.IsHanded();
            if (held && m.model != null)
            {   // the glass turned toward your face (yaw around the grip), like the NPC Spawner's screen
                var head = Player.local != null && Player.local.head != null ? Player.local.head.transform : null;
                if (head != null)
                {
                    Vector3 up = m.it.transform.up;
                    Vector3 toFace = Vector3.ProjectOnPlane(head.position - m.it.transform.position, up);
                    if (toFace.sqrMagnitude > 1e-4f) m.model.rotation = Quaternion.LookRotation(-toFace.normalized, up);
                }
            }
            if (m.target != null && held && now >= m.nextDraw) DrawMirror54(m);
        }

        void Gaze54(Mirror54 m, RagdollHand h)
        {
            var it = m.it; var pc = Player.local != null ? Player.local.creature : null;
            Vector3 dir = it.transform.up, from = it.transform.position;
            if (pc != null && Vector3.Dot(dir, pc.transform.position - from) > 0f && Player.local.head != null) dir = Player.local.head.transform.forward;   // held upside down: use your gaze
            Creature found = null; float best = 16f;
            foreach (var hit in Physics.SphereCastAll(from + dir * 0.15f, 0.15f, dir, 15f, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider != null ? hit.collider.GetComponentInParent<Creature>() : null;
                if (c == null || c.isPlayer || hit.distance >= best) continue;
                best = hit.distance; found = c;
            }
            m.target = found; m.life = found != null ? LifeOf54(found) : 0; m.idle = false;
            Sfx.Play("NRE_Resonance", it.transform, found != null ? 0.35f : 0.2f, found != null ? 1.6f : 1.2f);   // a soft chime
            Haptics.Pulse(h.side, found != null ? 0.45f : 0.2f, found != null ? 2 : 1);
            DrawMirror54(m);
        }

        // ---------------------------------------------------------------- the glass (twin of items54/seer.py)
        static bool LoadGlass54()
        {
            if (glassBg54 != null && fontPx54 != null) return true;
            var bg = GoreTex.MeshTex("Items54/seer_glass.png"); var ft = GoreTex.MeshTex("Items54/seer_font.png");
            if (bg == null || ft == null) return false;
            try
            {
                var b = bg.GetPixels32();
                glassBg54 = new Color32[MirW54 * MirH54];
                for (int y = 0; y < MirH54; y++) for (int x = 0; x < MirW54; x++)
                    glassBg54[y * MirW54 + x] = b[Mathf.Min(y, bg.height - 1) * bg.width + Mathf.Min(x, bg.width - 1)];
                fontPx54 = ft.GetPixels32(); fontW54 = ft.width; fontH54 = ft.height;
            }
            catch (Exception e) { Fail42("mirror.glass", e); glassBg54 = null; fontPx54 = null; return false; }
            return true;
        }

        static int Idx54(int x, int y) => (MirH54 - 1 - y) * MirW54 + x;      // y from the top

        static void Blend54(ref Color32 p, Color32 c, float a)
        {
            p.r = (byte)Mathf.Min(255, p.r + (c.r - p.r) * a); p.g = (byte)Mathf.Min(255, p.g + (c.g - p.g) * a); p.b = (byte)Mathf.Min(255, p.b + (c.b - p.b) * a);
        }

        static int Width54(string s) { int w = 0; foreach (char ch in s) { int i = ch - 32; w += i >= 0 && i < Adv54.Length ? Adv54[i] : 10; } return w; }

        static void Write54(Color32[] px, int x, int y, string s, Color32 col, float glow = 0.55f)
        {
            if (string.IsNullOrEmpty(s) || fontPx54 == null) return;
            foreach (char ch in s)
            {
                int i = ch - 32; if (i < 0 || i >= Adv54.Length) i = '?' - 32;
                if (x + CellW54 > MirW54 - 4) break;
                int cx = (i % Cols54) * CellW54, cy = (i / Cols54) * CellH54;
                for (int j = 0; j < CellH54; j++)
                {
                    int yy = y + j; if (yy < 0 || yy >= MirH54) continue;
                    int frow = (fontH54 - 1 - (cy + j)) * fontW54;
                    for (int k = 0; k < CellW54; k++)
                    {
                        var f = fontPx54[frow + cx + k];
                        if (f.r == 0 && f.g == 0) continue;
                        int pi = Idx54(x + k, yy);
                        var p = px[pi];
                        if (f.g > 0) { float g = f.g / 255f * glow * 0.45f; p.r = (byte)Mathf.Min(255, p.r + col.r * g); p.g = (byte)Mathf.Min(255, p.g + col.g * g); p.b = (byte)Mathf.Min(255, p.b + col.b * g); }
                        if (f.r > 0) Blend54(ref p, col, f.r / 255f);
                        px[pi] = p;
                    }
                }
                x += Adv54[i];
            }
        }

        static void Fill54(Color32[] px, int x0, int y0, int x1, int y1, Color32 c, float a)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(MirH54, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(MirW54, x1); x++) { int i = Idx54(x, y); var p = px[i]; Blend54(ref p, c, a); px[i] = p; }
        }

        void DrawMirror54(Mirror54 m)
        {
            if (m.tex == null) return;
            float now = Time.time; m.nextDraw = now + 0.5f;
            var px = m.px;
            if (!LoadGlass54()) { for (int i = 0; i < px.Length; i++) px[i] = new Color32(12, 14, 18, 255); m.tex.SetPixels32(px); m.tex.Apply(false); return; }
            Array.Copy(glassBg54, px, px.Length);
            var lines = new List<(string t, Color32 c, float bar)>();
            string title; var c = m.target;
            if (m.idle) { title = "Seer's Mirror"; L54(lines, "", Pale54); L54(lines, "Point the top at", Pale54); L54(lines, "someone and pull", Pale54); L54(lines, "the trigger.", Pale54); L54(lines, "", Pale54); L54(lines, "Other button:", DimS54); L54(lines, "turn the page.", DimS54); }
            else if (c == null) { title = "No one there"; L54(lines, "", Pale54); L54(lines, "The glass shows", DimS54); L54(lines, "only mist.", DimS54); L54(lines, "Point the top", DimS54); L54(lines, "at someone.", DimS54); }
            else if (!Same54(c, m.life)) { title = "The vision fades"; L54(lines, "", Pale54); L54(lines, "That body belongs", WarnS54); L54(lines, "to someone else", WarnS54); L54(lines, "now. Look again.", WarnS54); m.target = null; }
            else
            {
                TryGet(c, out var s);
                title = m.page == 0 ? "Vitals" : m.page == 1 ? "Wounds" : "Mind";
                if (m.page == 0) Vitals54(c, s, now, lines); else if (m.page == 1) Wounds54(c, s, now, lines); else Mind54(c, s, now, lines);
            }
            Write54(px, (MirW54 - Width54(title)) / 2, 14, title, Gold54, 0.9f);
            Fill54(px, 40, 46, MirW54 - 40, 48, Gold54, 0.7f);
            int y = 54;
            foreach (var (t, col, bar) in lines)
            {
                if (y > MirH54 - 30) break;
                if (bar >= 0f)
                {
                    Write54(px, 26, y, t, DimS54, 0.3f);
                    Fill54(px, 112, y + 11, 196, y + 13, DimS54, 0.6f);
                    Fill54(px, 112, y + 8, 112 + (int)(84 * Mathf.Clamp01(bar)), y + 16, col, 0.8f);
                    Write54(px, 202, y, Mathf.RoundToInt(bar * 100f) + "%", col, 0.4f);
                }
                else Write54(px, 26, y, t, col);
                y += 26;
            }
            m.tex.SetPixels32(px); m.tex.Apply(false);
        }

        static void L54(List<(string, Color32, float)> l, string t, Color32 c) => l.Add((t, c, -1f));

        static string Count54(int n) => n == 1 ? "One" : n == 2 ? "Two" : n == 3 ? "Three" : n == 4 ? "Four" : n.ToString();

        void Vitals54(Creature c, NpcState s, float now, List<(string, Color32, float)> l)
        {
            L54(l, s != null ? WhoIs(s) : c.name, Pale54);
            if (s != null && s.personality != null) L54(l, s.personality.type + ", " + ProfName(s.prof43), DimS54); else L54(l, "A stranger", DimS54);
            float hp = c.isKilled ? 0f : HpFrac(c);
            l.Add(("Health", hp > 0.5f ? Pale54 : hp > 0.2f ? WarnS54 : BadS54, hp));
            if (s == null) return;
            l.Add(("Pain", s.pain > 0.6f ? BadS54 : s.pain > 0.25f ? WarnS54 : Pale54, Mathf.Clamp01(s.pain)));
            if (exhaustion54) { float st = 1f - s.exert54; l.Add(("Breath", st > 0.5f ? Pale54 : st > 0.2f ? WarnS54 : BadS54, st)); }
            L54(l, s.bleedRate > 0.05f ? $"Bleeding {s.bleedRate:0.0} a breath" : "Not bleeding", s.bleedRate > 0.05f ? WarnS54 : DimS54);
            string state = c.isKilled ? "Dead" : s.surrendered ? "Surrendered" : s.knockedOut || s.passedOut ? "Knocked out" : s.inCombat ? "Fighting"
                         : s.pat54 == Check54 || s.pat54 == Look54 ? "Checking a noise" : s.walking54 ? "On patrol" : "Idle";
            L54(l, state, c.isKilled ? BadS54 : Pale54);
            L54(l, $"Life {s.life54}, thread {s.gen54}", DimS54);
        }

        static readonly (RagdollPart.Type t, string n)[] PartNames54 = {
            (RagdollPart.Type.Head, "Skull"), (RagdollPart.Type.Neck, "Neck"), (RagdollPart.Type.Torso, "Chest"),
            (RagdollPart.Type.LeftArm, "Left arm"), (RagdollPart.Type.RightArm, "Right arm"), (RagdollPart.Type.LeftHand, "Left hand"), (RagdollPart.Type.RightHand, "Right hand"),
            (RagdollPart.Type.LeftLeg, "Left leg"), (RagdollPart.Type.RightLeg, "Right leg"), (RagdollPart.Type.LeftFoot, "Left foot"), (RagdollPart.Type.RightFoot, "Right foot") };

        void Wounds54(Creature c, NpcState s, float now, List<(string, Color32, float)> l)
        {
            if (s == null) { L54(l, "The glass is clouded.", DimS54); return; }
            var all = new List<(string, Color32)>();
            foreach (var (t, n) in PartNames54) if ((s.brokenParts & t) != 0) all.Add((n + " broken", BadS54));
            if (s.headCrushed) all.Add(("Skull crushed", BadS54));
            if (s.spineBroken) all.Add(("Spine broken", BadS54));
            if (s.jawTorn) all.Add(("Jaw torn loose", BadS54)); else if (s.jawBroken) all.Add(("Jaw broken", BadS54));
            int eyes = 0; for (int i = 0; i < 8; i++) if ((s.gouged & (1 << i)) != 0) eyes++;
            if (eyes > 0) all.Add((eyes > 1 ? "Both eyes gouged out" : "An eye gouged out", BadS54));
            if (s.throatCutUntil > now) all.Add(("Throat cut", BadS54));
            if (s.gutsOut44 || s.gutImpaled) all.Add(("Guts out", BadS54));
            if (s.lostLeg) all.Add(("A leg gone", BadS54)); else if (s.dismembered) all.Add(("Dismembered", BadS54));
            if (s.ribsBroken > 0) all.Add(($"{Count54(s.ribsBroken)} rib{(s.ribsBroken > 1 ? "s" : "")} broken", WarnS54));
            if (s.concussUntil50 > now) all.Add(("Concussed", WarnS54)); else if (s.brain50 > 0.5f) all.Add(("Brain damage", WarnS54));
            if (s.noseBroken) all.Add(("Nose broken", WarnS54));
            if (s.teethLost > 0) all.Add(($"{Count54(s.teethLost)} {(s.teethLost > 1 ? "teeth" : "tooth")} lost", DimS54));
            if (s.charMarks50 > 0) all.Add(("Burns", WarnS54));
            if (s.shockScarred50) all.Add(("Lightning scars", DimS54));
            if (HasTag(s, Tag.Blind)) all.Add(("Blind", BadS54));
            if (all.Count == 0) { L54(l, "Unhurt", Pale54); return; }
            for (int i = 0; i < all.Count && i < 9; i++)
            {
                if (i == 8 && all.Count > 9) { L54(l, $"... and {all.Count - 8} more", DimS54); break; }
                L54(l, all[i].Item1, all[i].Item2);
            }
        }

        static readonly string[] Guards54 = { "Steady guard", "Guarding high", "Guarding low, wide", "Ready to dodge", "Waiting to riposte", "Backing off" };

        void Mind54(Creature c, NpcState s, float now, List<(string, Color32, float)> l)
        {
            if (s == null) { L54(l, "The glass is clouded.", DimS54); return; }
            float mo = Mathf.Clamp01(s.morale51);
            L54(l, $"Morale {(mo > 0.6f ? "steady" : mo > 0.3f ? "wavering" : "broken")}", mo > 0.6f ? Pale54 : mo > 0.3f ? WarnS54 : BadS54);
            bool angry = HasTag(s, Tag.Anger), afraid = HasTag(s, Tag.Fear);
            L54(l, angry && afraid ? "Angry, afraid" : angry ? "Angry" : afraid ? "Afraid" : "Calm", angry || afraid ? BadS54 : Pale54);
            L54(l, s.seenAt > 0f ? (now - s.seenAt < 3f ? "Sees you" : $"Saw you {now - s.seenAt:0}s ago") : "Has not seen you", Pale54);
            if (s.heardAt > 0f && now - s.heardAt < 10f) L54(l, s.heardKind == Stim.Noise ? (s.heardTag54 > 0 && s.heardTag54 <= Kinds54.Length ? Kinds54[s.heardTag54 - 1].heard : "Heard a noise") : "Heard " + s.heardKind.ToString().ToLowerInvariant(), DimS54);
            if (defense54) L54(l, Guards54[Mathf.Clamp(s.profile54, 0, Guards54.Length - 1)], Pale54);
            L54(l, $"{ProfName(s.prof43)} of the blade", DimS54);
            if (s.hasKnife && !s.knifeOut) L54(l, "Hides a knife", WarnS54);
            if (s.identity != null && s.identity.escapes > 0) L54(l, $"Escaped you {(s.identity.escapes == 1 ? "once" : s.identity.escapes == 2 ? "twice" : s.identity.escapes + " times")}", WarnS54);
            else if (s.brave) L54(l, "Brave", DimS54);
        }
    }
}

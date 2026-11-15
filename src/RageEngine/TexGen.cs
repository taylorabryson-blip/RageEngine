// Rage Engine 0.11 - procedural textures (generated once at runtime, no asset bundles needed).
//   Bone:   ivory cortical bone with lengthwise grain, pores and a bloody root; dark spongy marrow at the snapped end.
//   Meat:   torn piece of a person: the NPC's own skin colour on the outside, raw muscle with fibres, fat marbling
//           and dark blood inside.
//   Crater: the hole left where a chunk was torn out: dark wet centre, raw muscle, a yellow fat layer, then the
//           NPC's skin with a bloody torn edge.
//   Bruise: soft blotchy patch with transparent edges (the stage colour - red -> purple -> blue - is a tint).
// Every colour texture comes with a matching normal map, so the surfaces catch the light (wet, fibrous, pitted).
using System.Collections.Generic;
using UnityEngine;

namespace RageEngine
{
    internal static class TexGen
    {
        static Texture2D bone, boneN, bruise, meatN, craterN;
        static readonly Dictionary<int, Texture2D> meat = new Dictionary<int, Texture2D>(), crater = new Dictionary<int, Texture2D>();

        // high-detail textures shipped in the mod's Textures folder (loaded once); procedural versions are the fallback
        static Texture2D fileBone, fileBoneN, fileFlesh, fileFleshN, fileBruise;

        public static void LoadFiles(string modFolder)
        {
            if (string.IsNullOrEmpty(modFolder)) return;
            string dir = System.IO.Path.Combine(modFolder, "Textures");
            fileBone = Load(dir, "RE_Bone.png", false); fileBoneN = Load(dir, "RE_Bone_N.png", true);
            fileFlesh = Load(dir, "RE_Flesh.png", false); fileFleshN = Load(dir, "RE_Flesh_N.png", true);
            fileBruise = Load(dir, "RE_Bruise.png", false);
            Debug.Log("[RageEngine] textures from files: bone=" + (fileBone != null) + " flesh=" + (fileFlesh != null) + " bruise=" + (fileBruise != null));
        }

        static Texture2D Load(string dir, string name, bool linear)
        {
            try
            {
                string p = System.IO.Path.Combine(dir, name);
                if (!System.IO.File.Exists(p)) return null;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = name };
                if (!t.LoadImage(System.IO.File.ReadAllBytes(p), false)) return null;
                return t;
            }
            catch (System.Exception e) { Debug.LogWarning("[RageEngine] texture " + name + ": " + e.Message); return null; }
        }

        static float N(float x, float y) => Mathf.PerlinNoise(x, y);
        static float Fbm(float x, float y) => N(x, y) * 0.5f + N(x * 2.1f + 13f, y * 2.1f + 7f) * 0.3f + N(x * 4.3f + 31f, y * 4.3f + 3f) * 0.2f;

        static int SkinKey(Color c) => Mathf.RoundToInt(c.r * 10) * 121 + Mathf.RoundToInt(c.g * 10) * 11 + Mathf.RoundToInt(c.b * 10);

        static Texture2D New(int w, int h, bool linear = false)
            => new Texture2D(w, h, TextureFormat.RGBA32, true, linear) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 2 };

        // ---------------- bone (u = around, v = root(0) -> snapped tip(1)) ----------------
        public static Texture2D Bone()
        {
            if (bone != null) return bone;
            if (fileBone != null) { boneN = fileBoneN; return bone = fileBone; }
            const int W = 128, H = 256;
            var t = New(W, H); var px = new Color[W * H]; var h = new float[W * H];
            Color ivory = new Color(0.91f, 0.87f, 0.76f), yellow = new Color(0.83f, 0.76f, 0.58f);
            Color blood = new Color(0.42f, 0.03f, 0.03f), marrow = new Color(0.36f, 0.12f, 0.08f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float grain = N(u * 40f, v * 3f);                              // lengthwise grain
                    float mott = Fbm(u * 6f, v * 6f);
                    Color c = Color.Lerp(ivory, yellow, mott * 0.7f) * (0.88f + 0.14f * grain);
                    float pore = N(u * 90f, v * 60f);
                    if (pore > 0.78f) c *= 0.72f;                                   // small pits
                    float hh = grain * 0.5f + mott * 0.3f - (pore > 0.78f ? 0.4f : 0f);
                    // bloody where it comes out of the flesh, with drips running up the shaft
                    float drip = N(u * 18f, 0.5f) * 0.25f;
                    float b = Mathf.Clamp01((0.22f + drip - v) / 0.12f);
                    c = Color.Lerp(c, blood * (0.8f + 0.4f * mott), b);
                    // snapped end: spongy marrow
                    if (v > 0.9f)
                    {
                        float sp = N(u * 50f, v * 50f);
                        c = Color.Lerp(c, marrow * (0.7f + 0.6f * sp), Mathf.Clamp01((v - 0.9f) / 0.04f));
                        hh = sp;
                    }
                    c.a = 1f;
                    px[y * W + x] = c; h[y * W + x] = hh;
                }
            t.SetPixels(px); t.Apply(true); t.name = "RageEngine_Bone";
            boneN = NormalFrom(h, W, H, 2.5f);
            return bone = t;
        }
        public static Texture2D BoneNormal() { Bone(); return boneN; }

        // ---------------- torn flesh chunk (v = 0 top/outside skin .. 1 inside) ----------------
        public static Texture2D Meat(Color skin)
        {
            int key = SkinKey(skin);
            if (meat.TryGetValue(key, out var have) && have != null) return have;
            const int W = 256, H = 256;
            var t = New(W, H); var px = new Color[W * H]; var h = meatN == null && fileFleshN == null ? new float[W * H] : null;
            if (meatN == null && fileFleshN != null) meatN = fileFleshN;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float hh;
                    Color c = Flesh(u, v, out hh);
                    // outside of the chunk: the NPC's own skin, torn edge
                    float edge = 0.32f + 0.08f * N(u * 14f, 3.3f);
                    if (v < edge)
                    {
                        float sk = Fbm(u * 12f, v * 12f);
                        Color s = skin * (0.9f + 0.15f * sk); s.a = 1f;
                        float k = Mathf.Clamp01((edge - v) / 0.04f);
                        c = Color.Lerp(Color.Lerp(c, new Color(0.5f, 0.05f, 0.05f), 0.6f), s, k);
                        hh = 0.5f + 0.1f * sk;
                    }
                    px[y * W + x] = c;
                    if (h != null) h[y * W + x] = hh;
                }
            t.SetPixels(px); t.Apply(true); t.name = "RageEngine_Meat";
            if (h != null) meatN = NormalFrom(h, W, H, 3f);
            return meat[key] = t;
        }
        public static Texture2D MeatNormal(Color skin) { Meat(skin); return meatN; }

        /// Raw muscle: fibres, fat marbling, dark blood pooling. hh = height for the normal map.
        static Color Flesh(float u, float v, out float hh)
        {
            if (fileFlesh != null)
            {
                var fc = fileFlesh.GetPixelBilinear(u - Mathf.Floor(u), v - Mathf.Floor(v));
                hh = fc.grayscale; fc.a = 1f; return fc;
            }
            Color muscle = new Color(0.58f, 0.07f, 0.06f), dark = new Color(0.25f, 0.01f, 0.01f), fat = new Color(0.9f, 0.8f, 0.58f);
            float fib = N(u * 6f, v * 55f);                                         // muscle fibres
            float big = Fbm(u * 5f, v * 5f);
            Color c = Color.Lerp(dark, muscle, 0.45f + 0.55f * fib) * (0.85f + 0.3f * big);
            float marb = Fbm(u * 9f + 40f, v * 9f + 9f);
            if (marb > 0.62f) c = Color.Lerp(c, fat, Mathf.Clamp01((marb - 0.62f) / 0.06f) * 0.85f);
            float clot = N(u * 22f + 5f, v * 22f + 1f);
            if (clot > 0.72f) c = Color.Lerp(c, dark, 0.7f);
            c.a = 1f;
            hh = fib * 0.6f + (marb > 0.62f ? 0.3f : 0f) - (clot > 0.72f ? 0.2f : 0f);
            return c;
        }

        // ---------------- crater (polar: u = angle, v = 0 centre .. 1 edge) ----------------
        public static Texture2D Crater(Color skin)
        {
            int key = SkinKey(skin);
            if (crater.TryGetValue(key, out var have) && have != null) return have;
            const int W = 256, H = 128;
            var t = New(W, H); var px = new Color[W * H]; var h = craterN == null && fileFleshN == null ? new float[W * H] : null;
            if (craterN == null && fileFleshN != null) craterN = fileFleshN;
            Color fat = new Color(0.88f, 0.76f, 0.5f), blood = new Color(0.4f, 0.02f, 0.02f), deep = new Color(0.14f, 0.0f, 0.0f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float jag = (N(u * 24f, 1.7f) - 0.5f) * 0.12f;                // torn, irregular rings
                    float r = v + jag;
                    float hh;
                    Color c;
                    if (r < 0.4f) { float w = N(u * 30f, v * 30f); c = Color.Lerp(deep, blood, w * 0.6f); hh = -0.3f + w * 0.2f; }
                    else if (r < 0.78f) { c = Flesh(u * 3f, v * 2.5f, out hh); }
                    else if (r < 0.86f) { float w = N(u * 60f, v * 40f); c = fat * (0.85f + 0.25f * w); hh = 0.6f + 0.2f * w; }
                    else
                    {
                        float sk = Fbm(u * 20f, v * 10f);
                        Color s = skin * (0.9f + 0.15f * sk);
                        float bl = Mathf.Clamp01(1f - (r - 0.86f) / 0.12f) * (0.6f + 0.4f * N(u * 35f, 2f));
                        c = Color.Lerp(s, blood, bl); hh = 0.5f + 0.1f * sk;
                    }
                    c.a = 1f;
                    px[y * W + x] = c;
                    if (h != null) h[y * W + x] = hh;
                }
            t.SetPixels(px); t.Apply(true); t.name = "RageEngine_Crater";
            if (h != null) craterN = NormalFrom(h, W, H, 3f);
            return crater[key] = t;
        }
        public static Texture2D CraterNormal(Color skin) { Crater(skin); return craterN; }

        // ---------------- bruise (planar uv, alpha edges; tinted per stage) ----------------
        public static Texture2D Bruise()
        {
            if (bruise != null) return bruise;
            if (fileBruise != null) { fileBruise.wrapMode = TextureWrapMode.Clamp; return bruise = fileBruise; }
            const int W = 128;
            var t = New(W, W); var px = new Color[W * W];
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < W; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W - 0.5f, v = y / (float)W - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float blot = Fbm(x * 0.06f, y * 0.06f);
                    float a = Mathf.Clamp01((1f - r - (blot - 0.5f) * 0.5f) * 1.6f);
                    a = a * a * (3f - 2f * a);
                    float core = Mathf.Clamp01(1f - r * 1.4f);
                    float lum = 0.75f + 0.25f * blot - 0.25f * core;               // darker in the middle, blotchy
                    px[y * W + x] = new Color(lum, lum, lum, a * (0.75f + 0.25f * blot));
                }
            t.SetPixels(px); t.Apply(true); t.name = "RageEngine_Bruise";
            return bruise = t;
        }

        /// Tangent-space normal map from a height field (RGB packed; Quest/Android shaders read RGB normals).
        static Texture2D NormalFrom(float[] h, int W, int H, float strength)
        {
            var t = New(W, H, true); var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float l = h[y * W + (x + W - 1) % W], r = h[y * W + (x + 1) % W];
                    float d = h[((y + H - 1) % H) * W + x], u = h[((y + 1) % H) * W + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    px[y * W + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            t.SetPixels(px); t.Apply(true); t.name = "RageEngine_Normal";
            return t;
        }
    }
}

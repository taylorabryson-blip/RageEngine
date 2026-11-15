// Nomad Rage Engine - sounds, blood, haptics and the open-fracture bone shard.
// Sounds: plain 16-bit WAV files in the mod's "Sounds" folder, read and turned into AudioClips at startup, so no
// Unity asset bundle is needed. Files named Base_1.wav, Base_2.wav ... are variants of "Base" (picked at random).
using System;
using System.Collections.Generic;
using System.IO;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal static class Sfx
    {
        static readonly Dictionary<string, List<AudioClip>> clips = new Dictionary<string, List<AudioClip>>();
        const int MaxVoices = 14;
        static readonly List<AudioSource> live = new List<AudioSource>();
        static readonly Dictionary<string, List<int>> bags = new Dictionary<string, List<int>>();
        static readonly Dictionary<string, int> lastPlayed = new Dictionary<string, int>();

        static int NextIndex(string key, int count)
        {
            if (count <= 1) return 0;
            if (!bags.TryGetValue(key, out var bag)) { bag = new List<int>(count); bags[key] = bag; }
            if (bag.Count == 0)
            {
                for (int i = 0; i < count; i++) bag.Add(i);
                for (int i = count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); int t = bag[i]; bag[i] = bag[j]; bag[j] = t; }
                if (lastPlayed.TryGetValue(key, out var last) && bag[bag.Count - 1] == last) { int t = bag[0]; bag[0] = bag[bag.Count - 1]; bag[bag.Count - 1] = t; }
            }
            int idx = bag[bag.Count - 1];
            bag.RemoveAt(bag.Count - 1);
            lastPlayed[key] = idx;
            return idx;
        }

        public static void LoadAll(string modFolder)
        {
            if (string.IsNullOrEmpty(modFolder)) return;
            string dir = Path.Combine(modFolder, "Sounds");
            if (!Directory.Exists(dir)) { Debug.LogWarning("[RageEngine] no Sounds folder at " + dir); return; }
            int n = 0;
            foreach (var file in Directory.GetFiles(dir, "*.wav"))
            {
                try
                {
                    var clip = FromWav(File.ReadAllBytes(file), Path.GetFileNameWithoutExtension(file));
                    if (clip == null) continue;
                    string name = clip.name;
                    int us = name.LastIndexOf('_');
                    string key = us > 0 && int.TryParse(name.Substring(us + 1), out _) ? name.Substring(0, us) : name;
                    if (!clips.TryGetValue(key, out var list)) clips[key] = list = new List<AudioClip>();
                    list.Add(clip); n++;
                }
                catch (Exception e) { Debug.LogWarning("[RageEngine] bad sound " + file + ": " + e.Message); }
            }
            Debug.Log($"[RageEngine] loaded {n} sounds ({clips.Count} sets)");
        }

        /// Minimal RIFF/WAVE reader: PCM 16-bit or IEEE float 32-bit, any channel count.
        internal static AudioClip FromWav(byte[] d, string name)
        {
            if (d.Length < 44 || d[0] != 'R' || d[8] != 'W') return null;
            int p = 12, channels = 1, rate = 44100, bits = 16, fmt = 1; int dataPos = -1, dataLen = 0;
            while (p + 8 <= d.Length)
            {
                int size = BitConverter.ToInt32(d, p + 4);
                if (d[p] == 'f' && d[p + 1] == 'm' && d[p + 2] == 't')
                {
                    fmt = BitConverter.ToInt16(d, p + 8); channels = BitConverter.ToInt16(d, p + 10);
                    rate = BitConverter.ToInt32(d, p + 12); bits = BitConverter.ToInt16(d, p + 22);
                }
                else if (d[p] == 'd' && d[p + 1] == 'a' && d[p + 2] == 't' && d[p + 3] == 'a') { dataPos = p + 8; dataLen = Math.Min(size, d.Length - dataPos); break; }
                p += 8 + size + (size & 1);
            }
            if (dataPos < 0) return null;
            int bytesPer = bits / 8, total = dataLen / bytesPer;
            var samples = new float[total];
            for (int i = 0; i < total; i++)
            {
                int o = dataPos + i * bytesPer;
                samples[i] = fmt == 3 ? BitConverter.ToSingle(d, o) : BitConverter.ToInt16(d, o) / 32768f;
            }
            var clip = AudioClip.Create(name, total / channels, channels, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static bool Has(string key) => clips.ContainsKey(key);

        /// One-shot at a world position (for hits where the body part may move away).
        public static void PlayAt(string key, Vector3 pos, float volume)
        {
            if (!clips.TryGetValue(key, out var list) || list.Count == 0) return;
            RageEngineScript.Guard("sound", () =>
            {
                var go = new GameObject("RageEngine_SFX");
                go.transform.position = pos;
                Play(key, go.transform, volume);
                UnityEngine.Object.Destroy(go, 5f);
            });
        }

        /// Plays a random variant of a sound set at (and following) a transform. 3D, audible ~25 m.
        public static void Play(string key, Transform at, float volume) => Play(key, at, volume, 1f);

        public static void Play(string key, Transform at, float volume, float pitchMul)
        {
            if (at == null || !clips.TryGetValue(key, out var list) || list.Count == 0) return;
            RageEngineScript.Guard("sound", () =>
            {
                // 0.16: voice cap + shuffle bag (every variant plays once before any repeats; never the same twice in a row)
                for (int i = live.Count - 1; i >= 0; i--) if (live[i] == null || !live[i].isPlaying) live.RemoveAt(i);
                if (live.Count >= MaxVoices) return;
                var clip = list[NextIndex(key, list.Count)];
                var go = new GameObject("RageEngine_SFX");
                go.transform.SetParent(at, false);
                var src = go.AddComponent<AudioSource>();
                src.clip = clip;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1.5f;
                src.maxDistance = 25f;
                src.pitch = UnityEngine.Random.Range(0.95f, 1.05f) * pitchMul;
                src.volume = Mathf.Clamp01(RageEngineScript.sfxVolume * volume);
                src.dopplerLevel = 0f;
                src.Play();
                live.Add(src);
                UnityEngine.Object.Destroy(go, clip.length / src.pitch + 0.1f);
            });
        }
    }

    internal static class Effects
    {
        /// Vanilla game blood (spurt + drip) at a transform.
        public static void Blood(Transform at)
        {
            if (at == null) return;
            RageEngineScript.Guard("blood", () =>
            {
                var data = Catalog.GetData<EffectData>("PenetrationDeepBleeding", false);
                var inst = data?.Spawn(at, true);
                inst?.Play();
            });
        }
    }

    internal static class Haptics
    {
        public static void Pulse(Side side, float intensity, int count)
        {
            RageEngineScript.Guard("haptics", () =>
            {
                var hand = PlayerControl.GetHand(side);
                if (hand == null) return;
                for (int i = 0; i < count; i++) hand.HapticShort(Mathf.Clamp01(intensity), false);
            });
        }
    }

    /// The bone shard that sticks out of an open fracture: a procedural, slightly bent, jagged-ended bone,
    /// parented to the broken body part so it moves with the limb.
    internal static class BoneMesh
    {
        static Mesh mesh;
        static Material material;

        internal static void ResetMaterial() { material = null; }

        public static GameObject Attach(RagdollPart part, Vector3 point, Vector3 outward, string model = null)
        {
            if (material == null) material = Mats.MakeTextured(TexGen.Bone(), TexGen.BoneNormal(), Color.white, 0.4f, false);
            if (outward.sqrMagnitude < 1e-4f) outward = part.transform.forward;
            outward.Normalize();
            var c = part.ragdoll != null ? part.ragdoll.creature : null;
            var real = model != null ? BoneModels.Get(model) ?? BoneModels.For(part) : BoneModels.For(part);

            // where the bone comes out: the real skin surface nearest the hit
            Vector3 skin = point, nrm = outward;
            if (RageEngineScript.SkinAt(c, point, 0.08f, out var sp, out var sn, out _)) { skin = sp; nrm = sn; }
            Vector3 axis = RageEngineScript.LimbAxis(part);
            Vector3 outDir = Vector3.ProjectOnPlane(nrm, axis);
            outDir = outDir.sqrMagnitude > 1e-4f ? outDir.normalized : nrm;

            var go = new GameObject("RageEngine_Bone");
            go.transform.SetParent(part.transform, true);
            if (real != null)
            {
                // compound fracture: the broken end through the skin, the shaft angled back into the limb
                float sign = UnityEngine.Random.value < 0.5f ? 1f : -1f;
                // 0.18: only the jagged broken end pokes out (1.5-2.5 cm); the shaft stays hidden inside the limb
                Vector3 dir = (outDir * 0.6f + axis * sign * 0.8f).normalized;              // model +Y = out of the break
                go.transform.position = skin + outDir * UnityEngine.Random.Range(0.015f, 0.025f);
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                var ls = part.transform.lossyScale;
                go.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));
                go.AddComponent<MeshFilter>().sharedMesh = real;
                RageEngineScript.Log($"{real.name} through the skin of {part.name}");
            }
            else
            {
                if (mesh == null) mesh = Build();
                go.transform.position = skin + nrm * 0.004f;
                Vector3 dir = (outDir + axis * 0.6f).normalized;
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                float sc = 1.6f;
                var ls = part.transform.lossyScale;
                go.transform.localScale = new Vector3(sc / Mathf.Max(0.01f, ls.x), sc / Mathf.Max(0.01f, ls.y), sc / Mathf.Max(0.01f, ls.z));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
            }
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// Bone-white material that renders in the game's pipeline: clone the creature's skin material (guaranteed to
        /// work on Quest), then go through EVERY property of its shader - whatever the names are - turning all colour
        /// properties bone-white, removing all textures (skin colour, pores, tattoos) and killing shine/emission.
        static Material MakeMaterial(RagdollPart part)
        {
            Material src = null;
            var rend = part.ragdoll.creature.GetComponentInChildren<SkinnedMeshRenderer>();
            if (rend != null) src = rend.sharedMaterial;
            Material m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var sh = m.shader;
            int n = sh.GetPropertyCount();
            Recolour(m, new Color(0.90f, 0.86f, 0.76f), 0.35f);
            m.name = "RageEngine_Bone";
            RageEngineScript.Log("bone material from shader " + sh.name + " (" + n + " props)");
            return m;
        }

        /// Turns every colour property of a material's shader into one colour, removes all textures, kills
        /// metal / emission and sets a matte-ish smoothness - works whatever the shader's property names are.
        internal static void Recolour(Material m, Color col, float smooth)
        {
            var sh = m.shader;
            int n = sh.GetPropertyCount();
            for (int i = 0; i < n; i++)
            {
                string name = sh.GetPropertyName(i);
                string lower = name.ToLowerInvariant();
                switch (sh.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        m.SetColor(name, lower.Contains("emiss") ? Color.black : col);
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        // normal / metal / mask / occlusion / detail maps -> none (a white metallic map turned
                        // things chrome before 0.11); colour maps -> plain white, never a black/transparent default
                        bool none = lower.Contains("bump") || lower.Contains("normal") || lower.Contains("metal") || lower.Contains("mask")
                                    || lower.Contains("spec") || lower.Contains("occlusion") || lower.Contains("parallax") || lower.Contains("height")
                                    || lower.Contains("detail") || lower.Contains("emiss");
                        m.SetTexture(name, none ? null : Texture2D.whiteTexture);
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        if (lower.Contains("cutoff") || lower.Contains("alphaclip") || lower.Contains("surface") || lower == "_mode") m.SetFloat(name, 0f);
                        else if (lower.Contains("metal") || lower.Contains("emiss")) m.SetFloat(name, 0f);
                        else if (lower.Contains("smooth") || lower.Contains("gloss")) m.SetFloat(name, smooth);
                        break;
                }
            }
            m.DisableKeyword("_EMISSION");
            m.DisableKeyword("_NORMALMAP"); m.DisableKeyword("_METALLICSPECGLOSSMAP"); m.DisableKeyword("_SPECGLOSSMAP");
            m.DisableKeyword("_OCCLUSIONMAP"); m.DisableKeyword("_PARALLAXMAP"); m.DisableKeyword("_DETAIL_MULX2"); m.DisableKeyword("_DETAIL_SCALED");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 2000;
            if (m.HasProperty("_Color")) m.color = col;
        }

        /// ~10 cm bone: rounded shaft with a widening, jagged broken tip.
        static Mesh Build()
        {
            const int seg = 10, rings = 7;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            var rnd = new System.Random(7);
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float y = t * 0.10f;
                float radius = Mathf.Lerp(0.011f, 0.009f, t) + (r == rings ? 0.003f : 0f);
                float bend = Mathf.Sin(t * Mathf.PI) * 0.006f;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    float jag = r == rings ? (float)rnd.NextDouble() * 0.018f - 0.004f : 0f;   // splintered end
                    float rr = radius * (1f + (r == rings ? 0.25f * (float)rnd.NextDouble() : 0f));
                    v.Add(new Vector3(Mathf.Cos(a) * rr + bend, y + jag, Mathf.Sin(a) * rr));
                    uv.Add(new Vector2(i / (float)seg, t));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < seg; i++)
                {
                    int a = r * (seg + 1) + i, b = a + seg + 1;
                    tri.Add(a); tri.Add(b); tri.Add(a + 1);
                    tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
                }
            // marrow cap on the jagged end (dark centre)
            int centre = v.Count; v.Add(new Vector3(0.006f, 0.104f, 0f)); uv.Add(new Vector2(0.5f, 1f));
            int top = rings * (seg + 1);
            for (int i = 0; i < seg; i++) { tri.Add(top + i); tri.Add(centre); tri.Add(top + i + 1); }
            var m = new Mesh { name = "RageEngine_BoneShard" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            // bloody root, bone-white shaft, dark marrow at the snapped end (used when the shader is unlit)
            Mats.Shade(m, (i, p) =>
            {
                if (p.y > 0.1f) return new Color(0.35f, 0.08f, 0.06f);
                if (p.y < 0.02f) return Color.Lerp(new Color(0.6f, 0.05f, 0.05f), new Color(0.92f, 0.88f, 0.78f), p.y / 0.02f);
                return new Color(0.94f, 0.9f, 0.8f);
            });
            return m;
        }
    }
}

// Rage Engine 0.28 - night for the runtime maps: a starry sky with a moon, fog, drifting cloud layers (a sea of clouds
// under the skyscraper), moonlit outdoor surfaces. Map file lines:
//   SKY <texture>                         equirectangular sky (skybox if the game has the panoramic sky shader,
//                                         otherwise a sky dome around the map)
//   FOG r g b density                     exponential fog while you're in the map
//   AMB r g b                             ambient light while you're in the map (NPCs, items, you)
//   CLOUD <tex> x y z size tile alpha dx dz   a cloud layer: centre, size (m), texture repeats, opacity, drift per second
//   C <material> r g b                    colour multiplier for a map material (night tint for outdoor surfaces)
// Everything the game had before (skybox, fog, ambient) is put back when you leave or the level changes.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Rendering;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        sealed class CloudSpec28 { public string tex; public Vector3 centre; public float size, tile, alpha; public Vector2 drift; }
        sealed class CloudLive28 { public Material m; public Vector2 drift, offset; }

        static string env28Sky; static bool env28HasFog, env28HasAmb; static Color env28FogCol, env28Amb; static float env28FogDen;
        static readonly List<CloudSpec28> env28CloudSpecs = new List<CloudSpec28>();
        static readonly List<CloudLive28> env28Clouds = new List<CloudLive28>();
        static readonly Dictionary<string, Color> env28Tints = new Dictionary<string, Color>();
        static Material env28SkyMat; static string env28SkyHow = "none";

        // what the level had before
        static bool env28Saved; static Material env28OldSky; static bool env28OldFog; static Color env28OldFogCol, env28OldAmb;
        static FogMode env28OldFogMode; static float env28OldFogDen, env28OldFogStart, env28OldFogEnd; static AmbientMode env28OldAmbMode;

        static void ResetEnv28()
        {
            env28Sky = null; env28HasFog = env28HasAmb = false; env28CloudSpecs.Clear(); env28Clouds.Clear(); env28Tints.Clear();
            env28SkyMat = null; env28SkyHow = "none";
        }

        /// From the map reader.
        static void ParseEnv28(string[] p)
        {
            switch (p[0])
            {
                case "SKY": if (Application.platform == RuntimePlatform.Android) break;   // 0.32: sky dome hid the map on Quest
                    env28Sky = p[1].ToLowerInvariant(); break;
                // 0.32: no fog, no dark ambient, no night tints - on Quest they turned the whole map into a dark blue
                // silhouette (lit surfaces with almost no light, then blue fog on top). The level's own lighting is used.
                case "FOG": break;
                case "AMB": break;
                case "CLOUD":
                    env28CloudSpecs.Add(new CloudSpec28 { tex = p[1].ToLowerInvariant(), centre = new Vector3(PF(p[2]), PF(p[3]), PF(p[4])), size = PF(p[5]), tile = PF(p[6]), alpha = PF(p[7]), drift = new Vector2(PF(p[8]), PF(p[9])) });
                    break;
                case "C": break;
            }
        }

        static Color Tint28(string key) => env28Tints.TryGetValue(key, out var c) ? c : Color.white;

        /// A transparent material for clouds / sky, on the unlit shader when the game has it.
        static Material SeeThrough28(Texture2D tex, Color col, bool transparent)
        {
            // 0.28.2: never URP Unlit (stripped on Quest). Opaque things clone the level's own material.
            if (!transparent && mapLook != "1")
            {
                var d = Donor26();
                if (d != null)
                {
                    var m = DonorCopy26(d, tex, Color.white, 1f, col);
                    if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                    if (m.HasProperty("_EmissionColor") && m.HasProperty("_EmissionMap")) { if (m.IsKeywordEnabled("_EMISSION")) { m.SetTexture("_EmissionMap", tex); m.SetColor("_EmissionColor", Color.white * 0.8f); } }
                    return m;
                }
            }
            return Mats.MakeTextured(tex, null, col, 0f, transparent);
        }

        /// After the geometry: sky and cloud objects (children of the map root, so they go with it).
        static void BuildEnv28(Transform root)
        {
            // ---- sky
            if (env28Sky != null)
            {
                var tex = GoreTex.MeshTex("Map/" + env28Sky + ".png");
                if (tex == null) env28SkyHow = "texture missing";
                else
                {
                    tex.wrapModeU = TextureWrapMode.Repeat; tex.wrapModeV = TextureWrapMode.Clamp;
                    Shader pano = null;                                              // 0.28.2: skybox shader is stripped on Quest -> always the dome
                    if (pano != null)
                    {
                        env28SkyMat = new Material(pano);
                        env28SkyMat.SetTexture("_MainTex", tex);
                        if (env28SkyMat.HasProperty("_Mapping")) env28SkyMat.SetFloat("_Mapping", 1f);
                        env28SkyMat.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
                        if (env28SkyMat.HasProperty("_ImageType")) env28SkyMat.SetFloat("_ImageType", 0f);
                        if (env28SkyMat.HasProperty("_Exposure")) env28SkyMat.SetFloat("_Exposure", 1f);
                        env28SkyHow = "skybox (Skybox/Panoramic)";
                    }
                    else
                    {
                        // no sky shader in this build: a dome around the map
                        var go = new GameObject("RageEngine_SkyDome");
                        go.transform.SetParent(root, false);
                        go.transform.localPosition = new Vector3(15f, 0f, 12f);
                        go.AddComponent<MeshFilter>().sharedMesh = SkyDome28(250f);
                        var r = go.AddComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                        var m = SeeThrough28(tex, Color.white, false); m.renderQueue = 1000;
                        r.sharedMaterial = m;
                        env28SkyHow = "sky dome (" + m.shader.name + ")";
                    }
                }
            }
            // ---- clouds
            foreach (var c in (mapLook == "1" ? env28CloudSpecs : new List<CloudSpec28>()))     // 0.28.2: see-through clouds need a shader the Quest may not have
            {
                var tex = GoreTex.MeshTex("Map/" + c.tex + ".png");
                if (tex == null) continue;
                tex.wrapMode = TextureWrapMode.Repeat;
                var go = new GameObject("RageEngine_Clouds");
                go.transform.SetParent(root, false);
                go.transform.localPosition = c.centre;
                if (c.centre.y > 0f) go.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);    // high layers face down at you
                go.AddComponent<MeshFilter>().sharedMesh = Quad28(c.size, c.tile);
                var r = go.AddComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                var m = SeeThrough28(tex, new Color(1f, 1f, 1f, c.alpha), true);
                r.sharedMaterial = m;
                env28Clouds.Add(new CloudLive28 { m = m, drift = c.drift });
            }
        }

        static Mesh Quad28(float size, float tile)
        {
            float h = size * 0.5f;
            var m = new Mesh { name = "RageEngine_CloudQuad" };
            m.vertices = new[] { new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(h, 0, h), new Vector3(-h, 0, h) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(tile, 0), new Vector2(tile, tile), new Vector2(0, tile) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };                                    // facing up
            m.RecalculateNormals(); m.RecalculateBounds();
            m.bounds = new Bounds(Vector3.zero, new Vector3(size, 10f, size));
            return m;
        }

        /// Inward-facing sphere with equirectangular UVs (u = azimuth, v = altitude).
        static Mesh SkyDome28(float radius)
        {
            const int seg = 48, rings = 24;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float alt = Mathf.Lerp(90f, -90f, i / (float)rings) * Mathf.Deg2Rad;
                for (int j = 0; j <= seg; j++)
                {
                    float az = j / (float)seg * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(alt) * Mathf.Sin(az), Mathf.Sin(alt), Mathf.Cos(alt) * Mathf.Cos(az)) * radius);
                    uv.Add(new Vector2(j / (float)seg, 1f - i / (float)rings));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j, b = a + seg + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b); t.Add(a + 1); t.Add(b + 1); t.Add(b);   // wound to face inward
                }
            var m = new Mesh { name = "RageEngine_SkyDome" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// When you arrive in the map.
        static void ApplyEnv28()
        {
            if (!env28Saved)
            {
                env28Saved = true;
                env28OldSky = RenderSettings.skybox; env28OldFog = RenderSettings.fog; env28OldFogCol = RenderSettings.fogColor; env28OldFogMode = RenderSettings.fogMode;
                env28OldFogDen = RenderSettings.fogDensity; env28OldFogStart = RenderSettings.fogStartDistance; env28OldFogEnd = RenderSettings.fogEndDistance;
                env28OldAmbMode = RenderSettings.ambientMode; env28OldAmb = RenderSettings.ambientLight;
            }
            if (env28SkyMat != null) RenderSettings.skybox = env28SkyMat;
            if (env28HasFog)
            {
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogColor = env28FogCol;
                // the sky dome sits inside the fog: keep it thinner so the stars still show
                RenderSettings.fogDensity = Mathf.Min(env28FogDen, 0.004f);                    // 0.28.2: thin, so a missing surface can never become a blue wall
            }
            if (env28HasAmb) { RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = env28Amb; }
            Debug.Log($"[RageEngine] map night: sky {env28SkyHow}, fog {(env28HasFog ? RenderSettings.fogDensity.ToString("0.0000") : "off")}, {env28Clouds.Count} cloud layer(s)");
        }

        /// When you leave or the map is gone.
        static void RestoreEnv28()
        {
            if (!env28Saved) return;
            env28Saved = false;
            RenderSettings.skybox = env28OldSky; RenderSettings.fog = env28OldFog; RenderSettings.fogColor = env28OldFogCol; RenderSettings.fogMode = env28OldFogMode;
            RenderSettings.fogDensity = env28OldFogDen; RenderSettings.fogStartDistance = env28OldFogStart; RenderSettings.fogEndDistance = env28OldFogEnd;
            RenderSettings.ambientMode = env28OldAmbMode; RenderSettings.ambientLight = env28OldAmb;
        }

        /// Every frame: the clouds drift.
        static void DriftClouds28()
        {
            if (env28Clouds.Count == 0) return;
            float dt = Time.deltaTime;
            foreach (var c in env28Clouds)
            {
                if (c.m == null) continue;
                c.offset += c.drift * dt;
                if (c.offset.x > 1f) c.offset.x -= 1f; if (c.offset.y > 1f) c.offset.y -= 1f; if (c.offset.x < 0f) c.offset.x += 1f; if (c.offset.y < 0f) c.offset.y += 1f;
                c.m.mainTextureOffset = c.offset;
            }
        }
    }
}

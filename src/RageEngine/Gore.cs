// Nomad Rage Engine 0.10 - gore & heavy blunt trauma.
//   - Flesh tearing: a heavy enough blunt impact (a big thrown object, a golem rock, a hammer at full swing, a body
//     slammed into a wall) rips chunks out of them: meat chunks fly off with physics and a torn, bloody crater stays
//     on the body where it happened.
//   - Head crush: a massive impact to the head (heavy rock, slam into a wall at speed) caves the skull in: instant
//     death, crunch, the head deforms, blood and chunks.
//   - Spine break: a huge force to the back/torso (thrown hard into a wall, crushed by something heavy) breaks the
//     spine: legs go dead for good and they can never stand again - they fight on from the ground.
// Everything visible (bone, bruises, chunks, craters) uses Mats.Make: a material from a shader that is guaranteed
// to render on a plain mesh on Quest (URP Lit -> any lit prop material in the level -> Sprites/Default fallback).
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    /// Materials for the mod's procedural meshes.
    internal static class Mats
    {
        static Material baseMat;
        static bool vertexColorShader;
        static readonly Dictionary<int, Material> cache = new Dictionary<int, Material>();

        public static bool UsesVertexColor { get { Base(); return vertexColorShader; } }

        public static Material Make(Color col, float smooth)
        {
            CheckMode();
            int key = Mathf.RoundToInt(col.r * 255) | Mathf.RoundToInt(col.g * 255) << 8 | Mathf.RoundToInt(col.b * 255) << 16 | Mathf.RoundToInt(smooth * 100) << 24;
            if (cache.TryGetValue(key, out var hit) && hit != null) return hit;
            var b = Base();
            var m = new Material(b) { name = "RageEngine_Mat" };
            if (vertexColorShader) m.color = col;
            else BoneMesh.Recolour(m, col, smooth);
            cache[key] = m;
            return m;
        }

        static readonly Dictionary<string, Material> texCache = new Dictionary<string, Material>();

        /// Textured material (colour + normal map), optionally alpha-blended. Cached per texture/tint.
        public static Material MakeTextured(Texture2D albedo, Texture2D normal, Color tint, float smooth, bool transparent)
        {
            CheckMode();
            string key = (albedo != null ? albedo.GetHashCode() : 0) + "/" + (normal != null ? normal.GetHashCode() : 0) + "/" + tint + "/" + smooth + "/" + transparent;
            if (texCache.TryGetValue(key, out var have) && have != null) return have;
            var b = Base();
            var m = new Material(b) { name = "RageEngine_Tex" };
            if (vertexColorShader) { m.mainTexture = albedo; m.color = tint; return texCache[key] = m; }
            BoneMesh.Recolour(m, tint, smooth);
            try { m.mainTexture = albedo; } catch { }                           // the shader's [MainTexture] slot, whatever its name
            var sh = m.shader;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                string n = sh.GetPropertyName(i), l = n.ToLowerInvariant();
                if (l.Contains("bump") || l.Contains("normal")) { if (normal != null) { m.SetTexture(n, normal); m.EnableKeyword("_NORMALMAP"); } }
                else if (l.Contains("mask") || l.Contains("metal") || l.Contains("occlusion") || l.Contains("spec")) m.SetTexture(n, NeutralMask());
                else if (l.Contains("base") || l.Contains("main") || l.Contains("albedo") || l.Contains("diffuse") || l == "_colortex") m.SetTexture(n, albedo);
            }
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f);
            if (transparent && m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000;
            }
            return texCache[key] = m;
        }

        internal static Color SkinOf(Creature c)
        {
            Color col = new Color(0.78f, 0.58f, 0.48f);
            RageEngineScript.Guard("skin", () => { var k = c.GetColor(Creature.ColorModifier.Skin); if (k.maxColorComponent > 0.05f) col = k; });
            col.a = 1f;
            return col;
        }

        static int baseMode = -1;
        static Texture2D neutralMask;

        /// Called before making materials: if the "Gore material" option changed, start over with the new kind.
        static void CheckMode()
        {
            int mode = Mathf.RoundToInt(RageEngineScript.goreMaterialMode);
            if (mode == baseMode) return;
            baseMode = mode; baseMat = null; vertexColorShader = false;
            cache.Clear(); texCache.Clear(); BoneMesh.ResetMaterial(); RageEngineScript.ResetGoreMaterials();
        }

        static Material Base()
        {
            CheckMode();
            if (baseMat != null) return baseMat;
            int mode = baseMode;
            // 1) the material the game's own items render with on Quest (weapons/props) - proven to show textures
            if (mode == 0 || mode == 1)
            {
                var items = Item.allActive;
                if (items != null)
                    foreach (var it in items)
                    {
                        if (it == null) continue;
                        foreach (var r in it.GetComponentsInChildren<MeshRenderer>())
                        {
                            var m = r.sharedMaterial;
                            if (m == null || m.shader == null || !Usable(m.shader.name)) continue;
                            baseMat = new Material(m); Report("game item material '" + m.name + "' shader '" + m.shader.name + "'", baseMat); return baseMat;
                        }
                    }
                if (mode == 1)
                    foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
                    {
                        var m = r.sharedMaterial;
                        if (m == null || m.shader == null || !Usable(m.shader.name)) continue;
                        baseMat = new Material(m); Report("level material '" + m.name + "' shader '" + m.shader.name + "'", baseMat); return baseMat;
                    }
            }
            // 2) URP Lit
            if (mode == 0 || mode == 2)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh != null && sh.isSupported) { baseMat = new Material(sh); Report("URP Lit", baseMat); return baseMat; }
            }
            // 3) unlit: always shows the texture exactly (no lighting)
            var u = Shader.Find("Universal Render Pipeline/Unlit");
            if (u != null && u.isSupported && mode != 3) { baseMat = new Material(u); Report("URP Unlit", baseMat); return baseMat; }
            u = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            baseMat = new Material(u) { renderQueue = 2450 };
            vertexColorShader = true;
            Report("Sprites/Default (unlit, vertex colour)", baseMat);
            return baseMat;
        }

        static bool Usable(string shader)
        {
            string n = shader.ToLowerInvariant();
            return !(n.Contains("particle") || n.Contains("transparent") || n.Contains("unlit") || n.Contains("ui/") || n.Contains("sprite")
                     || n.Contains("skybox") || n.Contains("water") || n.Contains("glass") || n.Contains("hidden") || n.Contains("decal")
                     || n.Contains("hair") || n.Contains("eye") || n.Contains("skin") || n.Contains("foliage") || n.Contains("terrain"));
        }

        /// Packed mask for game shaders: no metal, full AO, no detail, medium-wet smoothness.
        static Texture2D NeutralMask()
        {
            if (neutralMask != null) return neutralMask;
            neutralMask = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = new Color(0f, 1f, 0f, 0.6f);
            neutralMask.SetPixels(px); neutralMask.Apply();
            return neutralMask;
        }

        static void Report(string what, Material m)
        {
            var props = new System.Text.StringBuilder();
            var sh = m.shader;
            for (int i = 0; i < sh.GetPropertyCount() && i < 40; i++)
                if (sh.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture) props.Append(sh.GetPropertyName(i)).Append(' ');
            Debug.Log("[RageEngine] gore/bone material: " + what + " | texture slots: " + props);
        }

        /// Bakes simple top-light shading into vertex colours (used when the shader is unlit).
        public static void Shade(Mesh mesh, System.Func<int, Vector3, Color> baseColor)
        {
            var v = mesh.vertices; var n = mesh.normals;
            var c = new Color[v.Length];
            Vector3 light = new Vector3(0.3f, 1f, 0.2f).normalized;
            for (int i = 0; i < v.Length; i++)
            {
                var bc = baseColor(i, v[i]);
                float l = 0.55f + 0.45f * Mathf.Max(0f, Vector3.Dot(n[i], light));
                c[i] = new Color(bc.r * l, bc.g * l, bc.b * l, 1f);
            }
            mesh.colors = c;
        }
    }

    public sealed partial class RageEngineScript
    {
        static Mesh[] chunkMeshes;
        internal static void ResetGoreMaterials() { bruiseMats = null; }
        static Mesh craterMesh;
        static readonly List<GameObject> chunks = new List<GameObject>();
        static readonly List<(Transform t, Vector3 scale)> crushedHeads = new List<(Transform, Vector3)>();
        static readonly Color Meat = new Color(0.55f, 0.06f, 0.06f), Flesh = new Color(0.78f, 0.32f, 0.30f), Fat = new Color(0.85f, 0.72f, 0.45f);

        // ---------------- heavy impact entry (from contacts and slams) ----------------
        /// Called with the momentum severity of a heavy blunt impact (object or slam). Works on corpses too.
        void HeavyBlunt(NpcState s, RagdollPart part, Vector3 point, Vector3 dir, float severity, bool slam)
        {
            var c = s.creature;
            var t = part.type;
            bool head = Has(t, RagdollPart.Type.Head);
            bool trunk = Has(t, RagdollPart.Type.Torso | RagdollPart.Type.Neck);

            if (head && headCrush && !s.headCrushed && severity >= headCrushForce) { CrushHead(s, part, point, dir, severity); return; }
            if (trunk && spineBreaks && !s.spineBroken && !c.isKilled && severity >= spineBreakForce && !SpineShrug(s, part, point)) BreakSpine(s, part, point);
            if (fleshTearing && severity >= fleshTearForce && !part.isSliced) TearFlesh(s, part, point, dir, severity);
            // heavy blunt to a limb always breaks it
            if (fracturesEnabled && !c.isKilled && severity >= fleshTearForce && LimbOf(t) != 0 && !Has(s.brokenParts, LimbOf(t)))
                Fracture(s, part, point, severity, true);
        }

        // ---------------- flesh tearing ----------------
        const RagdollPart.Type Extremities = RagdollPart.Type.LeftHand | RagdollPart.Type.RightHand | RagdollPart.Type.LeftFoot | RagdollPart.Type.RightFoot;

        void TearFlesh(NpcState s, RagdollPart part, Vector3 point, Vector3 dir, float severity)
        {
            if (Time.time < s.nextTear || Has(part.type, Extremities)) return;   // hands/feet are too small to take a chunk out of
            s.nextTear = Time.time + 0.25f;
            int n = Mathf.Clamp(Mathf.RoundToInt(1f + severity * 0.8f), 2, 7);
            Vector3 outward = point - part.transform.position;
            if (outward.sqrMagnitude < 1e-4f) outward = -dir;
            outward.Normalize();
            SpawnChunks(point, (outward * 0.7f + dir * 0.6f).normalized, n, Mathf.Clamp(2f + severity * 0.6f, 2f, 7f), Mathf.Clamp(0.8f + severity * 0.08f, 0.8f, 1.4f), s.creature);
            if (s.craters < 8) Guard("gore", () => AddCrater(s, part, point, outward, severity));
            RageEngineScript.Blood(part.transform);
            s.bleedRate += 0.25f * bleedingRate;
            s.pain = 1f;
            Sfx.PlayAt("NRE_GoreTear", point, 1f);
            Log($"{s.creature.name} FLESH TORN on {part.type} sev={severity:0.0} ({n} chunks)");
        }

        static void SpawnChunks(Vector3 point, Vector3 dir, int n, float speed, float size, Creature from = null, bool skull = false)
        {
            if (RageEngineScript.ExternalGore) return;
            if (chunkMeshes == null) BuildChunkMeshes();
            int max = Mathf.RoundToInt(maxGoreChunks);
            Color skin = from != null ? Mats.SkinOf(from) : new Color(0.78f, 0.58f, 0.48f);
            var mat = skull ? Mats.MakeTextured(TexGen.Bone(), TexGen.BoneNormal(), new Color(0.93f, 0.88f, 0.8f), 0.35f, false)
                            : Mats.MakeTextured(TexGen.Meat(skin), TexGen.MeatNormal(skin), Color.white, 0.8f, false);
            // chunks never collide with people: nobody trips over gore (0.11)
            var bodyCols = new List<Collider>();
            var all = Creature.allActive;
            if (all != null)
                foreach (var cr in all)
                    if (cr != null && cr.ragdoll != null && (cr.transform.position - point).sqrMagnitude < 36f)
                        bodyCols.AddRange(cr.ragdoll.GetComponentsInChildren<Collider>());
            for (int i = 0; i < n; i++)
            {
                while (chunks.Count > 0 && (chunks.Count >= max || chunks[0] == null))
                {
                    if (chunks[0] != null) Object.Destroy(chunks[0]);
                    chunks.RemoveAt(0);
                }
                var go = new GameObject("RageEngine_Chunk");
                go.transform.position = point + Random.insideUnitSphere * 0.03f;
                go.transform.rotation = Random.rotation;
                float sc = Random.Range(0.6f, 1.3f) * size;
                go.transform.localScale = skull ? new Vector3(sc * 1.3f, sc * 0.3f, sc * Random.Range(0.8f, 1.2f))   // thin curved plates of skull
                                                : new Vector3(sc, sc * Random.Range(0.6f, 1f), sc);
                go.AddComponent<MeshFilter>().sharedMesh = chunkMeshes[Random.Range(0, chunkMeshes.Length)];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var col = go.AddComponent<SphereCollider>();
                col.radius = 0.016f;
                foreach (var bc in bodyCols) if (bc != null) Physics.IgnoreCollision(col, bc, true);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.04f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Vector3 v = (dir + Random.insideUnitSphere * 0.7f).normalized * speed * Random.Range(0.5f, 1.2f) + Vector3.up * Random.Range(0.5f, 2f);
                rb.AddForce(v, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
                chunks.Add(go);
                Object.Destroy(go, 40f);
            }
        }

        /// Torn crater left on the body: a dark, raw, bloody patch (and a glimpse of bone on big hits).
        void AddCrater(NpcState s, RagdollPart part, Vector3 point, Vector3 outward, float severity)
        {
            // 0.20: no more crater meshes - torn meat flies out and the wound is painted raw into the skin
            SpawnChunks(point, outward, Mathf.Clamp(3 + (int)(severity * 1.5f), 3, 8), 3f, 0.8f, s.creature);
            Guard("woundpaint", () =>
            {
                if (!Stamp(s.creature, point, Mathf.Clamp(0.04f + severity * 0.008f, 0.04f, 0.08f), Random.value < 0.5f ? "crush_wound" : "torn_flesh"))
                SkinPaint.Paint(s.creature, point, Mathf.Clamp(0.035f + severity * 0.008f, 0.035f, 0.07f), new Color(0.3f, 0.02f, 0.03f, 0.9f), TexGen.Bruise());
                SkinPaint.Paint(s.creature, point, 0.022f, new Color(0.55f, 0.08f, 0.08f, 0.85f), TexGen.Bruise());
            });
            for (int i = 0; i < 2; i++) RageEngineScript.Blood(part.transform);
            s.craters++;
            if (severity >= fleshTearForce * 1.6f && Has(part.type, ArmParts | LegParts) && !Has(part.type, Extremities))
                s.boneMeshes.Add(BoneMesh.Attach(part, point, outward));
        }

        void AddCraterOld(NpcState s, RagdollPart part, Vector3 point, Vector3 outward, float severity)
        {
            if (craterMesh == null) craterMesh = BuildCrater();
            if (SkinAt(s.creature, point, 0.08f, out var cp, out var cn, out _)) { point = cp; outward = cn; }   // on the real skin
            var go = new GameObject("RageEngine_Crater");
            go.transform.SetParent(part.transform, true);
            go.transform.position = point + outward * 0.002f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, outward) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float d = Mathf.Clamp(0.06f + severity * 0.015f, 0.06f, 0.13f);
            var ls = part.transform.lossyScale;
            go.transform.localScale = new Vector3(d / Mathf.Max(0.01f, ls.x), d * 0.5f / Mathf.Max(0.01f, ls.y), d * Random.Range(0.7f, 1f) / Mathf.Max(0.01f, ls.z));
            go.AddComponent<MeshFilter>().sharedMesh = craterMesh;
            var r = go.AddComponent<MeshRenderer>();
            var skin = Mats.SkinOf(s.creature);
            r.sharedMaterial = Mats.MakeTextured(TexGen.Crater(skin), TexGen.CraterNormal(skin), Color.white, 0.8f, false);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.boneMeshes.Add(go);
            s.craters++;
            if (severity >= fleshTearForce * 1.6f && Has(part.type, ArmParts | LegParts) && !Has(part.type, Extremities))
                s.boneMeshes.Add(BoneMesh.Attach(part, point, outward));
        }

        // ---------------- head crush ----------------
        void CrushHead(NpcState s, RagdollPart head, Vector3 point, Vector3 dir, float severity)
        {
            if (!faceCave || projectileHit34) return;                                // 0.34: no head crush from one shot, none at all when switched off
            var c = s.creature;
            s.headCrushed = true;
            s.finalKill = true; s.dying = false; s.onAdrenaline = false; s.noGasp = true;
            Sfx.PlayAt("NRE_HeadCrush", point, 1f);
            Sfx.PlayAt("NRE_GoreTear", point, 0.9f);
            for (int i = 0; i < 3; i++) RageEngineScript.Blood(head.transform);
            SpawnChunks(point, (head.transform.position - point + dir).normalized, 6, 4f, 1.1f, c);
            Guard("gore", () => AddCrater(s, head, point, (point - head.transform.position).normalized, severity));
            // 0.15: cave the skull in by deforming the head's real mesh around the blow (not bone scaling), and
            // throw skull fragments with the flesh
            Transform mesh = head.meshBone != null ? head.meshBone : head.bone != null ? head.bone.mesh : null;
            Vector3 into = (dir.normalized * 0.6f + (head.transform.position - point).normalized * 0.4f).normalized;
            float k = Mathf.Clamp(severity / Mathf.Max(1f, headCrushForce), 1f, 2f);
            int dented = 0;
            if (mesh != null) Guard("headdent", () => dented = HeadDeform.Dent(c, mesh, point, into, 0.065f * k, 0.03f * k));
            SpawnChunks(point, (-into + Vector3.up * 0.3f).normalized, 5, 3.5f, 0.9f, c, true);
            Log($"{c.name}: skull caved in ({dented} mesh{(dented == 1 ? "" : "es")} deformed)");
            Guard("skullgore", () => SkullSmashExtras(s, head, point, dir));
            if (severity >= headCrushForce * 1.6f) { Guard("headpop", () => HeadPop49(s, head, point, dir)); return; }   // 0.49: big enough - goo
            RageMoment("skull crushed");
            if (c.jaw != null) { s.jawBroken = s.jawTorn = true; s.jawRest = c.jaw.localRotation; }
            if (!c.isKilled) Guard("kill", () => c.Kill());
            if (c.ragdoll.headPart != null && c.ragdoll.headPart.physicBody != null)
                c.ragdoll.headPart.physicBody.AddForce(dir * Mathf.Min(severity, 8f), ForceMode.VelocityChange);
            Log($"{c.name} HEAD CRUSHED sev={severity:0.0}");
        }

        static void LateUpdateCrushedHeads()
        {
            for (int i = crushedHeads.Count - 1; i >= 0; i--)
            {
                var (t, sc) = crushedHeads[i];
                if (t == null) { crushedHeads.RemoveAt(i); continue; }
                t.localScale = sc;
            }
        }

        // ---------------- spine ----------------
        void BreakSpine(NpcState s, RagdollPart part, Vector3 point)
        {
            var c = s.creature;
            s.spineBroken = true;
            s.pain = 1f;
            Sfx.PlayAt("NRE_SpineSnap", point, 1f);
            Sfx.PlayAt("NRE_BoneCrunch", point, 0.8f);
            foreach (var side in new[] { Side.Left, Side.Right }) if (breakHaptics) Haptics.Pulse(side, 1f, 2);
            MakeLimp(s, LegParts);
            Guard("vertebra", () => s.boneMeshes.Add(BoneMesh.Attach(part, point, (point - part.transform.position).normalized, "RE_Bone_Vertebra")));
            s.legWounded = true;
            s.balance = MinBalance;
            Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Add(InjuryHandler, 0.05f));
            // 0.18: a snapped spine is the end - no getting up, no adrenaline, no dying moments
            s.finalKill = true; s.dying = false; s.onAdrenaline = false; s.rushUntil = 0f;
            AllowFall(s, 3f);
            Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
            Speak(s, "deathlong");
            if (!c.isKilled) Guard("kill", () => c.Kill());
            Log($"{c.name} SPINE BROKEN - dies");
            RageMoment("spine snapped");
        }

        // ---------------- meshes ----------------
        static void BuildChunkMeshes()
        {
            chunkMeshes = new Mesh[4];
            for (int k = 0; k < chunkMeshes.Length; k++)
            {
                var rnd = new System.Random(11 + k * 7);
                const int lat = 5, lon = 7;
                var v = new List<Vector3>(); var tri = new List<int>();
                for (int a = 0; a <= lat; a++)
                {
                    float th = a / (float)lat * Mathf.PI;
                    for (int b = 0; b < lon; b++)
                    {
                        float ph = b / (float)lon * Mathf.PI * 2f;
                        float r = 0.018f * (0.7f + 0.6f * (float)rnd.NextDouble());
                        if (a == 0 || a == lat) r = 0.016f;
                        v.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * r, Mathf.Cos(th) * r, Mathf.Sin(th) * Mathf.Sin(ph) * r));
                    }
                }
                for (int a = 0; a < lat; a++)
                    for (int b = 0; b < lon; b++)
                    {
                        int i0 = a * lon + b, i1 = a * lon + (b + 1) % lon, i2 = i0 + lon, i3 = i1 + lon;
                        tri.Add(i0); tri.Add(i1); tri.Add(i2);
                        tri.Add(i1); tri.Add(i3); tri.Add(i2);
                    }
                var uv = new List<Vector2>();
                for (int a = 0; a <= lat; a++) for (int b = 0; b < lon; b++) uv.Add(new Vector2(b / (float)lon, a / (float)lat));
                var m = new Mesh { name = "RageEngine_Chunk" };
                m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
                var rr = new System.Random(5 + k);
                Mats.Shade(m, (i, p) =>
                {
                    double x = rr.NextDouble();
                    return x < 0.15 ? Fat : x < 0.55 ? Flesh : Meat;
                });
                chunkMeshes[k] = m;
            }
        }

        /// Ragged torn disc: raw red rim, dark centre, bits of fat.
        static Mesh BuildCrater()
        {
            const int seg = 16, rings = 4;
            var v = new List<Vector3>(); var tri = new List<int>(); var rnd = new System.Random(9);
            var ring = new List<int>();
            v.Add(new Vector3(0f, -0.4f, 0f));                       // the hole where the chunk was
            for (int r = 1; r <= rings; r++)
            {
                float t = r / (float)rings;
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    float rad = 0.5f * t * (r == rings ? 0.75f + 0.45f * (float)rnd.NextDouble() : 0.9f + 0.2f * (float)rnd.NextDouble());
                    float y = r == rings ? 0f : r == rings - 1 ? 0.2f : -0.35f + 0.3f * t;    // steep walls, torn lip
                    v.Add(new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad));
                }
            }
            for (int i = 0; i < seg; i++) { tri.Add(0); tri.Add(1 + (i + 1) % seg); tri.Add(1 + i); }
            for (int r = 1; r < rings; r++)
                for (int i = 0; i < seg; i++)
                {
                    int a = 1 + (r - 1) * seg + i, b = 1 + (r - 1) * seg + (i + 1) % seg, c = a + seg, d = b + seg;
                    tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c);
                }
            var uv = new List<Vector2> { new Vector2(0.5f, 0f) };
            for (int r = 1; r <= rings; r++) for (int i = 0; i < seg; i++) uv.Add(new Vector2(i / (float)seg, r / (float)rings));
            var m = new Mesh { name = "RageEngine_Crater" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            var rr = new System.Random(4);
            Mats.Shade(m, (i, p) =>
            {
                float d = new Vector2(p.x, p.z).magnitude * 2f;
                if (d < 0.45f) return new Color(0.22f, 0.01f, 0.01f);
                if (rr.NextDouble() < 0.12) return Fat;
                return d > 0.9f ? Flesh : Meat;
            });
            return m;
        }
    }
}

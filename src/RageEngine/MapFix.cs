// Rage Engine 0.25.3 - Red Room Diner shader fix + anchoring (see AnchorMap). The map bundle was built with BasSDK 1.1.0, but the game is 1.3.x:
// the bundle's materials point at the SDK's shared shader bundle, which doesn't match the game's -> everything renders
// purple (Unity's error shader). The scene itself loads fine. So when the diner is loaded, every material whose
// shader is missing/unsupported is rebuilt on the same game material base the gore uses (known to render on Quest),
// with the map's own texture loaded from Textures/Map/<material>.png. The skybox is replaced too.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static readonly HashSet<GameObject> fixedScenes = new HashSet<GameObject>();      // keyed by the first root object (Scene.handle differs between Unity versions)
        static readonly Dictionary<string, Material> mapMats = new Dictionary<string, Material>();
        static float nextMapCheck;

        void TickMapFix()
        {
            float now = Time.time;
            if (mapActive) Guard("rescue", () => MapRescue(now));
            if (now < nextMapCheck) return;
            nextMapCheck = now + 2f;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (!sc.isLoaded || sc.name == null || sc.name.IndexOf("RedRoom", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var roots = sc.GetRootGameObjects();
                if (roots == null || roots.Length == 0) continue;
                var key = roots[0];
                if (fixedScenes.Contains(key)) continue;
                fixedScenes.Add(key);
                int n = 0, fixedN = 0;
                foreach (var root in roots)
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        bool changed = false;
                        for (int k = 0; k < mats.Length; k++)
                        {
                            var m = mats[k]; n++;
                            if (m == null) continue;
                            var sh = m.shader;
                            bool broken = sh == null || !sh.isSupported || sh.name == "Hidden/InternalErrorShader" || sh.name.StartsWith("ThunderRoad/Environment/Lit_Moss");
                            if (!broken) continue;
                            mats[k] = MapMat(m.name); changed = true; fixedN++;
                        }
                        if (changed) r.sharedMaterials = mats;
                    }
                Guard("sky", () =>
                {
                    var sky = Shader.Find("Skybox/Procedural");
                    if (sky != null && sky.isSupported) RenderSettings.skybox = new Material(sky);
                    else { RenderSettings.skybox = null; foreach (var cam in Camera.allCameras) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.65f, 0.78f); } }
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.55f, 0.53f, 0.5f);
                });
                Log($"Red Room Diner: rebuilt {fixedN} of {n} materials on the game's own shader");
                Guard("anchor", () => AnchorMap(roots));
            }
        }

        // ------------------------------------------------------------------ 0.25.3 anchoring
        // "I spawn in and float up or fall": the SDK 1.1 bundle's collider layers don't line up with the 1.3 game's
        // layer table, so the floor isn't on a layer the player's locomotion stands on. Every static map collider is put
        // on the game's Default layer, props on PhysicObject, missing floor/wall colliders are added, a solid floor slab
        // goes under the whole map, and if you still end up under the floor or up in the air you're put back at the start.
        static bool mapActive; static Vector3 mapSpawn = new Vector3(7f, 0.1f, -1.5f); static Quaternion mapSpawnRot = Quaternion.Euler(0f, 180f, 0f);
        static float mapFloorY, mapLoadedAt, nextRescue; static int mapRescues, mapPosLogs; static GameObject mapRoot0;

        static void AnchorMap(GameObject[] roots)
        {
            int def = LayerMask.NameToLayer("Default"); if (def < 0) def = 0;
            int phys = LayerMask.NameToLayer("PhysicObject"); if (phys < 0) phys = def;
            int added = 0, relayered = 0, fixedMesh = 0;
            var layerCount = new Dictionary<int, int>();
            Bounds all = default; bool any = false;
            foreach (var root in roots)
            {
                if (root == null) continue;
                var st = root.transform.Find("Start");
                if (root.name == "LevelDefinition" && st != null) { mapSpawn = st.position + Vector3.up * 0.1f; mapSpawnRot = st.rotation; }
                foreach (var col in root.GetComponentsInChildren<Collider>(true))
                {
                    if (col == null) continue;
                    layerCount[col.gameObject.layer] = layerCount.TryGetValue(col.gameObject.layer, out var c0) ? c0 + 1 : 1;
                    if (col.isTrigger) continue;
                    var rb = col.attachedRigidbody;
                    int want = rb != null && !rb.isKinematic ? phys : def;
                    if (col.gameObject.layer != want) { col.gameObject.layer = want; relayered++; }
                    if (col is MeshCollider mc && mc.sharedMesh == null)
                    {
                        var mf0 = col.GetComponent<MeshFilter>();
                        if (mf0 != null && mf0.sharedMesh != null) { mc.sharedMesh = mf0.sharedMesh; fixedMesh++; }
                    }
                }
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    var r = mf.GetComponent<Renderer>();
                    if (r != null) { if (!any) { all = r.bounds; any = true; } else all.Encapsulate(r.bounds); }
                    if (mf.GetComponent<Collider>() != null || mf.GetComponentInParent<Rigidbody>() != null) continue;
                    if (mf.name.IndexOf("Shard", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    try { var mc2 = mf.gameObject.AddComponent<MeshCollider>(); mc2.sharedMesh = mf.sharedMesh; mf.gameObject.layer = def; added++; } catch { }
                }
            }
            // the floor height under the start point (ignore anything that isn't solid map)
            mapFloorY = 0f;
            if (Physics.Raycast(mapSpawn + Vector3.up * 1.5f, Vector3.down, out var hit, 6f, 1 << def, QueryTriggerInteraction.Ignore)) mapFloorY = hit.point.y;
            // a solid slab under the whole map, top at floor level
            var slab = new GameObject("RageEngine_MapFloor");
            slab.layer = def;
            var bc = slab.AddComponent<BoxCollider>();
            Vector3 centre = any ? all.center : mapSpawn; Vector3 size = any ? all.size : new Vector3(60f, 1f, 60f);
            slab.transform.position = new Vector3(centre.x, mapFloorY - 0.5f, centre.z);
            bc.size = new Vector3(size.x + 10f, 1f, size.z + 10f);
            mapRoot0 = roots[0];
            mapActive = true; mapLoadedAt = Time.time; mapRescues = 0; mapPosLogs = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var kv in layerCount) sb.Append(' ').Append(LayerMask.LayerToName(kv.Key)).Append('(').Append(kv.Key).Append(")=").Append(kv.Value);
            Debug.Log($"[RageEngine] Red Room Diner anchoring: colliders by layer:{sb} | relayered {relayered}, added {added} mesh colliders, fixed {fixedMesh} empty ones, floor y {mapFloorY:0.00}, slab {bc.size}, start {mapSpawn}");
        }

        static void MapRescue(float now)
        {
            if (mapRoot0 == null) { mapActive = false; return; }                        // the diner was unloaded
            var pl = Player.local; var pc = pl != null ? pl.creature : null;
            if (pc == null || now < nextRescue) return;
            float y = pc.transform.position.y;
            if (mapPosLogs < 4 && now - mapLoadedAt > mapPosLogs * 5f + 2f) { mapPosLogs++; Debug.Log($"[RageEngine] Red Room: player at {pc.transform.position}"); }
            if (y < mapFloorY - 2.5f || y > mapFloorY + 6f)
            {
                nextRescue = now + 2f;
                mapRescues++;
                Debug.Log($"[RageEngine] Red Room Diner: player {(y < mapFloorY ? "fell through" : "floated up")} to y {y:0.0} - back to the start (#{mapRescues})");
                Guard("teleport", () => pl.Teleport(mapSpawn, mapSpawnRot, false, true));
            }
            else nextRescue = now + 0.25f;
        }

        static Material MapMat(string matName)
        {
            string key = (matName ?? "").Replace(" (Instance)", "").Trim().ToLowerInvariant();
            if (mapMats.TryGetValue(key, out var have) && have != null) return have;
            var tex = GoreTex.MeshTex("Map/" + key + ".png");
            bool glass = key.Contains("glass");
            Color tint = glass ? new Color(0.75f, 0.88f, 0.95f, 0.35f) : Color.white;
            float smooth = glass ? 0.95f : key.Contains("steel") || key.Contains("metal") ? 0.6f : key.Contains("tile") || key.Contains("counter") || key.Contains("table") ? 0.45f : 0.2f;
            Material m = tex != null ? Mats.MakeTextured(tex, null, tint, smooth, glass) : Mats.Make(Guess(key), smooth);
            return mapMats[key] = m;
        }

        static Color Guess(string k) =>
            k.Contains("red") ? new Color(0.6f, 0.08f, 0.08f) : k.Contains("black") || k.Contains("dark") ? new Color(0.12f, 0.12f, 0.13f)
            : k.Contains("asphalt") ? new Color(0.25f, 0.25f, 0.26f) : k.Contains("yellow") ? new Color(0.9f, 0.75f, 0.1f) : new Color(0.75f, 0.72f, 0.66f);
    }
}

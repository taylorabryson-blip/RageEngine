// Rage Engine 0.31 - Rage Engine levels (Skybar, Rage Burger, Red Room Diner) in the game's level list.
// The level entry loads the Arena scene; when we see one of our level ids we build the map on top and move the
// GAME'S OWN wave system into it: the Arena's wave spawner now spawns at the map's spawn points, and the Arena's
// wave book stands next to you at the map's front door. No Rage Engine wave system.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static string levelSeen31, levelMap29;
        static float levelAt29 = -1f;
        static bool levelBuilt29, wavesHooked31;
        static readonly Dictionary<string, string> levelMaps31 = new Dictionary<string, string>
        {
            { "RE_RageTower", "RageTower" }, { "RE_RedRoomDiner", "RedRoomDiner" }, { "RE_Skybar", "Skybar" },
        };

        static IEnumerable<string> LevelIds40()
        {
            string a = null, b = null, c = null;
            Guard("lvl.master", () => { if (Level.master != null && Level.master.data != null) a = Level.master.data.id; });
            Guard("lvl.current", () => { if (Level.current != null && Level.current.data != null) b = Level.current.data.id; });
            Guard("lvl.manager", () => c = LevelManager.GetCurrentLevel());
            yield return a; yield return b; yield return c;
        }

        void TickLevel29()
        {
            // 0.40: the scene is the Arena's, so its own Level can say "Arena" - ask every source and take ours if any has it
            string id = null;
            foreach (var cand in LevelIds40())
                if (cand != null && levelMaps31.ContainsKey(cand)) { id = cand; break; }
            if (id == null) id = Level.current != null && Level.current.data != null ? Level.current.data.id : null;
            if (id != levelSeen31)
            {
                levelSeen31 = id;
                levelMap29 = id != null && levelMaps31.TryGetValue(id, out var m) ? m : null;
                levelAt29 = -1f; levelBuilt29 = false; wavesHooked31 = false;
                if (levelMap29 != null) { Debug.Log($"[RageEngine] level {id}: building {levelMap29}"); Notify($"Rage Engine: building {levelMap29}..."); }
            }
            if (levelMap29 == null) return;
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) return;
            float now = Time.time;
            if (levelAt29 < 0f) { levelAt29 = now; return; }
            if (!levelBuilt29)
            {
                if (now - levelAt29 < 1.5f) return;                                   // let the level settle first
                levelBuilt29 = true;
                mapName = levelMap29;
                MapGo();
                return;
            }
            if (!wavesHooked31 && map26In && now - levelAt29 > 2.5f) { wavesHooked31 = true; Guard("waves.hook", HookWaves31); }
        }

        /// Point the level's own wave spawner(s) at the map and bring the wave book over.
        /// The book's stand: climb up from the book while the parent is still a small object (not the level itself).
        static Transform BookRoot47(Transform t)
        {
            var cur = t;
            for (int i = 0; i < 4 && cur.parent != null; i++)
            {
                var p = cur.parent;
                if (p.parent == null || p.childCount > 12) break;                                      // the level / a big group: stop
                var r = p.GetComponentsInChildren<Renderer>(true);
                Bounds b = r.Length > 0 ? r[0].bounds : new Bounds(p.position, Vector3.zero);
                foreach (var x in r) b.Encapsulate(x.bounds);
                if (b.size.magnitude > 4f) break;                                                     // bigger than a book stand
                cur = p;
            }
            return cur;
        }

        static void HookWaves31()
        {
            var pts = new List<Transform>();
            foreach (var p in map26Spawns)
            {
                var t = new GameObject("RageEngine_WaveSpawn").transform;
                t.SetParent(map26Root.transform, true);
                t.position = map26Origin + p + Vector3.up * 0.05f;
                pts.Add(t);
            }
            if (pts.Count == 0) { Notify("This map has no spawn points."); return; }
            int spawners = 0, points = 0;
            foreach (var ws in Object.FindObjectsOfType<WaveSpawner>())
            {
                if (ws == null) continue;
                spawners++;
                if (ws.spawns != null) for (int i = 0; i < ws.spawns.Count; i++) ws.spawns[i] = pts[i % pts.Count];
                if (ws.spawns != null && ws.spawns.Count < pts.Count) for (int i = ws.spawns.Count; i < pts.Count; i++) ws.spawns.Add(pts[i]);
                if (ws.spawnPoints != null)
                {
                    for (int i = 0; i < ws.spawnPoints.Count; i++) if (ws.spawnPoints[i] != null) { ws.spawnPoints[i].transform = pts[i % pts.Count]; points++; }
                    for (int i = ws.spawnPoints.Count; i < pts.Count; i++) { ws.spawnPoints.Add(new WaveSpawner.SpawnPoint(pts[i])); points++; }
                }
                ws.ignoreDungeonLimits = true;
            }
            // the wave book: next to the front door, facing you
            int books = 0;
            Vector3 start = map26Origin + map26Start;
            Vector3 fwd = Quaternion.Euler(0f, map26Yaw, 0f) * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            // 0.47: inactive books count too (FindObjectsOfType skipped them), and the whole book stand moves with it
            foreach (var ui in Resources.FindObjectsOfTypeAll<UIWaveSpawner>())
            {
                if (ui == null || !ui.gameObject.scene.IsValid() || ui.hideFlags != HideFlags.None) continue;
                Transform t = BookRoot47(ui.transform);
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                if (!ui.gameObject.activeInHierarchy) ui.gameObject.SetActive(true);
                Vector3 pos = start + fwd * 1.4f + right * (0.9f + books * 1.2f);
                // stand it on the floor there
                if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var fl, 4f, ~0, QueryTriggerInteraction.Ignore)) pos = fl.point;
                Vector3 off = ui.transform.position - t.position;                         // where the book sits on its stand
                float lift = t == ui.transform ? 1.35f : 0f;
                t.position = pos + Vector3.up * lift;
                t.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(start - pos, Vector3.up).normalized, Vector3.up);
                books++;
                Debug.Log($"[RageEngine] wave book '{ui.name}' (stand '{t.name}') moved to the map");
            }
            Debug.Log($"[RageEngine] waves: {spawners} spawner(s), {points} spawn point(s), {books} wave book(s) moved to the map");
            Notify(spawners > 0 && books > 0 ? "Wave book is next to you - pick a wave and fight."
                 : spawners > 0 ? "Waves are hooked up, but this level has no wave book."
                 : "This level has no wave spawner.");
        }
    }
}

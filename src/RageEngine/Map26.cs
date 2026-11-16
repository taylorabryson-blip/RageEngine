// Rage Engine 0.26 - maps built by the mod itself, inside whatever level you're in.
// The bundled-scene maps kept breaking (they need the exact SDK of your game version: purple materials, no floor).
// So this one needs no SDK and no bundle: the mod reads a plain box list (Maps/<name>.txt), builds the geometry with
// the game's own material (the one the gore uses - known to render on Quest), gives everything colliders on the
// game's Default layer, bakes a NavMesh for it at runtime, and puts the whole place high above the level you loaded.
// Mod Options > Rage Engine: Maps: pick the place, "Go there", "Spawn enemies there", "Leave".
//   Windows break (hit them, throw someone through them), doors swing, chairs / tables / trash cans are loose props.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ThunderRoad;
using UnityEngine;
using UnityEngine.AI;

namespace RageEngine
{
    /// A pane of glass: enough of a hit and it goes.
    public sealed class GlassPane26 : MonoBehaviour
    {
        public float hp = 30f; public Material mat; bool broken;
        void OnCollisionEnter(Collision c)
        {
            if (broken || c == null) return;
            float v = c.relativeVelocity.magnitude;
            if (v < 1.8f) return;
            float m = c.rigidbody != null ? Mathf.Clamp(c.rigidbody.mass, 0.5f, 25f) : 2f;
            hp -= v * m * 1.5f;
            if (hp > 0f) { Sfx.PlayAt("NRE_MetalBang", transform.position, 0.25f); return; }
            Vector3 at = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            Break(at, -c.relativeVelocity);
        }
        public void Break(Vector3 at, Vector3 vel)
        {
            if (broken) return;
            broken = true;
            RageEngineScript.Guard("glass", () =>
            {
                Sfx.PlayAt("NRE_GlassBreak", at, 1f);
                Vector3 size = transform.localScale;                      // the pane is a unit cube scaled to its size
                int a0 = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;          // the two long axes
                int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
                int a1 = 3 - a0 - thin;
                int n0 = Mathf.Clamp(Mathf.RoundToInt(size[a0] / 0.5f), 2, 5), n1 = Mathf.Clamp(Mathf.RoundToInt(size[a1] / 0.5f), 2, 4);
                for (int i = 0; i < n0; i++)
                    for (int j = 0; j < n1; j++)
                    {
                        var sh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        sh.name = "RageEngine_Shard";
                        Vector3 local = Vector3.zero; local[a0] = (i + 0.5f) / n0 - 0.5f; local[a1] = (j + 0.5f) / n1 - 0.5f;
                        sh.transform.position = transform.TransformPoint(local);
                        sh.transform.rotation = transform.rotation * Quaternion.Euler(Random.insideUnitSphere * 12f);
                        Vector3 s = Vector3.one * 0.012f; s[a0] = size[a0] / n0 * Random.Range(0.55f, 0.95f); s[a1] = size[a1] / n1 * Random.Range(0.55f, 0.95f);
                        sh.transform.localScale = s;
                        if (mat != null) sh.GetComponent<MeshRenderer>().sharedMaterial = mat;
                        var rb = sh.AddComponent<Rigidbody>(); rb.mass = 0.3f;
                        Vector3 away = (sh.transform.position - at).normalized;
                        rb.AddForce(vel.normalized * Random.Range(0.5f, 2.5f) + away * Random.Range(0.3f, 1.5f), ForceMode.VelocityChange);
                        rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
                        Destroy(sh, Random.Range(10f, 16f));
                    }
            });
            Destroy(gameObject);
        }
    }

    /// Loose props that clang when they take a knock.
    public sealed class MapProp26 : MonoBehaviour
    {
        public string sound; float next;
        void OnCollisionEnter(Collision c)
        {
            if (sound == null || c == null || Time.time < next || c.relativeVelocity.magnitude < 2.5f) return;
            next = Time.time + 0.4f;
            Sfx.PlayAt(sound, transform.position, Mathf.Clamp01(c.relativeVelocity.magnitude / 6f));
        }
    }

    public sealed partial class RageEngineScript
    {
        const string CM = "Rage Engine: Maps";
        public static ModOptionString[] mapNames = { new ModOptionString("Rage Skybar (skyscraper)", null, "Skybar"), new ModOptionString("Rage Burger (drive-thru)", null, "BurgerJoint"), new ModOptionString("Red Room Diner", null, "RedRoomDiner") };
        public static ModOptionString[] mapWaveSizes = { new ModOptionString("3", null, "3"), new ModOptionString("5", null, "5"), new ModOptionString("8", null, "8"), new ModOptionString("1", null, "1") };
        public static ModOptionString[] mapWaveKinds = { new ModOptionString("Normal enemies", null, "0"), new ModOptionString("Super soldiers", null, "1"), new ModOptionString("Mixed", null, "2"), new ModOptionString("Mixed toughness (60-1500 HP)", null, "3") };
        public static ModOptionString[] mapLooks = { new ModOptionString("Auto", null, "0"), new ModOptionString("Game shader (lit)", null, "1"), new ModOptionString("Flat colours", null, "2") };
        public static ModOptionString[] mapGo = { new ModOptionString("Go", null, "go") };

        [ModOptionCategory(CM, 12), ModOptionOrder(0)]
        [ModOption("Place", "Which Rage Engine map to build. It is built in the sky above the level you're in - load any level first (Sandbox works).", valueSourceName = nameof(mapNames), defaultValueIndex = 0)]
        public static string mapName = "Skybar";
        public static string mapLook = "0";
        [ModOptionButton, ModOptionCategory(CM, 12), ModOptionOrder(1)]
        [ModOption("Go there", "Builds the place (first time) and puts you at its front door.", valueSourceName = nameof(mapGo), saveValue = false)]
        public static void MapGoButton(string _) => Guard("map.go", () => instance?.MapGo());
        [ModOptionCategory(CM, 12), ModOptionOrder(2)]
        [ModOption("Enemies per wave", "How many come when you press Spawn enemies.", valueSourceName = nameof(mapWaveSizes), defaultValueIndex = 0)]
        public static string mapWaveSize = "3";
        [ModOptionCategory(CM, 12), ModOptionOrder(3)]
        [ModOption("Enemy type", "Normal enemies, super soldiers (random super serum), or a mix.", valueSourceName = nameof(mapWaveKinds), defaultValueIndex = 0)]
        public static string mapWaveKind = "0";
        [ModOptionButton, ModOptionCategory(CM, 12), ModOptionOrder(4)]
        [ModOption("Spawn enemies there", "Enemies appear at the map's spawn points (parking lot, back lot, inside, roof) and come for you.", valueSourceName = nameof(mapGo), saveValue = false)]
        public static void MapWaveButton(string _) => Guard("map.wave", () => instance?.MapWave());
        [ModOptionButton, ModOptionCategory(CM, 12), ModOptionOrder(5)]
        [ModOption("Leave", "Back to where you were in the level.", valueSourceName = nameof(mapGo), saveValue = false)]
        public static void MapLeaveButton(string _) => Guard("map.leave", () => instance?.MapLeave());

        static GameObject map26Root; static string map26Built;
        static Vector3 map26Origin, map26Start, map26Return; static float map26Yaw; static Quaternion map26ReturnRot;
        static readonly List<Vector3> map26Spawns = new List<Vector3>();
        static readonly List<NavMeshDataInstance> map26Nav = new List<NavMeshDataInstance>();
        static bool map26In; static float map26NextCheck, map26KillY = -20f, map26SettleUntil; static int map26Rescues, map26SettleTry; static string map26LookBuilt;
        static readonly Dictionary<string, Material> mapMats26 = new Dictionary<string, Material>();
        static Shader unlit26; static bool unlitTried26; static int texOk26, texMiss26;
        static Mesh cube26;

        static float PF(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        void MapGo()
        {
            var pl = Player.local; var pc = pl != null ? pl.creature : null;
            if (pc == null) { Debug.Log("[RageEngine] map: no player"); return; }
            if (map26Root == null || map26Built != mapName || map26LookBuilt != mapLook)
            {
                if (map26In) { MapLeave(); }
                if (!BuildMap26(mapName, pc.transform.position)) return;
            }
            if (!map26In) { map26Return = pc.transform.position; map26ReturnRot = pc.transform.rotation; }
            map26In = true; map26Rescues = 0; map26SettleUntil = Time.time + 4f; map26SettleTry = 0;
            Physics.SyncTransforms();
            pl.Teleport(map26Origin + map26Start + Vector3.up * 0.1f, Quaternion.Euler(0f, map26Yaw, 0f), false, true);
            Guard("map.night", ApplyEnv28);
            Guard("lounge", StartLounge28);
            Guard("bounds.off", () => Bounds28(false));
            Debug.Log($"[RageEngine] map: you're at {map26Built} ({map26Origin + map26Start})");
        }

        void MapLeave()
        {
            var pl = Player.local;
            if (!map26In || pl == null) return;
            map26In = false;
            Guard("map.night.off", RestoreEnv28);
            Guard("lounge.off", StopLounge28);
            Guard("bounds.on", () => Bounds28(true));
            pl.Teleport(map26Return, map26ReturnRot, false, true);
            Debug.Log("[RageEngine] map: back in the level");
        }

        void MapWave()
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null || map26Root == null || map26Spawns.Count == 0) { Debug.Log("[RageEngine] map: build the place first (Go there)"); Notify("Go to a map first, then start a wave."); return; }
            int n = 3; int.TryParse(mapWaveSize, out n); n = Mathf.Clamp(n, 1, 8);
            int kind = 0; int.TryParse(mapWaveKind, out kind);
            LogFactions();
            int hostile = HostileFaction();
            // nearest-first spawn points that aren't right on top of you
            var pts = new List<Vector3>(map26Spawns);
            Vector3 me = pc.transform.position - map26Origin;
            pts.RemoveAll(p => (p - me).sqrMagnitude < 16f);
            if (pts.Count == 0) pts.AddRange(map26Spawns);
            NoteWave29("map");
            Notify($"Wave: {n} enemies incoming (faction {FactionName28(hostile)})");
            for (int i = 0; i < n; i++)
            {
                var data = PickSoldier(out int tableFaction);
                if (data == null) { Debug.Log("[RageEngine] map: no creature data in the catalog"); Notify("Rage Engine: couldn't find any enemy type to spawn."); return; }
                Vector3 pos = map26Origin + pts[Random.Range(0, pts.Count)] + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
                var lift = LiftArrival28(i);                                         // the first two come up in the elevator
                if (lift.HasValue) pos = lift.Value;
                Vector3 look = pc.transform.position - pos; look.y = 0f;
                float rotY = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
                int faction = hostile >= 0 ? hostile : tableFaction >= 0 && tableFaction != 2 ? tableFaction : data.factionId != 2 && data.factionId >= 0 ? data.factionId : -1;      // 0.28.2: always an aggressive faction first
                bool super = kind == 1 || (kind == 2 && Random.value < 0.4f);
                int comp = super ? RandomSuperSerum() : -1;
                string id = data.id;
                data.SpawnAsync(pos, rotY, null, false, null, cr =>
                {
                    if (cr == null) return;
                    Guard("map.faction", () => { if (faction >= 0 && faction != 2) cr.SetFaction(faction); });
                    if (kind == 3) QueueMixed(cr);
                    QueueSuit28(cr);
                    if (comp >= 0) pendingSoldiers.Add(new PendingSoldier { c = cr, at = Time.time + 1.2f, comp = comp });
                    Debug.Log($"[RageEngine] map enemy: {id} (faction {faction}){(comp >= 0 ? " - " + CompName[comp] : "")}");
                    MakeHostile28(cr, faction);
                });
            }
        }

        /// Per frame (cheap): cleans up after a level change, puts you back if you fall off the world.
        void TickMap26()
        {
            if (map26In) Guard("clouds", DriftClouds28);
            Guard("lounge", TickLounge28);
            Guard("hostile", TickHostile28);
            Guard("qol", TickQol29);
            float now = Time.time;
            if (now < map26NextCheck) return;
            map26NextCheck = now + 0.5f;
            if (map26Root == null)
            {
                if (map26Nav.Count > 0) { foreach (var n in map26Nav) Guard("nav.remove", () => n.Remove()); map26Nav.Clear(); }
                if (map26In || env28Saved) Guard("map.night.off", RestoreEnv28);
                Guard("lounge.off", ResetLounge28);
                Guard("bounds.on", () => Bounds28(true));
                map26In = false; map26Built = null;
                return;
            }
            // the death zone: anyone who goes over the edge
            float killY = map26Origin.y + map26KillY;
            foreach (var m in pool)
            {
                if (m == null || !m.active || m.creature == null || m.creature.isKilled) continue;
                var cr = m.creature;
                if (cr.transform.position.y > killY || (cr.transform.position - map26Origin).sqrMagnitude > 700f * 700f) continue;
                m.finalKill = true;
                Scream(m, true);
                Log($"{WhoIs(m)}: over the edge - gone");
                Guard("map.fallkill", () => cr.Kill());
                Guard("map.despawn", () => cr.Despawn(6f));
            }
            if (!map26In) return;
            var pl = Player.local; var pc = pl != null ? pl.creature : null;
            if (pc == null) return;
            // just arrived: make sure you are ON the floor, not in it (tries a different way of placing you each time)
            if (now < map26SettleUntil && pc.transform.position.y < map26Origin.y + map26Start.y - 0.6f)
            {
                map26SettleTry++;
                Vector3 to = map26Origin + map26Start + Vector3.up * (0.3f + 0.3f * map26SettleTry);
                Debug.Log($"[RageEngine] map: you sank into the floor ({pc.transform.position.y - map26Origin.y:0.00}) - placing you again, way #{map26SettleTry}");
                if (map26SettleTry % 2 == 1) Guard("settle.tp", () => pl.Teleport(to, Quaternion.Euler(0f, map26Yaw, 0f), false, false));
                else Guard("settle.move", () => { var lt = pl.locomotion != null ? pl.locomotion.transform : pl.transform; lt.position += to - pc.transform.position; var rb = lt.GetComponent<Rigidbody>(); if (rb != null) rb.velocity = Vector3.zero; Physics.SyncTransforms(); });
                return;
            }
            if (pc.transform.position.y < map26Origin.y + Mathf.Min(-6f, map26KillY))
            {
                map26Rescues++;
                Debug.Log($"[RageEngine] map: you fell off ({pc.transform.position}) - back to the start (#{map26Rescues})");
                pl.Teleport(map26Origin + map26Start + Vector3.up * 0.3f, Quaternion.Euler(0f, map26Yaw, 0f), false, true);
            }
        }

        // ------------------------------------------------------------------ building
        sealed class MeshAcc { public List<Vector3> v = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<int> t = new List<int>(); }

        static void AddBox(MeshAcc a, Vector3 mn, Vector3 mx, float uvs) => AddBox(a, a, a, mn, mx, uvs);
        /// Faces go to three lists (up/down, facing Z, facing X) so the unlit look can shade them differently.
        static void AddBox(MeshAcc aTop, MeshAcc aZ, MeshAcc aX, Vector3 mn, Vector3 mx, float uvs)
        {
            float x0 = mn.x, y0 = mn.y, z0 = mn.z, x1 = mx.x, y1 = mx.y, z1 = mx.z;
            MeshAcc a = aTop;
            void Q(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float w, float h)
            {
                int o = a.v.Count;
                a.v.Add(p0); a.v.Add(p1); a.v.Add(p2); a.v.Add(p3);
                float u = uvs > 0f ? w * uvs : 1f, vv = uvs > 0f ? h * uvs : 1f;
                a.uv.Add(new Vector2(0, 0)); a.uv.Add(new Vector2(u, 0)); a.uv.Add(new Vector2(u, vv)); a.uv.Add(new Vector2(0, vv));
                a.t.Add(o); a.t.Add(o + 2); a.t.Add(o + 1); a.t.Add(o); a.t.Add(o + 3); a.t.Add(o + 2);
            }
            Q(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), x1 - x0, z1 - z0);   // top
            a = aZ;
            Q(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), x1 - x0, z1 - z0);   // bottom
            Q(new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), x1 - x0, y1 - y0);   // +z
            Q(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), x1 - x0, y1 - y0);   // -z
            a = aX;
            Q(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), z1 - z0, y1 - y0);   // -x
            Q(new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), z1 - z0, y1 - y0);   // +x
        }

        static Mesh Cube26()
        {
            if (cube26 != null) return cube26;
            var a = new MeshAcc(); AddBox(a, Vector3.one * -0.5f, Vector3.one * 0.5f, 0f);
            cube26 = new Mesh { name = "RageEngine_Cube" };
            cube26.SetVertices(a.v); cube26.SetUVs(0, a.uv); cube26.SetTriangles(a.t, 0); cube26.RecalculateNormals(); cube26.RecalculateBounds();
            return cube26;
        }

        static GameObject Block(Transform parent, string name, Vector3 centre, Vector3 size, string mat, bool collider, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre; go.transform.localScale = size;
            if (mat != null && mat != "none")
            {
                go.AddComponent<MeshFilter>().sharedMesh = Cube26();
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = MapMat26(mat, 1); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        bool BuildMap26(string name, Vector3 playerPos)
        {
            string path = Path.Combine(modFolder ?? "", "Maps", name + ".txt");
            if (!File.Exists(path)) { Debug.Log("[RageEngine] map: file missing - " + path); return false; }
            if (map26Root != null) Object.Destroy(map26Root);
            foreach (var n in map26Nav) Guard("nav.remove", () => n.Remove());
            map26Nav.Clear(); map26Spawns.Clear();
            int def = GroundLayer26(out string layerWhy);
            ResetEnv28(); ResetLounge28(); ResetDonor26();
            mapMats26.Clear(); texOk26 = texMiss26 = 0; map26KillY = -20f; map26LookBuilt = mapLook;
            var items26 = new List<KeyValuePair<string, Vector3>>();
            map26Origin = new Vector3(Mathf.Round(playerPos.x), Mathf.Round(playerPos.y) + 300f, Mathf.Round(playerPos.z));
            var root = new GameObject("RageEngine_Map_" + name);
            root.transform.position = map26Origin;
            var colRoot = new GameObject("Colliders").transform; colRoot.SetParent(root.transform, false);
            var acc = new Dictionary<string, MeshAcc>();
            var nav = new List<NavMeshBuildSource>();
            Bounds bounds = new Bounds(); bool anyB = false;
            int boxes = 0, glass = 0, doors = 0, props = 0, lights = 0;
            map26Start = Vector3.up * 0.2f; map26Yaw = 0f;
            foreach (var raw in File.ReadAllLines(path))
            {
                if (raw.Length < 3 || raw[0] == '#') continue;
                var p = raw.Split(' ');
                try
                {
                    switch (p[0])
                    {
                        case "B":
                        {
                            string mat = p[1];
                            Vector3 mn = new Vector3(PF(p[2]), PF(p[3]), PF(p[4])), mx = new Vector3(PF(p[5]), PF(p[6]), PF(p[7]));
                            float uvs = PF(p[8]); bool noCol = p.Length > 9 && p[9].Contains("n");
                            if (mat != "none")
                            {
                                var a3 = new MeshAcc[3];
                                for (int q = 0; q < 3; q++) { string ak = mat + "|" + q; if (!acc.TryGetValue(ak, out a3[q])) acc[ak] = a3[q] = new MeshAcc(); }
                                AddBox(a3[0], a3[1], a3[2], mn, mx, uvs);
                            }
                            if (!noCol)
                            {
                                var go = new GameObject("c"); go.layer = def; go.transform.SetParent(colRoot, false);
                                go.transform.localPosition = (mn + mx) * 0.5f;
                                go.AddComponent<BoxCollider>().size = mx - mn;
                                nav.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = mx - mn, transform = Matrix4x4.TRS(map26Origin + (mn + mx) * 0.5f, Quaternion.identity, Vector3.one), area = 0 });
                                if (!anyB) { bounds = new Bounds(map26Origin + (mn + mx) * 0.5f, mx - mn); anyB = true; } else { bounds.Encapsulate(map26Origin + mn); bounds.Encapsulate(map26Origin + mx); }
                            }
                            boxes++;
                            break;
                        }
                        case "R":
                        {
                            string mat = p[1];
                            Vector3 c = new Vector3(PF(p[2]), PF(p[3]), PF(p[4])), size = new Vector3(PF(p[5]), PF(p[6]), PF(p[7]));
                            var rot = Quaternion.Euler(PF(p[8]), PF(p[9]), PF(p[10])); bool noCol = p.Length > 11 && p[11].Contains("n");
                            var go = Block(root.transform, "ramp", c, size, mat, !noCol, def);
                            go.transform.localRotation = rot;
                            if (!noCol) nav.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = size, transform = Matrix4x4.TRS(map26Origin + c, rot, Vector3.one), area = 0 });
                            boxes++;
                            break;
                        }
                        case "G":
                        {
                            Vector3 mn = new Vector3(PF(p[1]), PF(p[2]), PF(p[3])), mx = new Vector3(PF(p[4]), PF(p[5]), PF(p[6]));
                            var go = Block(root.transform, "Glass", (mn + mx) * 0.5f, mx - mn, "glass", true, def);
                            var gp = go.AddComponent<GlassPane26>(); gp.hp = PF(p[7]); gp.mat = MapMat26("glass", 0); if (mapLook != "1") { var gr = go.GetComponent<MeshRenderer>(); if (gr != null) gr.enabled = false; }   // 0.28.2: no see-through shader on Quest -> clear opening, still breakable
                            glass++;
                            break;
                        }
                        case "H":
                        {
                            string mat = p[1];
                            Vector3 mn = new Vector3(PF(p[2]), PF(p[3]), PF(p[4])), mx = new Vector3(PF(p[5]), PF(p[6]), PF(p[7]));
                            Vector3 hinge = new Vector3(PF(p[8]), (mn.y + mx.y) * 0.5f, PF(p[9]));
                            var door = new GameObject("Door"); door.layer = def;
                            door.transform.SetParent(root.transform, false); door.transform.localPosition = hinge;
                            Block(door.transform, "Leaf", (mn + mx) * 0.5f - hinge, mx - mn, mat, true, def);
                            var rb = door.AddComponent<Rigidbody>(); rb.mass = 10f; rb.angularDrag = 1.5f; rb.drag = 0.2f;
                            var hj = door.AddComponent<HingeJoint>(); hj.axis = Vector3.up; hj.anchor = Vector3.zero;
                            hj.useLimits = true; hj.limits = new JointLimits { min = -105f, max = 105f };
                            hj.useSpring = true; hj.spring = new JointSpring { spring = 6f, damper = 2f, targetPosition = 0f };
                            doors++;
                            break;
                        }
                        case "P": Guard("map.prop", () => MakeProp(root.transform, p[1], new Vector3(PF(p[2]), PF(p[3]), PF(p[4])), PF(p[5]), def)); props++; break;
                        case "L":
                        {
                            var go = new GameObject("Light"); go.transform.SetParent(root.transform, false); go.transform.localPosition = new Vector3(PF(p[1]), PF(p[2]), PF(p[3]));
                            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(PF(p[4]), PF(p[5]), PF(p[6])); l.range = PF(p[7]); l.intensity = PF(p[8]); l.shadows = LightShadows.None;
                            lights++;
                            break;
                        }
                        case "K": map26KillY = PF(p[1]); break;
                        case "SKY": case "FOG": case "AMB": case "CLOUD": case "C": ParseEnv28(p); break;
                        case "MUSIC": case "AMBIENT": case "E": case "DRESS": ParseLounge28(p); break;
                        case "I": items26.Add(new KeyValuePair<string, Vector3>(p[1], new Vector3(PF(p[2]), PF(p[3]), PF(p[4])))); break;
                        case "S": map26Spawns.Add(new Vector3(PF(p[1]), PF(p[2]), PF(p[3]))); break;
                        case "T": map26Start = new Vector3(PF(p[1]), PF(p[2]) + 0.15f, PF(p[3])); map26Yaw = PF(p[4]); break;
                    }
                }
                catch (System.Exception e) { Debug.Log($"[RageEngine] map: bad line '{raw}': {e.Message}"); }
            }
            // one mesh per material
            foreach (var kv in acc)
            {
                if (kv.Value.v.Count == 0) continue;
                string mk = kv.Key.Substring(0, kv.Key.LastIndexOf('|')); int shade = kv.Key[kv.Key.Length - 1] - '0';
                var go = new GameObject("Mesh_" + kv.Key); go.layer = def; go.transform.SetParent(root.transform, false);
                var m = new Mesh { name = "RageEngine_Map_" + kv.Key };
                if (kv.Value.v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(kv.Value.v); m.SetUVs(0, kv.Value.uv); m.SetTriangles(kv.Value.t, 0); m.RecalculateNormals(); m.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = MapMat26(mk, shade); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // NavMesh for every agent type the game knows
            int navOk = 0;
            if (anyB)
            {
                bounds.Expand(new Vector3(2f, 6f, 2f));
                int count = 1; try { count = Mathf.Max(1, NavMesh.GetSettingsCount()); } catch { }
                for (int i = 0; i < count; i++)
                {
                    int idx = i;
                    Guard("map.nav", () =>
                    {
                        var st = NavMesh.GetSettingsByIndex(idx);
                        var data = NavMeshBuilder.BuildNavMeshData(st, nav, bounds, Vector3.zero, Quaternion.identity);
                        if (data == null) return;
                        var inst = NavMesh.AddNavMeshData(data);
                        if (inst.valid) { map26Nav.Add(inst); navOk++; }
                    });
                }
            }
            Guard("map.env", () => BuildEnv28(root.transform));
            Guard("map.lounge", () => BuildLounge28(root.transform));
            map26Root = root; map26Built = name;
            Physics.SyncTransforms();
            // weapons lying around the place
            foreach (var it in items26)
            {
                var itv = it;
                Guard("map.item", () => { if (System.Enum.TryParse<ToolKind>(itv.Key, out var tk)) SpawnTool(tk, map26Origin + itv.Value, null); });
            }
            string report = $"map '{name}' v{Version}: look {mapLook}, surface material {donorWhy26}, textures loaded {texOk26}, missing {texMiss26}, floor layer {def} ({LayerMask.LayerToName(def)}; {layerWhy}), navmesh agent types {navOk}, kill height {map26KillY}, sky {env28SkyHow}, cloud layers {env28Clouds.Count}";
            Debug.Log("[RageEngine] " + report);
            Notify($"Rage Engine map: {name}\nSurfaces: {(donor26 != null ? "level material OK" : mapLook == "2" ? "flat colours" : "game shader")}  -  textures {texOk26}/{texOk26 + texMiss26}\nIf it looks wrong: Map look setting -> Leave -> Go there");
            try { File.WriteAllText(Path.Combine(modFolder ?? "", "RageEngine_map_report.txt"), report + "\n"); } catch { }
            Debug.Log($"[RageEngine] map '{name}' built at {map26Origin}: {boxes} boxes, {acc.Count} materials, {glass} glass panes, {doors} doors, {props} props, {lights} lights, {map26Spawns.Count} spawn points, navmesh for {navOk} agent type(s)");
            return true;
        }

        /// The layer the floor must be on: one your locomotion treats as ground AND physically collides with.
        static int GroundLayer26(out string why)
        {
            int def = LayerMask.NameToLayer("Default"); if (def < 0) def = 0;
            why = "default";
            try
            {
                var loco = Player.local != null ? Player.local.locomotion : null;
                if (loco == null) { why = "no player locomotion - Default"; return def; }
                int mask = loco.groundMask.value;
                int capLayer = loco.capsuleCollider != null ? loco.capsuleCollider.gameObject.layer : loco.gameObject.layer;
                bool Ok(int l) => (mask & (1 << l)) != 0 && !Physics.GetIgnoreLayerCollision(capLayer, l);
                if (Ok(def)) { why = $"Default is ground (mask {mask}, capsule layer {capLayer})"; return def; }
                for (int l = 0; l < 32; l++) if (Ok(l)) { why = $"Default is NOT ground for your capsule (mask {mask}, capsule layer {capLayer}) - using layer {l}"; return l; }
                for (int l = 0; l < 32; l++) if (!Physics.GetIgnoreLayerCollision(capLayer, l) && l != capLayer) { why = $"nothing in the ground mask collides (mask {mask}) - using colliding layer {l}"; return l; }
                why = $"no usable layer found (mask {mask}, capsule layer {capLayer})";
            }
            catch (System.Exception e) { why = "layer check failed: " + e.Message; }
            return def;
        }

        /// Map materials. Auto = an unlit textured shader when the game has one (textures always show, no lighting needed;
        /// faces are shaded by direction instead), otherwise the game's lit shader.
        static Material donor26; static bool donorTried26; static string donorWhy26 = "not looked";
        static readonly string[] donorBad26 = { "transparent", "water", "sky", "particle", "decal", "unlit", "glass", "foliage", "leaf", "leaves", "hair", "ui", "text", "fx", "fade", "cloud", "fog", "blood", "fire", "cutout", "grass" };

        /// The biggest opaque, textured renderer in the loaded level (not ours, not a creature or item).
        internal static Material Donor26()
        {
            if (donorTried26) return donor26;
            donorTried26 = true;
            float best = 0f;
            try
            {
                foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (map26Root != null && r.transform.IsChildOf(map26Root.transform)) continue;
                    if (r.GetComponentInParent<Creature>() != null || r.GetComponentInParent<Item>() != null) continue;
                    var mat = r.sharedMaterial;
                    if (mat == null || mat.shader == null || mat.mainTexture == null || mat.renderQueue > 2450) continue;
                    string sn = (mat.shader.name + " " + mat.name).ToLowerInvariant();
                    bool bad = false; foreach (var b in donorBad26) if (sn.Contains(b)) { bad = true; break; }
                    if (bad) continue;
                    var e = r.bounds.size; float score = e.x * e.z + e.y * Mathf.Max(e.x, e.z);
                    if (sn.Contains("lit")) score *= 2f;
                    if (score > best) { best = score; donor26 = mat; }
                }
            }
            catch (System.Exception ex) { donorWhy26 = "error " + ex.Message; }
            donorWhy26 = donor26 != null ? $"{donor26.name} ({donor26.shader.name})" : "none found";
            return donor26;
        }

        internal static void ResetDonor26() { donor26 = null; donorTried26 = false; }

        internal static Material DonorCopy26(Material donor, Texture2D tex, Color baseCol, float sh, Color tn)
        {
            var m = new Material(donor) { name = "RageEngine_Map" };
            if (tex != null)
            {
                m.mainTexture = tex;
                foreach (var t in new[] { "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo" }) if (m.HasProperty(t)) m.SetTexture(t, tex);
            }
            m.mainTextureScale = Vector2.one; m.mainTextureOffset = Vector2.zero;
            foreach (var t in new[] { "_BaseMap", "_MainTex" }) if (m.HasProperty(t)) { m.SetTextureScale(t, Vector2.one); m.SetTextureOffset(t, Vector2.zero); }
            Color c = new Color(baseCol.r * sh * tn.r, baseCol.g * sh * tn.g, baseCol.b * sh * tn.b, 1f);
            m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.2f);
            if (m.HasProperty("_DetailAlbedoMap")) m.SetTexture("_DetailAlbedoMap", null);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            return m;
        }

        static Material MapMat26(string key, int shade)
        {
            key = (key ?? "").ToLowerInvariant();
            string ck = key + "|" + shade;
            if (mapMats26.TryGetValue(ck, out var have) && have != null) return have;
            bool glass = key == "glass";
            float sh = shade == 0 ? 1f : shade == 1 ? 0.8f : 0.64f;
            Color tn = Tint28(key);
            Material m = null;
            if (mapLook == "2") m = Mats.Make(Guess(key) * sh * tn, 0.2f);
            else if (mapLook != "1")
            {
                // 0.28.2: Auto clones a material the loaded level is already drawing with, so the shader
                // variant is guaranteed to exist on the Quest (URP Unlit was stripped -> invisible map).
                var donor = Donor26();
                if (donor != null)
                {
                    var tex = GoreTex.MeshTex("Map/" + key + ".png");
                    if (tex != null) texOk26++; else texMiss26++;
                    m = DonorCopy26(donor, tex, tex != null ? Color.white : Guess(key), sh, tn);
                    if (glass) { m.color = new Color(0.55f, 0.7f, 0.8f, 1f); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.color); }
                }
            }
            if (m == null)
            {
                var tex = GoreTex.MeshTex("Map/" + key + ".png");
                if (tex != null) texOk26++; else texMiss26++;
                m = tex != null ? Mats.MakeTextured(tex, null, glass ? new Color(0.75f, 0.88f, 0.95f, 0.35f) : tn, glass ? 0.95f : 0.25f, glass) : Mats.Make(Guess(key), 0.2f);
            }
            return mapMats26[ck] = m;
        }

        static void MakeProp(Transform root, string kind, Vector3 pos, float yaw, int layer)
        {
            var go = new GameObject("Prop_" + kind); go.layer = layer;
            go.transform.SetParent(root, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float mass = 5f; string sound = null;
            switch (kind)
            {
                case "chair":
                    Block(go.transform, "Seat", new Vector3(0, 0.465f, 0), new Vector3(0.42f, 0.05f, 0.42f), "chair_red", true, layer);
                    Block(go.transform, "Back", new Vector3(0, 0.72f, 0.2f), new Vector3(0.42f, 0.46f, 0.04f), "chair_red", true, layer);
                    foreach (var l in new[] { new Vector2(-0.18f, -0.18f), new Vector2(0.18f, -0.18f), new Vector2(-0.18f, 0.18f), new Vector2(0.18f, 0.18f) })
                        Block(go.transform, "Leg", new Vector3(l.x, 0.22f, l.y), new Vector3(0.03f, 0.44f, 0.03f), "steel", true, layer);
                    mass = 5f; break;
                case "table":
                    Block(go.transform, "Top", new Vector3(0, 0.755f, 0), new Vector3(0.9f, 0.03f, 0.9f), "table_top", true, layer);
                    Block(go.transform, "Pole", new Vector3(0, 0.37f, 0), new Vector3(0.08f, 0.74f, 0.08f), "steel", true, layer);
                    Block(go.transform, "Foot", new Vector3(0, 0.015f, 0), new Vector3(0.55f, 0.03f, 0.55f), "steel", true, layer);
                    mass = 18f; break;
                case "trash":
                    Block(go.transform, "Can", new Vector3(0, 0.425f, 0), new Vector3(0.48f, 0.85f, 0.48f), "trash_gray", true, layer);
                    Block(go.transform, "Rim", new Vector3(0, 0.86f, 0), new Vector3(0.52f, 0.03f, 0.52f), "steel", false, layer);
                    mass = 6f; sound = "NRE_MetalBang"; break;
                case "stool":
                    Block(go.transform, "Seat", new Vector3(0, 0.76f, 0), new Vector3(0.36f, 0.06f, 0.36f), "leather", true, layer);
                    Block(go.transform, "Pole", new Vector3(0, 0.37f, 0), new Vector3(0.06f, 0.74f, 0.06f), "steel", true, layer);
                    Block(go.transform, "Foot", new Vector3(0, 0.015f, 0), new Vector3(0.4f, 0.03f, 0.4f), "steel", true, layer);
                    mass = 7f; sound = "NRE_MetalBang"; break;
                case "crate":
                    Block(go.transform, "Box", new Vector3(0, 0.2f, 0), new Vector3(0.5f, 0.4f, 0.5f), "cardboard", true, layer);
                    mass = 2.5f; break;
                default:
                    Block(go.transform, "Cone", new Vector3(0, 0.25f, 0), new Vector3(0.22f, 0.5f, 0.22f), "curb_yellow", true, layer);
                    Block(go.transform, "Base", new Vector3(0, 0.015f, 0), new Vector3(0.36f, 0.03f, 0.36f), "curb_yellow", true, layer);
                    mass = 1.5f; break;
            }
            var rb = go.AddComponent<Rigidbody>(); rb.mass = mass; rb.drag = 0.2f; rb.angularDrag = 0.5f;
            rb.isKinematic = true;                                                     // 0.28.3: stays put until something actually hits it
            go.AddComponent<PropWake28>();
            if (sound != null) go.AddComponent<MapProp26>().sound = sound;
        }
    }
}

// Rage Engine 0.25 - corpse avoidance. NPCs used to run straight at you through bodies lying on the floor and trip
// over them. A body that has settled gets a carving NavMeshObstacle (the same Unity component the game itself uses on
// its sliding level objects - ThunderRoad.ObjectSlider), so the navmesh opens a hole around it and NPCs path around.
// "Carve only stationary": while a body is dragged or thrown it stops carving and starts again where it comes to rest.
// Checked once a second over the game's active-creature list; no per-frame work.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using UnityEngine.AI;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        [ModOptionCategory(C0, 0), ModOptionOrder(40)]
        [ModOption("NPCs walk around bodies", "ON: bodies on the floor become obstacles, so NPCs path around them instead of running into them and tripping.", defaultValueIndex = 1)]
        public static bool corpseAvoid = true;

        sealed class CorpseBlock { public GameObject go; public NavMeshObstacle obs; public Vector3 at; }
        static readonly Dictionary<Creature, CorpseBlock> corpseBlocks = new Dictionary<Creature, CorpseBlock>();
        static readonly List<Creature> corpseGone = new List<Creature>();
        static float nextCorpseTick;
        static bool corpseBroken;

        void TickCorpses()
        {
            float now = Time.time;
            if (now < nextCorpseTick) return;
            nextCorpseTick = now + 1f;
            if (!corpseAvoid || corpseBroken) { ClearCorpses(); return; }
            var all = Creature.allActive;
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    if (c == null || !c.isKilled || c.isPlayer || c.ragdoll == null || c.ragdoll.rootPart == null || c.ragdoll.headPart == null) continue;
                    var hips = c.ragdoll.rootPart; var head = c.ragdoll.headPart;
                    bool moving = hips.physicBody != null && hips.physicBody.velocity.sqrMagnitude > 0.04f;
                    bool held = c.ragdoll.handlers != null && c.ragdoll.handlers.Count > 0;
                    corpseBlocks.TryGetValue(c, out var b);
                    if (moving || held) { if (b != null && b.obs != null) b.obs.enabled = false; continue; }
                    Vector3 mid = (hips.transform.position + head.transform.position) * 0.5f;
                    if (b != null && b.obs != null && b.obs.enabled && (mid - b.at).sqrMagnitude < 0.09f) continue;   // still where it was
                    if (b == null)
                    {
                        b = new CorpseBlock { go = new GameObject("RageEngine_CorpseBlock") };
                        try
                        {
                            b.obs = b.go.AddComponent<NavMeshObstacle>();
                            b.obs.shape = NavMeshObstacleShape.Box;
                            b.obs.carving = true;
                            b.obs.carveOnlyStationary = true;
                        }
                        catch (System.Exception e)
                        {
                            corpseBroken = true; Object.Destroy(b.go);
                            Log("corpse avoidance unavailable on this platform: " + e.Message);
                            return;
                        }
                        corpseBlocks[c] = b;
                    }
                    Vector3 along = Vector3.ProjectOnPlane(head.transform.position - hips.transform.position, Vector3.up);
                    float len = Mathf.Clamp(along.magnitude * 2.2f, 0.9f, 1.9f);
                    b.go.transform.position = new Vector3(mid.x, Mathf.Min(hips.transform.position.y, head.transform.position.y), mid.z);
                    b.go.transform.rotation = along.sqrMagnitude > 0.01f ? Quaternion.LookRotation(along.normalized, Vector3.up) : Quaternion.identity;
                    b.obs.size = new Vector3(0.6f, 0.5f, len);
                    b.obs.center = new Vector3(0f, 0.25f, 0f);
                    b.obs.enabled = true;
                    b.at = mid;
                }
            // bodies that were despawned / recycled / revived
            corpseGone.Clear();
            foreach (var kv in corpseBlocks)
                if (kv.Key == null || !kv.Key.isKilled || all == null || !all.Contains(kv.Key)) corpseGone.Add(kv.Key);
            foreach (var c in corpseGone) { var b = corpseBlocks[c]; if (b.go != null) Object.Destroy(b.go); corpseBlocks.Remove(c); }
        }

        static void ClearCorpses()
        {
            if (corpseBlocks.Count == 0) return;
            foreach (var b in corpseBlocks.Values) if (b.go != null) Object.Destroy(b.go);
            corpseBlocks.Clear();
        }
    }
}

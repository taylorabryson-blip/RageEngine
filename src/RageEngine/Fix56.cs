// Rage Engine 0.56 - fixes from the 0.55 play test.
//   VOICES       NPCs use the creature's own gender (not its name) to pick male / female voices; male-only recordings
//                (breathing, retching, gasps) are pitched up for women.
//   SENSES       "Realistic" (the new default): the game's own detection, made human - narrower view, crouching hides you
//                much better, they lose interest faster, allies take longer to pick up a target you haven't shown them,
//                and they hear less far. The mod's own noises no longer go through walls: a sound behind a wall only
//                reaches someone if there's a short walking route to it (a door), and quieter.
//   DEBUG        "Show NPC senses": a cone for how far and how wide each NPC sees, and a ring on the floor for how far he
//                hears. Green = calm, yellow = suspicious, red = he knows you're there.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using UnityEngine.AI;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        [ModOptionCategory("NPCs", 2), ModOptionOrder(51)]
        [ModOption("Show NPC senses", "Debug: a cone for how far and wide each NPC sees and a ring on the floor for how far he hears. Green = calm, yellow = suspicious, red = he knows where you are.", defaultValueIndex = 0)]
        public static bool showSenses56 = false;

        // ------------------------------------------------------------------ two copies installed
        /// Two Rage Engine folders in Mods (an old one left behind) run twice: double health bars, double senses,
        /// double gore - and they fight each other. The older copy switches itself off and says so.
        static void CheckDuplicates56()
        {
            if (ModManager.loadedMods == null) return;
            var mine = myModData; var copies = new List<ModManager.ModData>();
            foreach (var m in ModManager.loadedMods)
            {
                if (m == null) continue;
                bool re = m.Name == "Rage Engine" || (m.folderName ?? "").IndexOf("RageEngine", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!re && m.assemblyPaths != null) foreach (var a in m.assemblyPaths) if (a != null && a.EndsWith("RageEngine.dll", System.StringComparison.OrdinalIgnoreCase)) { re = true; break; }
                if (re) copies.Add(m);
            }
            if (copies.Count < 2) return;
            var names = new List<string>(); foreach (var m in copies) names.Add($"{m.folderName} ({m.ModVersion})");
            Debug.Log("[RageEngine] TWO COPIES INSTALLED: " + string.Join(", ", names));
            ModManager.ModData newest = null;
            foreach (var m in copies) if (newest == null || string.CompareOrdinal(m.ModVersion ?? "", newest.ModVersion ?? "") > 0) newest = m;
            if (mine != null && newest != null && !ReferenceEquals(mine, newest) && (mine.fullPath ?? "") != (newest.fullPath ?? ""))
            {
                modEnabled = false;
                Notify($"Two Rage Engine folders are installed:\n{string.Join("\n", names)}\nThe old one ({mine.folderName}) has switched itself off - delete it from Mods.");
            }
        }

        // ------------------------------------------------------------------ voices
        internal static bool IsFemale56(Creature c)
        {
            if (c == null) return false;
            bool f = false;
            Guard("gender", () => { if (c.data != null) f = c.data.gender == CreatureData.Gender.Female || (c.data.id != null && c.data.id.IndexOf("Female", System.StringComparison.OrdinalIgnoreCase) >= 0); });
            return f || c.name.IndexOf("Female", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
        /// Male-only recordings played on a woman: up about three semitones.
        internal static float FemaleLift56(Creature c) => IsFemale56(c) ? 1.2f : 1f;

        // ------------------------------------------------------------------ sound through walls
        static NavMeshPath path56;
        static readonly RaycastHit[] wallHits56 = new RaycastHit[8];
        /// Solid level geometry (not bodies, not loose items) between the two points?
        static bool Walled56(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; float len = d.magnitude; if (len < 0.5f) return false;
            int n = Physics.RaycastNonAlloc(a, d / len, wallHits56, len - 0.3f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = wallHits56[i].collider;
                if (col == null || col.attachedRigidbody != null || col.GetComponentInParent<Creature>() != null) continue;
                return true;
            }
            return false;
        }
        /// How far a sound at 'from' reaches someone at 'to': the full radius in the open; behind a wall only along a
        /// short walking route (through a door), and quieter. Returns the effective distance, or -1 if he can't hear it.
        static float HeardDistance56(Vector3 from, Vector3 to, Vector3 feet, float radius)
        {
            float d = Vector3.Distance(from, to);
            if (d > radius) return -1f;
            if (!Walled56(from, to)) return d;
            if (path56 == null) path56 = new NavMeshPath();
            float walk = -1f;
            Guard("sound.path", () =>
            {
                if (!NavMesh.SamplePosition(from, out var a, 2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(feet, out var b, 2f, NavMesh.AllAreas)) return;
                if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path56) || path56.status != NavMeshPathStatus.PathComplete) return;
                var c = path56.corners; float L = 0f; for (int i = 1; i < c.Length; i++) L += Vector3.Distance(c[i - 1], c[i]);
                walk = L;
            });
            if (walk < 0f) return -1f;
            float eff = walk * 1.6f;                                                                 // muffled by the wall it went round
            return eff <= radius * 0.8f ? eff : -1f;
        }

        /// The game has him on its own patrol route (so our rounds leave him alone).
        static bool GamePatrol56(NpcState s) { int st = -1; Guard("patrol56", () => st = (int)s.creature.brain.state); return st == (int)Brain.State.Patrol; }

        // ------------------------------------------------------------------ the game's detection, made human
        /// Called from Senses54 for "Realistic". o = saved originals (Combat54).
        static void RealSenses56(BrainModuleDetection d, float[] o)
        {
            d.sightDetectionHorizontalFov = Mathf.Min(o[0], 120f);
            d.sightDetectionVerticalFov = o[1] * 0.9f;
            d.hearMaxDistance = o[2] * 0.75f;
            if (o.Length >= 8)
            {
                d.crouchingMultiplier = o[4] * 0.45f;                                                 // crouching really hides you
                d.enemyInFOVIncrease = o[5] * 0.75f;                                                  // a glimpse isn't instant
                d.alertednessLossPerSec = o[6] * 1.6f;                                                // they calm down when you're gone
                d.acquireSharedTargetDelay = Mathf.Max(o[7] * 2f, 2.5f);                              // allies don't all know at once
            }
        }

        // ------------------------------------------------------------------ debug: sight cone + hearing ring
        sealed class SenseViz { public LineRenderer cone, ring; public int lvl = -1; }
        static readonly Dictionary<NpcState, SenseViz> viz56 = new Dictionary<NpcState, SenseViz>();
        static Material[] vizMats56;

        static LineRenderer VizLine56(string name)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>(); lr.useWorldSpace = true; lr.widthMultiplier = 0.02f; lr.loop = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
            return lr;
        }

        void TickSenses56()
        {
            if (!showSenses56)
            {
                if (viz56.Count > 0) { foreach (var v in viz56.Values) { if (v.cone != null) Object.Destroy(v.cone.gameObject); if (v.ring != null) Object.Destroy(v.ring.gameObject); } viz56.Clear(); }
                return;
            }
            if (vizMats56 == null) vizMats56 = new[] { Mats.Make(new Color(0.2f, 0.9f, 0.3f), 0.2f), Mats.Make(new Color(1f, 0.85f, 0.1f), 0.2f), Mats.Make(new Color(1f, 0.15f, 0.1f), 0.2f) };
            var seen = new HashSet<NpcState>();
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active || s.creature == null || s.creature.isKilled || s.det54 == null) continue;
                var c = s.creature; var head = c.ragdoll != null ? c.ragdoll.headPart : null; if (head == null) continue;
                seen.Add(s);
                if (!viz56.TryGetValue(s, out var v)) { v = new SenseViz { cone = VizLine56("RageEngine_SightCone"), ring = VizLine56("RageEngine_HearRing") }; v.ring.loop = true; viz56[s] = v; }
                var d = s.det54;
                float fov = 90f, hear = 10f, range = 20f, alert = 0f;
                Guard("viz.read", () =>
                {
                    fov = d.sightDetectionHorizontalFov; hear = d.hearMaxDistance;
                    alert = d.maxAlertednessValue > 0.01f ? d.alertednessLevel / d.maxAlertednessValue : 0f;
                    var cv = d.distanceMultCurve;
                    if (cv != null && cv.length > 0) { range = cv.keys[cv.length - 1].time; for (float x = 0f; x < range; x += 0.5f) if (cv.Evaluate(x) <= 0.05f) { range = x; break; } }
                });
                int st = -1; Guard("viz.state", () => st = (int)c.brain.state);
                int lvl = st == (int)Brain.State.Combat || st == (int)Brain.State.Alert || alert > 0.66f ? 2 : st == (int)Brain.State.Investigate || alert > 0.2f ? 1 : 0;
                if (lvl != v.lvl) { v.lvl = lvl; v.cone.sharedMaterial = vizMats56[lvl]; v.ring.sharedMaterial = vizMats56[lvl]; }
                range = Mathf.Clamp(range, 2f, 60f); hear = Mathf.Clamp(hear, 0.5f, 80f);
                // sight cone, flat at eye height
                Vector3 eye = head.transform.position;
                Vector3 fwd = Vector3.ProjectOnPlane(c.centerEyes != null ? c.centerEyes.forward : c.transform.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 0.5f) fwd = c.transform.forward;
                const int arc = 14;
                v.cone.positionCount = arc + 3;
                v.cone.SetPosition(0, eye);
                for (int k = 0; k <= arc; k++) { float a = -fov * 0.5f + fov * k / arc; v.cone.SetPosition(k + 1, eye + Quaternion.AngleAxis(a, Vector3.up) * fwd * range); }
                v.cone.SetPosition(arc + 2, eye);
                // hearing ring on the floor
                const int seg = 32;
                Vector3 feet = c.transform.position + Vector3.up * 0.05f;
                v.ring.positionCount = seg;
                for (int k = 0; k < seg; k++) { float a = Mathf.PI * 2f * k / seg; v.ring.SetPosition(k, feet + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * hear); }
            }
            List<NpcState> gone = null;
            foreach (var kv in viz56) if (!seen.Contains(kv.Key)) (gone ??= new List<NpcState>()).Add(kv.Key);
            if (gone != null) foreach (var g in gone) { var v = viz56[g]; if (v.cone != null) Object.Destroy(v.cone.gameObject); if (v.ring != null) Object.Destroy(v.ring.gameObject); viz56.Remove(g); }
        }
    }
}

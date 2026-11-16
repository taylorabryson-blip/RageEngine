// Rage Engine 0.29 - the maps are real levels now: Skybar, Rage Burger and Red Room Diner show up in the game's own
// level selection (map board / Sandbox level list) under "Rage Engine". The level loads the Arena scene and this module
// builds the Rage Engine map on top and puts you in it. Waves come automatically (Mod Options > Maps > Auto waves).
using System.Collections;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public class LevelModuleRageMap : LevelModule
    {
        public string map = "Skybar";
        public bool waves = true;

        public override IEnumerator OnLoadCoroutine()
        {
            RageEngineScript.RequestLevelMap29(map, waves);
            yield break;
        }

        public override void OnUnload() => RageEngineScript.EndLevelMap29();
    }

    public sealed partial class RageEngineScript
    {
        [ModOptionCategory(CM, 12), ModOptionOrder(1)]
        [ModOption("Auto waves", "ON: in the Rage Engine levels a new wave comes a few seconds after you clear the last one.", defaultValueIndex = 1)]
        public static bool autoWaves = true;

        static string levelMap29; static bool levelWaves29; static float levelAt29 = -1f, nextAutoWave29;
        static bool levelBuilt29;

        internal static void RequestLevelMap29(string map, bool waves)
        {
            levelMap29 = map; levelWaves29 = waves; levelAt29 = -1f; levelBuilt29 = false;
            Debug.Log($"[RageEngine] level: {map} requested");
        }
        internal static void EndLevelMap29() { levelMap29 = null; levelBuilt29 = false; }

        void TickLevel29()
        {
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
                nextAutoWave29 = now + 8f;
                return;
            }
            if (!levelWaves29 || !autoWaves || !map26In) return;
            if (wave29.Count > 0) { nextAutoWave29 = now + 6f; return; }
            if (now < nextAutoWave29) return;
            nextAutoWave29 = now + 6f;
            MapWave();
        }
    }
}

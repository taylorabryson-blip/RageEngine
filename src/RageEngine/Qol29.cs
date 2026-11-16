// Rage Engine 0.29 - fewer settings, "Stay on feet", and quality-of-life.
//   STAY ON FEET    one slider for how hard NPCs are to knock over (1 = default).
//   STYLE           one-pick presets: Arcade / Realistic / Tough guys (Custom leaves your settings alone).
//   INJURIES CAN KILL  one switch for shock, bleeding out, knockouts and toxins.
//   QUICK ACTIONS   heal me, remove all enemies, clear bodies, clean up blood & gore, unstuck, repeat last wave,
//                   spawn a mixed wave here.
//   BODIES FADE     dead NPCs clean themselves up after the time you pick.
//   WAVE CLEARED    a message in the headset when the last enemy of a wave goes down.
//   WELCOME         a short note when the mod has loaded, so you know it's on.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        [ModOptionCategory("Rage", 0), ModOptionOrder(5)]
        [ModOption("Stay on feet", "How hard NPCs are to knock over. 1 = default, higher = they stay standing more, lower = they drop easier.", defaultValueIndex = 3)]
        [ModOptionSlider, ModOptionFloatValues(0.25f, 3f, 0.25f)]
        public static float stayOnFeet = 1f;

        public static ModOptionString[] styleValues = { new ModOptionString("Custom", null, "custom"), new ModOptionString("Arcade", null, "arcade"), new ModOptionString("Realistic", null, "real"), new ModOptionString("Tough guys", null, "tough") };
        public static string style = "custom";

        public static bool injuriesKill = true;

        public static ModOptionString[] fadeValues = { new ModOptionString("Never", null, "0"), new ModOptionString("After 1 minute", null, "60"), new ModOptionString("After 3 minutes", null, "180"), new ModOptionString("After 5 minutes", null, "300") };
        public static string bodiesFade = "180";

        public static ModOptionString[] quickValues = {
            new ModOptionString("Heal me", null, "heal"), new ModOptionString("Remove all enemies", null, "clear"),
            new ModOptionString("Clear dead bodies", null, "bodies"), new ModOptionString("Clean up blood & gore", null, "gore"),
            new ModOptionString("Unstuck (back to map start)", null, "unstuck") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(20)]
        [ModOption("Quick action", "Pick one, then press Do it.", valueSourceName = nameof(quickValues), defaultValueIndex = 0)]
        public static string quickAction = "heal";
        public static ModOptionString[] doIt = { new ModOptionString("Do it", null, "go") };
        [ModOptionButton, ModOptionCategory("Rage", 0), ModOptionOrder(21)]
        [ModOption("Do it", "Runs the quick action above.", valueSourceName = nameof(doIt), saveValue = false)]
        public static void QuickButton(string _) => Guard("quick", () => instance?.Quick29(quickAction));

        internal static float Tough33(NpcState s) => Mathf.Max(0.25f, stayOnFeet) * (s != null ? Mathf.Max(0.6f, s.hpT) : 1f);

        static string lastWave29;            // "map", "mixed", "soldiers"
        static string styleSeen29;
        static bool welcomed29;
        static float nextQol29, loadedAt29 = -1f;
        static readonly Dictionary<Creature, float> deadSince29 = new Dictionary<Creature, float>();
        static readonly List<Creature> wave29 = new List<Creature>();

        internal static void NoteWave29(string kind) { lastWave29 = kind; wave29.Clear(); }
        internal static void NoteEnemy29(Creature c) { if (c != null && !wave29.Contains(c)) wave29.Add(c); }

        void Quick29(string a)
        {
            var pc = Player.local != null ? Player.local.creature : null;
            switch (a)
            {
                case "heal":
                    if (pc == null) return;
                    pc.Heal(pc.maxHealth); Notify("Healed"); break;
                case "clear":
                {
                    int n = 0;
                    foreach (var c in new List<Creature>(Creature.allActive)) if (c != null && !c.isPlayer) { Guard("q.despawn", () => c.Despawn(0f)); n++; }
                    wave29.Clear(); Notify($"Removed {n} NPC(s)"); break;
                }
                case "bodies":
                {
                    int n = 0;
                    foreach (var c in new List<Creature>(Creature.allActive)) if (c != null && !c.isPlayer && c.isKilled) { Guard("q.body", () => c.Despawn(0f)); n++; }
                    Notify($"Cleared {n} bod{(n == 1 ? "y" : "ies")}"); break;
                }
                case "gore":
                {
                    int n = chunks.Count + bloodDecals.Count + goreModels.Count;
                    foreach (var g in chunks) if (g != null) Object.Destroy(g);
                    foreach (var g in bloodDecals) if (g != null) Object.Destroy(g);
                    foreach (var g in goreModels) if (g != null) Object.Destroy(g);
                    chunks.Clear(); bloodDecals.Clear(); goreModels.Clear();
                    Notify($"Cleaned up {n} bits of blood and gore"); break;
                }
                case "unstuck":
                    if (!map26In || Player.local == null) { Notify("Unstuck only works inside a Rage Engine map."); return; }
                    Player.local.Teleport(map26Origin + map26Start + Vector3.up * 0.3f, Quaternion.Euler(0f, map26Yaw, 0f), false, true);
                    break;
                case "again":
                    if (lastWave29 == "map") MapWave();
                    else if (lastWave29 == "mixed") SpawnMixedWave();
                    else if (lastWave29 == "soldiers") SpawnDuel();
                    else Notify("No wave yet - spawn one first.");
                    break;
                case "mixed": SpawnMixedWave(); break;
            }
        }

        static void ApplyStyle29()
        {
            if (styleSeen29 == null) { styleSeen29 = style; return; }        // loading saved settings is not a change
            if (style == styleSeen29) return;
            styleSeen29 = style;
            switch (style)
            {
                case "arcade": punchDamage = 1f; stayOnFeet = 0.6f; npcHealth = 150f; break;
                case "real": punchDamage = 0.5f; stayOnFeet = 1f; npcHealth = 300f; break;
                case "tough": punchDamage = 0.35f; stayOnFeet = 1.8f; npcHealth = 800f; break;
                default: return;
            }
            Notify($"Style: {style} (applies to NPCs that spawn from now on)");
        }

        static void TickQol29()
        {
            float now = Time.time;
            if (now < nextQol29) return;
            nextQol29 = now + 1f;
            shockDeaths = bleedOutKills = knockoutKills = toxinsKill = injuriesKill;
            ApplyStyle29();
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) return;
            if (loadedAt29 < 0f) loadedAt29 = now;
            if (!welcomed29 && now - loadedAt29 > 6f) { welcomed29 = true; Notify($"Rage Engine {Version} is on.\nSettings: Mod Options > Rage"); }

            // wave cleared
            if (wave29.Count > 0)
            {
                bool alive = false;
                foreach (var c in wave29) if (c != null && !c.isKilled && c.gameObject.activeInHierarchy) { alive = true; break; }
                if (!alive) { Notify($"Wave cleared! {wave29.Count} down."); wave29.Clear(); }
            }

            // bodies fade
            float fade = 0f; float.TryParse(bodiesFade, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fade);
            if (fade <= 0f) { deadSince29.Clear(); return; }
            foreach (var c in Creature.allActive)
            {
                if (c == null || c.isPlayer) continue;
                if (!c.isKilled) { deadSince29.Remove(c); continue; }
                if (!deadSince29.ContainsKey(c)) deadSince29[c] = now;
            }
            List<Creature> done = null;
            foreach (var kv in deadSince29)
            {
                if (kv.Key == null || !kv.Key.isKilled) { (done ??= new List<Creature>()).Add(kv.Key); continue; }
                if (now - kv.Value < fade) continue;
                var c = kv.Key;
                Guard("fade.despawn", () => c.Despawn(0f));
                (done ??= new List<Creature>()).Add(c);
            }
            if (done != null) foreach (var c in done) deadSince29.Remove(c);
        }
    }
}

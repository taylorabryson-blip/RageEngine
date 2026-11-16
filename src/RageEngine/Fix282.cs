// Rage Engine 0.28.2 - fixes after the first mod.io playtest.
//   Messages in the headset (no logs on Quest), spawned enemies forced onto an aggressive faction twice
//   (right away and again once their brain has loaded, which can overwrite it).
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        [ModOptionCategory(CM, 12), ModOptionOrder(20)]
        [ModOption("Show messages", "Short notes in the headset when a map loads or enemies spawn.", defaultValueIndex = 1)]
        public static bool showMessages = true;

        static float lastNotifyAt = -10f;

        internal static void Notify(string text)
        {
            Debug.Log("[RageEngine] " + text);
            if (!showMessages) return;
            if (Time.unscaledTime - lastNotifyAt < 0.4f) return;
            lastNotifyAt = Time.unscaledTime;
            Guard("notify", () =>
            {
                if (DisplayMessage.instance == null) return;
                DisplayMessage.instance.ShowMessage(new DisplayMessage.MessageData(text, 10, 0f, null, null, false, true, false, false, default, null, true, 4f));
            });
        }

        internal static string FactionName28(int id)
        {
            if (id < 0) return "default";
            var gd = Catalog.gameData;
            if (gd != null && gd.factions != null) foreach (var f in gd.factions) if (f != null && f.id == id) return f.name;
            return id.ToString();
        }

        struct Hostile28 { public Creature c; public int faction; public float at; }
        static readonly List<Hostile28> hostile28 = new List<Hostile28>();

        /// Sets the faction now and once more after the creature has finished loading.
        internal static void MakeHostile28(Creature cr, int faction)
        {
            if (cr == null || faction < 0 || faction == 2) return;
            Guard("hostile.now", () => cr.SetFaction(faction));
            hostile28.Add(new Hostile28 { c = cr, faction = faction, at = Time.time + 1.5f });
        }

        static void TickHostile28()
        {
            if (hostile28.Count == 0) return;
            float now = Time.time;
            for (int i = hostile28.Count - 1; i >= 0; i--)
            {
                var h = hostile28[i];
                if (h.c == null || h.c.isKilled) { hostile28.RemoveAt(i); continue; }
                if (now < h.at) continue;
                hostile28.RemoveAt(i);
                if (h.c.factionId != h.faction) Guard("hostile.again", () => h.c.SetFaction(h.faction));
            }
        }
    }
}

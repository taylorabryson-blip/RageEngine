// Rage Engine 0.25.1 - spawn serums, super serums, vials, toxin oils and custom serums from Mod Options
// (Rage Engine: Serums > "Spawn" + "Spawn it"). Works whether or not the game put the item in the sandbox book:
//   * if the game registered the item (RE_Bio_*, RE_Vial_*, RE_Oil_*, RE_Custom_*), that item is spawned;
//   * if it didn't, the base item (common dagger / health potion) is spawned and turned into the device in code.
// Also logs, once, which Rage Engine items the game's catalog actually contains - so the log tells us why the
// book is missing them.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // literal keys/labels (no references to other partial files' static arrays in static initializers)
        static readonly string[] SpawnKeys =
        {
            "Bio_Neurostatic", "Bio_Verdant", "Bio_CryoSleep", "Bio_AdrenalSurge", "Bio_Hallucinatory", "Bio_MotorShutdown",
            "Bio_X01_NightSoldier", "Bio_X02_HeroX", "Bio_X03_AdrenalOverload", "Bio_X04_Titan", "Bio_X05_Phantom", "Bio_X06_Phoenix",
            "Bio_X07_Ravager", "Bio_X08_Overmind", "Bio_X09_Colossus", "Bio_Loyalty", "Bio_Empty",
            "Custom_1", "Custom_2", "Custom_3", "Custom_4", "Custom_5", "Custom_6",
            "Oil_Neurotoxin", "Oil_Paralytic", "Oil_Hemotoxin", "Oil_Sedative", "Oil_Acid", "Oil_Berserk", "Oil_Terror",
        };
        static readonly string[] SpawnLabels =
        {
            "Serum: Neurostatic", "Serum: Verdant Syndrome", "Serum: Cryo-Sleep", "Serum: Adrenal Surge", "Serum: Hallucinatory Syndrome", "Serum: Motor Shutdown",
            "Super Serum: Night Soldier", "Super Serum: Hero X", "Super Serum: Adrenal Overload", "Super Serum: Titan", "Super Serum: Phantom", "Super Serum: Phoenix",
            "Super Serum: Ravager", "Super Serum: Overmind", "Super Serum: Colossus", "Loyalty Serum (makes him your teammate)", "Empty Syringe",
            "Custom Serum 1", "Custom Serum 2", "Custom Serum 3", "Custom Serum 4", "Custom Serum 5", "Custom Serum 6",
            "Toxin Oil: Neurotoxin", "Toxin Oil: Paralytic", "Toxin Oil: Hemotoxin", "Toxin Oil: Sedative", "Toxin Oil: Acid", "Toxin Oil: Berserk", "Toxin Oil: Terror",
        };
        public static ModOptionString[] spawnDeviceValues = BuildSpawnValues();
        static ModOptionString[] BuildSpawnValues()
        {
            var l = new ModOptionString[SpawnKeys.Length];
            for (int i = 0; i < SpawnKeys.Length; i++) l[i] = new ModOptionString(SpawnLabels[i], null, SpawnKeys[i]);
            return l;
        }
        public static ModOptionString[] spawnItButton = { new ModOptionString("Spawn", null, "spawn") };

        public static string spawnDevice = "Bio_Neurostatic";

        public static void SpawnDeviceButton(string _) => Guard("spawn", () => SpawnDevice(spawnDevice));

        static void SpawnDevice(string key)
        {
            if (string.IsNullOrEmpty(key) || instance == null) return;
            CatalogReport();
            // what it is
            int kind = -1, comp = -1, tox = -1, custom = -1, special = 0;
            if (key == "Bio_Empty") kind = 0;
            else if (key == "Bio_Loyalty") { kind = 2; special = 1; }
            else if (key.StartsWith("Bio_")) { comp = System.Array.IndexOf(CompKey, key.Substring(4)); if (comp >= 0) kind = KindFor(comp); }
            else if (key.StartsWith("Vial_")) { comp = System.Array.IndexOf(CompKey, key.Substring(5)); if (comp >= 0) kind = 4; }
            else if (key.StartsWith("Oil_")) { tox = System.Array.IndexOf(ToxName, key.Substring(4)); if (tox >= 0) kind = 4; }
            else if (key.StartsWith("Custom_") && int.TryParse(key.Substring(7), out int n) && n >= 1 && n <= LabSlots) { custom = n - 1; kind = 2; LoadRecipes(); }
            if (kind < 0) { Log("spawn: unknown item " + key); return; }
            string id = "RE_" + key;
            var data = FindItem(d => d.id == id);
            bool registered = data != null;
            if (!registered)
                data = kind == 4 ? (FindItem(d => d.id == "PotionHealth") ?? FindItem(d => d.type == ItemData.Type.Potion))
                                 : (FindItem(d => d.id == "DaggerCommon") ?? FindItem(d => d.type == ItemData.Type.Weapon && Contains(d.id, "dagger")));
            if (data == null) { Log("spawn: no base item found"); return; }
            var cam = Player.local != null && Player.local.head != null && Player.local.head.cam != null ? Player.local.head.cam.transform : null;
            Vector3 pos = cam != null ? cam.position + cam.forward * 0.4f - Vector3.up * 0.2f : Vector3.zero;
            Quaternion rot = cam != null ? Quaternion.LookRotation(cam.forward, Vector3.up) : Quaternion.identity;
            int k = kind, c = comp, t = tox, cu = custom, sp = special;
            data.SpawnAsync(item =>
            {
                if (item == null) return;
                // a registered item is dressed by the device scan from its id; a base item is dressed right here
                if (!registered && !devices.ContainsKey(item)) Guard("dev.dress", () => instance.DressDevice(item, k, c, t, cu, sp));
                Log($"spawned {id} ({(registered ? "from the item catalog" : "on base item '" + data.id + "' - the game did not register " + id)})");
            }, pos, rot, null, false);
        }

        static bool catalogReported;
        /// Once: which Rage Engine items did the game load into its catalog?
        static void CatalogReport()
        {
            if (catalogReported) return;
            catalogReported = true;
            var list = Catalog.GetDataList(Category.Item);
            if (list == null) { Log("catalog: item list unavailable"); return; }
            var have = new HashSet<string>();
            foreach (var cd in list) if (cd is ItemData d && d.id != null && d.id.StartsWith("RE_")) have.Add(d.id);
            var missing = new List<string>();
            foreach (var k in SpawnKeys) if (!have.Contains("RE_" + k)) missing.Add("RE_" + k);
            Log($"catalog: {have.Count} Rage Engine items registered; missing {missing.Count}: {string.Join(", ", missing)}");
        }
    }
}

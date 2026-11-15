// Rage Engine 0.25.3 - super soldier fights, from Mod Options > Rage Engine: Super Serums.
//   "Super soldier fight" picks Duel (1) or a wave of 3 or 5; "Spawn super soldiers" drops them in front of you.
//   Each one is a normal enemy from the game's own creature tables (so outfits, weapons and brains are the game's)
//   carrying a random super serum (or the one you pick) - eyes, aura and powers included.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        public static ModOptionString[] duelSizeValues =
        {
            new ModOptionString("Duel - 1 super soldier", null, "1"),
            new ModOptionString("Wave - 3 super soldiers", null, "3"),
            new ModOptionString("Wave - 5 super soldiers", null, "5"),
        };
        public static ModOptionString[] duelSerumValues =
        {
            new ModOptionString("Random serum each", null, "-1"),
            new ModOptionString("Night Soldier", null, "6"), new ModOptionString("Hero X", null, "7"), new ModOptionString("Adrenal Overload", null, "8"),
            new ModOptionString("Titan", null, "9"), new ModOptionString("Phantom", null, "10"), new ModOptionString("Phoenix", null, "11"),
            new ModOptionString("Ravager", null, "12"), new ModOptionString("Overmind", null, "13"), new ModOptionString("Colossus", null, "14"),
        };
        public static ModOptionString[] duelButton = { new ModOptionString("Spawn", null, "spawn") };

        [ModOptionCategory(CX, 7), ModOptionOrder(-3)]
        [ModOption("Super soldier fight", "A duel with one super soldier, or a wave of them.", valueSourceName = nameof(duelSizeValues), defaultValueIndex = 0)]
        public static string duelSize = "1";
        [ModOptionCategory(CX, 7), ModOptionOrder(-2)]
        [ModOption("Their serum", "Random = every super soldier gets a random super serum.", valueSourceName = nameof(duelSerumValues), defaultValueIndex = 0)]
        public static string duelSerum = "-1";
        [ModOptionButton, ModOptionCategory(CX, 7), ModOptionOrder(-1)]
        [ModOption("Spawn super soldiers", "Spawns them a few metres in front of you, enhanced and hostile.", valueSourceName = nameof(duelButton), saveValue = false)]
        public static void SpawnDuelButton(string _) => Guard("duel", () => instance?.SpawnDuel());

        sealed class PendingSoldier { public Creature c; public float at; public int comp; }
        static readonly List<PendingSoldier> pendingSoldiers = new List<PendingSoldier>();
        static bool factionsLogged;

        void SpawnDuel()
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) { Debug.Log("[RageEngine] super soldiers: no player"); return; }
            int n = 1; int.TryParse(duelSize, out n); n = Mathf.Clamp(n, 1, 8);
            int fixedComp = -1; int.TryParse(duelSerum, out fixedComp);
            LogFactions();
            int hostile = HostileFaction();
            Vector3 fwd = Vector3.ProjectOnPlane(Player.local.head != null ? Player.local.head.transform.forward : pc.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = pc.transform.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            for (int i = 0; i < n; i++)
            {
                var data = PickSoldier(out int tableFaction);
                if (data == null) { Debug.Log("[RageEngine] super soldiers: no creature data found in the catalog"); return; }
                float side = n == 1 ? 0f : (i - (n - 1) * 0.5f) * 1.6f;
                Vector3 pos = pc.transform.position + fwd * (5f + (i % 2) * 1.2f) + right * side;
                if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var hitG, 8f, ~0, QueryTriggerInteraction.Ignore)) pos = hitG.point + Vector3.up * 0.05f;
                Vector3 look = pc.transform.position - pos; look.y = 0f;
                float rotY = Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : -fwd).eulerAngles.y;
                int comp = fixedComp >= (int)Comp.NightSoldier && fixedComp < (int)Comp.Count ? fixedComp : RandomSuperSerum();
                int faction = tableFaction >= 0 && tableFaction != 2 ? tableFaction : data.factionId != 2 && data.factionId >= 0 ? data.factionId : hostile;
                string id = data.id;
                data.SpawnAsync(pos, rotY, null, false, null, cr =>
                {
                    if (cr == null) return;
                    Guard("duel.faction", () => { if (faction >= 0 && faction != 2) cr.SetFaction(faction); });
                    pendingSoldiers.Add(new PendingSoldier { c = cr, at = Time.time + 1.2f, comp = comp });
                    Debug.Log($"[RageEngine] super soldier spawned: {id} (faction {faction}) - {CompName[comp]}");
                });
            }
            Debug.Log($"[RageEngine] spawning {n} super soldier(s)");
        }

        static int RandomSuperSerum()
        {
            var pool = new List<int>();
            for (var x = Comp.NightSoldier; x < Comp.Count; x++) if (CompAllowed(x)) pool.Add((int)x);
            if (pool.Count == 0) for (var x = Comp.NightSoldier; x < Comp.Count; x++) pool.Add((int)x);
            return pool[Random.Range(0, pool.Count)];
        }

        /// Per frame: once a spawned soldier is loaded and tracked, the serum goes in.
        void TickDuelSpawns()
        {
            if (pendingSoldiers.Count == 0) return;
            float now = Time.time;
            for (int i = pendingSoldiers.Count - 1; i >= 0; i--)
            {
                var p = pendingSoldiers[i];
                if (p.c == null || p.c.isKilled || now > p.at + 15f) { pendingSoldiers.RemoveAt(i); continue; }
                if (now < p.at) continue;
                if (!TryGet(p.c, out var s)) continue;                                   // the regular scan picks him up
                pendingSoldiers.RemoveAt(i);
                var chest = ChestPart(p.c);
                Guard("duel.serum", () => ForceCompound(s, (Comp)p.comp, chest != null ? chest.transform.position : p.c.transform.position));
            }
        }

        /// The serum goes in even if that super serum is switched off in the menu (the fight asked for it).
        void ForceCompound(NpcState s, Comp comp, Vector3 site)
        {
            bool prevBio = bioEnabled; bioEnabled = true;
            var flag = SerumFlag(comp); bool prev = flag != null && (bool)flag.GetValue(null);
            if (flag != null) flag.SetValue(null, true);
            try { ApplyCompound(s, comp, 1f, site); }
            finally { if (flag != null) flag.SetValue(null, prev); bioEnabled = prevBio; }
        }

        static System.Reflection.FieldInfo SerumFlag(Comp comp)
        {
            string n = comp == Comp.NightSoldier ? nameof(xNight) : comp == Comp.HeroX ? nameof(xHero) : comp == Comp.AdrenalOverload ? nameof(xAdrenal)
                     : comp == Comp.Titan ? nameof(xTitan) : comp == Comp.Phantom ? nameof(xPhantom) : comp == Comp.Phoenix ? nameof(xPhoenix)
                     : comp == Comp.Ravager ? nameof(xRavager) : comp == Comp.Overmind ? nameof(xOvermind) : comp == Comp.Colossus ? nameof(xColossus) : null;
            return n == null ? null : typeof(RageEngineScript).GetField(n, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        }

        // ------------------------------------------------------------------ picking an enemy from the game's own tables
        static readonly string[] TablePrefs = { "humanmelee", "melee", "bandit", "mixed", "human" };
        static readonly string[] TableAvoid = { "player", "wildlife", "test", "ally", "friend", "prisoner", "civil", "archer", "ranged", "mage", "boss" };

        static CreatureData PickSoldier(out int faction)
        {
            faction = -1;
            List<CatalogData> tables = null;
            try { tables = Catalog.GetDataList(Category.CreatureTable); } catch { }
            if (tables != null && tables.Count > 0)
            {
                foreach (var pref in TablePrefs)
                {
                    var cands = new List<CreatureTable>();
                    foreach (var d in tables)
                    {
                        if (!(d is CreatureTable t) || string.IsNullOrEmpty(t.id)) continue;
                        string id = t.id.ToLowerInvariant().Replace("_", "").Replace(" ", "");
                        if (!id.Contains(pref)) continue;
                        bool bad = false; foreach (var a in TableAvoid) if (id.Contains(a)) bad = true;
                        if (!bad) cands.Add(t);
                    }
                    if (cands.Count == 0) continue;
                    var tbl = cands[Random.Range(0, cands.Count)];
                    CreatureData cd = null;
                    bool ok = false;
                    try { ok = tbl.TryPick(ref cd, new System.Random(Random.Range(0, int.MaxValue))); } catch { }
                    if (ok && cd != null)
                    {
                        if (tbl.drops != null) foreach (var dr in tbl.drops) if (dr != null && dr.overrideFaction && dr.factionID != 2) { faction = dr.factionID; break; }
                        Debug.Log($"[RageEngine] super soldier from table '{tbl.id}' -> {cd.id}");
                        return cd;
                    }
                }
            }
            foreach (var id in new[] { Random.value < 0.5f ? "HumanMale" : "HumanFemale", "HumanMale", "HumanFemale" })
            {
                CreatureData cd = null;
                try { cd = Catalog.GetData<CreatureData>(id, false); } catch { }
                if (cd != null) return cd;
            }
            return null;
        }

        static int HostileFaction()
        {
            var gd = Catalog.gameData;
            if (gd == null || gd.factions == null) return -1;
            foreach (var want in new[] { "bandit", "enemy", "mercenar", "rogue", "outlaw", "eraden" })
                foreach (var f in gd.factions) if (f != null && f.id != 2 && !string.IsNullOrEmpty(f.name) && f.name.ToLowerInvariant().Contains(want)) return f.id;
            foreach (var f in gd.factions)
            {
                if (f == null || f.id == 2 || string.IsNullOrEmpty(f.name)) continue;
                string n = f.name.ToLowerInvariant();
                if (n.Contains("player") || n.Contains("ignore") || n.Contains("ally") || n.Contains("neutral") || n.Contains("wild") || n.Contains("passive")) continue;
                return f.id;
            }
            return -1;
        }

        static void LogFactions()
        {
            if (factionsLogged) return;
            factionsLogged = true;
            try
            {
                var gd = Catalog.gameData;
                if (gd != null && gd.factions != null) foreach (var f in gd.factions) if (f != null) Debug.Log($"[RageEngine] faction {f.id} = {f.name}");
                var tables = Catalog.GetDataList(Category.CreatureTable);
                if (tables != null) { var sb = new System.Text.StringBuilder("[RageEngine] creature tables:"); foreach (var t in tables) if (t != null) sb.Append(' ').Append(t.id); Debug.Log(sb.ToString()); }
            }
            catch (System.Exception e) { Debug.Log("[RageEngine] faction list: " + e.Message); }
        }
    }
}

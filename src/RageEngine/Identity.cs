// Rage Engine 0.17 - persistent NPC identities.
// Every NPC gets a name and a record (personality, adrenaline profile, encounters, escapes). Records are saved to
// RageEngine_roster.json in the game's save folder (only when something changed, never every frame).
//   - An NPC who gets away (despawns alive after you fought him) can come back later as the SAME person: same name,
//     same temperament - and he remembers you: braver, holds a grudge (Anger), a bit tougher.
//   - An NPC you killed is marked defeated and never comes back.
using System;
using System.Collections.Generic;
using System.IO;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    [Serializable] internal sealed class NpcRecord
    {
        public string id, name, friends = ""; public bool female; public int scars, brokenLegs, ppZones; public string serum = ""; public int personality, adren, encounters, escapes; public bool defeated; public long lastSeen; public int prof;   // 0.43: weapon proficiency 1-100 (0 = not rolled yet)
    }
    [Serializable] internal sealed class Roster { public List<NpcRecord> npcs = new List<NpcRecord>(); }

    public sealed partial class RageEngineScript
    {
        static Roster roster;
        static bool rosterDirty;
        static float nextRosterSave;
        static readonly HashSet<string> activeIds = new HashSet<string>();
        static readonly string[] MaleFirst = { "Aldric", "Bram", "Cedric", "Doran", "Edric", "Falk", "Garrett", "Hugh", "Ivo", "Jory", "Kael", "Leof", "Marek", "Ned", "Osric", "Piers", "Rolf", "Silas", "Tobin", "Ulric", "Varek", "Wendel", "Yorick", "Bertram", "Cole", "Dunstan", "Emeric", "Gideon", "Hollis", "Jasper" };
        static readonly string[] FemaleFirst = { "Ada", "Brenna", "Cora", "Della", "Elsie", "Freya", "Greta", "Hilde", "Isolde", "Jessa", "Kira", "Lena", "Mira", "Nell", "Odile", "Petra", "Rhea", "Sabine", "Tilda", "Una", "Vera", "Wren", "Yara", "Agnes", "Bess", "Clara", "Edda", "Gwen", "Ilse", "Maud" };
        static readonly string[] Last = { "Ashford", "Blackwood", "Crane", "Dunmore", "Fenwick", "Grimsby", "Hale", "Ironside", "Kettle", "Lockwood", "Marsh", "Norwood", "Oakes", "Pike", "Quill", "Redfern", "Stone", "Thorne", "Underhill", "Vance", "Ward", "Yates", "Barrow", "Cobb", "Drake", "Flint", "Gage", "Holt", "Mercer", "Rook" };

        static string RosterPath => Path.Combine(Application.persistentDataPath, "RageEngine_roster.txt");

        static void LoadRoster()
        {
            if (roster != null) return;
            roster = new Roster();
            try { if (File.Exists(RosterPath)) roster = Parse(File.ReadAllLines(RosterPath)); }
            catch (Exception e) { Debug.LogWarning("[RageEngine] roster unreadable, starting fresh: " + e.Message); roster = new Roster(); }
            if (roster.npcs == null) roster.npcs = new List<NpcRecord>();
            int alive = 0; foreach (var r in roster.npcs) if (!r.defeated) alive++;
            Log($"roster: {roster.npcs.Count} known people ({alive} still out there)");
        }

        static void SaveRosterIfDirty(bool force = false)
        {
            if (!rosterDirty || roster == null || (!force && Time.time < nextRosterSave)) return;
            rosterDirty = false; nextRosterSave = Time.time + 10f;
            if (roster.npcs.Count > 400) roster.npcs.RemoveRange(0, roster.npcs.Count - 400);   // keep the file small
            try { File.WriteAllLines(RosterPath, Format(roster)); }
            catch (Exception e) { Debug.LogWarning("[RageEngine] roster save failed: " + e.Message); }
        }

        // one person per line: id|name|female|personality|adrenaline|encounters|escapes|defeated|lastSeen|friend ids
        static Roster Parse(string[] lines)
        {
            var r = new Roster();
            foreach (var line in lines)
            {
                var f = line.Split('|');
                if (f.Length < 9) continue;
                try
                {
                    r.npcs.Add(new NpcRecord { id = f[0], name = f[1], female = f[2] == "1", personality = int.Parse(f[3]), adren = int.Parse(f[4]),
                        encounters = int.Parse(f[5]), escapes = int.Parse(f[6]), defeated = f[7] == "1", lastSeen = long.Parse(f[8]), friends = f.Length > 9 ? f[9] : "", scars = f.Length > 10 ? int.Parse(f[10]) : 0, brokenLegs = f.Length > 11 ? int.Parse(f[11]) : 0, ppZones = f.Length > 12 ? int.Parse(f[12]) : 0, serum = f.Length > 13 ? f[13] : "" });
                }
                catch { }
            }
            return r;
        }

        static string[] Format(Roster r)
        {
            var lines = new string[r.npcs.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                var n = r.npcs[i];
                lines[i] = string.Join("|", n.id, (n.name ?? "").Replace("|", " "), n.female ? "1" : "0", n.personality.ToString(), n.adren.ToString(),
                                       n.encounters.ToString(), n.escapes.ToString(), n.defeated ? "1" : "0", n.lastSeen.ToString(), n.friends ?? "", n.scars.ToString(), n.brokenLegs.ToString(), n.ppZones.ToString(), n.serum ?? "");
            }
            return lines;
        }

        /// Called from Track: a returning person (same name, same temperament, a grudge) or a new one.
        void AssignIdentity(NpcState s)
        {
            LoadRoster();
            var c = s.creature;
            bool female = RageEngineScript.IsFemale56(c);
            NpcRecord rec = null;
            if (UnityEngine.Random.value < 0.3f)
            {
                var pool = new List<NpcRecord>();
                foreach (var r in roster.npcs) if (!r.defeated && r.escapes > 0 && r.female == female && !activeIds.Contains(r.id)) pool.Add(r);
                if (pool.Count > 0) rec = pool[UnityEngine.Random.Range(0, pool.Count)];
            }
            if (rec != null)
            {
                s.personality = Personality.All[Mathf.Clamp(rec.personality, 0, Personality.All.Length - 1)];
                s.adrenProfile = (AdrenProfile)rec.adren;
                s.brave = true;
                s.toughness *= 1.1f;
                AddTag(s, Tag.Anger, 9999f, 2f);                                   // remembers you
                Log($"{c.name} is {rec.name} - BACK again (escaped {rec.escapes}x, met {rec.encounters}x) and holding a grudge");
            }
            else
            {
                var first = female ? FemaleFirst : MaleFirst;
                rec = new NpcRecord
                {
                    id = Guid.NewGuid().ToString("N").Substring(0, 12), female = female,
                    name = first[UnityEngine.Random.Range(0, first.Length)] + " " + Last[UnityEngine.Random.Range(0, Last.Length)],
                    personality = Array.IndexOf(Personality.All, s.personality), adren = (int)s.adrenProfile
                };
                roster.npcs.Add(rec);
                Log($"{c.name} is {rec.name} ({s.personality.type})");
            }
            rec.encounters++;
            rec.lastSeen = DateTime.UtcNow.Ticks;
            s.identity = rec;
            activeIds.Add(rec.id);
            rosterDirty = true;
        }

        /// Called from Release: dead for good = defeated; walked away alive after a fight = escaped (may return).
        static void CloseIdentity(NpcState s)
        {
            var rec = s.identity;
            if (rec == null) return;
            var c = s.creature;
            bool dead = s.diedFinal || s.deadSeen54 || (c != null && c.isKilled && LifeOf54(c) == s.life54);   // 0.54: a body re-used before we looked still died
            if (dead) { rec.defeated = true; Log($"{rec.name} is dead - won't be back"); }
            else if (s.lastHitTime > 0f) { rec.escapes++; rec.scars |= s.scarMask | (int)s.brokenParts; if (ppPersistence == "Persistent") rec.ppZones |= DisabledZoneMask(s); Log($"{rec.name} got away (escapes: {rec.escapes}) - he'll remember this"); }
            activeIds.Remove(rec.id);
            s.identity = null;
            rosterDirty = true;
        }

        internal static string WhoIs(NpcState s) => s.identity != null ? s.identity.name : Name(s);
    }
}

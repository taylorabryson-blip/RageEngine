// Rage Engine 0.25 - SERUM LAB. Combine the existing compounds (serums, super serums, toxins) into your own custom
// serums, stored in 6 slots that map to 6 "Custom Serum" syringes in the item book (category "Custom Serums").
//
// It is an EXPANSION of the existing systems, not a replacement:
//   * ingredients are applied through the existing pipelines - ApplyCompound (serums / super serums, per-NPC BioFx
//     list, reversible multipliers, natural expiry) and Envenom (toxins, per-NPC toxin state);
//   * the custom syringes are ordinary Rage Engine devices (same models, plunger, needle-in detection, dose);
//   * a central REACTION REGISTRY resolves every combination: duplicates are dropped, conflicts are resolved
//     (sleep vs. stimulant neutralise, only one super serum and one toxin can take hold), and registered combination
//     bonuses (fictional reactions) are added on top. New reactions = one new entry in Reactions[].
// UI: the game's Mod Options menu (the only in-game UI Nomad documents for mods) - Rage Engine: Serum Lab.
// Recipes are saved in RageEngine_serums.txt next to the roster, and every injection is logged to the research file.
using System.Collections.Generic;
using System.IO;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const string CL = "Rage Engine: Serum Lab";
        internal const int LabSlots = 6;
        const int NComp = 15, NTox = 7, NIng = NComp + NTox;                                    // ingredient index: comps, then toxins

        static string IngLabel(int i) => i < 0 || i >= NIng ? "None" : IngLabels[i];
        static bool IngIsSuper(int i) => i >= 6 && i < NComp;
        static bool IngIsTox(int i) => i >= NComp && i < NIng;

        // literal labels: static initializers in other partial files (CompName, ToxName) may not have run yet
        static readonly string[] IngLabels = { "Serum: Neurostatic", "Serum: Verdant Syndrome", "Serum: Cryo-Sleep", "Serum: Adrenal Surge", "Serum: Hallucinatory Syndrome", "Serum: Motor Shutdown", "Super Serum: Night Soldier", "Super Serum: Hero X", "Super Serum: Adrenal Overload", "Super Serum: Titan", "Super Serum: Phantom", "Super Serum: Phoenix", "Super Serum: Ravager", "Super Serum: Overmind", "Super Serum: Colossus", "Toxin: Neurotoxin", "Toxin: Paralytic", "Toxin: Hemotoxin", "Toxin: Sedative", "Toxin: Acid", "Toxin: Berserk", "Toxin: Terror" };
        public static ModOptionString[] labIngValues = BuildIngValues();
        static ModOptionString[] BuildIngValues()
        {
            var l = new List<ModOptionString> { new ModOptionString("None", null, "-1") };
            for (int i = 0; i < IngLabels.Length; i++) l.Add(new ModOptionString(IngLabels[i], null, i.ToString()));
            return l.ToArray();
        }
        public static ModOptionString[] labSlotValues =
        {
            new ModOptionString("Custom Serum 1", null, "1"), new ModOptionString("Custom Serum 2", null, "2"), new ModOptionString("Custom Serum 3", null, "3"),
            new ModOptionString("Custom Serum 4", null, "4"), new ModOptionString("Custom Serum 5", null, "5"), new ModOptionString("Custom Serum 6", null, "6"),
        };
        static readonly string[] LabNames = { "Auto", "Black Tonic", "Grave Draught", "Iron Blood", "Widow's Kiss", "Ash Elixir", "Wolfsbane", "Red Mercy",
            "Bone Wine", "Dusk Tincture", "Hollow Ichor", "King's Ruin", "Last Rites", "Devil's Milk", "Saint's Fever", "Butcher's Brew" };
        public static ModOptionString[] labNameValues = BuildList(LabNames);
        static readonly string[] LabColorNames = { "Auto (blend)", "Crimson", "Amber", "Gold", "Emerald", "Teal", "Azure", "Violet", "Rose", "Ivory", "Pitch black" };
        static readonly Color[] LabColors = { Color.clear, new Color(0.6f, 0.02f, 0.06f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.82f, 0.2f), new Color(0.1f, 0.75f, 0.3f),
            new Color(0.1f, 0.7f, 0.7f), new Color(0.2f, 0.45f, 1f), new Color(0.55f, 0.2f, 0.9f), new Color(1f, 0.4f, 0.6f), new Color(0.95f, 0.92f, 0.82f), new Color(0.05f, 0.05f, 0.06f) };
        public static ModOptionString[] labColorValues = BuildList(LabColorNames);
        static ModOptionString[] BuildList(string[] a) { var l = new ModOptionString[a.Length]; for (int i = 0; i < a.Length; i++) l[i] = new ModOptionString(a[i], null, a[i]); return l; }
        public static ModOptionString[] labButton = { new ModOptionString("Do it", null, "go") };

        public static string labSlot = "1";
        public static string labIng1 = "-1";
        public static string labIng2 = "-1";
        public static string labIng3 = "-1";
        public static string labIng4 = "-1";
        public static string labName = "Auto";
        public static string labColor = "Auto (blend)";
        public static float labStrength = 1f;
        public static float labDuration = 1f;
        public static void LabSave(string _) => Guard("lab", () => SaveSlot(Slot()));
        public static void LabDuplicate(string _) => Guard("lab", () => DuplicateSlot(Slot()));
        public static void LabDelete(string _) => Guard("lab", () => { recipes[Slot()] = null; SaveRecipes(); Log($"lab: slot {Slot() + 1} deleted"); RefreshCustomDevices(); });
        public static void LabLoad(string _) => Guard("lab", () => LoadSlot(Slot()));

        static int Slot() { int.TryParse(labSlot, out int n); return Mathf.Clamp(n - 1, 0, LabSlots - 1); }

        // ------------------------------------------------------------------ data
        internal sealed class Recipe
        {
            public string id, name, nameChoice = "Auto", colorChoice = "Auto (blend)";
            public List<int> ings = new List<int>();
            public float strength = 1f, duration = 1f;
            public Color color;
            public string description = "";
        }
        static readonly Recipe[] recipes = new Recipe[LabSlots];
        static bool recipesLoaded;
        static string RecipePath => Path.Combine(Path.GetDirectoryName(RosterPath), "RageEngine_serums.txt");

        static void LoadRecipes()
        {
            if (recipesLoaded) return;
            recipesLoaded = true;
            try
            {
                if (!File.Exists(RecipePath)) return;
                foreach (var line in File.ReadAllLines(RecipePath))
                {
                    var f = line.Split('|');
                    if (f.Length < 7 || !int.TryParse(f[0], out int slot) || slot < 1 || slot > LabSlots) continue;
                    var r = new Recipe { id = f[1], nameChoice = f[2], colorChoice = f[3] };
                    foreach (var x in f[4].Split(',')) if (int.TryParse(x, out int ing) && ing >= 0 && ing < NIng) r.ings.Add(ing);
                    float.TryParse(f[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r.strength);
                    float.TryParse(f[6], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r.duration);
                    Finish(r);
                    recipes[slot - 1] = r;
                }
                Log($"lab: {System.Array.FindAll(recipes, x => x != null).Length} saved custom serums loaded");
            }
            catch (System.Exception e) { Log("lab load: " + e.Message); }
        }

        static void SaveRecipes()
        {
            try
            {
                var lines = new List<string>();
                for (int i = 0; i < LabSlots; i++)
                {
                    var r = recipes[i]; if (r == null) continue;
                    lines.Add($"{i + 1}|{r.id}|{r.nameChoice}|{r.colorChoice}|{string.Join(",", r.ings)}|{r.strength.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{r.duration.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }
                File.WriteAllLines(RecipePath, lines);
            }
            catch (System.Exception e) { Log("lab save: " + e.Message); }
        }

        static void SaveSlot(int slot)
        {
            LoadRecipes();
            var r = new Recipe { nameChoice = labName, colorChoice = labColor, strength = labStrength, duration = labDuration };
            foreach (var s in new[] { labIng1, labIng2, labIng3, labIng4 })
                if (int.TryParse(s, out int i) && i >= 0 && i < NIng && !r.ings.Contains(i)) r.ings.Add(i);        // no duplicate registration
            if (r.ings.Count == 0) { Log("lab: pick at least one ingredient"); return; }
            r.id = "RE_CS_" + System.DateTime.Now.ToString("yyMMddHHmmss") + "_" + (slot + 1);
            Finish(r);
            recipes[slot] = r;
            SaveRecipes();
            RefreshCustomDevices();
            Log($"lab: slot {slot + 1} = {r.name} [{r.description}]");
        }

        static void DuplicateSlot(int slot)
        {
            LoadRecipes();
            var src = recipes[slot];
            if (src == null) { Log("lab: that slot is empty"); return; }
            for (int i = 0; i < LabSlots; i++)
                if (recipes[i] == null)
                {
                    var r = new Recipe { nameChoice = src.nameChoice, colorChoice = src.colorChoice, strength = src.strength, duration = src.duration, ings = new List<int>(src.ings) };
                    r.id = src.id + "_copy" + (i + 1);
                    Finish(r);
                    recipes[i] = r; SaveRecipes(); RefreshCustomDevices();
                    Log($"lab: slot {slot + 1} copied to slot {i + 1}");
                    return;
                }
            Log("lab: no empty slot to copy into");
        }

        static void LoadSlot(int slot)
        {
            LoadRecipes();
            var r = recipes[slot];
            if (r == null) { Log("lab: that slot is empty"); return; }
            string I(int k) => k < r.ings.Count ? r.ings[k].ToString() : "-1";
            labIng1 = I(0); labIng2 = I(1); labIng3 = I(2); labIng4 = I(3);
            labName = r.nameChoice; labColor = r.colorChoice; labStrength = r.strength; labDuration = r.duration;
            Log($"lab: slot {slot + 1} loaded into the editor ({r.name})");
        }

        /// Name, colour and description from the ingredients and the reaction registry.
        static void Finish(Recipe r)
        {
            var res = Resolve(r.ings);
            string auto = res.reactions.Count > 0 ? Reactions[res.reactions[0]].name
                        : string.Join(" + ", r.ings.ConvertAll(i => i < NComp ? CompName[i] : ToxName[i - NComp]));
            r.name = r.nameChoice == "Auto" || string.IsNullOrEmpty(r.nameChoice) ? auto : r.nameChoice;
            int ci = System.Array.IndexOf(LabColorNames, r.colorChoice);
            if (ci > 0) r.color = LabColors[ci];
            else
            {
                Color c = Color.black;
                foreach (var i in r.ings) c += i < NComp ? CompLiquid[i] : ToxColor[i - NComp];
                r.color = r.ings.Count > 0 ? c / r.ings.Count : Color.gray; r.color.a = 1f;
            }
            r.description = string.Join(", ", r.ings.ConvertAll(IngLabel)) + (res.reactions.Count > 0 ? " | reactions: " + string.Join(", ", res.reactions.ConvertAll(x => Reactions[x].name)) : "")
                          + (res.notes.Count > 0 ? " | " + string.Join("; ", res.notes) : "");
        }

        // ------------------------------------------------------------------ reaction registry
        internal enum RxKind { Bonus, Conflict }
        internal sealed class Reaction
        {
            public string name; public int[] needs; public RxKind kind; public float seconds; public string text;
            public System.Action<RageEngineScript, NpcState> onStart;
            public System.Action<RageEngineScript, NpcState, float> onTick;
            public System.Action<RageEngineScript, NpcState> onEnd;
        }
        const int iNeuro = 0, iVerdant = 1, iCryo = 2, iSurge = 3, iHalluc = 4, iMotor = 5, iNight = 6, iHero = 7, iOverload = 8, iTitan = 9, iPhantom = 10,
                  iPhoenix = 11, iRavager = 12, iOvermind = 13, iColossus = 14, tNeuro = NComp + 0, tPara = NComp + 1, tHemo = NComp + 2, tSed = NComp + 3,
                  tAcid = NComp + 4, tBerserk = NComp + 5, tTerror = NComp + 6;
        static readonly object LabDmgHandler = new object();

        /// The registry. Order matters only for naming (the first matching bonus names an "Auto" serum).
        internal static readonly Reaction[] Reactions =
        {
            new Reaction { name = "Bloodrage", needs = new[] { iOverload, tBerserk }, seconds = 20f, text = "Last Stand at once: no pain, charging, for 20 s",
                onStart = (e, s) => { s.bioLastStandUntil = Time.time + 20f; s.rushUntil = Time.time + 3f; Scream(s, true); RageMoment("Bloodrage"); } },
            new Reaction { name = "Night Terror", needs = new[] { iHalluc, tTerror }, seconds = 25f, text = "drops everything and runs from what he sees",
                onStart = (e, s) => { foreach (var sd in new[] { Side.Left, Side.Right }) { var h = Hand(s.creature, sd); if (h != null && h.grabbedHandle != null) { var hh = h; Guard("drop", () => hh.UnGrab(false)); } } Scream(s, true); },
                onTick = (e, s, now) => { if (Random.value < 0.08f) Scream(s, false); SetMelee(s, false, now + 0.5f); } },
            new Reaction { name = "Iron Statue", needs = new[] { iColossus, iMotor }, seconds = 20f, text = "frozen on his feet, takes almost no damage",
                onStart = (e, s) => { s.creature.damageMultipliers[LabDmgHandler] = 0.15f; },
                onTick = (e, s, now) => { SetMelee(s, false, now + 0.5f); s.balance = 0f; },
                onEnd = (e, s) => { s.creature.damageMultipliers.Remove(LabDmgHandler); } },
            new Reaction { name = "Green Rot", needs = new[] { iVerdant, tAcid }, seconds = 30f, text = "burns spread fast and keep eating",
                onTick = (e, s, now) => { if (Random.value < 0.06f && s.creature.ragdoll != null) { var parts = s.creature.ragdoll.parts; var p = parts[Random.Range(0, parts.Count)]; if (p != null) Stamp(s.creature, p.transform.position, 0.05f, "burn"); Guard("dmg", () => s.creature.Damage(2f)); } } },
            new Reaction { name = "Dead Sleep", needs = new[] { iCryo, tPara }, seconds = 30f, text = "drops asleep at once, deep",
                onStart = (e, s) => { e.Sleep(s, 30f); } },
            new Reaction { name = "Ashes to Fire", needs = new[] { iPhoenix, tHemo }, seconds = 600f, text = "bleeds hard - but his rebirth heals him completely",
                onTick = (e, s, now) => { var c = s.creature; if (c.maxHealth > 0f && c.currentHealth / c.maxHealth < 0.1f && !s.labRebirthUsed) { s.labRebirthUsed = true; Guard("heal", () => c.Heal(c.maxHealth)); s.bleedRate = 0f; Scream(s, true); RageMoment("Ashes to Fire"); } } },
            new Reaction { name = "Hive Fever", needs = new[] { iOvermind, iNeuro }, seconds = 30f, text = "his allies go punch-drunk instead of sharp",
                onTick = (e, s, now) => { if (Random.value < 0.1f) foreach (var o in e.pool) if (o != s && o.active && o.creature != null && !o.creature.isKilled && (o.creature.transform.position - s.creature.transform.position).sqrMagnitude < 64f) o.drunk = Mathf.Max(o.drunk, 0.7f); } },
            new Reaction { name = "Ghost Rush", needs = new[] { iPhantom, iSurge }, seconds = 25f, text = "blindingly fast, then a hard crash",
                onTick = (e, s, now) => { if (s.nextPunch > now + 0.3f) s.nextPunch = now + 0.3f; },
                onEnd = (e, s) => { s.fatigue = 1f; s.drunk = Mathf.Max(s.drunk, 0.8f); } },
        };

        internal sealed class Resolution { public List<int> apply = new List<int>(); public List<int> reactions = new List<int>(); public List<string> notes = new List<string>(); }

        static readonly int[] Sleepers = { iCryo, tSed }, Stimulants = { iSurge, iOverload, tBerserk, iRavager };

        /// Resolve a set of ingredients: what actually takes hold, which reactions fire, and why.
        internal static Resolution Resolve(List<int> ings)
        {
            var r = new Resolution();
            var set = new HashSet<int>(ings);                                                        // duplicates can't double-register
            // conflict: a sleeper and a stimulant neutralise each other (both drop out)
            bool sl = false, st = false;
            foreach (var i in set) { if (System.Array.IndexOf(Sleepers, i) >= 0) sl = true; if (System.Array.IndexOf(Stimulants, i) >= 0) st = true; }
            if (sl && st)
            {
                foreach (var i in Sleepers) set.Remove(i);
                foreach (var i in Stimulants) set.Remove(i);
                r.notes.Add("sleep and stimulant neutralise");
            }
            // only one super serum takes hold (the first in the recipe); the rest overload the body
            int super = -1;
            foreach (var i in ings) if (set.Contains(i) && IngIsSuper(i)) { if (super < 0) super = i; else { set.Remove(i); r.notes.Add($"{CompName[i]} overloads"); } }
            // only one toxin takes hold (the first); the rest strengthen it
            int tox = -1;
            foreach (var i in ings) if (set.Contains(i) && IngIsTox(i)) { if (tox < 0) tox = i; else { set.Remove(i); r.notes.Add($"{ToxName[i - NComp]} strengthens the toxin"); } }
            foreach (var i in ings) if (set.Contains(i) && !r.apply.Contains(i)) r.apply.Add(i);
            // bonuses: every registered reaction whose ingredients are all in the ORIGINAL recipe
            var all = new HashSet<int>(ings);
            for (int k = 0; k < Reactions.Length; k++)
            {
                bool ok = true; foreach (var n in Reactions[k].needs) if (!all.Contains(n)) { ok = false; break; }
                if (ok) r.reactions.Add(k);
            }
            return r;
        }

        // ------------------------------------------------------------------ applying a custom serum to an NPC
        internal sealed class ActiveRx { public int rx; public float until; }

        void ApplyCustom(NpcState s, int slot, float dose, RagdollPart part, Vector3 site)
        {
            LoadRecipes();
            var r = slot >= 0 && slot < LabSlots ? recipes[slot] : null;
            var c = s.creature;
            if (r == null || c == null || c.isKilled) { Log($"custom serum {slot + 1}: empty slot - nothing happens"); return; }
            var res = Resolve(r.ings);
            int extraTox = 0; foreach (var i in r.ings) if (IngIsTox(i)) extraTox++;
            int overload = 0; foreach (var i in r.ings) if (IngIsSuper(i)) overload++;
            float k = dose * r.strength;
            foreach (var i in res.apply)
            {
                if (i < NComp)
                {
                    ApplyCompound(s, (Comp)i, Mathf.Clamp(k, 0.2f, 1.5f), site);
                    foreach (var fx in s.bioFx) if (fx.comp == (Comp)i) fx.dur *= r.duration;
                }
                else Envenom(s, (Tox)(i - NComp), k * (1f + 0.4f * (extraTox - 1)) * 1.5f, part != null ? part : c.ragdoll.rootPart, site);
            }
            if (overload > 1) { Convulse(s, 0.4f * (overload - 1)); s.drunk = Mathf.Max(s.drunk, 0.5f); }
            foreach (var x in res.reactions)
            {
                var rx = Reactions[x];
                s.labRx.RemoveAll(a => a.rx == x);                                                   // no duplicate registration
                s.labRx.Add(new ActiveRx { rx = x, until = Time.time + rx.seconds * r.duration });
                var rx2 = rx;
                Guard("lab.rx", () => rx2.onStart?.Invoke(this, s));
                Log($"{WhoIs(s)}: REACTION {rx.name} - {rx.text}");
            }
            s.labSerum = r.name; s.labUntil = Time.time + 75f * r.duration; s.labColor = r.color;
            MarkInjected(s, r.color, site);
            LabResearch(s, r, res, dose);
            Log($"{WhoIs(s)} INJECTED custom serum '{r.name}' ({r.description}), dose {dose:0.00}");
        }

        /// 0.1 s tick: active reactions run and expire.
        void TickLab(NpcState s, float now)
        {
            if (s.labRx.Count == 0) return;
            for (int i = s.labRx.Count - 1; i >= 0; i--)
            {
                var a = s.labRx[i]; var rx = Reactions[a.rx];
                if (now >= a.until || s.creature.isKilled)
                {
                    s.labRx.RemoveAt(i);
                    Guard("lab.end", () => rx.onEnd?.Invoke(this, s));
                    Log($"{WhoIs(s)}: reaction {rx.name} wears off");
                    continue;
                }
                Guard("lab.tick", () => rx.onTick?.Invoke(this, s, now));
            }
        }

        /// Body recycled: end every reaction cleanly (multipliers off).
        void ClearLab(NpcState s)
        {
            foreach (var a in s.labRx) { var rx = Reactions[a.rx]; Guard("lab.end", () => rx.onEnd?.Invoke(this, s)); }
            s.labRx.Clear(); s.labSerum = ""; s.labUntil = 0f; s.labRebirthUsed = false;
            if (s.creature != null) Guard("lab.dmg", () => s.creature.damageMultipliers.Remove(LabDmgHandler));
        }

        static void LabResearch(NpcState s, Recipe r, Resolution res, float dose)
        {
            try
            {
                string p = Path.Combine(Path.GetDirectoryName(RosterPath), "RageEngine_research.txt");
                string subject = s.identity != null ? s.identity.name : s.creature.name;
                File.AppendAllText(p, $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}|{subject}|CUSTOM {r.name} [{r.id}]|{r.description}|dose {dose:0.00}|hp {s.creature.maxHealth:0}\n");
            }
            catch (System.Exception e) { Log("research log: " + e.Message); }
        }

        /// Custom syringes already in the world pick up a changed recipe (colour / filled state).
        static void RefreshCustomDevices()
        {
            foreach (var d in devices.Values)
            {
                if (d.custom < 0 || d.state == DevState.Injecting || d.item == null) continue;
                var r = recipes[d.custom];
                if (d.liquid != null) d.liquid.GetComponent<MeshRenderer>().sharedMaterial = Mats.Make(r != null ? r.color : Color.white, 0.9f);
                if (d.state != DevState.Empty || r != null) { d.state = r != null && d.state != DevState.Empty ? DevState.Full : d.state; }
            }
        }
    }
}

// Rage Engine 0.25.3 - your Mod Options are remembered.
// Every Rage Engine option you change (toggles, sliders, lists) is written to RageEngine_settings.txt in the mod
// folder a few seconds later, and put back the next time the game loads - through the game's own ModOption.Apply,
// so the menu shows the saved value too. Buttons are never saved or replayed.
using System.Collections.Generic;
using System.IO;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static ModManager.ModData myModData;
        static bool settingsApplied;
        static float settingsReadyAt = -1f, nextSettingsCheck;
        static string settingsSnapshot;

        static string SettingsPath => string.IsNullOrEmpty(modFolder) ? null : Path.Combine(modFolder, "RageEngine_settings.txt");
        static string OptKey(ModOption o) => (o.category ?? "") + "|" + (o.name ?? "");

        static List<ModOption> MyOptions()
        {
            var l = new List<ModOption>();
            if (myModData == null || myModData.modOptions == null) return l;
            foreach (var o in myModData.modOptions)
                if (o != null && o.interactionType != ModOption.InteractionType.ButtonList && !string.IsNullOrEmpty(o.name)) l.Add(o);
            return l;
        }

        static string Snapshot(List<ModOption> opts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var o in opts) sb.Append(OptKey(o)).Append('=').Append(o.currentValueIndex).Append('\n');
            return sb.ToString();
        }

        /// Per frame (cheap): first apply the saved file once the game has built the options, then watch for changes.
        static void TickSettingsSave()
        {
            if (myModData == null || myModData.modOptions == null || myModData.modOptions.Count == 0) return;
            float now = Time.unscaledTime;
            if (!settingsApplied)
            {
                if (settingsReadyAt < 0f) { settingsReadyAt = now + 2f; return; }       // let the game finish its own load
                if (now < settingsReadyAt) return;
                settingsApplied = true;
                ApplySavedSettings();
                settingsSnapshot = Snapshot(MyOptions());
                nextSettingsCheck = now + 3f;
                return;
            }
            if (now < nextSettingsCheck) return;
            nextSettingsCheck = now + 3f;
            var opts = MyOptions();
            string snap = Snapshot(opts);
            if (snap == settingsSnapshot) return;
            settingsSnapshot = snap;
            try { var p = SettingsPath; if (p != null) File.WriteAllText(p, "# Rage Engine settings - written by the mod, safe to delete (resets everything to default)\n" + snap); }
            catch (System.Exception e) { Debug.Log("[RageEngine] settings save failed: " + e.Message); }
        }

        static void ApplySavedSettings()
        {
            var p = SettingsPath;
            if (p == null || !File.Exists(p)) { Debug.Log("[RageEngine] no saved settings yet"); return; }
            var saved = new Dictionary<string, int>();
            foreach (var line in File.ReadAllLines(p))
            {
                if (line.StartsWith("#")) continue;
                int eq = line.LastIndexOf('=');
                if (eq > 0 && int.TryParse(line.Substring(eq + 1), out int idx)) saved[line.Substring(0, eq)] = idx;
            }
            int n = 0;
            foreach (var o in MyOptions())
            {
                if (!saved.TryGetValue(OptKey(o), out int idx) || idx == o.currentValueIndex || idx < 0) continue;
                try { o.Apply(idx); o.RefreshUI(); n++; }
                catch (System.Exception e) { Debug.Log($"[RageEngine] setting '{o.name}' not restored: {e.Message}"); }
            }
            try { ModManager.RefreshModOptionsUI(); } catch { }
            Debug.Log($"[RageEngine] restored {n} saved setting(s) from RageEngine_settings.txt");
        }
    }
}

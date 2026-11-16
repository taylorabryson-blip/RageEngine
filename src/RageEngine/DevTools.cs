// Rage Engine 0.16 - developer mode. Off by default; the buttons do nothing until it's switched on.
//   Regenerate nearby NPCs : full health, every Rage Engine injury / tag / muscle / eye / bruise / gore state reset, dead
//                            ones brought back (cut-off limbs stay off - the game can't re-attach them), then they get up.
//   Log NPC conditions     : writes each nearby NPC's behavior, tags and capabilities to Player.log.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        public static bool developerMode = false;

        public static ModOptionString[] runButton = { new ModOptionString("Run", null, "run") };

        public static void RegenerateButton(string _) => Guard("dev", () => instance?.Regenerate());

        public static void LogConditionsButton(string _) => Guard("dev", () =>
        {
            if (!developerMode) { Debug.Log("[RageEngine] developer mode is off"); return; }
            if (instance == null) return;
            foreach (var s in instance.pool) if (s.active && s.creature != null) Debug.Log("[RageEngine] " + Describe(s));
        });

        void Regenerate()
        {
            if (!developerMode) { Debug.Log("[RageEngine] developer mode is off - regenerate ignored"); return; }
            var me = Player.local != null ? Player.local.creature : null;
            Vector3 at = me != null ? me.transform.position : Vector3.zero;
            int n = 0;
            var list = Creature.allActive;
            if (list == null) return;
            foreach (var c in list.ToArray())
            {
                if (c == null || c.isPlayer || (me != null && Vector3.Distance(c.transform.position, at) > 15f)) continue;
                if (byCreature.TryGetValue(c, out var s)) Release(s);                // puts the body back exactly as it was
                Guard("regen", () =>
                {
                    if (c.isKilled) c.Resurrect(c.maxHealth, null);
                    else c.Heal(c.maxHealth);
                    if (c.brain.instance == null || !c.brain.instance.isActive) c.brain.Load(c.data.brainId);
                    if (c.ragdoll.state != Ragdoll.State.Standing) c.ragdoll.SetState(Ragdoll.State.Destabilized);   // gets up
                    c.locomotion.allowMove = true;
                });
                Track(c);                                                            // fresh state, new profile
                n++;
            }
            Debug.Log($"[RageEngine] regenerated {n} NPC(s)");
        }
    }
}

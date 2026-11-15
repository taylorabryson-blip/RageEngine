// Rage Engine 0.16 - adrenaline profiles. Opt-in per NPC (most NPCs have none and NEVER get adrenaline).
//   Relentless : once, at the killing blow - keeps fighting for a while, then dies.
//   LastStand  : once, at the killing blow - a longer last stand, then dies.
//   Unyielding : once, while alive - a deep stab triggers a rush: ignores the pain, keeps attacking; then exhausted.
// Adrenaline never repairs anything: dead arms stay dead, a pin still holds, legs that are gone stay gone.
using UnityEngine;

namespace RageEngine
{
    internal enum AdrenProfile : byte { None, Relentless, LastStand, Unyielding }

    public sealed partial class RageEngineScript
    {
        static AdrenProfile PickAdrenProfile(NpcState s)
        {
            float chance = Mathf.Clamp01(adrenalineChance * s.personality.adrenaline);
            if (Random.value >= chance) return AdrenProfile.None;
            float r = Random.value;
            return r < 0.5f ? AdrenProfile.Relentless : r < 0.75f ? AdrenProfile.Unyielding : AdrenProfile.LastStand;
        }
    }
}

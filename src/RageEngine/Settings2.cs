// Nomad Rage Engine 0.10 - new Mod Options (with tooltips). Slider defaultValueIndex = (default - start) / step;
// toggles: 0 = off, 1 = on.
using ThunderRoad;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const string C8 = "Rage Engine: Gore & Death";

        // ---------------- physics ----------------
        public static bool forcePhysics = false;

        public static float physicsRange = 8f;

        public static float punchesToDrop = 7f;

        public static bool staggerOnHits = true;

        public static bool blockCheapKnockdowns = true;

        public static bool groundedReactions = true;

        public static bool tougherLimbs = true;

        // ---------------- bones ----------------
        public static bool grabBreaks = true;

        public static bool spineBreaks = true;

        public static float spineBreakForce = 6f;

        // ---------------- gore & death ----------------
        public static bool headCrush = true;

        public static float headCrushForce = 9f;

        public static bool fleshTearing = true;

        public static float fleshTearForce = 6f;

        public static float bluntGoreDamage = 45f;

        public static float goreMaterialMode = 0f;

        public static float maxGoreChunks = 40f;

        public static bool deathEyesClose = true;

        public static bool deathGasp = true;

        // ---------------- general ----------------
        public static bool fairFights = false;

        public static bool leaveDeathsToOtherMods = false;

        // ---------------- fisticuffs ----------------
        const string C9 = "Rage Engine: Fisticuffs";

        public static bool fistsWhenDisarmed = true;

        public static float npcPunchDamage = 6f;

        public static bool pickUpWeapons = true;

        public static bool throwObjects = true;

        public static bool fistGuard = true;
    }
}

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
        [ModOptionCategory(CD, 2), ModOptionOrder(4)]
        [ModOption("Head smashing", "ON: a heavy enough blow (a hammer or mace swung hard, or a hit in slow motion) - or repeated heavy blows to a cracked skull - smashes the head: skull fragments, brain, teeth. OFF: never.", defaultValueIndex = 1)]
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
        [ModOptionCategory(C0, 0), ModOptionOrder(6)]
        [ModOption("Fair fights", "ON: some NPCs see you barehanded and throw their weapon away to fight you with fists. OFF (default): they keep their weapons.", defaultValueIndex = 0)]
        public static bool fairFights = false;

        [ModOptionCategory(C0, 0), ModOptionOrder(5)]
        [ModOption("Leave deaths to other mods", "Turn on if you use a death mod (e.g. Realistic Deaths): Rage Engine stops reviving NPCs (dying moments, adrenaline, surviving dismemberment) and stops shock deaths. Realistic Deaths is detected automatically.", defaultValueIndex = 0)]
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

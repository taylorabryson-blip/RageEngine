// Nomad Rage Engine - in-game settings: Mod Options > Nomad Rage Engine.
// Uses the game's real ModOption attributes. Every option has a tooltip (second ModOption argument).
// defaultValueIndex = position of the default in the option list ((default - min) / step; bools: 0 = off, 1 = on).
using ThunderRoad;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const string C0 = "Rage Engine: General", C1 = "Rage Engine: Physics Reactions", C2 = "Rage Engine: Pain & Bleeding",
                     C3 = "Rage Engine: Bones", C4 = "Rage Engine: Wounds", C5 = "Rage Engine: Dismemberment & Adrenaline",
                     C6 = "Rage Engine: Choking", CD = "Rage Engine: Deaths";

        // ======================= 0. General =======================
        [ModOptionCategory("Rage", 0), ModOptionOrder(0)]
        [ModOption("Rage Engine enabled", "Master switch. Off = NPCs behave like vanilla (the JSON stumble tuning still applies).", defaultValueIndex = 1)]
        public static bool modEnabled = true;

        public static bool personalitiesEnabled = true;

        public static bool awarenessEnabled = true;

        public static float sfxVolume = 0.8f;

        public static bool debugLog = true;

        // ======================= 1. Physics reactions =======================
        public static float reactionStrength = 1.2f;

        public static float muscleLoss = 0.7f;

        public static float balanceLoss = 1f;

        public static float staggerThreshold = 1f;

        public static float recoverySpeed = 1f;

        public static float npcToughness = 1f;

        public static float glassJawChance = 1f;

        public static float playerPunchStrength = 0.6f;

        public static float knockdownForce = 5f;

        [ModOptionCategory("Rage", 0), ModOptionOrder(4)]
        [ModOption("NPC HP", "100 = a normal man. Every HP above 100 makes them tougher too: harder to knock over, shrug off pain, slower to go down. 0 = the game's own health.", defaultValueIndex = 6)]
        [ModOptionSlider, ModOptionFloatValues(0f, 5000f, 50f)]
        public static float npcHealth = 300f;
        static float npcHealthMultiplier = 1f;   // (no longer used: health is absolute now)

        public static bool balanceReflexes = true;

        public static bool stayDownWhenHurt = true;

        public static bool headStuns = false;

        public static bool loseGrip = true;

        public static bool bruising = true;

        public static float bruiseIntensity = 1f;

        // ======================= 2. Pain & bleeding =======================
        public static float painIntensity = 1f;

        public static bool bleedingEnabled = true;

        public static float bleedingRate = 1f;

        public static bool shockDeaths = true;

        public static bool bleedOutKills = true;

        public static bool knockoutKills = true;

        public static float shockSeconds = 45f;

        // ======================= 3. Bones (blunt only) =======================
        public static bool fracturesEnabled = true;

        public static float boneToughness = 1f;

        internal const float breakChance = 0.5f;     // 0.14: breaks scale with force, not a slider

        internal const float openFractureChance = 0.3f;

        public static bool jawBreaks = true;

        public static bool breakHaptics = true;

        // ======================= 4. Wounds =======================
        public static float woundThreshold = 5f;

        // 0.14: clutch time comes from pain (ClutchTime), not a setting

        internal const bool cutArmsDisable = true;   // 0.14: always - a cut tendon/muscle is not a preference

        internal const bool legWoundsDrop = false;   // 0.14: leg strength comes from the cut muscles instead

        // ======================= 5. Dismemberment & adrenaline =======================
        public static bool surviveDismember = true;

        public static float fleeChance = 0.3f;

        public static float adrenalineChance = 0.25f;

        public static float adrenalineSeconds = 6f;

        public static bool dyingMoments = true;

        public static float dyingSeconds = 1.5f;

        public static bool screaming = true;

        // ======================= 6. Choking =======================
        public static float chokeOutSeconds = 7f;

        public static float headCrushEase = 1f;

        public static bool goreTextures = true;

        public static bool groundBlood = true;

        public static bool chokeOutKills = false;

        public static float chokeHaptics = 0.6f;

        public static bool chokeRedEyes = true;
    }
}

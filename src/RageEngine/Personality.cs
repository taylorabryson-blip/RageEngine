// Nomad Rage Engine
// Personality profiles. Pure data: no game calls, safe to unit test.
namespace RageEngine
{
    public enum PersonalityType { Calm, Confident, Nervous, Aggressive, Cautious, Unpredictable }

    public sealed class Personality
    {
        public PersonalityType type;
        public float weight;              // spawn probability weight

        // Recovery (BrainModuleHitReaction.recoverySpeed is multiplied by this)
        public float recoveryMult;
        // Muscle tone: joint force multipliers on the ragdoll (1 = vanilla)
        public float postureStrength;
        // How much muscle tone a hit knocks out (fraction per push level), and regain rate per second
        public float hitWobble;
        public float wobbleRecoveryPerSec;
        // Movement speed multiplier (Locomotion.globalMoveSpeedMultiplier)
        public float moveSpeed;
        // Awareness: hearing distance multiplier and alertedness decay multiplier
        public float hearingMult;
        public float alertDecayMult;
        // Reaction to an ally dying nearby: temporary muscle-tone loss (shaken), 0 = unfazed
        public float witnessShock;
        // Bravery 0..1 (fights on when hurt or dismembered) and adrenaline multiplier
        public float bravery;
        public float adrenaline;

        public static readonly Personality[] All =
        {
            new Personality { type = PersonalityType.Calm,          weight = 0.25f, recoveryMult = 1.00f, postureStrength = 1.00f, hitWobble = 0.20f, wobbleRecoveryPerSec = 0.35f, moveSpeed = 1.00f, hearingMult = 1.00f, alertDecayMult = 1.00f, witnessShock = 0.05f, bravery = 0.55f, adrenaline = 1.0f },
            new Personality { type = PersonalityType.Confident,     weight = 0.20f, recoveryMult = 1.15f, postureStrength = 1.10f, hitWobble = 0.15f, wobbleRecoveryPerSec = 0.45f, moveSpeed = 1.05f, hearingMult = 0.90f, alertDecayMult = 1.20f, witnessShock = 0.00f, bravery = 0.75f, adrenaline = 1.2f },
            new Personality { type = PersonalityType.Nervous,       weight = 0.15f, recoveryMult = 0.85f, postureStrength = 0.92f, hitWobble = 0.30f, wobbleRecoveryPerSec = 0.25f, moveSpeed = 1.08f, hearingMult = 1.30f, alertDecayMult = 0.50f, witnessShock = 0.30f, bravery = 0.15f, adrenaline = 0.6f },
            new Personality { type = PersonalityType.Aggressive,    weight = 0.15f, recoveryMult = 1.25f, postureStrength = 1.05f, hitWobble = 0.18f, wobbleRecoveryPerSec = 0.50f, moveSpeed = 1.10f, hearingMult = 1.00f, alertDecayMult = 1.40f, witnessShock = 0.00f, bravery = 0.9f, adrenaline = 1.6f },
            new Personality { type = PersonalityType.Cautious,      weight = 0.15f, recoveryMult = 0.95f, postureStrength = 1.00f, hitWobble = 0.22f, wobbleRecoveryPerSec = 0.30f, moveSpeed = 0.92f, hearingMult = 1.20f, alertDecayMult = 0.60f, witnessShock = 0.15f, bravery = 0.35f, adrenaline = 0.8f },
            new Personality { type = PersonalityType.Unpredictable, weight = 0.10f, recoveryMult = 1.00f, postureStrength = 1.00f, hitWobble = 0.25f, wobbleRecoveryPerSec = 0.40f, moveSpeed = 1.00f, hearingMult = 1.10f, alertDecayMult = 1.00f, witnessShock = 0.20f, bravery = 0.5f, adrenaline = 1.3f },
        };

        /// <summary>Weighted pick. r must be in [0,1).</summary>
        public static Personality Pick(float r)
        {
            float total = 0f;
            for (int i = 0; i < All.Length; i++) total += All[i].weight;
            float t = r * total;
            for (int i = 0; i < All.Length; i++)
            {
                t -= All[i].weight;
                if (t < 0f) return All[i];
            }
            return All[0];
        }
    }
}

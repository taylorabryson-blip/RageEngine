// Rage Engine 0.55 - random NPC stats.
//
// Each NPC rolls how much of a fighter he is when he's first seen: Weak (15%), Normal (55%), Tough (22%) or Elite (8%).
// The roll sets his health (base x 0.45 to x3), a little of his speed and size, and nudges his skill; elites are
// always brave. By default this is only in Crystal Hunt dungeons, so the run's people aren't all the same.
// Mixed waves keep their own per-NPC health (they set it themselves).
using ThunderRoad;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RageEngine
{
    public partial class RageEngineScript
    {
        public static ModOptionString[] varyValues55 = { new ModOptionString("Dungeons", null, "dungeons"), new ModOptionString("Everywhere", null, "all"), new ModOptionString("Off", null, "off") };
        [ModOptionCategory("NPCs", 2), ModOptionOrder(50)]
        [ModOption("Random NPC stats", "Each NPC rolls Weak, Normal, Tough or Elite: his health (about half to three times), a little of his speed, size and skill. Elites never run. Dungeons: only in Crystal Hunt dungeons.", valueSourceName = nameof(varyValues55), defaultValueIndex = 0)]
        public static string vary55 = "dungeons";

        static readonly object VaryHandler55 = new object();
        static readonly string[] VaryNames55 = { "", "Weak", "Normal", "Tough", "Elite" };
        static float dungeonAt55 = -10f; static bool dungeonNow55;

        /// Level.IsDungeon, asked at most every 2 seconds.
        static bool InDungeon55()
        {
            float now = Time.time;
            if (now - dungeonAt55 > 2f || now < dungeonAt55) { dungeonAt55 = now; bool d = false; Guard("dungeon55", () => d = Level.IsDungeon); dungeonNow55 = d; }
            return dungeonNow55;
        }

        /// Called while tracking a new NPC, after his skill is rolled and before his health is set.
        void Vary55(NpcState s)
        {
            s.vary55 = 0;
            if (vary55 == "off" || s.hpOverride > 0f) return;
            if (vary55 == "dungeons" && !InDungeon55()) return;
            var c = s.creature; if (c == null || c.isPlayer) return;
            float r = Random.value, f;
            int roll = r < 0.15f ? 1 : r < 0.70f ? 2 : r < 0.92f ? 3 : 4;
            f = roll == 1 ? Random.Range(0.45f, 0.75f) : roll == 2 ? Random.Range(0.8f, 1.25f) : roll == 3 ? Random.Range(1.3f, 1.9f) : Random.Range(2f, 3f);
            float baseHp = npcHealth;
            if (baseHp <= 0f) Guard("vary.base", () => { c.healthModifier.Remove(HealthHandler); baseHp = c.maxHealth; });
            if (baseHp <= 1f) return;
            s.vary55 = roll; s.varyHp55 = f;
            s.hpOverride = Mathf.Clamp(baseHp * f, 5f, 5000f);
            // speed and size: small, so nobody looks or moves wrong
            float sp = roll == 1 ? Random.Range(0.88f, 0.98f) : roll == 2 ? Random.Range(0.95f, 1.05f) : roll == 3 ? Random.Range(0.98f, 1.08f) : Random.Range(1.02f, 1.15f);
            s.varySpeed55 = sp;
            Guard("vary.speed", () => c.locomotion.globalMoveSpeedMultiplier.Add(VaryHandler55, sp));
            float sz = roll == 1 ? Random.Range(0.93f, 0.98f) : roll == 2 ? Random.Range(0.97f, 1.03f) : roll == 3 ? Random.Range(1.02f, 1.06f) : Random.Range(1.04f, 1.08f);
            Guard("vary.size", () => { float h = c.GetHeight(); if (h > 0.5f) { s.varyHeight55 = h; c.SetHeight(h * sz); } });
            // skill: weak ones are clumsier, elites sharper (still his own roll underneath)
            float dk = roll == 1 ? -0.15f : roll == 3 ? 0.08f : roll == 4 ? 0.18f : 0f;
            s.prof43 = Mathf.Clamp01(s.prof43 + dk);
            s.defSkill = Mathf.Clamp01(s.defSkill + dk * 0.8f);
            if (roll == 4) s.brave = true;
            Log($"{c.name}: rolled {VaryNames55[roll]} - health x{f:0.00} ({s.hpOverride:0} HP), speed x{sp:0.00}, size x{sz:0.00}");
        }

        /// On release: his speed and size back to what the game gave this body.
        static void VaryRelease55(NpcState s)
        {
            var c = s.creature; if (c == null) return;
            Guard("vary.speed", () => c.locomotion.globalMoveSpeedMultiplier.Remove(VaryHandler55));
            if (s.varyHeight55 > 0.5f && !c.isKilled) { float h = s.varyHeight55; Guard("vary.size", () => c.SetHeight(h)); }
            s.varyHeight55 = 0f; s.vary55 = 0;
        }

        static string VaryLabel55(NpcState s) => s != null && s.vary55 > 0 && s.vary55 < VaryNames55.Length ? VaryNames55[s.vary55] : null;
    }
}

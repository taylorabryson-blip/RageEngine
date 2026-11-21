// Nomad Rage Engine - choke-outs: grab an NPC by the neck (or head) and hold.
//   - they sag as the air runs out; their eyes go red; they choke (game voice) and late on gurgle with blood at the mouth
//   - your controller buzzes with their struggle; the buzz fades as they go limp, then stops
//   - after "Choke-out time" they pass out (and come round later) or die, depending on the setting
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const float KnockoutSeconds = 12f;
        static readonly Color ScleraNormal = new Color(0.92f, 0.89f, 0.86f);
        static readonly Color ScleraBloodshot = new Color(0.85f, 0.12f, 0.1f);

        static RagdollHand PlayerHandOnThroat(Creature c)
        {
            var hands = c.ragdoll != null ? c.ragdoll.handlers : null;
            if (hands == null) return null;
            for (int i = 0; i < hands.Count; i++)
            {
                var h = hands[i];
                if (h == null || h.creature == null || !h.creature.isPlayer) continue;
                var hr = h.grabbedHandle as HandleRagdoll;
                if (hr == null || hr.ragdollPart == null || hr.ragdollPart.ragdoll != c.ragdoll) continue;
                if (Has(hr.ragdollPart.type, RagdollPart.Type.Neck | RagdollPart.Type.Head)) return h;
            }
            return null;
        }

        /// 0.50: how many of your hands are on his throat: grips on the neck or head, or a grip on his upper chest / jaw
        /// with your hand right at his neck (the second hand often lands there when the first already holds the neck).
        static int PlayerHandsOnThroat50(Creature c, RagdollHand first)
        {
            var hands = c.ragdoll != null ? c.ragdoll.handlers : null;
            if (hands == null || first == null) return 1;
            var neck = PartOf(c, RagdollPart.Type.Neck);
            int n = 1;
            for (int i = 0; i < hands.Count; i++)
            {
                var h = hands[i];
                if (h == null || h == first || h.creature == null || !h.creature.isPlayer) continue;
                var hr = h.grabbedHandle as HandleRagdoll;
                if (hr == null || hr.ragdollPart == null || hr.ragdollPart.ragdoll != c.ragdoll) continue;
                if (Has(hr.ragdollPart.type, RagdollPart.Type.Neck | RagdollPart.Type.Head)) { n = 2; break; }
                if (neck != null && (h.transform.position - neck.transform.position).sqrMagnitude < 0.13f * 0.13f) { n = 2; break; }
            }
            return n;
        }

        static void SetSclera(Creature c, Color col) => Guard("eyes", () => c.SetColor(col, Creature.ColorModifier.EyesSclera, true));

        void TickChoke(NpcState s, float dt)
        {
            var c = s.creature;
            if (s.knockedOutUntil > 0f)
            {
                if (c.isKilled) { s.knockedOutUntil = 0f; return; }
                if (Time.time >= s.knockedOutUntil)
                {
                    s.knockedOutUntil = 0f; s.chokeTime = 0f;
                    if (chokeRedEyes) SetSclera(c, s.scleraOrig);
                    Guard("brain", () => c.brain.Load(c.data.brainId));    // comes round; the game handles getting up
                    Log($"{c.name} came round");
                }
                return;
            }
            if (c.isKilled) return;

            var hand = PlayerHandOnThroat(c);
            if (hand == null)
            {
                if (s.chokeTime > 0f)
                {
                    s.chokeTime = Mathf.Max(0f, s.chokeTime - dt * 2f);       // they get their breath back
                    if (chokeRedEyes && s.chokeTime <= 0f) SetSclera(c, s.scleraOrig);
                }
                return;
            }

            bool starting = s.chokeTime <= 0f;
            int handsOn = PlayerHandsOnThroat50(c, hand);
            s.chokeTime += dt * (handsOn >= 2 ? 1.8f : 1f);                                      // 0.50: two hands choke much faster
            float k = Mathf.Clamp01(s.chokeTime / chokeOutSeconds);
            s.balance = Mathf.Max(MinBalance, Mathf.Min(s.balance, 1f - 0.7f * k));
            s.pain = Mathf.Max(s.pain, k);
            if (chokeRedEyes) SetSclera(c, Color.Lerp(s.scleraOrig, ScleraBloodshot, Mathf.SmoothStep(0f, 1f, k)));
            if (starting) Speak(s, "choke");

            // their struggle in your hand: strong and jerky, fading to a faint twitch, then nothing
            if (chokeHaptics > 0f)
            {
                float struggle = k < 0.55f ? 1f : Mathf.Lerp(1f, 0.1f, (k - 0.55f) / 0.45f);
                float jitter = 0.55f + 0.45f * Mathf.PerlinNoise(Time.time * 7f, (c.GetHashCode() % 997) * 0.01f);
                Haptics.Pulse(hand.side, chokeHaptics * struggle * jitter, 1);
                if (handsOn >= 2) Haptics.Pulse(hand.side == Side.Left ? Side.Right : Side.Left, chokeHaptics * struggle * jitter, 1);
            }

            if (k > 0.35f && Time.time >= s.nextChokeFx)
            {
                s.nextChokeFx = Time.time + Random.Range(1.4f, 2.4f);
                var mouth = Mouth(c);
                if (k < 0.7f) Speak(s, "choke");
                else { Speak(s, "choke"); RageEngineScript.Blood(mouth); }
            }

            if (k >= 1f)
            {
                Speak(s, "breathlessdeath");
                if (chokeOutKills) { s.finalKill = true; c.Kill(); }
                else
                {
                    Guard("brain", () => c.brain.Stop());
                    { AllowFallAlways(s, 1.5f); Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized)); }
                    s.knockedOutUntil = Time.time + KnockoutSeconds;
                }
                Log($"{c.name} choked out");
            }
        }
    }
}

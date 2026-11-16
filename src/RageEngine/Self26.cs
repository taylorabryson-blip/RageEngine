// Rage Engine 0.26 - inject yourself.
// Hold a full syringe / injector, put the needle against your own body (other arm, chest, thigh - anywhere but the
// hand holding it) and squeeze the TRIGGER. The plunger goes down over 0.8 s; let go early = a smaller dose.
//   Super serums   Night Soldier / Hero X / Phoenix regenerate you, Titan / Colossus make you hit like a truck and
//                  shrug off damage, Phantom / Adrenal Overload make you fast, and so on (table below).
//   Serums         the nasty ones are nasty for you too: slowed, hurt over time (never below 10% HP).
//   While it works a glowing marker in the serum's colour floats over each of your hands. About 75 s per full dose.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        public static bool selfInject = true;

        // regen HP/s, damage shrugged (0-1), move speed, hit power, damage/s
        static readonly float[,] SelfFx =
        {
            { 0f, 0f, 0.6f, 1f, 2f },        // Neurostatic
            { 0f, 0f, 0.85f, 1f, 1.5f },     // Verdant Syndrome
            { 0f, 0f, 0.5f, 1f, 0.5f },      // Cryo-Sleep
            { 0f, 0.1f, 1.25f, 1.3f, 0f },   // Adrenal Surge
            { 0f, 0f, 0.85f, 1f, 0.5f },     // Hallucinatory Syndrome
            { 0f, 0f, 0.4f, 0.8f, 1f },      // Motor Shutdown
            { 3f, 0.4f, 1.1f, 1.4f, 0f },    // Night Soldier
            { 5f, 0.6f, 1.15f, 1.8f, 0f },   // Hero X
            { 0f, 0.3f, 1.4f, 1.5f, 0f },    // Adrenal Overload
            { 0f, 0.7f, 0.9f, 2.2f, 0f },    // Titan
            { 0f, 0f, 1.5f, 1.2f, 0f },      // Phantom
            { 8f, 0.2f, 1f, 1f, 0f },        // Phoenix
            { 0f, 0.2f, 1.2f, 1.8f, 0f },    // Ravager
            { 1f, 0.3f, 1.15f, 1.2f, 0f },   // Overmind
            { 0f, 0.75f, 0.85f, 2.5f, 0f },  // Colossus
        };

        static readonly object SelfHandler = new object();
        static readonly Collider[] selfHits = new Collider[24];
        static Device selfDev; static float selfT0;
        static Creature selfCreature;
        static float selfUntil, selfRegen, selfResist, selfSpeed = 1f, selfDot, selfLastHp, selfNextTick;
        internal static float selfPower = 1f;
        static bool selfSpeedOn, selfRebirth; static int selfComp = -1; static Color selfColor;
        static GameObject selfMarkL, selfMarkR;

        /// Every frame.
        void TickSelf()
        {
            var pl = Player.local; var pc = pl != null ? pl.creature : null;
            float now = Time.time;
            if (pc == null || pc != selfCreature || pc.isKilled)
            {
                if (selfCreature != null || selfUntil > 0f) EndSelf(null);
                selfCreature = pc; selfDev = null;
                if (pc == null || pc.isKilled) return;
            }
            if (selfInject) Guard("self.inject", () => SelfInjection(pc, now));
            if (selfUntil <= 0f) return;
            if (now >= selfUntil) { EndSelf(pl); Log("your serum has worn off"); return; }
            // shrug off part of any damage taken since the last frame
            float hp = pc.currentHealth;
            if (selfResist > 0f && hp < selfLastHp - 0.01f && hp > 0f) { float back = (selfLastHp - hp) * selfResist; Guard("self.resist", () => pc.Heal(back)); hp += back; }
            selfLastHp = hp;
            if (now >= selfNextTick)
            {
                selfNextTick = now + 0.5f;
                if (selfRegen > 0f && hp < pc.maxHealth) Guard("self.regen", () => pc.Heal(selfRegen * 0.5f));
                if (selfDot > 0f && hp > pc.maxHealth * 0.1f) Guard("self.dot", () => pc.Damage(Mathf.Min(selfDot * 0.5f, hp - pc.maxHealth * 0.1f)));
                if (selfRebirth && hp < pc.maxHealth * 0.15f)
                {
                    selfRebirth = false;
                    Guard("self.rebirth", () => pc.Heal(pc.maxHealth * 0.6f));
                    Sfx.PlayAt("NRE_SerumSurge", pc.transform.position, 1f);
                    Haptics.Pulse(Side.Left, 1f, 3); Haptics.Pulse(Side.Right, 1f, 3);
                    Log("PHOENIX - you get a second wind");
                }
                selfLastHp = pc.currentHealth;
            }
            // markers over the hands
            if (bioIndicators)
            {
                float left = selfUntil - now;
                float pulse = (left < 8f ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(now * 8f)) : 1f + 0.2f * Mathf.Sin(now * 5f));   // flickers when it's about to run out
                SelfMark(ref selfMarkL, pc.handLeft, pulse); SelfMark(ref selfMarkR, pc.handRight, pulse);
            }
        }

        static void SelfMark(ref GameObject go, RagdollHand hand, float pulse)
        {
            if (hand == null) return;
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(go.GetComponent<Collider>());
                go.name = "RageEngine_SelfMark";
                go.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(new Color(selfColor.r, selfColor.g, selfColor.b, 1f));
            }
            go.transform.position = hand.transform.position + Vector3.up * 0.09f;
            go.transform.localScale = Vector3.one * 0.03f * pulse;
            var cam = Camera.main;
            if (cam != null) go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
        }

        void SelfInjection(Creature pc, float now)
        {
            if (selfDev != null)
            {
                var d = selfDev;
                bool held = d.item != null && d.item.mainHandler != null && d.item.mainHandler.creature == pc;
                bool trig = false;
                if (held) { var hh = d.item.mainHandler; trig = hh.playerHand != null && hh.playerHand.controlHand != null && hh.playerHand.controlHand.usePressed; }
                float p = Mathf.Clamp01((now - selfT0) / 0.8f);
                bool touching = held && NeedleOnSelf(pc, d, d.item.mainHandler) != null;
                SetFill(d, 1f - p);
                if (p >= 1f || !trig || !touching)
                {
                    d.state = DevState.Empty; selfDev = null;
                    int comp = d.comp, custom = d.custom, special = d.special;
                    d.comp = -1;
                    if (p > 0.1f) ApplySelf(pc, comp, custom, special, p);
                    Log($"self-injection {(p >= 0.99f ? "complete" : $"stopped at {p * 100f:0}%")}");
                }
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                var h = i == 0 ? pc.handLeft : pc.handRight;
                if (h == null || h.grabbedHandle == null || h.grabbedHandle.item == null) continue;
                if (!devices.TryGetValue(h.grabbedHandle.item, out var d) || d.kind == 4 || d.state != DevState.Full) continue;
                if (d.comp < 0 && d.special == 0 && (d.custom < 0 || recipes[d.custom] == null)) continue;
                if (h.playerHand == null || h.playerHand.controlHand == null || !h.playerHand.controlHand.usePressed) continue;
                var part = NeedleOnSelf(pc, d, h);
                if (part == null) continue;
                selfDev = d; selfT0 = now; d.state = DevState.Injecting;
                Sfx.Play("NRE_Inject", d.item.transform, 1f);
                Haptics.Pulse(h.side, 0.6f, 2);
                Log($"needle in your own {part.type} - injecting");
                return;
            }
        }

        /// The body part of YOURS the needle tip is touching (never the arm that holds the syringe).
        static RagdollPart NeedleOnSelf(Creature pc, Device d, RagdollHand holder)
        {
            if (d.root == null || pc.ragdoll == null) return null;
            Vector3 tip = d.root.position;
            var skipArm = holder.side == Side.Left ? RagdollPart.Type.LeftArm | RagdollPart.Type.LeftHand : RagdollPart.Type.RightArm | RagdollPart.Type.RightHand;
            int n = Physics.OverlapSphereNonAlloc(tip, 0.045f, selfHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var part = selfHits[i] != null ? selfHits[i].GetComponentInParent<RagdollPart>() : null;
                if (part == null || part.ragdoll != pc.ragdoll || Has(part.type, skipArm)) continue;
                return part;
            }
            // fallback: no body collider there (first-person bodies are thin) - close to the chest / other forearm counts
            foreach (var part in pc.ragdoll.parts)
            {
                if (part == null || Has(part.type, skipArm) || Has(part.type, RagdollPart.Type.Head)) continue;
                float r = Has(part.type, RagdollPart.Type.Torso) ? 0.17f : 0.08f;
                if ((part.transform.position - tip).sqrMagnitude < r * r) return part;
            }
            return null;
        }

        void ApplySelf(Creature pc, int comp, int custom, int special, float dose)
        {
            EndSelf(Player.local);
            float regen, resist, speed, power, dot; string name; Color col;
            if (special == 1) { Log("the Loyalty Serum does nothing to you - you're already on your side"); return; }
            if (custom >= 0 && recipes[custom] != null)
            {
                var r = recipes[custom];
                regen = 1.5f; resist = 0.2f; speed = 1.15f; power = 1.3f; dot = 0f; name = "custom serum " + r.name; col = r.color; comp = -1;
            }
            else if (comp >= 0 && comp < (int)Comp.Count)
            {
                regen = SelfFx[comp, 0]; resist = SelfFx[comp, 1]; speed = SelfFx[comp, 2]; power = SelfFx[comp, 3]; dot = SelfFx[comp, 4];
                name = CompName[comp]; col = CompLiquid[comp];
            }
            else return;
            float k = Mathf.Clamp01(dose);
            selfRegen = regen * k; selfResist = resist * k; selfSpeed = Mathf.Lerp(1f, speed, k); selfPower = Mathf.Lerp(1f, power, k); selfDot = dot * k;
            selfComp = comp; selfColor = col; selfRebirth = comp == (int)Comp.Phoenix;
            selfUntil = Time.time + 75f * Mathf.Max(0.3f, k);
            selfLastHp = pc.currentHealth; selfNextTick = Time.time + 0.5f;
            var pl = Player.local;
            if (Mathf.Abs(selfSpeed - 1f) > 0.01f && pl != null && pl.locomotion != null)
                Guard("self.speed", () => { pl.locomotion.globalMoveSpeedMultiplier.Add(SelfHandler, selfSpeed); selfSpeedOn = true; });
            Sfx.PlayAt("NRE_SerumSurge", pc.transform.position, 1f);
            Haptics.Pulse(Side.Left, 0.9f, 3); Haptics.Pulse(Side.Right, 0.9f, 3);
            var chest = ChestPart(pc);
            if (bioIndicators) Guard("self.puff", () => Puff(chest != null ? chest.transform.position + pc.transform.forward * 0.25f : pc.transform.position + Vector3.up * 1.2f, col, 20, 0.4f));
            Debug.Log($"[RageEngine] YOU are on {name}: regen {selfRegen:0.0}/s, shrug {selfResist * 100f:0}%, speed x{selfSpeed:0.00}, power x{selfPower:0.00}{(selfDot > 0f ? $", hurting {selfDot:0.0}/s" : "")}, {selfUntil - Time.time:0}s");
        }

        static void EndSelf(Player pl)
        {
            if (selfSpeedOn && pl != null && pl.locomotion != null) Guard("self.speed.off", () => pl.locomotion.globalMoveSpeedMultiplier.Remove(SelfHandler));
            selfSpeedOn = false;
            selfUntil = 0f; selfRegen = selfResist = selfDot = 0f; selfSpeed = 1f; selfPower = 1f; selfComp = -1; selfRebirth = false;
            if (selfMarkL != null) Object.Destroy(selfMarkL);
            if (selfMarkR != null) Object.Destroy(selfMarkR);
            selfMarkL = selfMarkR = null;
        }
    }
}

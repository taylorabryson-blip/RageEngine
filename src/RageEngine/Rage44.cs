// Rage Engine 0.44 - from the 0.41 playtest:
//   STYLE          the presets really change things now: the menu sliders move to show them, NPCs already out there
//                  are re-done (HP, toughness) keeping how hurt they are, moving a slider yourself switches to Custom.
//   INJURY EFFECTS new setting (Off / Light / Medium / Heavy, default Medium): the more he's been hurt - HP lost, broken
//                  bones, bleeding, ribs, eyes - the slower he moves, the slower and later he swings and the worse he
//                  parries. It builds up gradually, so a beaten man fights like one.
//   KNOCKDOWNS     the lower his HP, the more likely he drops; strong-willed men can power through power shots while
//                  they've still got something left.
//   VOMITING       he stops, bends over with a hand on his stomach and it comes up toward the floor; the chunks and the
//                  puddle land on the floor (they used to catch on his own body and hang in the air); the retching and
//                  splashing are body sounds now, not voices, so turning Mod voices off no longer mutes them.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    /// A chunk of vomit: flies on a simple arc and stops where it hits the world - never on a body.
    public sealed class VomitBlob44 : MonoBehaviour
    {
        public Vector3 vel; bool landed; float life;
        static readonly RaycastHit[] hits = new RaycastHit[8];
        void Update()
        {
            if (landed) return;
            float dt = Mathf.Min(Time.deltaTime, 0.04f);
            life += dt;
            if (life > 3f) { Destroy(gameObject); return; }
            vel += Physics.gravity * dt;
            Vector3 p = transform.position, step = vel * dt;
            float len = step.magnitude;
            if (len > 0.0001f)
            {
                int n = Physics.RaycastNonAlloc(p, step / len, hits, len + 0.01f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue; int bi = -1;
                for (int i = 0; i < n; i++)
                {
                    var c = hits[i].collider;
                    if (c == null || c.GetComponentInParent<Creature>() != null || c.GetComponentInParent<VomitBlob44>() != null) continue;
                    if (hits[i].distance < best) { best = hits[i].distance; bi = i; }
                }
                if (bi >= 0)
                {
                    var h = hits[bi];
                    landed = true;
                    transform.position = h.point + h.normal * 0.004f;
                    transform.up = h.normal;
                    var s = transform.localScale; transform.localScale = new Vector3(s.x * 1.8f, s.y * 0.25f, s.z * 1.8f);   // splats flat
                    Destroy(gameObject, Random.Range(6f, 12f));
                    return;
                }
            }
            transform.position = p + step;
        }
    }

    public sealed partial class RageEngineScript
    {
        // ------------------------------------------------------------------ settings
        public static ModOptionString[] wearValues = { new ModOptionString("Off", null, "0"), new ModOptionString("Light", null, "1"), new ModOptionString("Medium", null, "2"), new ModOptionString("Heavy", null, "3") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(7)]
        [ModOption("Injury effects", "How much getting hurt slows them down - movement, attacks and blocking get worse gradually as they lose HP, break bones and bleed.", valueSourceName = nameof(wearValues), defaultValueIndex = 2)]
        public static string injuryEffects = "2";
        static float WearK => injuryEffects == "0" ? 0f : injuryEffects == "1" ? 0.5f : injuryEffects == "3" ? 1.6f : 1f;
        static readonly object WearHandler = new object(), VomitHold = new object();

        /// Moves a Rage option in the menu to the value given (and so writes it to the field too).
        static void SetOptionUi44(string name, object value)
        {
            foreach (var o in MyOptions())
            {
                if (o.name != name || o.parameterValues == null) continue;
                int best = -1; double bd = double.MaxValue;
                for (int i = 0; i < o.parameterValues.Length; i++)
                {
                    var v = o.parameterValues[i] != null ? o.parameterValues[i].value : null;
                    if (v == null) continue;
                    if (value is string sv) { if (v.ToString() == sv) { best = i; break; } continue; }
                    double d; try { d = System.Math.Abs(System.Convert.ToDouble(v) - System.Convert.ToDouble(value)); } catch { continue; }
                    if (d < bd) { bd = d; best = i; }
                }
                if (best >= 0 && best != o.currentValueIndex) { o.Apply(best); o.RefreshUI(); }
                return;
            }
        }

        /// The style changed: redo HP and toughness on everyone already out there (keeping how hurt they are).
        void Restyle44()
        {
            int n = 0;
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active || s.creature == null || s.creature.isKilled || s.hpOverride > 0f) continue;
                var c = s.creature;
                float frac = HpFrac(c);
                if (s.hpScaled && s.hpT > 0f) { float r = Mathf.Sqrt(s.hpT); s.toughness /= r; s.painSensitivity *= r; s.balanceStability /= r; s.hpScaled = false; s.hpT = 1f; }
                Guard("restyle", () =>
                {
                    c.healthModifier.Remove(HealthHandler);
                    if (npcHealth > 0f && c.maxHealth > 1f) c.healthModifier.Add(HealthHandler, npcHealth / c.maxHealth);
                    c.currentHealth = Mathf.Max(1f, c.maxHealth * frac);
                });
                s.healthApplied = true;
                Guard("hpscale", () => ScaleByHp(s));
                n++;
            }
            Debug.Log($"[RageEngine] style {style}: {n} NPCs re-done ({npcHealth:0} HP, stay on feet x{stayOnFeet:0.##})");
        }

        // ------------------------------------------------------------------ injury effects (0.1 s, from Tick43)
        void Wear44(NpcState s, float now)
        {
            if (now < s.nextWear44) return;
            s.nextWear44 = now + 0.5f;
            var c = s.creature;
            float K = WearK;
            float target = 0f;
            if (K > 0f && !Tank(s))
            {
                int broken = CountBits((int)s.brokenParts);
                target = Mathf.Clamp01((1f - HpFrac(c)) * 0.6f + broken * 0.15f + Mathf.Min(0.3f, s.bleedRate * 0.3f) + s.ribsBroken * 0.04f
                                       + (s.leftArmDead || s.rightArmDead ? 0.1f : 0f) + (s.blindEyes != 0 ? 0.08f : 0f) + (s.legWounded ? 0.1f : 0f));
                if (s.will44) target *= 0.7f;                                                     // strong will: shows it less
            }
            s.wear44 = Mathf.MoveTowards(s.wear44, target, 0.04f);                              // builds up (and eases) gradually
            float w = s.wear44 * K;
            float spd = Mathf.Clamp(1f - 0.4f * w, 0.45f, 1f);
            if (Mathf.Abs(spd - s.wearSpeed44) > 0.02f || (spd >= 0.999f && s.wearOn44))
            {
                s.wearSpeed44 = spd;
                Guard("wear.speed", () =>
                {
                    c.locomotion.globalMoveSpeedMultiplier.Remove(WearHandler);
                    if (spd < 0.999f) { c.locomotion.globalMoveSpeedMultiplier.Add(WearHandler, spd); s.wearOn44 = true; } else s.wearOn44 = false;
                });
            }
        }

        /// Tempo multipliers from proficiency x injuries (ProfTick43 applies them to the game's melee module).
        static float WearAnim44(NpcState s) => 1f - 0.18f * Mathf.Clamp01(s.wear44 * WearK);
        static float WearDelay44(NpcState s) => 1f + 0.8f * Mathf.Clamp01(s.wear44 * WearK);
        internal static float WearDefense44(NpcState s) => 1f - 0.55f * Mathf.Clamp01(s.wear44 * WearK);

        // ------------------------------------------------------------------ vomiting
        /// The heave: he bends over, hand on his stomach, and it comes up toward the floor.
        void VomitLean44(NpcState s, float now)
        {
            var c = s.creature;
            var chest = ChestPart(c);
            if (c.ragdoll.state != Ragdoll.State.Standing) return;
            Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Neck | RagdollPart.Type.Head, 0.75f, 0.45f);
            Vector3 fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized;
            if (chest != null && chest.physicBody != null) chest.physicBody.AddForce(fwd * 1.1f + Vector3.down * 0.9f, ForceMode.VelocityChange);
            var head = c.ragdoll.headPart;
            if (head != null && head.physicBody != null) head.physicBody.AddForce(fwd * 0.6f + Vector3.down * 1.2f, ForceMode.VelocityChange);
            if (!s.clutching && chest != null) { StartClutch(s, chest, chest.transform.position + fwd * 0.12f - Vector3.up * 0.18f, null, false, false); if (s.clutching) s.clutchUntil = Mathf.Max(s.clutchUntil, s.vomitUntil); }
            SetMelee(s, false, s.vomitUntil + 0.5f);
        }

        static void VomitHoldOn44(NpcState s)
        {
            var c = s.creature;
            Guard("vomit.hold", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(VomitHold); c.locomotion.globalMoveSpeedMultiplier.Add(VomitHold, 0.05f); });
            s.vomitHold44 = true;
        }
        static void VomitHoldOff44(NpcState s)
        {
            if (!s.vomitHold44) return;
            s.vomitHold44 = false;
            var c = s.creature;
            if (c != null) Guard("vomit.hold", () => c.locomotion.globalMoveSpeedMultiplier.Remove(VomitHold));
        }

        /// Puts the puddle on the floor under the stream - never on his own legs.
        static bool FloorBelow44(Vector3 from, out RaycastHit floor)
        {
            floor = default;
            var hits = Physics.RaycastAll(from, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool ok = false;
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.GetComponentInParent<Creature>() != null || h.collider.GetComponentInParent<VomitBlob44>() != null) continue;
                if (h.distance < best) { best = h.distance; floor = h; ok = true; }
            }
            return ok;
        }
    }
}

// Rage Engine 0.26 - toughness, super-soldier durability, fighting from the ground.
//   HP = TOUGHNESS   the more HP an NPC has, the higher his pain threshold: hits register lighter, pain builds slower.
//   SUPER SOLDIERS   anyone on a super serum (Night Soldier, Hero X, Titan ...) is a tank: blunt hits land at a third
//                    of their force and most of the blunt damage is shrugged off, his neck can't be snapped, he almost
//                    never gets knocked out or knocked down, a fist won't go through him. He still takes damage.
//   GROUND FIGHT     an NPC who is down (or can't get up) doesn't just lie there scared: he swings at your legs when
//                    you're close and drags himself after you when you're not. Only the truly terrified give up.
//   BLOOD COUGH      coughing blood from body shots needs real damage: ribs broken on BOTH sides, or a long beating
//                    to the body. One cracked rib no longer does it.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        [ModOptionCategory(C9, 9), ModOptionOrder(40)]
        [ModOption("Downed NPCs keep fighting", "ON: an NPC on the ground swings at your legs and crawls after you instead of just lying there.", defaultValueIndex = 1)]
        public static bool groundFight = true;
        public static bool superTank = true;

        /// On a super serum right now?
        internal static bool Tank(NpcState s)
        {
            if (!superTank || s == null) return false;
            foreach (var f in s.bioFx) if (IsSerum(f.comp)) return true;
            return false;
        }

        /// From HandleHit, before anything reacts: how hard this hit really lands on THIS body.
        float Toughen(NpcState s, CollisionInstance hit, float severity)
        {
            var c = s.creature;
            bool blunt = hit.damageStruct.damageType == DamageType.Blunt;
            // the player on a serum hits harder
            if (selfPower > 1.01f && PlayerSource(hit, out _, out _, out _))
            {
                severity *= selfPower;
                float extra = hit.damageStruct.damage * (selfPower - 1f);
                if (extra > 0.5f && !c.isKilled) Guard("selfpower", () => c.Damage(extra));
            }
            { float sv2 = severity; Guard("punchpower", () => sv2 = PunchPower28(s, hit, sv2)); severity = sv2; }
            // big HP pool = hard man (0.28: gentler - 0.26 made everyone feel like a tank)
            severity *= 1f - (blunt ? 0.3f : 0.12f) * s.hpK;
            if (Tank(s))
            {
                if (blunt)
                {
                    severity *= 0.5f;
                    float back = hit.damageStruct.damage * 0.4f;
                    if (back > 0.1f && !c.isKilled && c.currentHealth > 0f) Guard("tank.heal", () => c.Heal(back));
                }
                else severity *= 0.7f;
                s.pain = Mathf.Min(s.pain, 0.35f);
            }
            return severity;
        }

        /// Blunt gore (skull cracking, flesh tearing) scales with the same toughness.
        static float BluntGoreScale(NpcState s) => Tank(s) ? 0.3f : 1f - 0.3f * s.hpK;

        /// Knockouts mostly bounce off a super soldier.
        bool TankShrug(NpcState s, string what)
        {
            if (!Tank(s) || Random.value < 0.12f) return false;
            if (Time.time >= s.nextShrugLog) { s.nextShrugLog = Time.time + 2f; Log($"{WhoIs(s)}: shrugs off {what} (super soldier)"); }
            return true;
        }

        /// Both sides of the rib cage broken, or beaten to the body long enough.
        static bool LungsHurt(NpcState s) => (s.ribsL > 0 && s.ribsR > 0) || s.bodyShots >= 8;

        void NoteRib(NpcState s, RagdollPart part, Vector3 point)
        {
            var chest = ChestPart(s.creature) ?? part;
            if (Vector3.Dot(point - chest.transform.position, s.creature.transform.right) < 0f) s.ribsL++; else s.ribsR++;
        }

        // ------------------------------------------------------------------ fighting from the ground (0.1 s tick)
        void TickGroundFight(NpcState s, float now)
        {
            if (!groundFight) return;
            var c = s.creature;
            if (c.isKilled || s.ally || s.knockedOut || s.knockedOutUntil > 0f || s.spineBroken || s.paralysed || s.surrendered || s.shocked || s.dying) return;
            if (c.ragdoll.state == Ragdoll.State.Standing) { s.groundSince = 0f; return; }
            if (s.groundSince <= 0f) { s.groundSince = now; s.nextGroundAct = now + Random.Range(1.2f, 2.2f); return; }
            if (now < s.nextGroundAct || s.tox >= 0 && s.toxStage >= 2) return;
            // the truly terrified don't fight back
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null || pc.ragdoll == null) return;
            var chest = ChestPart(c);
            if (chest == null) return;
            // nearest bit of you: feet, hips
            Vector3 from = chest.transform.position;
            Vector3 best = pc.transform.position; float bd = (best - from).sqrMagnitude;
            foreach (var p in pc.ragdoll.parts)
            {
                if (p == null || !Has(p.type, RagdollPart.Type.LeftFoot | RagdollPart.Type.RightFoot | RagdollPart.Type.LeftLeg | RagdollPart.Type.RightLeg | RagdollPart.Type.Torso)) continue;
                float d2 = (p.transform.position - from).sqrMagnitude;
                if (d2 < bd) { bd = d2; best = p.transform.position; }
            }
            float d = Mathf.Sqrt(bd);
            bool flee = (TagIntensity(s, Tag.Fear) > 0.5f && !s.bioNoQuit) || (!HandUsable(s, Side.Left) && !HandUsable(s, Side.Right));
            float fury = Mathf.Clamp01(0.4f + 0.5f * s.trAggression + (HasTag(s, Tag.Anger) ? 0.3f : 0f) + (s.bioNoQuit ? 0.3f : 0f));
            if (flee)
            {
                // 0.28: can't fight, can't run - drags himself AWAY from you
                if (d < 9f)
                {
                    Vector3 away = Vector3.ProjectOnPlane(from - best, Vector3.up).normalized;
                    if (chest.physicBody != null) chest.physicBody.AddForce(away * 1.5f + Vector3.up * 0.3f, ForceMode.VelocityChange);
                    Side cs = (s.crawlFlip = !s.crawlFlip) ? Side.Left : Side.Right;
                    var ch = Hand(c, cs);
                    if (ch != null && ch.physicBody != null && HandUsable(s, cs)) ch.physicBody.AddForce((away + Vector3.up * 0.25f) * 3f, ForceMode.VelocityChange);
                    if (Random.value < 0.2f) Scream(s, false);
                }
                s.nextGroundAct = now + Random.Range(0.7f, 1.1f);
            }
            else if (d < 1.2f)
            {
                // swing at it
                Side side = Random.value < 0.5f ? Side.Left : Side.Right;
                if (!HandUsable(s, side)) side = side == Side.Left ? Side.Right : Side.Left;
                if (!HandUsable(s, side)) { s.nextGroundAct = now + 2f; return; }
                var hand = Hand(c, side);
                if (hand != null && hand.physicBody != null)
                {
                    Vector3 dir = (best - hand.transform.position).normalized;
                    float v = Mathf.Lerp(4.5f, 7.5f, fury);
                    hand.physicBody.AddForce((dir + Vector3.up * 0.15f).normalized * v, ForceMode.VelocityChange);
                    if (chest.physicBody != null) chest.physicBody.AddForce(dir * 1.2f, ForceMode.VelocityChange);
                    s.groundSwingUntil = now + 0.6f;
                    Sfx.PlayAt("NRE_Swoosh", hand.transform.position, 0.5f);
                    if (Random.value < 0.4f) Speak(s, "attackmelee");
                    if (now >= s.nextShrugLog) { s.nextShrugLog = now + 3f; Log($"{WhoIs(s)}: swings at you from the ground"); }
                }
                s.nextGroundAct = now + Mathf.Lerp(2.2f, 0.9f, fury) * Random.Range(0.8f, 1.3f);
            }
            else if (d < 6f && (s.cantStand || now - s.groundSince > 4f))
            {
                // drag himself after you
                Vector3 dir = Vector3.ProjectOnPlane(best - from, Vector3.up).normalized;
                if (chest.physicBody != null) chest.physicBody.AddForce(dir * Mathf.Lerp(1.1f, 2f, fury) + Vector3.up * 0.3f, ForceMode.VelocityChange);
                Side side = (s.crawlFlip = !s.crawlFlip) ? Side.Left : Side.Right;
                var hand = Hand(c, side);
                if (hand != null && hand.physicBody != null && HandUsable(s, side)) hand.physicBody.AddForce((dir + Vector3.up * 0.25f) * 3f, ForceMode.VelocityChange);
                if (Random.value < 0.15f) Speak(s, "attackmelee");
                s.nextGroundAct = now + Random.Range(0.7f, 1.1f);
            }
            else s.nextGroundAct = now + 1f;
        }
    }
}

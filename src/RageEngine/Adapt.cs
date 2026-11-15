// Rage Engine 0.17 - combat adaptation + physical grabs.
// Adaptation (per NPC, per fight - it fades): if you keep going for the head, they start slipping your head shots
// (head ducks aside, a step back). If they're losing the exchange (taking hits, landing none) a nervous one gets
// scared (hesitates, guards more), a brave one gets angry (stops hesitating, throws more).
// Grabs: a grabbed limb goes loose enough to follow your hand (no snapping back), they struggle to tear it free
// and their free hand reaches for your hand to pry it off; when you let go they need a moment to recover.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        /// From HandleHit: remember what the player is doing to this NPC.
        static void NoteHit(NpcState s, RagdollPart.Type t)
        {
            float now = Time.time;
            if (Has(t, RagdollPart.Type.Head | RagdollPart.Type.Neck)) s.headHits += 1f; else if (Has(t, RagdollPart.Type.Torso)) s.bodyHits += 1f;
            if (now - s.exchangeStart > 6f) { s.exchangeStart = now; s.hitsTaken = 0; s.hitsLanded = 0; }
            s.hitsTaken++;
            if (s.hitsTaken >= 4 && s.hitsLanded == 0 && now >= s.nextMoodShift)
            {
                s.nextMoodShift = now + 6f;
                if (s.brave || s.personality.type == PersonalityType.Aggressive) { AddTag(s, Tag.Anger, 10f, 1f); Log($"{WhoIs(s)} is losing the exchange - gets ANGRY"); }
                else { AddTag(s, Tag.Fear, 8f, 1f); Log($"{WhoIs(s)} is losing the exchange - gets scared"); }
            }
        }

        void TickAdapt(NpcState s, float dt)
        {
            var c = s.creature;
            float now = Time.time;
            s.headHits = Mathf.Max(0f, s.headHits - 0.04f * dt);
            s.bodyHits = Mathf.Max(0f, s.bodyHits - 0.04f * dt);
            if (c.isKilled || c.ragdoll.state != Ragdoll.State.Standing) return;
            var pc = Player.local != null ? Player.local.creature : null;
            var head = c.ragdoll.headPart;
            if (pc == null || head == null) return;

            // learned: slip the head shot
            if (s.headHits >= 2.5f && now >= s.nextSlip && CapOf(s, Cap.ContinueCombat) != Ability.Unavailable)
            {
                foreach (var ph in new[] { pc.handLeft, pc.handRight })
                {
                    if (ph == null || ph.physicBody == null) continue;
                    Vector3 v = ph.physicBody.velocity; Vector3 from = ph.transform.position;
                    if (ph.grabbedHandle != null && ph.grabbedHandle.item != null && ph.grabbedHandle.item.physicBody != null)
                    { v = ph.grabbedHandle.item.physicBody.velocity; from = ph.grabbedHandle.item.transform.position; }
                    Vector3 to = head.transform.position - from;
                    if (v.magnitude < 3.5f || to.magnitude > 0.8f || Vector3.Dot(v.normalized, to.normalized) < 0.6f) continue;
                    float chance = Mathf.Min(0.6f, 0.2f + 0.1f * (s.headHits - 2.5f)) * (s.personality.type == PersonalityType.Cautious || s.personality.type == PersonalityType.Confident ? 1.3f : 1f) * Mathf.Lerp(0.6f, 1.4f, s.trAwareness);
                    s.nextSlip = now + 1.2f;
                    if (Random.value > chance) break;
                    Vector3 side = Vector3.Cross(Vector3.up, v).normalized * (Random.value < 0.5f ? -1f : 1f);
                    head.physicBody?.AddForce((side + Vector3.down * 0.3f) * 4f, ForceMode.Impulse);
                    Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso, 0.35f, 0.35f);
                    s.coreUntil = Mathf.Max(s.coreUntil, now + 0.5f);
                    ShoveBody(s, Vector3.ProjectOnPlane(v, Vector3.up), 0.7f);
                    Log($"{WhoIs(s)} slips your head shot (learned)");
                    break;
                }
            }
            // scared: hesitates now and then; angry: never pauses and punches sooner
            if (HasTag(s, Tag.Fear) && now >= s.nextFearPause) { s.nextFearPause = now + Random.Range(2.5f, 5f); SetMelee(s, false, now + Random.Range(0.6f, 1.2f)); }
            if (HasTag(s, Tag.Anger) && s.nextPunch > now + 0.6f) s.nextPunch = now + 0.6f;

            TickGrabbed(s, pc, now);
        }

        void TickGrabbed(NpcState s, Creature pc, float now)
        {
            var c = s.creature;
            RagdollPart held = null; RagdollHand by = null;
            foreach (var ph in new[] { pc.handLeft, pc.handRight })
                if (ph != null && ph.grabbedHandle is HandleRagdoll hr && hr.ragdollPart != null && hr.ragdollPart.ragdoll != null && hr.ragdollPart.ragdoll.creature == c)
                { held = hr.ragdollPart; by = ph; break; }
            if (held == null)
            {
                if (s.grabbedPart != null)
                {
                    s.grabbedPart = null;
                    AddTag(s, Tag.Recovering, 1.2f);
                    if (s.pryAnchor && c.ragdoll.ik != null) { c.ragdoll.ik.SetHandAnchor(s.prySide, null); c.ragdoll.ik.SetHandState(s.prySide, false, false); }
                    s.pryAnchor = false;
                }
                return;
            }
            if (s.grabbedPart != held) { s.grabbedPart = held; s.grabY = held.transform.position.y; s.nextStruggle = now + Random.Range(0.4f, 0.8f); Log($"{WhoIs(s)}: grabbed by the {held.type}"); if (!c.isKilled) Speak(s, "grappled"); }
            // 0.25: THROWS. While they stand, the game's standing ragdoll (plus our planted legs) glues them to the floor.
            // A hard yank, lifting them, or a two-handed grab with a pull (judo) takes them off their feet - the body
            // goes to a real physics fall and carries your hand's momentum.
            if (c.ragdoll.state == Ragdoll.State.Standing && !c.isKilled)
            {
                Loosen(s, LegParts, 0.45f, 0.25f);                                            // 0.25.1: held = not planted
                int hands = 0;
                foreach (var ph in new[] { pc.handLeft, pc.handRight })
                    if (ph != null && ph.grabbedHandle is HandleRagdoll hr2 && hr2.ragdollPart != null && hr2.ragdollPart.ragdoll != null && hr2.ragdollPart.ragdoll.creature == c) hands++;
                Vector3 hv = by.physicBody != null ? by.physicBody.velocity : Vector3.zero;
                float need = Mathf.Lerp(1.4f, 2.0f, s.hpK) * (s.bioNoQuit ? 1.3f : 1f);   // 0.25.1: easier
                bool yank = hv.magnitude > need;
                bool judo = hands >= 2 && hv.magnitude > need * 0.45f;
                bool lifted = held.transform.position.y - s.grabY > 0.15f;
                if (yank || judo || lifted)
                {
                    AllowFall(s, 3f);
                    bool ok = false;
                    Guard("throw", () => { c.ragdoll.SetState(Ragdoll.State.Destabilized); ok = true; });
                    if (ok)
                    {
                        Vector3 carry = Vector3.ClampMagnitude(hv, 6f) * (judo ? 0.7f : 0.5f);
                        foreach (var part in c.ragdoll.parts) if (part != null && part.physicBody != null) part.physicBody.AddForce(carry, ForceMode.VelocityChange);
                        s.balance = Mathf.Max(s.balance, 0.6f);
                        Speak(s, "falling");
                        Log($"{WhoIs(s)}: THROWN ({(judo ? "two-handed" : lifted ? "lifted" : "yanked")}, hand {hv.magnitude:0.0} m/s)");
                    }
                }
            }
            // the held limb follows your hand instead of snapping back to the animation
            var region = Has(held.type, ArmParts) ? LimbOf(held.type) : Has(held.type, RagdollPart.Type.Head | RagdollPart.Type.Neck) ? RagdollPart.Type.Head | RagdollPart.Type.Neck : 0;
            if (region != 0) Loosen(s, region, 0.7f, 0.3f);
            s.heldUntil = now + 0.4f;                                                        // 0.23: other systems stop pushing a body you're holding
            Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Neck, 0.25f, 0.3f);
            if (c.isKilled) return;
            if (now >= s.nextStruggle && !Has(s.limpParts, held.type))
            {
                s.nextStruggle = now + Random.Range(1.0f, 1.8f) * (1f + s.hpK);
                Vector3 pull = by.physicBody != null ? -by.physicBody.velocity : Vector3.zero;
                if (pull.sqrMagnitude < 0.1f) pull = (held.transform.position - by.transform.position);
                held.physicBody?.AddForce(pull.normalized * 1.4f + Random.insideUnitSphere * 0.4f, ForceMode.Impulse);   // 0.23: a tug, not a bounce
                if (Random.value < 0.35f) Scream(s, false);
            }
            // free hand pries at yours
            Side free = Has(held.type, LeftArmParts) ? Side.Right : Has(held.type, RightArmParts) ? Side.Left : (HandUsable(s, Side.Right) ? Side.Right : Side.Left);
            if (!s.pryAnchor && HandUsable(s, free) && !(s.clutching && s.clutchSide == free) && s.punchPhase == 0 && c.ragdoll.ik != null)
            {
                c.ragdoll.ik.SetHandAnchor(free, by.transform);
                c.ragdoll.ik.SetHandState(free, true, true);
                s.pryAnchor = true; s.prySide = free;
            }
        }
    }
}

// Rage Engine 0.15 - hit director. Every blade hit is read for WHERE it landed, WHAT it was (slash or stab, how deep)
// and HOW HARD, and the NPC reacts to that injury like a person instead of playing the game's generic small stagger:
//   face slash      -> head snaps away, spine bends back, they step back, then a hand goes to the face and they fight on
//   throat          -> recoil, both hands to the throat, choking on blood
//   chest stab      -> jolt, hunch around it, gasp/cough blood, hand on it
//   gut stab        -> instant: doubles over the blade, both hands to it, drops what they hold
//   blade left in   -> they writhe and thrash in pain while it's in them (standing or on the ground), and scream when
//                      it comes out
//   arm / leg       -> the limb jerks away; leg buckles with a stagger
//   adrenaline      -> a deep stab can trigger it on the spot: they ignore the pain and keep attacking, unbothered,
//                      until it wears off - then it all hits them at once
// Also: a watchdog that puts an NPC back on the ground if it ever ends up floating (seen after adrenaline revives).
using ThunderRoad;
using UnityEngine;
using Effect = ThunderRoad.BrainModuleHitReaction.PushBehaviour.Effect;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static readonly object RushHandler = new object();

        /// Adrenaline of either kind: revived after a killing blow, or a rush while still alive.
        internal static bool Pumped(NpcState s) => s.onAdrenaline || Time.time < s.rushUntil;

        void DirectReaction(NpcState s, CollisionInstance hit, RagdollPart part, RagdollPart.Type t, float severity)
        {
            var c = s.creature;
            if (c.isKilled || c.ragdoll == null || s.shocked || s.knockedOut) return;
            var ds = hit.damageStruct;
            bool slash = ds.damageType == DamageType.Slash, stab = ds.damageType == DamageType.Pierce;
            if (!slash && !stab) return;
            float dmg = ds.damage;
            bool deep = ds.penetration == DamageStruct.Penetration.Skewer || ds.penetrationDeepReached;
            if (dmg < woundThreshold * 0.5f && !deep) return;
            float now = Time.time;
            if (now < s.nextDirected) return;
            s.nextDirected = now + 0.45f;

            Vector3 point = hit.contactPoint;
            Vector3 away = Away(s, hit);
            float k = Mathf.Clamp01(dmg / Mathf.Max(1f, woundThreshold * 4f) + (deep ? 0.35f : 0f));   // how bad, 0..1
            // 0.17: what went in matters. A dagger is a quick sharp shank (flinch, pull away, keep going); a spear is a
            // heavy thrust that drives them back and can take them off their feet; arrows jolt.
            var weapon = hit.sourceCollider != null ? hit.sourceCollider.GetComponentInParent<Item>() : null;
            var kind = KindOf(weapon);
            if (stab && kind == WeaponKind.Dagger) k *= 0.65f;
            else if (stab && kind == WeaponKind.Spear) k = Mathf.Clamp01(k * 1.35f + 0.1f);
            else if (stab && kind == WeaponKind.Missile) k *= 0.5f;
            bool standing = c.ragdoll.state == Ragdoll.State.Standing;
            bool head = Has(t, RagdollPart.Type.Head), neck = Has(t, RagdollPart.Type.Neck), torso = Has(t, RagdollPart.Type.Torso);
            bool arm = Has(t, ArmParts), leg = Has(t, LegParts);

            // 0.16: any deep stab is tracked (torso, limbs, neck): while the blade stays in they writhe, and if it is
            // also stuck in the world they are pinned (RNAS Pinned behavior)
            if (stab && deep)
            {
                var it = hit.sourceCollider != null ? hit.sourceCollider.GetComponentInParent<Item>() : null;
                if (it != null && s.impaleItem != it)
                { s.impaleItem = it; s.impalePart = part; s.impaleSince = now; s.nextThrash = now + 0.4f; s.impaleCols = null; s.pinned = false; s.pinCheckAt = now + 0.2f; }
                AddTag(s, Tag.Pierced);
            }

            // a blade going in can trigger adrenaline right there
            if (stab && (deep || k > 0.5f) && !Pumped(s) && !s.rushUsed && (torso || leg || arm)
                && s.adrenProfile == AdrenProfile.Unyielding)                          // 0.16: profile-gated, once
            { StartRush(s); return; }
            if (Pumped(s))
            {
                // unbothered: a small jolt, nothing more
                part.physicBody?.AddForceAtPosition(away * 2f, point, ForceMode.Impulse);
                return;
            }

            string what;
            if (stab && kind == WeaponKind.Spear && (torso || leg))
            {
                // spear: the thrust drives the whole body back along the shaft; a hard one takes them off their feet
                Vector3 drive = Vector3.ProjectOnPlane(hit.impactVelocity.sqrMagnitude > 0.25f ? hit.impactVelocity : away, Vector3.up).normalized;
                if (standing) ShoveBody(s, drive, Mathf.Lerp(1.2f, 2.4f, k));
                if (standing && k > 0.8f && !s.brave) { AllowFallHurt(s, 1.5f); Fall(s, drive, 2f + k); }
                else DoubleOver(s, part, point, drive, k, standing);
                QueueClutch(s, part, point, true, 0.15f);
                what = "spear thrust";
            }
            else if (stab && kind == WeaponKind.Dagger && !head && !neck && !(deep && k > 0.7f))
            {
                // dagger: sharp flinch away from the blade, a hand slaps over it, they keep fighting (repeated shanks add up)
                s.shanks = Time.time - s.lastShank < 3f ? s.shanks + 1 : 1; s.lastShank = Time.time;
                part.physicBody?.AddForceAtPosition(away * (2f + 2f * k), point, ForceMode.Impulse);
                Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Neck | RagdollPart.Type.Head, 0.45f + 0.1f * s.shanks, 0.35f);
                s.coreUntil = Mathf.Max(s.coreUntil, Time.time + 0.6f);
                if (standing) ShoveBody(s, away, 0.35f + 0.1f * s.shanks);
                if (s.shanks >= 3) { DoubleOver(s, part, point, away, 0.7f, standing); QueueClutch(s, part, point, true, 0.1f); }
                else QueueClutch(s, part, point, false, 0.2f);
                Scream(s, s.shanks >= 2);
                what = $"dagger shank x{s.shanks}";
            }
            else if (stab && kind == WeaponKind.Missile)
            {
                part.physicBody?.AddForceAtPosition(away * 3f, point, ForceMode.Impulse);
                if (standing && (torso || head || neck)) Stagger(s, away, Effect.StaggerLight, RagdollPart.Type.Torso);
                QueueClutch(s, part, point, false, 0.3f);
                what = "arrow";
            }
            else if (head)
            {
                what = slash ? "face slash" : "head stab";
                Recoil(s, point, away, 0.5f + 0.5f * k, standing, true);
                QueueClutch(s, part, point, false, 0.22f);
            }
            else if (neck)
            {
                what = "throat";
                Recoil(s, point, away, 0.6f + 0.4f * k, standing, true);
                s.bloodInAirway = true;
                QueueClutch(s, part, point, true, 0.15f);
            }
            else if (torso)
            {
                bool gut = point.y < part.transform.position.y - 0.02f || (c.ragdoll.rootPart != null && part == c.ragdoll.rootPart);
                if (stab && (gut || deep))
                {
                    what = gut ? "gut stab" : "chest stab";
                    DoubleOver(s, part, point, away, 0.6f + 0.4f * k, standing);
                    if (!gut) s.bloodInAirway = true;
                    QueueClutch(s, part, point, true, 0.1f);
                }
                else
                {
                    what = "body slash";
                    if (k >= 0.55f) DisablingCut(s, part, point, away, k);           // 0.20: a deep cut across the trunk takes them out of the fight
                    // the trunk twists away along the cut
                    Vector3 along = Vector3.ProjectOnPlane(hit.impactVelocity, Vector3.up);
                    if (along.sqrMagnitude > 0.01f) part.physicBody?.AddForceAtPosition(along.normalized * (3f + 5f * k), point, ForceMode.Impulse);
                    Recoil(s, point, away, 0.35f + 0.4f * k, standing, false);
                    QueueClutch(s, part, point, false, 0.25f);
                }
            }
            else if (arm)
            {
                what = "arm";
                var hand = Hand(c, Has(t, LeftArmParts) ? Side.Left : Side.Right);
                if (hand != null && hand.physicBody != null) hand.physicBody.AddForce((away + Vector3.down * 0.3f) * (2f + 3f * k), ForceMode.Impulse);
                Loosen(s, LimbOf(t), 0.7f, 0.5f);
            }
            else if (leg)
            {
                what = "leg";
                BuckleLeg(s, t, k);                                                // 0.20: the knee gives
                if (standing) Stagger(s, away, k > 0.6f ? Effect.StaggerMedium : Effect.StaggerLight, RagdollPart.Type.Torso);
            }
            else return;
            s.pain = Mathf.Min(1f, s.pain + (0.15f + 0.4f * k) * painIntensity);
            Log($"{c.name}: {what} (dmg {dmg:0}{(deep ? ", deep" : "")}, {kind}) -> reacts");
        }

        /// Direction away from whoever hit them (flat).
        Vector3 Away(NpcState s, CollisionInstance hit)
        {
            var c = s.creature;
            Vector3 d = Vector3.ProjectOnPlane(hit.impactVelocity, Vector3.up);
            if (d.sqrMagnitude < 0.04f)
            {
                var p = PlayerTarget();
                d = p != null ? Vector3.ProjectOnPlane(c.transform.position - p.position, Vector3.up) : -c.transform.forward;
            }
            return d.sqrMagnitude > 1e-4f ? d.normalized : -c.transform.forward;
        }

        /// Head/upper body snaps away from the blow, spine bends back with the hips braced, and they take a step back.
        void Recoil(NpcState s, Vector3 point, Vector3 away, float k, bool standing, bool headFirst)
        {
            var c = s.creature;
            var rag = c.ragdoll;
            float dur = Mathf.Lerp(0.45f, 0.9f, k);
            Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso, Mathf.Lerp(0.5f, 0.8f, k), dur);
            s.coreUntil = Mathf.Max(s.coreUntil, Time.time + dur + 0.3f);
            Vector3 back = (away + Vector3.up * 0.15f).normalized;
            if (headFirst && rag.headPart != null && rag.headPart.physicBody != null)
                rag.headPart.physicBody.AddForceAtPosition(back * Mathf.Lerp(3f, 7f, k), point, ForceMode.Impulse);
            var chest = ChestPart(c);
            if (chest != null && chest.physicBody != null) chest.physicBody.AddForce(back * Mathf.Lerp(3f, 9f, k), ForceMode.Impulse);
            if (standing) ShoveBody(s, away, Mathf.Lerp(0.5f, 1.4f, k));
            if (standing)
            {
                s.nextStagger = Time.time + 1f;
                Stagger(s, away, k > 0.55f ? Effect.StaggerMedium : Effect.StaggerLight, headFirst ? RagdollPart.Type.Head : RagdollPart.Type.Torso);
            }
            Scream(s, k > 0.7f);
        }

        /// Stabbed in the body: the chest and head fold forward around the blade, hips pushed back, a step back.
        void DoubleOver(NpcState s, RagdollPart part, Vector3 point, Vector3 away, float k, bool standing)
        {
            var c = s.creature;
            var rag = c.ragdoll;
            Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso, Mathf.Lerp(0.6f, 0.85f, k), 1.4f);
            s.coreUntil = Mathf.Max(s.coreUntil, Time.time + 1.6f);
            var chest = ChestPart(c);
            if (chest != null && chest.physicBody != null) chest.physicBody.AddForce((-away * 0.6f + Vector3.down) * Mathf.Lerp(4f, 8f, k), ForceMode.Impulse);
            if (rag.headPart != null && rag.headPart.physicBody != null) rag.headPart.physicBody.AddForce((-away * 0.5f + Vector3.down) * Mathf.Lerp(2f, 4f, k), ForceMode.Impulse);
            if (rag.rootPart != null && rag.rootPart.physicBody != null) rag.rootPart.physicBody.AddForce(away * Mathf.Lerp(2f, 5f, k), ForceMode.Impulse);
            if (standing) ShoveBody(s, away, Mathf.Lerp(0.4f, 1f, k));
            if (standing) { s.nextStagger = Time.time + 1.2f; Stagger(s, away, Effect.StaggerLight, RagdollPart.Type.Torso); }
            SetMelee(s, false, Time.time + Mathf.Lerp(1.5f, 4f, k));
            Scream(s, true);
        }

        static RagdollPart ChestPart(Creature c)
        {
            var rag = c.ragdoll;
            if (rag == null || rag.parts == null) return null;
            RagdollPart best = null;
            foreach (var p in rag.parts)
                if (p != null && Has(p.type, RagdollPart.Type.Torso) && p != rag.rootPart && (best == null || p.transform.position.y > best.transform.position.y)) best = p;
            return best ?? rag.rootPart;
        }

        void QueueClutch(NpcState s, RagdollPart part, Vector3 point, bool both, float delay)
        {
            s.clutchAt = Time.time + delay; s.clutchAtPart = part; s.clutchAtBoth = both;
            s.clutchAtPoint = part.transform.InverseTransformPoint(point);
        }

        void StartRush(NpcState s)
        {
            var c = s.creature;
            s.rushUsed = true;
            s.rushUntil = Time.time + adrenalineSeconds * Random.Range(0.9f, 1.5f);
            ReleaseClutch(s);
            s.agonyUntil = 0f;
            if (s.meleeOff) SetMelee(s, true, 0f);
            Guard("speed", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(RushHandler); c.locomotion.globalMoveSpeedMultiplier.Add(RushHandler, 1.15f); });
            Scream(s, true);
            Log($"{c.name}: ADRENALINE RUSH - ignores the stab for {s.rushUntil - Time.time:0.0}s");
        }

        /// Runs every tick: delayed clutches, the blade still in them, adrenaline wearing off, floating watchdog.
        void TickDirector(NpcState s, float dt)
        {
            var c = s.creature;
            var rag = c.ragdoll;
            if (rag == null) return;
            float now = Time.time;
            bool standing = rag.state == Ragdoll.State.Standing;

            // floating watchdog: alive, "standing", but nothing under them
            if (!c.isKilled && standing)
            {
                bool air = false;
                Guard("float", () => air = c.locomotion != null && !c.locomotion.isGrounded && GroundBelow(c) > 1.5f);
                s.airTime = air ? s.airTime + dt : 0f;
                if (s.airTime > 0.8f && TryFix(s, now))
                {
                    s.airTime = 0f;
                    AllowFall(s, 3f);
                    Guard("fall", () => rag.SetState(Ragdoll.State.Destabilized));
                    Log($"{c.name}: was floating - dropped back to the ground");
                }
            }
            GroundClipWatch(s, dt);
            if (now < s.noseBleedUntil && now >= s.nextNoseDrip && !c.isKilled && rag.headPart != null)
            { s.nextNoseDrip = now + Random.Range(2.5f, 5f); Guard("blood", () => RageEngineScript.Blood(rag.headPart.transform)); }
            if (c.isKilled && !s.onAdrenaline) { s.impaleItem = null; return; }

            if (s.clutchAt > 0f && now >= s.clutchAt)
            {
                s.clutchAt = 0f;
                if (s.clutchAtPart != null && !Pumped(s))
                {
                    Vector3 p = s.clutchAtPart.transform.TransformPoint(s.clutchAtPoint);
                    StartClutch(s, s.clutchAtPart, p, null, false, s.clutchAtBoth);
                    if (s.clutchAtBoth) SecondHand(s);
                }
            }

            if (s.rushUntil > 0f && now >= s.rushUntil)
            {
                s.rushUntil = 0f;
                Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Remove(RushHandler));
                s.pain = Mathf.Min(1f, s.pain + 0.35f);
                Scream(s, true);
                Log($"{c.name}: adrenaline wears off - the pain hits");
                if (s.clutchAnchor != null && s.clutchAnchor.parent != null) s.nextReclutch = now;
                AgonyCheck(s, 1.5f);
            }

            if (s.impaleItem != null)
            {
                bool still = false;
                Guard("impale", () => still = StillIn(s.impaleItem, c));
                if (!still || now - s.impaleSince > 60f)
                {
                    s.impaleItem = null; s.pinned = false; s.impaleCols = null; s.rnasDirty = true;
                    if (s.impalePart != null) for (int i = 0; i < 2; i++) Guard("blood", () => RageEngineScript.Blood(s.impalePart.transform));
                    Scream(s, true);
                    s.pain = Mathf.Min(1f, s.pain + 0.2f);
                    Log($"{c.name}: the blade comes out");
                }
                else
                {
                    if (now >= s.pinCheckAt) { s.pinCheckAt = now + 0.25f; bool was = s.pinned; s.pinned = PinnedToWorld(s); if (s.pinned != was) { s.rnasDirty = true; Log($"{c.name}: {(s.pinned ? "PINNED to the world by" : "no longer pinned by")} {s.impaleItem.data?.id}"); } }
                }
                if (s.impaleItem != null && !Pumped(s) && now >= s.nextThrash)
                {
                    // writhing in pain around the blade
                    s.nextThrash = now + Random.Range(0.35f, 0.8f);
                    s.pain = Mathf.Min(1f, s.pain + 0.05f);
                    SetMelee(s, false, now + 1.2f);
                    Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck | (standing ? (RagdollPart.Type)0 : LegParts) | ArmParts, Random.Range(0.4f, 0.75f), 0.5f);
                    float f = standing ? 2.5f : 5f;
                    foreach (var p in rag.parts)
                    {
                        if (p == null || p.physicBody == null || Random.value > 0.35f) continue;
                        bool limb = Has(p.type, ArmParts) || (!standing && Has(p.type, LegParts)) || Has(p.type, RagdollPart.Type.Head);
                        if (limb) p.physicBody.AddForce(Random.insideUnitSphere * f, ForceMode.Impulse);
                    }
                    if (!s.clutching && s.impalePart != null) QueueClutch(s, s.impalePart, s.impalePart.transform.position, true, 0f);
                    if (now >= s.nextImpaleScream) { s.nextImpaleScream = now + Random.Range(1.8f, 3.5f); Scream(s, Random.value < 0.5f); }
                }
            }
        }

        static bool StillIn(Item it, Creature c)
        {
            if (it == null || !it.isPenetrating || it.collisionHandlers == null) return false;
            foreach (var ch in it.collisionHandlers)
            {
                if (ch == null || ch.penetratedObjects == null) continue;
                foreach (var po in ch.penetratedObjects)
                    if (po != null && po.ragdollPart != null && po.ragdollPart.ragdoll != null && po.ragdollPart.ragdoll.creature == c) return true;
            }
            return false;
        }

        static readonly RaycastHit[] rayHits = new RaycastHit[12];
        static float GroundBelowFrom(Creature c, Vector3 from, float dist)
        {
            int n = Physics.RaycastNonAlloc(from, Vector3.down, rayHits, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = 99f;
            for (int i = 0; i < n; i++)
            {
                var col = rayHits[i].collider;
                if (col == null || col.GetComponentInParent<Creature>() == c) continue;
                if (rayHits[i].distance < best) best = rayHits[i].distance;
            }
            return best;
        }
        /// Distance from the hips down to the first thing under them that isn't their own body.
        static float GroundBelow(Creature c)
        {
            var root = c.ragdoll.rootPart;
            Vector3 from = root != null ? root.transform.position : c.transform.position + Vector3.up;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, rayHits, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = 99f;
            for (int i = 0; i < n; i++)
            {
                var col = rayHits[i].collider;
                if (col == null || col.GetComponentInParent<Creature>() == c) continue;
                if (rayHits[i].distance < best) best = rayHits[i].distance;
            }
            return best;
        }

        /// The other hand joins the first on the wound.
        void SecondHand(NpcState s)
        {
            var c = s.creature;
            var ik = c.ragdoll.ik;
            if (!s.clutching || s.clutchAnchor == null || ik == null) return;
            Side other = Other(s.clutchSide);
            if (Dead(s, other)) return;
            var h = Hand(c, other);
            if (h == null) return;
            if (h.grabbedHandle != null && !(h.grabbedHandle is HandleRagdoll) && !PainDrops(s)) return;                // 0.25: keeps hold of his weapon
            if (h.grabbedHandle != null) { var hh = h; Guard("drop", () => hh.UnGrab(false)); }
            if (s.clutchAnchor2 == null) s.clutchAnchor2 = new GameObject("RageEngine_Clutch2").transform;
            s.clutchAnchor2.SetParent(s.clutchAnchor.parent, false);
            s.clutchAnchor2.position = s.clutchAnchor.position + c.transform.right * (other == Side.Left ? -0.07f : 0.07f) + Vector3.up * 0.03f;
            s.clutchAnchor2.rotation = s.clutchAnchor.rotation;
            ik.SetHandAnchor(other, s.clutchAnchor2);
            ik.SetHandState(other, true, true);
            s.clutching2 = true;
        }
    
        // ---------------- 0.16: ground adhesion / pinning / ground clipping ----------------
        /// Moves the NPC's locomotion body (the thing the ragdoll is pinned to) with a hit, so they travel with it instead
        /// of being yanked back to where they stood. dv in m/s, horizontal.
        static void ShoveBody(NpcState s, Vector3 dir, float dv)
        {
            var c = s.creature;
            if (dv < 0.05f) return;
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) return;
            flat.Normalize();
            dv = Mathf.Min(dv, 2.4f);
            Guard("shove", () =>
            {
                var pb = c.locomotion != null ? c.locomotion.physicBody : null;
                if (pb == null) return;
                pb.velocity += flat * dv;
            });
            AddTag(s, Tag.Unbalanced, 0.8f + dv * 0.5f);
        }

        static readonly Collider[] overlap = new Collider[16];
        /// The embedded weapon is ALSO stuck in the world (not held, not moving, its blade inside static geometry).
        /// This only detects it; the game's own penetration joint is what physically holds the body.
        static bool PinnedToWorld(NpcState s)
        {
            var it = s.impaleItem;
            if (it == null || it.mainHandler != null) return false;
            if (it.physicBody != null && it.physicBody.velocity.sqrMagnitude > 0.04f) return false;
            if (s.impaleCols == null) s.impaleCols = it.GetComponentsInChildren<Collider>();
            var c = s.creature;
            int checkedCols = 0;
            foreach (var col in s.impaleCols)
            {
                if (col == null || col.isTrigger || ++checkedCols > 6) continue;
                var b = col.bounds;
                int n = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * 0.01f, overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var o = overlap[i];
                    if (o == null || o.transform.IsChildOf(it.transform)) continue;
                    if (o.attachedRigidbody != null && !o.attachedRigidbody.isKinematic) continue;      // something loose, not the world
                    if (o.GetComponentInParent<Creature>() != null) continue;                              // a body, not the world
                    return true;
                }
            }
            return false;
        }

        /// Alive NPC whose hips are under the ground surface for a moment: lift the parts that are below it.
        void GroundClipWatch(NpcState s, float dt)
        {
            var c = s.creature;
            var rag = c.ragdoll;
            if (c.isKilled || rag == null || rag.rootPart == null) { s.clipTime = 0f; return; }
            Vector3 hips = rag.rootPart.transform.position;
            float groundY = 0f; bool found = false;
            Guard("clip", () => found = GroundAbove(c, hips, out groundY));
            s.clipTime = found && groundY > hips.y + 0.12f ? s.clipTime + dt : 0f;
            if (s.clipTime < 0.5f || !TryFix(s, Time.time)) return;
            s.clipTime = 0f;
            int lifted = 0;
            foreach (var p in rag.parts)
            {
                if (p == null || p.physicBody == null) continue;
                if (p.transform.position.y < groundY + 0.02f) { p.physicBody.velocity = Vector3.up * 2.5f; lifted++; }
            }
            Log($"{c.name}: was stuck in the ground - lifted {lifted} parts out");
        }

        /// Ground surface height over a point (ray from above, ignoring their own body). For clip detection.
        static bool GroundAbove(Creature c, Vector3 at, out float y)
        {
            y = 0f;
            // a floor-like surface no more than 0.6 m above the hips (not a table they're lying under)...
            int n = Physics.RaycastNonAlloc(at + Vector3.up * 0.6f, Vector3.down, rayHits, 0.6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                var col = rayHits[i].collider;
                if (col == null || col.attachedRigidbody != null || rayHits[i].normal.y < 0.7f || col.GetComponentInParent<Creature>() != null) continue;
                if (rayHits[i].point.y > best) best = rayHits[i].point.y;
            }
            if (float.IsNegativeInfinity(best)) return false;
            // ...and nothing solid right under the hips (they're inside/under the floor, not lying on it)
            if (GroundBelowFrom(c, at, 0.8f) < 0.8f) return false;
            y = best; return true;
        }
    
        /// 0.16: blunt hits to the face: a black eye (a bigger, darker bruise around the eye that was hit) and a nosebleed
        /// that drips for a while.
        void FaceInjury(NpcState s, RagdollPart head, Vector3 point, float severity)
        {
            var c = s.creature;
            if (severity < 0.45f || c.isKilled) return;
            var eyes = c.allEyes;
            if (eyes != null)
                for (int i = 0; i < eyes.Count; i++)
                {
                    if (eyes[i] == null || (s.blackEyes & (1 << i)) != 0) continue;
                    if (Vector3.Distance(point, eyes[i].transform.position) > 0.07f) continue;
                    s.blackEyes |= 1 << i;
                    Vector3 p = eyes[i].transform.position;
                    Vector3 n = (p - head.transform.position).normalized;
                    BlackEye(s, head, p, n, severity);
                }
            if (c.centerEyes != null)
            {
                Vector3 nose = c.centerEyes.position - c.centerEyes.up * 0.04f + c.centerEyes.forward * 0.06f;
                if (Vector3.Distance(point, nose) < 0.08f && severity >= 0.6f)
                {
                    bool fresh = Time.time >= s.noseBleedUntil;
                    s.noseBleedUntil = Time.time + Mathf.Lerp(15f, 40f, Mathf.Clamp01(severity - 0.6f));
                    if (fresh) { s.nextNoseDrip = Time.time; Log($"{c.name}: bloody nose"); }
                }
            }
        }
    
        /// 0.17 black eye: dark purple painted into the skin all around the socket (lids, under-eye, brow), the eye
        /// half-closed, and real swelling - the head mesh pushed out under the eye and on the lid.
        void BlackEye(NpcState s, RagdollPart head, Vector3 eye, Vector3 outward, float severity)
        {
            var c = s.creature;
            Vector3 up = c.centerEyes != null ? c.centerEyes.up : Vector3.up;
            Vector3 side = Vector3.Cross(up, outward).normalized;
            Color deep = new Color(0.22f, 0.08f, 0.2f, 0.75f), red = new Color(0.45f, 0.1f, 0.16f, 0.5f);
            int painted = 0;
            Vector3[] ring = { eye - up * 0.016f, eye + up * 0.014f, eye + side * 0.017f, eye - side * 0.017f, eye - up * 0.024f + side * 0.006f, eye };
            foreach (var p in ring)
                Guard("skinpaint", () => { if (SkinPaint.Paint(c, p + outward * 0.01f, 0.022f, painted % 2 == 0 ? deep : red, TexGen.Bruise())) painted++; });
            Transform bone = head.meshBone != null ? head.meshBone : head.bone != null ? head.bone.mesh : null;
            int swollen = 0;
            if (bone != null)
                // 0.33: no swelling bumps (3D welts)
            if (painted == 0) for (int k = 0; k < 3; k++) AddBruise(s, head, eye + Random.insideUnitSphere * 0.012f, outward, Mathf.Max(severity, 1.2f));   // fallback
            s.eyesShutUntil = Mathf.Max(s.eyesShutUntil, Time.time + 2f);       // squints shut a moment
            if (!livingEyes.Contains(s)) livingEyes.Add(s);
            Log($"{c.name}: black eye (painted {painted}, swelling on {swollen} mesh(es))");
        }
    
        internal enum WeaponKind : byte { Unknown, Dagger, Sword, Spear, Axe, Heavy, Missile }
        static readonly System.Collections.Generic.Dictionary<string, WeaponKind> kindCache = new System.Collections.Generic.Dictionary<string, WeaponKind>();

        /// Classifies a weapon by its catalog id, falling back to its size and weight.
        internal static WeaponKind KindOf(Item it)
        {
            if (it == null || it.data == null) return WeaponKind.Unknown;
            string id = it.data.id ?? "";
            if (kindCache.TryGetValue(id, out var k)) return k;
            string n = id.ToLowerInvariant();
            if (n.Contains("arrow") || n.Contains("bolt") || n.Contains("dart") || n.Contains("javelinthrow")) k = WeaponKind.Missile;
            else if (n.Contains("dagger") || n.Contains("knife") || n.Contains("stiletto") || n.Contains("dirk") || n.Contains("shiv") || n.Contains("kunai")) k = WeaponKind.Dagger;
            else if (n.Contains("spear") || n.Contains("pike") || n.Contains("halberd") || n.Contains("lance") || n.Contains("javelin") || n.Contains("trident") || n.Contains("glaive") || n.Contains("staff")) k = WeaponKind.Spear;
            else if (n.Contains("axe")) k = WeaponKind.Axe;
            else if (n.Contains("mace") || n.Contains("hammer") || n.Contains("club") || n.Contains("flail") || n.Contains("maul")) k = WeaponKind.Heavy;
            else if (n.Contains("sword") || n.Contains("blade") || n.Contains("katana") || n.Contains("rapier") || n.Contains("sabre") || n.Contains("saber") || n.Contains("scimitar")) k = WeaponKind.Sword;
            else
            {
                // unknown modded weapon: judge by length
                var b = new Bounds(it.transform.position, Vector3.zero);
                foreach (var r in it.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                float len = b.size.magnitude;
                k = len < 0.45f ? WeaponKind.Dagger : len > 1.4f ? WeaponKind.Spear : WeaponKind.Sword;
            }
            kindCache[id] = k;
            return k;
        }
    
        /// 0.18: a solid punch to the throat: they recoil, both hands go to the neck, they gag and stop swinging a moment.
        void NeckBlow(NpcState s, RagdollPart neck, Vector3 point, float severity)
        {
            var c = s.creature;
            float now = Time.time;
            if (severity < 0.35f || c.isKilled || now < s.nextNeckGrab || Pumped(s)) return;
            s.nextNeckGrab = now + 3f;
            var p = PlayerTarget();
            Vector3 away = p != null ? Vector3.ProjectOnPlane(c.transform.position - p.position, Vector3.up).normalized : -c.transform.forward;
            float k = Mathf.Clamp01(severity / 1.2f);
            bool standing = c.ragdoll.state == Ragdoll.State.Standing;
            Recoil(s, point, away, 0.4f + 0.5f * k, standing, true);
            QueueClutch(s, neck, point, true, 0.12f);
            SetMelee(s, false, now + 1.2f + 1.5f * k);
            s.pain = Mathf.Min(1f, s.pain + 0.25f * painIntensity);
            Speak(s, "choke");
            Log($"{c.name}: punched in the throat - grabs the neck, gagging");
        }
    }
}

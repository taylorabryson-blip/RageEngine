// Nomad Rage Engine - physical impact reactions (Euphoria-style partial ragdoll).
// Driven by real CONTACT, not only by damage: anything that physically hits an NPC (a thrown chair, a poke, a shield
// bash, a body) is measured by momentum (mass x speed). Then:
//   1. the hit region's muscles let go for a moment and the struck part is shoved along the hit direction
//   2. heavy objects push the whole body (chest + hips) - they get knocked back
//   3. balance drains; follow-up hits rock them more
//   4. escalation through the game's own systems: flinch -> stagger step (native push) -> dazed (head) ->
//      knee buckle (legs) -> fall with a real tumble; hard falls can knock the weapon out of their hand
using ThunderRoad;
using UnityEngine;
using Effect = ThunderRoad.BrainModuleHitReaction.PushBehaviour.Effect;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ---------------- contact entry point ----------------
        void HandleContact(CollisionInstance hit, RagdollPart part)
        {
            var c = part.ragdoll.creature;
            if (!TryGet(c, out var s)) return;
            bool dead = c.isKilled && !s.onAdrenaline;
            var col = hit.sourceCollider;
            if (col == null) return;
            var rb = col.attachedRigidbody;
            if (rb == null || rb.isKinematic) { HandleSlam(s, hit, part, dead); return; }   // walls / floor / pillars
            if (!dead && s.knockedOutUntil > 0f) return;
            var srcPart = col.GetComponentInParent<RagdollPart>();
            if (srcPart != null && srcPart.ragdoll == part.ragdoll) return;            // own body
            var item = col.GetComponentInParent<Item>();
            if (item != null && item.mainHandler != null && item.mainHandler.creature == c) return;   // own weapon

            Vector3 point = hit.contactPoint;
            Vector3 rel = rb.GetPointVelocity(point) - (part.physicBody != null ? part.physicBody.velocity : Vector3.zero);
            float speed = Mathf.Max(rel.magnitude, hit.impactVelocity.magnitude);
            if (speed < 1.5f || speed > 25f) return;                                 // >25 m/s = a teleport/snap, not a real hit
            // items this NPC is holding or just picked up never hurt it (a snatched weapon snapping into the hand used
            // to read as a 30 m/s impact and broke every limb)
            if (item != null && (s.phantom == item || s.lastGrabbed == item && Time.time - s.lastGrabTime < 2f)) return;
            if (item != null && item.handlers != null)
                foreach (var hh in item.handlers) if (hh != null && hh.creature == c) return;

            float mass = Mathf.Clamp(rb.mass, 0.3f, 40f);
            bool playerDriven = (item != null && item.mainHandler != null && item.mainHandler.creature != null && item.mainHandler.creature.isPlayer)
                                || (srcPart != null && srcPart.ragdoll != null && srcPart.ragdoll.creature != null && srcPart.ragdoll.creature.isPlayer);
            bool fist = srcPart is RagdollHand;
            if (playerDriven) mass = Mathf.Max(mass, fist ? 1.0f : 2.0f);              // the player's arm is behind a punch / held weapon
            float severity = mass * speed / 14f;
            float raw = severity;
            Vector3 hdir = rel.sqrMagnitude > 0.25f ? rel.normalized : hit.impactVelocity.normalized;
            // heavy THROWN / falling objects (rocks, golem boulders, a thrown anvil) - gore, crushed skulls, broken spines;
            // corpses too. Weapons swung in a hand go through the damage path instead (their hand speed isn't mass).
            bool loose = item == null ? srcPart == null : item.handlers == null || item.handlers.Count == 0;
            if (!fist && loose && severity >= Mathf.Min(fleshTearForce, Mathf.Min(headCrushForce, spineBreakForce)))
                HeavyBlunt(s, part, point, hdir, severity, false);
            if (dead) return;
            if (fist && playerDriven) severity = Mathf.Min(severity * playerPunchStrength, 1.2f);   // human punches, not cannonballs
            // Haymaker: punching an arm/leg the player is holding breaks it
            if (fist && playerDriven && fracturesEnabled && grabBreaks && LimbOf(part.type) != 0 && !Has(s.brokenParts, LimbOf(part.type))
                && raw >= boneToughness * 0.6f && PlayerHolding(s, part))
            {
                var hand = srcPart as RagdollHand;
                if (breakHaptics && hand != null) Haptics.Pulse(hand.side, 1f, 2);
                Fracture(s, part, point, raw, true);
            }
            if (severity < 0.12f) return;
            if (!AcceptReaction(s, col, rel, hit.contactNormal, part.type, ref severity)) return;      // 0.19: dedupe, per-region, glancing
            s.lastReactTime = Time.time;

            Vector3 dir = rel.sqrMagnitude > 0.25f ? rel : hit.impactVelocity;
            if (dir.sqrMagnitude < 0.01f) dir = part.transform.position - point;
            Impact(s, part, part.type, point, dir.normalized, severity, fist || item == null, fist);
            if (fist || (item == null && srcPart == null)) Guard("bruises", () => AddBruise(s, part, point, hit.contactNormal, severity));
            if (Time.time >= s.nextHitSfx && severity > 0.25f)
            {
                s.nextHitSfx = Time.time + 0.1f;
                Sfx.PlayAt(fist ? "NRE_Punch" : "NRE_BodyHit", point, Mathf.Clamp(0.4f + severity * 0.4f, 0.4f, 1f));
            }
            Log($"{c.name} CONTACT {part.type} m={mass:0.0} v={speed:0.0} sev={severity:0.00} bal={s.balance:0.00}");
        }

        // ---------------- damage entry point (when no contact reaction just happened) ----------------
        void ApplyImpactReaction(NpcState s, CollisionInstance hit, RagdollPart part, RagdollPart.Type t, float severity)
        {
            if (!AcceptReaction(s, hit.sourceCollider, hit.impactVelocity, hit.contactNormal, t, ref severity)) return;
            s.lastReactTime = Time.time;
            Vector3 dir = hit.impactVelocity;
            if (dir.sqrMagnitude < 0.25f)
            {
                dir = hit.contactNormal;
                if (Vector3.Dot(dir, part.transform.position - hit.contactPoint) < 0f) dir = -dir;
            }
            Impact(s, part, t, hit.contactPoint, dir.normalized, severity, hit.damageStruct.damageType == DamageType.Blunt);
        }

        // ---------------- the reaction ----------------
        void Impact(NpcState s, RagdollPart part, RagdollPart.Type t, Vector3 point, Vector3 dir, float severity, bool blunt, bool fist = false)
        {
            // 0.12 reaction model (Euphoria-inspired, built for B&S's balance-driven active ragdoll):
            //   - a real IMPULSE at the contact point (N*s, not a velocity): physics decides the result, so a 5 kg head
            //     snaps and twists away, a 30 kg chest barely rocks, an arm swings - proportional to the actual hit
            //   - only the struck head/neck or arm goes loose for a moment; the trunk and legs keep full strength while
            //     standing (weakening them is what made B&S's balance give out and drop NPCs before 0.12)
            //   - solid hits make them take the game's own small stagger step to catch themselves (never a fall)
            //   - they only go down from the punch-drunk meter or a genuinely huge single force
            var c = s.creature;
            if (s.shocked || s.knockedOut) return;                                    // 0.20: the game's electrocution owns the body
            severity *= s.bioFlinch;                                                   // 0.24: serums blunt the reaction
            s.lastHitDir = dir;
            s.lastHitTime = Time.time;
            severity /= Mathf.Max(0.3f, s.toughness);                                // tough NPCs take it, glass jaws don't
            bool head = Has(t, RagdollPart.Type.Head), neck = Has(t, RagdollPart.Type.Neck), leg = Has(t, LegParts);
            bool arm = Has(t, ArmParts), trunk = Has(t, RagdollPart.Type.Torso);
            float wild = s.personality.type == PersonalityType.Unpredictable ? Random.Range(0.75f, 1.35f) : 1f;
            float adren = Pumped(s) ? 0.4f : 1f;
            bool standing = c.ragdoll.state == Ragdoll.State.Standing;

            // 1) impulse at the contact point
            if (reactionStrength > 0f && part.physicBody != null)
            {
                float j = reactionStrength * 6f * Mathf.Clamp(severity, 0.15f, 3f) * wild * adren;   // N*s
                if (trunk) j *= 0.6f; else if (leg) j *= 0.25f;
                part.physicBody.AddForceAtPosition(dir * Mathf.Min(j, 24f), point, ForceMode.Impulse);
                // head hits also whip the neck a little
                if (head && part.parentPart != null && part.parentPart.physicBody != null)
                    part.parentPart.physicBody.AddForce(dir * Mathf.Min(j, 24f) * 0.3f, ForceMode.Impulse);
                // 0.16 ground adhesion fix: the ragdoll is pinned to the locomotion body, which never moved - so the
                // body was pulled straight back to where it stood. Move the locomotion body with the hit too.
                if (standing && (head || neck || trunk)) ShoveBody(s, dir, Mathf.Min(j, 24f) / 70f * 1.6f / Mathf.Max(0.6f, s.balanceStability));
            }

            // 0.20: hits from behind arch the back; legs buckle at the knee; heavy body shots can bring blood up
            bool backHit = (trunk || neck) && BackHit(s, part, t, point, dir, severity, standing);
            if (leg && standing && severity > 0.35f) BuckleLeg(s, t, Mathf.Clamp01(severity / 1.5f));
            if (trunk && blunt && severity > 1.1f && Random.value < 0.3f) SpitBlood(s, 1);
            // 2) the struck region gives with the hit. Head/body hits bend the neck AND spine (Euphoria-style whole-chain
            //    reaction) while the hips/legs brace extra hard (core brace) so the bend can't topple them.
            RagdollPart.Type region = head || neck ? RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso
                                    : trunk ? RagdollPart.Type.Torso | RagdollPart.Type.Neck | RagdollPart.Type.Head
                                    : arm ? LimbOf(t) : 0;
            if (region != 0)
            {
                float loss = muscleLoss * Mathf.Clamp(0.4f + 0.4f * severity, 0.35f, 0.9f) * adren;
                float dur = Mathf.Clamp(0.25f + 0.2f * severity, 0.25f, 0.7f) / Mathf.Max(0.25f, recoverySpeed);
                Loosen(s, region, loss, dur);
                if (Has(region, RagdollPart.Type.Torso)) s.coreUntil = Mathf.Max(s.coreUntil, Time.time + dur + 0.25f);
            }

            // 3) balance (drives posture, speed and the punch-drunk wobble; recovers in a couple of seconds)
            float drop = Mathf.Min(0.08f + 0.12f * severity, s.glassJaw ? 0.45f : 0.3f) * balanceLoss * adren / Mathf.Max(0.5f, s.balanceStability);
            drop *= 0.6f / Tough33(s);                                                          // 0.28.3: much steadier on their feet
            s.balance = Mathf.Max(0.45f, s.balance - drop);
            s.fatigue = Mathf.Min(1f, s.fatigue + 0.04f * severity);

            if (!standing || Pumped(s)) return;
            float thr = Mathf.Max(0.05f, staggerThreshold);
            float eff = severity * wild;
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            flat = flat.sqrMagnitude > 0.01f ? flat.normalized : -c.transform.forward;
            MaybeStumble(s, flat, eff / thr * 0.65f / Tough33(s));                                       // 0.19: stumbles when already off balance

            // 4) punch-drunk meter: clean blunt shots to the head build up; full = they drop
            if (blunt)
            {
                float where = head ? 1f : neck ? 0.6f : trunk ? 0.12f : 0f;
                float gainDaze = where * Mathf.Clamp(severity / 0.5f, 0.4f, 1.5f) / Mathf.Max(1f, punchesToDrop);
                if (s.glassJaw) gainDaze *= 2f;
                s.daze += gainDaze * adren;
            }
            // 0.20 knockout: enough head trauma and they're out cold for good
            if (blunt && (head || neck) && (s.daze >= 1.35f || (s.drunk >= 1.25f && eff >= knockdownForce * thr * 0.5f) || eff >= knockdownForce * thr * 1.5f * (s.glassJaw ? 0.6f : 1f)))
            { Knockout(s, flat, fist || PlayerTarget() != null && Vector3.Distance(PlayerTarget().position, point) < 2.5f); return; }
            if (s.daze >= 1f || eff >= knockdownForce * thr * (s.glassJaw ? 0.6f : 1f))
            {
                s.daze = 0.25f;
                Fall(s, flat, Mathf.Max(eff, 1.5f));
                return;
            }

            // 5) stagger step: the game's own catch-your-balance step, sized by the hit
            if (staggerOnHits && Time.time >= s.nextStagger && (head || neck || trunk) && eff >= 0.5f * thr)
            {
                s.nextStagger = Time.time + Random.Range(0.9f, 1.4f);
                var e = eff >= 1.6f * thr || s.daze >= 0.6f ? Effect.StaggerMedium : Effect.StaggerLight;
                Stagger(s, flat, e, head || neck ? RagdollPart.Type.Head : RagdollPart.Type.Torso);
            }
            if (head && blunt && headStuns && eff >= thr * 4f)
                Guard("stun", () => c.brain.instance.GetModule<BrainModuleHitReaction>(false)?.TestStun(Mathf.Clamp(0.6f + eff * 0.4f, 0.8f, 2.5f)));
        }

        /// Flesh sounds for blades (the game already plays weapon impact sounds; these add the wet body part).
        static void HitSound(NpcState s, CollisionInstance hit, RagdollPart part)
        {
            if (Time.time < s.nextHitSfx) return;
            var type = hit.damageStruct.damageType;
            if (type == DamageType.Slash) { s.nextHitSfx = Time.time + 0.12f; Sfx.PlayAt("NRE_Slash", hit.contactPoint, 0.8f); }
            else if (type == DamageType.Pierce) { s.nextHitSfx = Time.time + 0.12f; Sfx.PlayAt("NRE_Stab", hit.contactPoint, 0.8f); }
        }

        static void PushBody(Creature c, Vector3 dir, float dv)
        {
            dv = Mathf.Min(dv, 6f);
            var root = c.ragdoll.rootPart;
            if (root != null && root.physicBody != null) root.physicBody.AddForce(dir * dv, ForceMode.VelocityChange);
            var head = c.ragdoll.headPart;
            if (head != null && head.physicBody != null) head.physicBody.AddForce(dir * dv * 1.2f, ForceMode.VelocityChange);
        }

        /// Full physics fall in the hit direction (they get back up on their own).
        void Fall(NpcState s, Vector3 flat, float eff)
        {
            var c = s.creature;
            bool ok = false;
            AllowFall(s, 1f);
            Guard("fall", () => { c.ragdoll.SetState(Ragdoll.State.Destabilized); ok = true; });
            if (!ok) { Stagger(s, flat, Effect.Destabilize, RagdollPart.Type.Torso); return; }
            PushBody(c, flat + Vector3.up * 0.1f, Mathf.Clamp(1f + eff * 0.6f, 1f, 4f) * Mathf.Max(0.3f, reactionStrength));
            s.balance = Mathf.Max(s.balance, 0.35f);
            LoseGrip(s, eff);
            Log($"{c.name} FALLS (eff {eff:0.00})");
            Emit(c.transform.position, 6f, Stim.Impact, c);
        }

        /// Hard hits and falls can knock what they're holding out of their hands.
        void LoseGrip(NpcState s, float eff)
        {
            if (!loseGrip) return;
            float chance = Mathf.Clamp(0.07f * eff * (0.5f + s.pain) * (1f - 0.6f * s.hpK) * (s.bioNoQuit ? 0.3f : 1f), 0f, 0.5f);   // 0.25: weapons stay in hand more
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                var h = Hand(s.creature, side);
                if (h != null && h.grabbedHandle != null && !(h.grabbedHandle is HandleRagdoll) && Random.value < chance)
                {
                    h.UnGrab(false);
                    Log($"{s.creature.name} loses grip ({side})");
                }
            }
        }

        static void Stagger(NpcState s, Vector3 dir, Effect effect, RagdollPart.Type part)
        {
            var c = s.creature;
            bool ok = false;
            if (effect == Effect.Destabilize) AllowFall(s, 1f);
            Guard("stagger", () => { c.ForceStagger(dir, effect, part); ok = true; });
            if (!ok && effect == Effect.Destabilize) Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
            Log($"{c.name} {effect}");
        }
    }
}

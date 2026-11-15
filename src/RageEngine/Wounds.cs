// Nomad Rage Engine - wounds. NPCs respond to cuts and stabs like people:
//   - serious cut/stab to an arm  -> that hand drops its weapon, can't grab again, the arm hangs (no muscle);
//                                    the other hand grabs the wound
//   - cut/stab to the chest/neck   -> a free hand presses on the bleeding wound (if it's bad enough they drop the
//                                    off-hand weapon to do it), and they keep going back to it while it hurts
//   - cut/stab to a leg            -> the leg gives out (they fall), then they get up and limp/drag it
//   - cut above the eyes           -> blood in the eyes: a hand goes to the face and wipes, vision dulled briefly
// Hands are moved by the game's own IK, so it's physical and blends with whatever they're doing.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        void ApplyWounds(NpcState s, CollisionInstance hit, RagdollPart part, RagdollPart.Type t)
        {
            var ds = hit.damageStruct;
            if (ds.damageType != DamageType.Slash && ds.damageType != DamageType.Pierce) return;
            var c = s.creature;
            float dmg = ds.damage;
            bool deep = ds.penetration == DamageStruct.Penetration.Skewer || ds.penetrationDeepReached;
            Vector3 point = hit.contactPoint;
            if (!Has(t, RagdollPart.Type.Head) && dmg >= 0.5f) CutMuscle(s, part, point, dmg, deep);   // 0.14: the muscle under the blade
            if (dmg >= woundThreshold * 0.6f) Guard("clothblood", () => SkinPaint.Paint(c, point, Mathf.Clamp(0.02f + dmg * 0.002f, 0.025f, 0.06f), new Color(0.32f, 0.02f, 0.03f, 0.8f), TexGen.Bruise(), true));   // 0.19: blood soaks the clothes

            // blood in the eyes: any cut across the upper head
            bool nearEyes = c.centerEyes != null && Vector3.Distance(point, c.centerEyes.position) < 0.09f;
            if (Has(t, RagdollPart.Type.Head) && (point.y > part.transform.position.y || nearEyes))
            {
                bool eyeHit = c.centerEyes != null && Vector3.Distance(point, c.centerEyes.position) < 0.06f;
                // 0.18: a blade that goes through an eye blinds it for good (stab, deep cut or a solid slash right across it)
                float dEye = 1f;
                if (c.allEyes != null) foreach (var e in c.allEyes) if (e != null) dEye = Mathf.Min(dEye, Vector3.Distance(point, e.transform.position));
                bool stabbed = (ds.damageType == DamageType.Pierce && dEye < 0.045f) || (deep && dEye < 0.05f)
                               || (ds.damageType == DamageType.Slash && dEye < 0.032f && dmg >= woundThreshold)
                               || (eyeHit && dmg >= woundThreshold * 2f);
                s.eyeBloodUntil = Mathf.Max(s.eyeBloodUntil, Time.time + (eyeHit ? 20f : 10f));
                s.pain = Mathf.Min(1f, s.pain + (stabbed ? 0.6f : eyeHit ? 0.35f : 0.2f) * painIntensity);
                HurtEyes(s, point, stabbed, true);                              // 0.14: eyes squeeze shut; a stabbed eye stays shut, blind
                BloodInEyes(s, part);
                if (eyeHit) Scream(s, true);
                return;
            }
            if (dmg < woundThreshold && !deep)
            {
                // light pokes and nicks still get a reaction
                if (dmg < 0.5f) return;
                if (Has(t, LegParts))
                {
                    Loosen(s, LimbOf(t), 0.85f, 0.7f);                              // the leg jerks / gives a little
                    s.balance = Mathf.Max(MinBalance, s.balance - 0.15f);
                }
                else
                {
                    Side? use = null;
                    if (Has(t, ArmParts))
                    {
                        Side hurt = Has(t, LeftArmParts) ? Side.Left : Side.Right;
                        PainArm(s, hurt, 5f + dmg);
                        use = Other(hurt);
                    }
                    s.pain = Mathf.Min(1f, s.pain + 0.08f * painIntensity);
                    StartClutch(s, part, point, use, false, false);
                }
                return;
            }
            bool severe = dmg >= woundThreshold * 2f || deep;

            if (Has(t, ArmParts))
            {
                Side armSide = Has(t, LeftArmParts) ? Side.Left : Side.Right;
                if (cutArmsDisable && severe) KillArm(s, armSide);
                else PainArm(s, armSide, 12f + dmg);
                StartClutch(s, part, point, Other(armSide), false, severe);
                Scream(s, severe);
            }
            else if (Has(t, RagdollPart.Type.Torso | RagdollPart.Type.Neck))
            {
                StartClutch(s, part, point, null, false, severe);
                if (severe) Scream(s, false);
            }
            else if (Has(t, LegParts))
            {
                // the leg is hurt: they limp on it (slower, weak leg) - they only go down if it's really bad and the
                // "Leg wounds drop them" option is on
                s.legWounded = true;
                MakeLimp(s, LimbOf(t));
                Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Add(WoundHandler, 0.6f));
                if (legWoundsDrop && severe && c.ragdoll.state == Ragdoll.State.Standing)
                    Stagger(s, -c.transform.forward, BrainModuleHitReaction.PushBehaviour.Effect.Destabilize, t);
                else if (c.ragdoll.state == Ragdoll.State.Standing)
                    Stagger(s, -c.transform.forward, BrainModuleHitReaction.PushBehaviour.Effect.StaggerLight, t);
                StartClutch(s, part, point, null, false, false);
                Scream(s, severe);
            }
        }

        /// How long they hold a wound: it follows the pain (0.14 - not a setting). A scratch: a couple of seconds.
        /// Agony: they hold it for a long time.
        internal static float ClutchTime(NpcState s)
            => Mathf.Lerp(2.5f, 16f, Mathf.Clamp01(s.pain * painIntensity * s.painSensitivity)) * Random.Range(0.85f, 1.15f);

        /// Puts a hand on the wound. prefer = which hand to use; dropWeapon = allowed to let go of a weapon to do it.
        /// 0.14: always happens. If the preferred hand is busy the other free hand goes; if both hold something and it
        /// hurts enough, the off-hand lets go; otherwise they go to it the moment a hand is free.
        void StartClutch(NpcState s, RagdollPart part, Vector3 point, Side? prefer, bool wipe, bool dropWeapon)
        {
            var c = s.creature;
            if (s.shocked || s.knockedOut) return;
            var ik = c.ragdoll != null ? c.ragdoll.ik : null;
            if (ik == null) return;

            // remember the wound first, so they come back to it even if no hand is free right now
            if (s.clutchAnchor == null) s.clutchAnchor = new GameObject("RageEngine_Clutch").transform;
            s.clutchAnchor.SetParent(part.transform, false);
            Vector3 outward = point - part.transform.position;
            if (outward.sqrMagnitude < 1e-4f) outward = part.transform.forward;
            outward.Normalize();
            s.clutchAnchor.position = point + outward * 0.04f;
            s.clutchAnchor.rotation = Quaternion.LookRotation(-outward, Vector3.up);
            s.clutchBase = s.clutchAnchor.localPosition;
            if (Pumped(s) && !wipe) { s.nextReclutch = s.rushUntil; return; }   // adrenaline: they don't even notice it yet

            Side side;
            if (prefer.HasValue && !Dead(s, prefer.Value)) side = prefer.Value;
            else if (!Dead(s, Side.Left) && HandFree(c, Side.Left)) side = Side.Left;
            else if (!Dead(s, Side.Right) && HandFree(c, Side.Right)) side = Side.Right;
            else side = !Dead(s, Side.Left) ? Side.Left : Side.Right;           // off-hand first
            if (Dead(s, side)) { side = Other(side); if (Dead(s, side)) return; }

            var hand = Hand(c, side);
            if (hand != null && hand.grabbedHandle != null && !wipe)
            {
                var other = Other(side);
                if (!Dead(s, other) && HandFree(c, other)) { side = other; hand = Hand(c, side); }
                else if ((dropWeapon && Random.value < 0.3f * (1f - 0.7f * s.hpK)) || PainDrops(s) || hand.grabbedHandle is HandleRagdoll)   // 0.25: rarely lets go of a weapon just to clutch
                {
                    var h = hand; Guard("drop", () => h.UnGrab(false));              // hurts too much to keep holding it
                }
                else { s.clutchSide = side; s.nextReclutch = Time.time + Random.Range(0.6f, 1.4f); return; }
            }

            ReleaseClutch(s);
            ik.SetHandAnchor(side, s.clutchAnchor);
            ik.SetHandState(side, true, true);
            s.clutchSide = side; s.clutching = true; s.clutchWipe = wipe;
            float hold = wipe ? 2.5f : ClutchTime(s);
            s.clutchUntil = Time.time + hold;
            s.nextReclutch = s.clutchUntil + Mathf.Lerp(4f, 0.8f, Mathf.Clamp01(s.pain * painIntensity));
            // the game's melee AI drives the arms every frame and pulls the hand off the wound: they stop swinging
            // while they get a hand on it - longer the more it hurts
            if (!wipe && !s.onAdrenaline) SetMelee(s, false, Time.time + Mathf.Min(hold, 1.2f + 6f * s.pain * painIntensity));
        }

        /// Blood running into the eyes: they either wipe it away (quick, repeated) or cover their eyes with one hand
        /// and keep swinging with the other. Keeps happening every few seconds while it's still bleeding.
        void BloodInEyes(NpcState s, RagdollPart head)
        {
            var c = s.creature;
            Vector3 eyes = c.centerEyes != null ? c.centerEyes.position : head.transform.position + c.transform.forward * 0.08f;
            Side? off = HandFree(c, Side.Left) || !HandFree(c, Side.Right) ? Side.Left : Side.Right;   // the hand not holding the main weapon
            StartClutch(s, head, eyes, off, true, false);
            if (!s.clutching) return;
            bool cover = Random.value < 0.5f;
            if (cover) { s.clutchWipe = false; s.clutchUntil = Time.time + Random.Range(2.5f, 4.5f); }
            else s.clutchUntil = Time.time + Random.Range(1.3f, 2.2f);
            // the game's melee AI drives the arms every frame and overrode the hand going to the eyes: pause attacking
            // while they deal with it, then they swing again (blind and angry)
            SetMelee(s, false, s.clutchUntil);
            head.physicBody?.AddForce(-c.transform.forward * 2f + Vector3.down, ForceMode.Impulse);   // head jerks back
            Scream(s, false);
            s.nextEyeWipe = s.clutchUntil + Random.Range(2.5f, 5f);
            Log($"{c.name} blood in eyes: {(cover ? "covers eyes" : "wipes")}");
        }

        /// 0.21: pain bad enough to let go of a weapon (was 55% raw pain - a poke could do it).
        internal static bool PainDrops(NpcState s) => !Pumped(s) && !s.bioNoQuit && s.pain * painIntensity * s.painSensitivity >= 0.92f + 0.06f * s.hpK;

        static void SetMelee(NpcState s, bool on, float until)
        {
            var c = s.creature;
            Guard("melee", () =>
            {
                var m = c.brain.instance?.GetModule<BrainModuleMelee>(false);
                if (m == null) return;
                m.meleeEnabled = on;
                s.meleeOff = !on; s.meleeOffUntil = until;
            });
        }

        /// A cut arm/hand that still works: weak, trembling grip - you can see they don't want to hold the weapon,
        /// but they keep fighting with it.
        static void PainArm(NpcState s, Side side, float seconds)
        {
            float until = Time.time + seconds;
            if (side == Side.Left) s.weakArmLUntil = Mathf.Max(s.weakArmLUntil, until);
            else s.weakArmRUntil = Mathf.Max(s.weakArmRUntil, until);
            Loosen(s, side == Side.Left ? LeftArmParts : RightArmParts, 0.8f, 0.5f);
        }

        static bool HandFree(Creature c, Side side) { var h = Hand(c, side); return h != null && h.grabbedHandle == null; }
        static bool Dead(NpcState s, Side side) => side == Side.Left ? s.leftArmDead : s.rightArmDead;

        internal static void ReleaseClutch(NpcState s)
        {
            if (!s.clutching) return;
            var ik = s.creature != null && s.creature.ragdoll != null ? s.creature.ragdoll.ik : null;
            if (ik != null) { ik.SetHandAnchor(s.clutchSide, null); ik.SetHandState(s.clutchSide, false, false); }
            if (ik != null && s.clutching2) { var o = Other(s.clutchSide); ik.SetHandAnchor(o, null); ik.SetHandState(o, false, false); }
            s.clutching = false; s.clutching2 = false;
        }

        void TickWounds(NpcState s, float dt)
        {
            var c = s.creature;
            if (s.meleeOff && (Time.time >= s.meleeOffUntil || c.isKilled)) SetMelee(s, true, 0f);
            if (c.isKilled && !s.onAdrenaline) { ReleaseClutch(s); return; }
            bool standing = c.ragdoll != null && c.ragdoll.state == Ragdoll.State.Standing;

            if (s.clutching)
            {
                if (Time.time >= s.clutchUntil || Dead(s, s.clutchSide) || (!standing && !s.dismembered && s.pain < 0.4f)) ReleaseClutch(s);
                else if (s.clutchWipe && s.clutchAnchor != null)
                    s.clutchAnchor.localPosition = s.clutchBase + s.clutchAnchor.InverseTransformDirection(Vector3.right) * (Mathf.Sin(Time.time * 9f) * 0.03f);
            }
            else if (s.pain > 0.1f && !Pumped(s) && s.clutchAnchor != null && s.clutchAnchor.parent != null && Time.time >= s.nextReclutch && !c.isKilled)
            {
                // still hurting: goes back to holding the wound, with whichever hand is free (automatic - 0.14)
                Side side = s.clutchSide;
                if (Dead(s, side) || !HandFree(c, side)) side = Other(side);
                if (!Dead(s, side) && HandFree(c, side) && c.ragdoll.ik != null)
                {
                    var ik = c.ragdoll.ik;
                    ik.SetHandAnchor(side, s.clutchAnchor);
                    ik.SetHandState(side, true, true);
                    s.clutchSide = side; s.clutching = true; s.clutchWipe = false;
                    float hold = ClutchTime(s);
                    s.clutchUntil = Time.time + hold;
                    s.nextReclutch = s.clutchUntil + Mathf.Lerp(4f, 0.8f, Mathf.Clamp01(s.pain * painIntensity));   // the more it hurts, the sooner they go back to it
                    if (standing && !s.onAdrenaline) SetMelee(s, false, Time.time + Mathf.Min(hold, 1f + 5f * s.pain * painIntensity));
                }
                else if (PainDrops(s) && standing && !Dead(s, side) && Hand(c, side) != null && Hand(c, side).grabbedHandle != null)
                {
                    var h = Hand(c, side); Guard("drop", () => h.UnGrab(false));   // it hurts too much: lets go to hold it
                    s.nextReclutch = Time.time + 0.3f;
                }
                else s.nextReclutch = Time.time + 1f;
            }

            if (s.eyeBloodUntil > Time.time && !s.clutching && Time.time >= s.nextEyeWipe && standing && c.ragdoll.headPart != null)
                BloodInEyes(s, c.ragdoll.headPart);
            if (s.eyeBloodUntil > 0f && Time.time >= s.eyeBloodUntil)
            {
                s.eyeBloodUntil = 0f;
                UpdateVision(s);
            }

            // pained arms tremble and flinch now and then
            if ((Time.time < s.weakArmLUntil || Time.time < s.weakArmRUntil) && Time.time >= s.nextTremble && standing)
            {
                s.nextTremble = Time.time + Random.Range(1.2f, 2.8f);
                Side side = Time.time < s.weakArmLUntil ? Side.Left : Side.Right;
                var h = Hand(c, side);
                if (h != null && h.physicBody != null) h.physicBody.AddForce(Random.insideUnitSphere * 1.2f, ForceMode.VelocityChange);
                Loosen(s, side == Side.Left ? LeftArmParts : RightArmParts, 0.6f, 0.3f);
            }
            // a dead arm never holds anything again
            if (s.leftArmDead && c.handLeft != null && c.handLeft.grabbedHandle != null) c.handLeft.UnGrab(false);
            if (s.rightArmDead && c.handRight != null && c.handRight.grabbedHandle != null) c.handRight.UnGrab(false);
        }

        static void ClearWounds(NpcState s)
        {
            ReleaseClutch(s);
            if (s.clutchAnchor != null) Object.Destroy(s.clutchAnchor.gameObject);
            s.clutchAnchor = null;
            if (s.clutchAnchor2 != null) Object.Destroy(s.clutchAnchor2.gameObject);
            s.clutchAnchor2 = null;
            Guard("speed", () => s.creature?.locomotion?.globalMoveSpeedMultiplier.Remove(RushHandler));
            var c = s.creature;
            if (c != null)
            {
                Guard("vision", () => { c.detectionFOVModifier.Remove(WoundHandler); c.detectionFOVModifier.Remove(EyeHandler); });
                Guard("blockgrab", () =>
                {
                    if (c.handLeft != null) c.handLeft.SetBlockGrab(false, false);
                    if (c.handRight != null) c.handRight.SetBlockGrab(false, false);
                });
            }
            s.leftArmDead = s.rightArmDead = s.legWounded = false;
            s.eyeBloodUntil = 0f;
        }
    }
}

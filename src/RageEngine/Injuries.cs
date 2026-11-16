// Nomad Rage Engine - pain, blunt-force fractures (arm, leg, jaw), open fractures, internal bleeding, coughing blood.
// How you know a bone broke:
//   - loud bone snap + crunch (WAVs shipped in the mod's Sounds folder, loaded at runtime - no asset bundle needed)
//   - sharp double buzz on the controller that did it ("Break vibration" option)
//   - they cry out, and the limb goes dead: a broken arm drops its weapon and dangles on physics, a broken leg buckles
//     (they go down) and drags/limps afterwards; a broken jaw hangs open and crooked
//   - open fracture: a bone shard sticks out of the limb and it bleeds
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const float BrokenLegSpeed = 0.55f;

        void ApplyInjuries(NpcState s, CollisionInstance hit, RagdollPart part, RagdollPart.Type t, float severity)
        {
            var ds = hit.damageStruct;
            s.pain = Mathf.Min(1f, s.pain + 0.2f * severity * painIntensity);
            if (Has(s.brokenParts, LimbOf(t)) && LimbOf(t) != 0) Scream(s, false);    // hitting a broken limb hurts

            if (ds.damageType == DamageType.Blunt && Has(t, RagdollPart.Type.Torso))
                BluntTorso(s, hit, part, severity);
            else if (fracturesEnabled && ds.damageType == DamageType.Blunt)
                TryFracture(s, hit, part, t, BoneSeverity(hit));

            if (bleedingEnabled && bleedingRate > 0f && ds.penetration != DamageStruct.Penetration.None
                && (ds.damageType == DamageType.Pierce || ds.damageType == DamageType.Slash))
            {
                bool neck = Has(t, RagdollPart.Type.Neck), chest = Has(t, RagdollPart.Type.Torso);
                if (neck || chest)
                {
                    s.bleedRate += (neck ? 0.8f : 0.35f) * bleedingRate;
                    if (!s.bloodInAirway) { s.bloodInAirway = true; s.nextCoughTime = Time.time + Random.Range(1.5f, 3f); }
                }
            }
        }

        void TryFracture(NpcState s, CollisionInstance hit, RagdollPart part, RagdollPart.Type t, float severity)
        {
            // Bones take real punishment. A limb breaks from ONE very heavy blunt hit, or from repeated heavy hits on the
            // same limb (bone stress builds up and slowly fades). Holding the limb while you hit it (Haymaker) breaks it
            // from a solid hit. A normal punch / light weapon hit never breaks anything.
            bool held = grabBreaks && PlayerHolding(s, part);
            float tough = boneToughness;
            if (EnvironmentHit(hit) && severity < 4f * tough) return;                    // 0.22: tripping / falling over doesn't snap bones
            var c = s.creature;

            if (Has(t, RagdollPart.Type.Head))
            {
                if (severity < 1.8f * tough || Random.value > breakChance) return;
                // jaw = lower half of the head
                if (!jawBreaks || s.jawBroken || c.jaw == null || hit.contactPoint.y > part.transform.position.y) return;
                s.jawBroken = true;
                s.jawSkew = Random.Range(-12f, 12f);
                s.jawRest = c.jaw.localRotation;
                s.jawTorn = severity >= boneToughness * 1.6f;                     // smashed loose: hangs and swings
                if (s.jawTorn) { s.bleedRate += 0.4f * bleedingRate; RageEngineScript.Blood(c.jaw); }
                Guard("jaw", () => { var speak = c.brain.instance.GetModule<BrainModuleSpeak>(false); if (speak != null) speak.moveMouth = false; });
                Sfx.Play("NRE_BoneSnap_Jaw", c.jaw, 1f);
                Sfx.Play("NRE_BoneCrunch", c.jaw, 0.7f);
                RageEngineScript.Blood(c.jaw);
                BreakFeedback(hit);
                s.pain = 1f; s.balance = Mathf.Max(MinBalance, s.balance - 0.3f);
                Scream(s, true);
                Log($"{c.name} JAW BROKEN");
                return;
            }

            // 0.20: hands break too - grip gone, weapon dropped
            if (Has(t, RagdollPart.Type.LeftHand | RagdollPart.Type.RightHand))
            {
                if (fracturesEnabled && severity >= tough * 1.1f && Random.value < breakChance) BreakHand(s, part, hit.contactPoint, severity);
                return;
            }
            var limb = LimbOf(t);
            if (limb == 0 || Has(s.brokenParts, limb) || severity < 0.4f) return;
            float stress = AddStress(s, limb, severity);
            bool snap = held ? severity >= 0.6f * tough : severity >= 2.2f * tough || stress >= 4f * tough;
            if (!snap) return;
            if (!held && Random.value > breakChance) { AddStress(s, limb, -stress * 0.3f); return; }   // the bone held - this time
            bool open = held || severity >= 3.3f * tough || Random.value < openFractureChance;
            BreakFeedback(hit);
            Fracture(s, part, hit.contactPoint, severity, open);
        }

        /// How hard a blunt hit was for the bone: damage and impact speed (not the game's push level, which is high
        /// for almost every punch and made bones snap far too easily before 0.11).
        static float BoneSeverity(CollisionInstance hit)
            => Mathf.Max(hit.damageStruct.damage / 20f, (hit.impactVelocity.magnitude - 2f) / 4f, 0f);

        static float AddStress(NpcState s, RagdollPart.Type limb, float add)
        {
            if (limb == LeftArmParts) return s.stressLA = Mathf.Max(0f, s.stressLA + add);
            if (limb == RightArmParts) return s.stressRA = Mathf.Max(0f, s.stressRA + add);
            if (limb == LeftLegParts) return s.stressLL = Mathf.Max(0f, s.stressLL + add);
            if (limb == RightLegParts) return s.stressRL = Mathf.Max(0f, s.stressRL + add);
            return 0f;
        }

        /// Breaks the limb this part belongs to (arm or leg). forceOpen = always push the bone through the skin.
        void Fracture(NpcState s, RagdollPart part, Vector3 point, float severity, bool forceOpen)
        {
            var c = s.creature;
            var t = part.type;
            RagdollPart.Type limb = LimbOf(t);
            if (limb == 0 || Has(s.brokenParts, limb)) return;
            bool leg = Has(limb, LegParts);
            s.brokenParts |= limb;
            s.pain = 1f;

            Sfx.Play(leg ? "NRE_BoneSnap_Leg" : "NRE_BoneSnap_Arm", part.transform, 1f);
            Sfx.Play("NRE_BoneCrunch", part.transform, 0.75f);
            Scream(s, true);

            if (forceOpen)
            {
                Vector3 outDir = (point - part.transform.position);
                Guard("bonemesh", () => s.boneMeshes.Add(BoneMesh.Attach(part, point, outDir)));
                Guard("gore", () => AddCrater(s, part, point, outDir.normalized, 1.5f));
                Stamp(c, point, 0.055f, "open_fracture");
                RageEngineScript.Blood(part.transform);
                s.bleedRate += 0.3f * bleedingRate;
            }
            Guard("joint", () => LoosenJoint(part));
            MakeLimp(s, limb);
            Guard("fracturevis", () => FractureLook(s, part, point, forceOpen));
            BoneBreakShock(s, part, severity);                                    // 0.20: every break - scream + full-body jolt
            if (s.identity != null && leg) s.identity.brokenLegs |= 1;

            if (leg)
            {
                s.legWounded = true;
                s.balance = MinBalance;
                Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Add(InjuryHandler, BrokenLegSpeed));
                if (c.ragdoll.state == Ragdoll.State.Standing) Stagger(s, -c.transform.forward, BrainModuleHitReaction.PushBehaviour.Effect.Destabilize, t);
            }
            else
            {
                KillArm(s, Has(limb, LeftArmParts) ? Side.Left : Side.Right);
                s.balance = Mathf.Max(MinBalance, s.balance - 0.3f);
            }
            Log($"{c.name} {(leg ? "LEG" : "ARM")} BROKEN ({t})");
        }

        /// Blunt trauma to the chest/stomach: winded (doubles over, holds it) and, if hard enough, cracked ribs.
        void BluntTorso(NpcState s, CollisionInstance hit, RagdollPart part, float severity)
        {
            var c = s.creature;
            float now = Time.time;
            if (severity >= 0.5f) s.bodyShots++;
            if (severity >= 0.5f && now >= s.nextWinded)
            {
                s.nextWinded = now + 1.5f;
                Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Neck | RagdollPart.Type.Head, Mathf.Clamp(0.5f + severity * 0.2f, 0.5f, 0.85f), 0.9f);
                s.pain = Mathf.Min(1f, s.pain + 0.15f * severity * painIntensity * (s.ribsBroken > 0 ? 1.8f : 1f));
                StartClutch(s, part, hit.contactPoint, null, false, false);
                if (s.clutching) s.clutchUntil = now + 1.5f;
            }
            if (!fracturesEnabled || severity < boneToughness * 0.8f) return;
            if (Random.value > breakChance * (1f + 0.5f * (severity - boneToughness))) return;
            if (s.ribsBroken >= 4) return;                                        // 0.17: stop re-cracking forever
            s.ribsBroken++;
            NoteRib(s, part, hit.contactPoint);
            if (severity >= boneToughness * 1.2f) Guard("ribspoke", () => RibsPoke(s, part, hit.contactPoint, severity));
            if (severity >= boneToughness * 2.6f && s.ribsBroken >= 2) Guard("ribbone", () => { s.boneMeshes.Add(BoneMesh.Attach(part, hit.contactPoint, hit.contactPoint - part.transform.position, "RE_Bone_Rib")); Stamp(s.creature, hit.contactPoint, 0.045f, "open_fracture"); RageEngineScript.Blood(part.transform); });
            s.ribPart = part; s.ribLocal = part.transform.InverseTransformPoint(hit.contactPoint);
            s.ribHesitateAt = now + Random.Range(2f, 4f); s.ribGuardAt = now + 3f;
            // a heavy blow caves the chest wall in a little (real mesh deformation)
            if (severity >= boneToughness * 1.4f)
            {
                Transform bone = part.meshBone != null ? part.meshBone : part.bone != null ? part.bone.mesh : null;
                Vector3 inward = (part.transform.position - hit.contactPoint).normalized;
                if (bone != null) Guard("ribdent", () => HeadDeform.Dent(c, bone, hit.contactPoint, inward, 0.05f, 0.009f * Mathf.Clamp(severity / boneToughness, 1f, 2f)));
            }
            s.pain = 1f;
            s.balance = Mathf.Max(MinBalance, s.balance - 0.3f);
            Sfx.PlayAt("NRE_RibCrack", hit.contactPoint, 1f);
            if (LungsHurt(s)) SpitBlood(s, 2);                                     // 0.26: only with both sides broken or a long body beating
            BreakFeedback(hit);
            Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Neck | RagdollPart.Type.Head, 0.9f, 1.4f);
            StartClutch(s, part, hit.contactPoint, null, false, false);
            if (s.clutching) s.clutchUntil = now + 2.5f;
            if (LungsHurt(s) && bleedingEnabled) { s.bloodInAirway = true; s.nextCoughTime = now + Random.Range(1.5f, 3f); }
            Scream(s, true);
            Log($"{c.name} RIBS CRACKED ({s.ribsBroken})");
        }

        /// The arm is out of action: drops what it holds, can never grab again, hangs on physics.
        internal static void KillArm(NpcState s, Side side)
        {
            var c = s.creature;
            if (side == Side.Left) s.leftArmDead = true; else s.rightArmDead = true;
            var hand = Hand(c, side);
            Handle held = null;
            if (hand != null)
            {
                held = hand.grabbedHandle;
                Guard("drop", () => { if (hand.grabbedHandle != null) hand.UnGrab(false); });
                Guard("blockgrab", () => hand.SetBlockGrab(true, true));
            }
            // they don't give up: the weapon goes to the good hand and they keep fighting
            var other = Hand(c, Other(side));
            if (held != null && !(held is HandleRagdoll) && other != null && other.grabbedHandle == null && !Dead(s, Other(side)))
                Guard("swap", () => other.Grab(held));
            if (s.clutching && s.clutchSide == side) ReleaseClutch(s);
            MakeLimp(s, side == Side.Left ? LeftArmParts : RightArmParts);
        }

        /// Sharp double buzz on the player's hand that landed the blow (or both hands if unknown).
        static void BreakFeedback(CollisionInstance hit)
        {
            if (!breakHaptics) return;
            Guard("haptics", () =>
            {
                Side? side = null;
                var col = hit.sourceCollider;
                if (col != null)
                {
                    var rh = col.GetComponentInParent<RagdollHand>();
                    if (rh != null && rh.creature != null && rh.creature.isPlayer) side = rh.side;
                    else
                    {
                        var item = col.GetComponentInParent<Item>();
                        if (item != null && item.mainHandler != null && item.mainHandler.creature != null && item.mainHandler.creature.isPlayer) side = item.mainHandler.side;
                    }
                }
                if (side.HasValue) Haptics.Pulse(side.Value, 1f, 2);
                else { Haptics.Pulse(Side.Left, 0.7f, 2); Haptics.Pulse(Side.Right, 0.7f, 2); }
            });
        }

        /// Opens the joint limits so the broken limb bends in ways a healthy one can't.
        static void LoosenJoint(RagdollPart part)
        {
            var j = part.characterJoint;
            if (j == null) return;
            RememberLimits(j);
            var s1 = j.swing1Limit; s1.limit = Mathf.Max(s1.limit, 120f); j.swing1Limit = s1;
            var s2 = j.swing2Limit; s2.limit = Mathf.Max(s2.limit, 100f); j.swing2Limit = s2;
            var lo = j.lowTwistLimit; lo.limit = Mathf.Min(lo.limit, -110f); j.lowTwistLimit = lo;
            var hi = j.highTwistLimit; hi.limit = Mathf.Max(hi.limit, 110f); j.highTwistLimit = hi;
        }

        /// After animation: a broken jaw hangs open and crooked (absolute pose - it can never spin); a smashed jaw
        /// hangs much lower and swings loose with every head movement.
        void LateUpdateJaws()
        {
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active || !s.jawBroken || s.creature == null || s.creature.jaw == null) continue;
                var jaw = s.creature.jaw;
                if (s.jawAxis == Vector3.zero) s.jawAxis = JawAxis(s.creature);     // 0.17: the rig's real hinge axis
                float open = 14f;
                if (s.jawTorn)
                {
                    // pendulum driven by head motion
                    var head = s.creature.ragdoll != null ? s.creature.ragdoll.headPart : null;
                    Vector3 hp = head != null ? head.transform.position : jaw.position;
                    Vector3 v = (hp - s.lastHeadPos) / Mathf.Max(Time.deltaTime, 0.001f);
                    s.lastHeadPos = hp;
                    float accel = Mathf.Clamp(-v.y * 6f + Random.Range(-2f, 2f), -60f, 60f);
                    s.jawSwingVel += (accel - s.jawSwing * 30f - s.jawSwingVel * 4f) * Time.deltaTime;
                    s.jawSwing = Mathf.Clamp(s.jawSwing + s.jawSwingVel * Time.deltaTime, -14f, 14f);
                    open = 24f + s.jawSwing * 0.6f;
                }
                // always relative to the stored rest pose (never to last frame's) and clamped: it can't spin
                open = Mathf.Clamp(open, 0f, 32f);
                s.jawOpenCur = Mathf.Lerp(s.jawOpenCur, open, 1f - Mathf.Exp(-12f * Time.deltaTime));
                float skew = Mathf.Clamp(s.jawSkew, -8f, 8f);
                jaw.localRotation = s.jawRest * Quaternion.AngleAxis(s.jawOpenCur, s.jawAxis) * Quaternion.AngleAxis(skew, Vector3.Cross(s.jawAxis, Vector3.right).sqrMagnitude > 0.1f ? Vector3.Cross(s.jawAxis, Vector3.right).normalized : Vector3.up);
            }
        }

        void TickInjuries(NpcState s, float dt)
        {
            var c = s.creature;
            if (s.pain > 0f) s.pain = Mathf.Max(0f, s.pain - 0.03f * dt);
            float heal = 0.05f * dt;                                              // bone stress fades slowly
            s.stressLA = Mathf.Max(0f, s.stressLA - heal); s.stressRA = Mathf.Max(0f, s.stressRA - heal);
            s.stressLL = Mathf.Max(0f, s.stressLL - heal); s.stressRL = Mathf.Max(0f, s.stressRL - heal);
            // cracked ribs: every breath hurts
            if (s.ribsBroken > 0 && Time.time >= s.nextRibWince && c.ragdoll.state == Ragdoll.State.Standing)
            {
                s.nextRibWince = Time.time + Random.Range(4f, 8f);
                Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.35f, 0.45f);
            }
            // 0.17 broken ribs change how they fight: they guard that side, hesitate before swinging (it hurts to
            // twist), move slower, and grunt when they exert themselves
            if (s.ribsBroken > 0 && !c.isKilled && c.ragdoll.state == Ragdoll.State.Standing && !Pumped(s))
            {
                float now2 = Time.time;
                if (now2 >= s.ribGuardAt && !s.clutching && s.ribPart != null)
                {
                    s.ribGuardAt = now2 + Random.Range(5f, 9f) / Mathf.Max(1f, s.ribsBroken * 0.7f);
                    StartClutch(s, s.ribPart, s.ribPart.transform.TransformPoint(s.ribLocal), null, false, false);
                    if (s.clutching) s.clutchUntil = now2 + Random.Range(1.2f, 2.5f);
                }
                if (now2 >= s.ribHesitateAt)
                {
                    s.ribHesitateAt = now2 + Random.Range(3.5f, 7f);
                    SetMelee(s, false, now2 + Random.Range(0.5f, 1.1f) * Mathf.Min(2f, s.ribsBroken));
                    Scream(s, false);
                }
                Guard("ribspeed", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(RibHandler); c.locomotion.globalMoveSpeedMultiplier.Add(RibHandler, Mathf.Max(0.65f, 1f - 0.1f * s.ribsBroken)); });
            }
            if (c.isKilled && !s.onAdrenaline) return;

            if (bleedingEnabled && s.bleedRate > 0f)
            {
                s.bleedAccum += s.bleedRate * bleedingRate * dt;
                if (s.bleedAccum >= 1f)
                {
                    float dmg = Mathf.Floor(s.bleedAccum); s.bleedAccum -= dmg;
                    if (!bleedOutKills) dmg = Mathf.Min(dmg, Mathf.Floor(c.currentHealth - 1f));     // 0.21: bleeding never takes the last HP
                    if (!s.onAdrenaline && dmg >= 1f) Guard("bleed", () => c.Damage(dmg));
                }
                s.bleedRate = Mathf.Max(0f, s.bleedRate - s.bleedRate * 0.01f * dt);
                if (s.bleedRate < 0.02f) { s.bleedRate = 0f; s.bloodInAirway = false; }
            }

            // coughing up blood: doubles over, blood and a wet gurgle at the mouth
            if (s.bloodInAirway && Time.time >= s.nextCoughTime)
            {
                // coughing blood: head jerks forward, blood at the mouth (no balance loss, no trunk weakening -
                // that dropped NPCs out of nowhere before 0.12)
                Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck, 0.45f, 0.4f);
                var hp = c.ragdoll.headPart;
                if (hp != null && hp.physicBody != null) hp.physicBody.AddForce(c.transform.forward * 1.5f + Vector3.down, ForceMode.Impulse);
                s.pain = Mathf.Min(1f, s.pain + 0.1f * painIntensity);
                var mouth = Mouth(c);
                RageEngineScript.Blood(mouth);
                s.nextCoughTime = Time.time + Random.Range(3f, 6f);
            }
        }
    
        /// Which local axis of the jaw bone opens the mouth on this rig: the one that swings the chin DOWN.
        static Vector3 JawAxis(Creature c)
        {
            var jaw = c.jaw;
            Vector3 fwd = c.centerEyes != null ? c.centerEyes.forward : c.transform.forward;
            Vector3 chin = jaw.position + fwd * 0.07f - Vector3.up * 0.03f;
            Vector3 chinLocal = Quaternion.Inverse(jaw.rotation) * (chin - jaw.position);
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3 best = Vector3.right; float bestScore = float.NegativeInfinity;
            foreach (var a in axes)
            {
                Vector3 moved = jaw.position + jaw.rotation * (Quaternion.AngleAxis(12f, a) * chinLocal);
                float score = chin.y - moved.y;                                    // how far the chin drops
                if (score > bestScore) { bestScore = score; best = a; }
            }
            Log($"{c.name}: jaw hinge axis {best}");
            return best;
        }
    
        /// 0.18: what a broken limb LOOKS like. Every break: the limb visibly kinks at the break (real mesh
        /// deformation), the skin there swells and goes dark red-purple (painted into the skin). Open fractures: a
        /// ragged dark-red wound painted around where the bone comes through.
        void FractureLook(NpcState s, RagdollPart part, Vector3 point, bool open)
        {
            var c = s.creature;
            Transform bone = part.meshBone != null ? part.meshBone : part.bone != null ? part.bone.mesh : null;
            Vector3 axis = LimbAxis(part);
            Vector3 outward = Vector3.ProjectOnPlane(point - part.transform.position, axis);
            if (outward.sqrMagnitude < 1e-4f) outward = Vector3.ProjectOnPlane(c.transform.forward, axis);
            outward.Normalize();
            int kinked = 0;
            if (bone != null)
                kinked = 0;                                                                            // 0.33: no bone bumps (read as 3D welts)
            var tex = TexGen.Bruise();
            SkinPaint.Paint(c, point, 0.05f, new Color(0.38f, 0.1f, 0.22f, 0.65f), tex);
            SkinPaint.Paint(c, point + axis * 0.04f, 0.035f, new Color(0.3f, 0.1f, 0.28f, 0.5f), tex);
            if (open)
                for (int i = 0; i < 3; i++) SkinPaint.Paint(c, point + Random.insideUnitSphere * 0.012f, 0.022f, new Color(0.35f, 0.02f, 0.03f, 0.85f), tex);
            Log($"{c.name}: fracture shows - kink on {kinked} mesh(es){(open ? ", open wound" : "")}");
        }
    }
}

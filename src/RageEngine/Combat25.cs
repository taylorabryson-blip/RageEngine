// Rage Engine 0.25.3 - hand-to-hand and takedowns.
//   UPPERCUTS     a rising fist/blunt hit on the jaw snaps the head back and lifts the body - weak or low-HP NPCs fold
//                 and go up, hard ones get rocked (daze, jaw crunch) and can be knocked out.
//   KNOCKBACK     kicks and strong hits shove NPCs back (0.25.3: toned way down - a shove, not a launch; only a
//                 weakened NPC gets knocked off his feet).
//   PUNCH-THROUGH a fist (or a heavy blunt weapon) driven into the chest fast enough goes through: instant kill.
//                 Tough NPCs (high HP, super serums) take it as a huge blow instead. Option: Deaths > Punch-through kills.
//   MOUTH COVER   hand over his mouth: hold it and he passes out; squeeze the TRIGGER and you break his jaw.
//   NECK SNAP     grab his neck (or head) and press the SPELL / cast button on that hand - the neck breaks.
//                 Just holding or twisting him does nothing (0.25.3). Option: Deaths > Neck snaps.
//   BLACK EYES    blunt hits around the eyes: a purple-black ring and the lid swells shut, stage by stage.
//   INTEGRITY     every broken bone, cracked rib, bleed, deep wound and the pain itself takes points off his
//                 structural integrity; low integrity = he trips and wobbles, very low = he can't stand. Hard rules:
//                 both legs broken, or a broken leg with both arms broken = he can't get up; broken spine = no
//                 movement at all; a broken limb isn't used.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        public static bool punchThrough = false;                                    // 0.42: removed (feedback)
        public static bool neckSnaps = true;
        public static float punchThroughSpeed = 6f;

        // ------------------------------------------------------------------ hit source
        static bool PlayerSource(CollisionInstance hit, out RagdollPart ppart, out Item pitem, out float speed)
        {
            ppart = null; pitem = null; speed = hit.impactVelocity.magnitude;
            var col = hit.sourceCollider;
            if (col == null) return false;
            pitem = col.GetComponentInParent<Item>();
            if (pitem != null)
            {
                bool mine = pitem.mainHandler != null && pitem.mainHandler.creature != null && pitem.mainHandler.creature.isPlayer;
                if (mine && pitem.physicBody != null) speed = Mathf.Max(speed, pitem.physicBody.velocity.magnitude);
                return mine;
            }
            ppart = col.GetComponentInParent<RagdollPart>();
            bool p = ppart != null && ppart.ragdoll != null && ppart.ragdoll.creature != null && ppart.ragdoll.creature.isPlayer;
            if (p && ppart.physicBody != null) speed = Mathf.Max(speed, ppart.physicBody.velocity.magnitude);
            return p;
        }

        static float HpFrac(Creature c) => c.maxHealth > 0f ? Mathf.Clamp01(c.currentHealth / c.maxHealth) : 1f;

        /// From HandleHit.
        void Combat25Hit(NpcState s, CollisionInstance hit, RagdollPart part, RagdollPart.Type t, float severity)
        {
            var c = s.creature;
            if (c.isKilled || !PlayerSource(hit, out var ppart, out var pitem, out float speed)) return;
            float now = Time.time;
            var dt = hit.damageStruct.damageType;
            Vector3 vel = hit.impactVelocity.sqrMagnitude > 0.01f ? hit.impactVelocity : (part.transform.position - hit.contactPoint);
            Vector3 dir = vel.normalized;
            bool fist = ppart != null && Has(ppart.type, RagdollPart.Type.LeftHand | RagdollPart.Type.RightHand | RagdollPart.Type.LeftArm | RagdollPart.Type.RightArm);
            bool kick = ppart != null && Has(ppart.type, LegParts);
            float mass = pitem != null && pitem.physicBody != null ? pitem.physicBody.mass : 0f;
            bool heavyBlunt = pitem != null && dt == DamageType.Blunt && mass >= 2.2f;
            float hpLow = 1f - HpFrac(c);
            var chest = ChestPart(c);

            // ---- punch-through (0.49: JoJo-style - he hangs on your arm until you pull it out)
            // 0.53: punch-through removed

            // ---- uppercut: a rising blow to the jaw
            bool rising = Vector3.Dot(dir, Vector3.up) > 0.45f;
            if (dt == DamageType.Blunt && rising && Has(t, RagdollPart.Type.Head | RagdollPart.Type.Neck) && speed > 3f)
            {
                float k = Mathf.Clamp01((speed - 3f) / 6f) * (1f + 0.5f * hpLow) * (heavyBlunt ? 1.3f : 1f);
                var head = c.ragdoll.headPart;
                Vector3 back = -Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized;
                if (head != null && head.physicBody != null) head.physicBody.AddForce((Vector3.up * 0.8f + back * 0.5f) * (1.2f + 2.4f * k), ForceMode.VelocityChange);
                if (chest != null && chest.physicBody != null) chest.physicBody.AddForce((Vector3.up * 0.9f + back * 0.3f) * (0.3f + 1.2f * k), ForceMode.VelocityChange);
                Sfx.Play("NRE_BoneCrunch", head != null ? head.transform : part.transform, 0.55f + 0.4f * k, 1.25f);
                s.daze = Mathf.Max(s.daze, 0.7f + 0.3f * k);
                s.drunk = Mathf.Max(s.drunk, 0.5f * k);
                // only a big shot on a hurt man (or a near-dead one) takes his legs - healthy NPCs get rocked and stay up
                if (!Tank(s) && ((k > 0.9f && hpLow > 0.6f) || hpLow > 0.85f))
                {
                    AllowFall(s, 3f);
                    Guard("up", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
                }
                Log($"{WhoIs(s)}: UPPERCUT ({speed:0.0} m/s, power {k:0.00})");
                if (Random.value < (k * 0.12f + hpLow * 0.08f) / Tough33(s)) Knockout(s, Vector3.up, true);
                return;
            }

            // ---- knockback: a shove, not a launch (0.25.2 sent them flying into walls and the impact killed them).
            //      Velocity is capped low and only the hit part and the chest get it; he only goes down if he's badly hurt.
            if ((kick && speed > 3f) || speed > 7f || (heavyBlunt && speed > 4f))
            {
                float launch = speed * (kick ? 0.2f : 0.08f) * (0.35f + 0.8f * hpLow) * (heavyBlunt ? 1.2f : 1f) / Tough33(s);      // 0.28.3: less shove; 0.29: Stay on feet      // 0.28.2: back to 0.25.3, a touch more
                if (launch > 0.5f)
                {
                    float cap = kick ? 2.4f : 1.5f;
                    Vector3 push = (Vector3.ProjectOnPlane(dir, Vector3.up).normalized + Vector3.up * 0.1f).normalized * Mathf.Min(launch, cap);
                    bool drop = !Tank(s) && c.ragdoll.state == Ragdoll.State.Standing && kick && hpLow > 0.75f && launch > 1.6f;
                    if (drop)
                    {
                        AllowFall(s, 2f);
                        Guard("kb", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
                    }
                    else Guard("kb.stagger", () => Stagger(s, dir, BrainModuleHitReaction.PushBehaviour.Effect.StaggerMedium, t));
                    if (part.physicBody != null) part.physicBody.AddForce(push, ForceMode.VelocityChange);
                    if (drop && chest != null && chest != part && chest.physicBody != null) chest.physicBody.AddForce(push * 0.6f, ForceMode.VelocityChange);
                    Log($"{WhoIs(s)}: knocked back ({(kick ? "kick" : "blow")} {speed:0.0} m/s, HP {(1f - hpLow) * 100f:0}%{(drop ? ", off his feet" : "")})");
                }
            }

            // ---- black eyes
            if (dt == DamageType.Blunt && Has(t, RagdollPart.Type.Head) && c.allEyes != null)
                for (int i = 0; i < c.allEyes.Count && i < 4; i++)
                {
                    var e = c.allEyes[i];
                    if (e == null || Vector3.Distance(hit.contactPoint, e.transform.position) > 0.07f) continue;
                    int st = s.blackEye[i] = Mathf.Min(4, s.blackEye[i] + (severity > 1.2f ? 2 : 1));
                    var ep = e.transform.position;
                    Stamp(c, ep - Vector3.up * 0.005f, 0.035f + 0.006f * st, "black_eye", Mathf.Min(0.95f, 0.35f + 0.15f * st));
                    var head = c.ragdoll.headPart;
                    var bone = head != null ? (head.meshBone != null ? head.meshBone : head.bone != null ? head.bone.mesh : null) : null;
                    var ce = c.centerEyes;
                    if (false && bone != null && ce != null) Guard("swell", () => HeadDeform.Dent(c, bone, ep + ce.forward * 0.01f - ce.up * 0.008f, ce.forward, 0.022f, 0.0025f * st));
                    if (st >= 3) Log($"{WhoIs(s)}: {e.eyeTag} eye swelling shut");
                }
        }

        // ------------------------------------------------------------------ spell-button presses, every frame
        static readonly float[] castDownAt = { -1f, -1f };
        static readonly bool[] castHeld = new bool[2];
        internal static float lastCastAt = -10f;
        static void PollCastInput()
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) return;
            for (int i = 0; i < 2; i++)
            {
                var h = i == 0 ? pc.handLeft : pc.handRight;
                bool down = false;
                try { down = h != null && h.playerHand != null && h.playerHand.controlHand != null && (h.playerHand.controlHand.castPressed || h.playerHand.controlHand.usePressed); } catch { }
                if (down && !castHeld[i]) castDownAt[i] = Time.time;
                if (down) lastCastAt = Time.time;
                castHeld[i] = down;
            }
        }

        // ------------------------------------------------------------------ takedowns (0.1 s tick, only near the player)
        void TickTakedowns(NpcState s, float now)
        {
            var c = s.creature;
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null || c.isKilled || s.knockedOutUntil > 0f || c.ragdoll.headPart == null || c.centerEyes == null) { s.mouthT = 0f; return; }
            if ((c.ragdoll.headPart.transform.position - pc.transform.position).sqrMagnitude > 2.5f) { s.mouthT = 0f; return; }
            var ce = c.centerEyes; var head = c.ragdoll.headPart;
            Vector3 mouth = ce.position - ce.up * 0.085f + ce.forward * 0.07f;
            int onHead = 0; bool covering = false, trigger = false, snapPress = false; RagdollHand cover = null;
            for (int hi = 0; hi < 2; hi++)
            {
                var h = hi == 0 ? pc.handLeft : pc.handRight;
                if (h == null) continue;
                var hr = h.grabbedHandle as HandleRagdoll;
                bool holdsNeck = hr != null && hr.ragdollPart != null && hr.ragdollPart.ragdoll == c.ragdoll && Has(hr.ragdollPart.type, RagdollPart.Type.Head | RagdollPart.Type.Neck);
                bool wasHolding = hi == 0 ? s.neckGrabL : s.neckGrabR;
                if (holdsNeck && !wasHolding) s.neckGrabAt = now;
                if (hi == 0) s.neckGrabL = holdsNeck; else s.neckGrabR = holdsNeck;
                // a press of the spell button that started after he was grabbed (polled every frame in PollCastInput)
                if (holdsNeck) { onHead++; if (castDownAt[hi] > s.neckGrabAt + 0.1f && castDownAt[hi] > now - 0.35f && castDownAt[hi] > s.neckSnapUsedAt) { snapPress = true; s.neckSnapUsedAt = castDownAt[hi]; } }
                if (h.grabbedHandle != null) continue;                                   // mouth cover = an open, empty hand
                Vector3 palm = h.transform.position + h.PalmDir.normalized * 0.03f;
                if ((palm - mouth).sqrMagnitude < 0.075f * 0.075f)
                {
                    covering = true; cover = h;
                    bool tr = false; Guard("input", () => tr = h.playerHand != null && h.playerHand.controlHand != null && h.playerHand.controlHand.usePressed);
                    trigger |= tr;
                }
            }
            // ---- neck snap: hold the neck or head, press the spell (cast) button on that hand
            if (neckSnaps && onHead > 0 && snapPress && Tank(s))
            {
                Sfx.Play("NRE_BoneCrunch", head.transform, 0.35f, 0.7f);
                s.rushUntil = Mathf.Max(s.rushUntil, now + 2f);
                Log($"{WhoIs(s)}: you wrench his neck - it doesn't give (super soldier)");
            }
            else if (neckSnaps && onHead > 0 && snapPress)
            {
                Sfx.Play("NRE_BoneSnap_Arm", head.transform, 1f, 0.75f);
                Sfx.Play("NRE_BoneCrunch", head.transform, 0.6f, 0.9f);
                MakeLimp(s, RagdollPart.Type.Neck | RagdollPart.Type.Head);
                Loosen(s, RagdollPart.Type.Neck | RagdollPart.Type.Head, 1f, 60f);
                s.neckSnapped = true; s.finalKill = true;
                Guard("kill", () => c.Kill());
                Log($"{WhoIs(s)}: NECK SNAPPED (spell button while holding his {(onHead > 1 ? "head with both hands" : "neck")})");
                RageMoment("neck snap");
                return;
            }
            // ---- hand over the mouth
            if (!covering) { s.mouthT = Mathf.Max(0f, s.mouthT - 0.2f); return; }
            s.mouthT += 0.1f;
            if (s.mouthT < 0.15f) { Log($"{WhoIs(s)}: hand over his mouth"); Speak(s, "muffled"); }
            if (trigger && s.mouthT > 0.3f && !s.jawBroken)
            {
                // the existing broken-jaw system: the jaw hangs skewed, the mouth stops moving with speech
                s.jawBroken = true;
                if (c.jaw != null) { s.jawSkew = Random.Range(-14f, 14f); s.jawRest = c.jaw.localRotation; s.jawTorn = true; }
                Guard("jawspeak", () => { var speak = c.brain.instance.GetModule<BrainModuleSpeak>(false); if (speak != null) speak.moveMouth = false; });
                Sfx.Play("NRE_BoneSnap_Jaw", c.jaw != null ? c.jaw : head.transform, 1f);
                Sfx.Play("NRE_BoneCrunch", head.transform, 0.8f, 1.1f);
                s.bleedRate += 0.3f * bleedingRate;
                SpawnGoreModels(mouth, ce.forward + Vector3.up * 0.2f, "RE_Tooth", Random.Range(1, 3), ToothMat(), 0.8f, c);
                Guard("spit", () => SpitBlood(s, 2));
                s.pain = Mathf.Min(1f, s.pain + 0.6f); s.daze = 1f;
                Scream(s, true);
                Log($"{WhoIs(s)}: JAW BROKEN (crushed with the hand over his mouth)");
                if (Random.value < 0.5f + 0.4f * (1f - HpFrac(c))) Knockout(s, -ce.forward, true);
                return;
            }
            if (Random.value < 0.15f) Speak(s, "muffled");
            if (cover != null && cover.physicBody != null && Random.value < 0.3f) Guard("haptic", () => { });
            float need2 = Mathf.Lerp(3f, 6f, s.hpK) * (Pumped(s) ? 1.5f : 1f);
            if (s.mouthT >= need2)
            {
                Log($"{WhoIs(s)}: smothered - passes out ({s.mouthT:0.0} s)");
                s.mouthT = 0f;
                Knockout(s, -ce.forward, true);
            }
        }

        // ------------------------------------------------------------------ structural integrity
        void TickIntegrity(NpcState s, float now)
        {
            var c = s.creature;
            if (c.isKilled) return;
            bool lLeg = Has(s.brokenParts, LeftLegParts) || s.lostLeg, rLeg = Has(s.brokenParts, RightLegParts);
            bool lArm = Has(s.brokenParts, LeftArmParts), rArm = Has(s.brokenParts, RightArmParts);
            // 0.25.3: lighter - pain, HP and bleeding count for much less, so NPCs stay up and keep taking a beating
            float pts = (lLeg ? 30f : 0f) + (rLeg ? 30f : 0f) + (lArm ? 10f : 0f) + (rArm ? 10f : 0f)
                      + Mathf.Min(18f, s.ribsBroken * 4f) + Mathf.Min(12f, s.bleedRate * 12f)
                      + Mathf.Clamp01(s.pain * painIntensity) * 8f + (1f - HpFrac(c)) * 8f
                      + (s.legWounded ? 6f : 0f) + (s.jawBroken ? 3f : 0f);
            float resist = (1f - 0.45f * s.hpK) * (s.bioNoQuit ? 0.5f : 1f) * (Pumped(s) ? 0.75f : 1f);
            s.integrity = Mathf.Clamp(100f - pts * resist, 0f, 100f);
            if (RegenActive(s)) { s.cantStand = false; return; }                      // a regenerating body always gets back up
            string why = null;
            if (s.spineBroken) why = "spine broken";
            else if (lLeg && rLeg) why = "both legs broken";
            else if ((lLeg || rLeg) && lArm && rArm) why = "a broken leg and both arms broken - the pain";
            else if (s.integrity < 8f) why = $"body too broken (integrity {s.integrity:0})";
            if (why != null && !s.cantStand)
            {
                s.cantStand = true;
                Log($"{WhoIs(s)}: CAN'T STAND - {why}");
            }
            if (s.cantStand && c.ragdoll.state == Ragdoll.State.Standing)
            {
                AllowFall(s, 2f);
                Guard("down", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
            }
            // a broken spine: no movement at all
            if (s.spineBroken && !s.paralysed)
            {
                s.paralysed = true;
                MakeLimp(s, ArmParts | LegParts);
                SetMelee(s, false, now + 9999f);
                Log($"{WhoIs(s)}: paralysed - doesn't move");
            }
            // low integrity: wobbly, trips
            if (!s.cantStand && s.integrity < 35f && c.ragdoll.state == Ragdoll.State.Standing && now >= s.nextIntegrityTrip)
            {
                s.nextIntegrityTrip = now + Random.Range(5f, 9f);
                if (Random.value < (35f - s.integrity) / 35f * 0.35f)
                {
                    Stagger(s, Random.insideUnitSphere, BrainModuleHitReaction.PushBehaviour.Effect.StaggerLight, (lLeg ? RagdollPart.Type.LeftLeg : RagdollPart.Type.RightLeg));
                    Log($"{WhoIs(s)}: stumbles (integrity {s.integrity:0})");
                }
            }
        }

        // ------------------------------------------------------------------ loyalty serum
        void ApplyLoyalty(NpcState s, float dose)
        {
            var c = s.creature;
            if (c == null || c.isKilled) return;
            if (dose < 0.5f) { Log($"{WhoIs(s)}: loyalty serum - not enough went in ({dose * 100f:0}%)"); return; }
            Guard("faction", () => c.SetFaction(2));                        // GameData faction 2 = Player
            s.ally = true;
            ReleaseGuard(s); s.stanceOn = false;
            SetMelee(s, true, 0f);
            MarkInjected(s, AllyEyes, c.transform.position);                    // eyes go ice-blue (Serum25 eye tint)
            Speak(s, "grappled");
            Log($"{WhoIs(s)}: LOYALTY SERUM - now fights on your side");
            Research2(s, "Loyalty Serum");
        }

        static void Research2(NpcState s, string what)
        {
            try
            {
                string p = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(RosterPath), "RageEngine_research.txt");
                System.IO.File.AppendAllText(p, $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}|{(s.identity != null ? s.identity.name : s.creature.name)}|{what}\n");
            }
            catch { }
        }
    }
}

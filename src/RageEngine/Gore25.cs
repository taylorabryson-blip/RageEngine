// Rage Engine 0.25 - unique gore interactions, built on the existing wound / skin-paint / bone-model / clutch systems.
//   BROKEN NOSE     a hard blunt hit square on the nose: crunch, the nose caves in a little, it pours for a minute.
//   TEETH           a hard blunt hit on the mouth knocks teeth out (real tooth models fly), blood from the mouth.
//   THROAT CUT      a slash across the neck: arterial spurts in pulses, both hands go to the throat, gurgling,
//                   he sinks to his knees. Lethal unless "Bleeding out can kill" is off (then it slows to a trickle).
//   BLADE OUT       pulling a blade out of the belly/chest: a wet squelch and a gush, he doubles over.
//   EYE GOUGE       push a thumb/finger (empty hand) into an eye and hold it there: the eye is gone, the socket
//                   bleeds, he screams and covers it. Half-blind for the rest of the fight.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        public static bool uniqueGore = true;

        /// From HandleHit (every hit on a tracked NPC).
        void Gore25Hit(NpcState s, CollisionInstance hit, RagdollPart part, RagdollPart.Type t, float severity)
        {
            if (!uniqueGore || part == null) return;
            var c = s.creature;
            var dt = hit.damageStruct.damageType;
            Vector3 p = hit.contactPoint;
            if (dt == DamageType.Blunt && Has(t, RagdollPart.Type.Head) && c.centerEyes != null)
            {
                var ce = c.centerEyes;
                Vector3 nose = ce.position - ce.up * 0.04f + ce.forward * 0.06f;
                Vector3 mouth = ce.position - ce.up * 0.1f + ce.forward * 0.05f;
                // ---- broken nose
                if (!s.noseBroken && severity >= 0.8f && Vector3.Distance(p, nose) < 0.06f)
                {
                    s.noseBroken = true;
                    Sfx.Play("NRE_BoneCrunch", part.transform, 0.7f);
                    var bone = part.meshBone != null ? part.meshBone : part.bone != null ? part.bone.mesh : null;
                    if (bone != null) Guard("nose", () => HeadDeform.Dent(c, bone, nose, -ce.forward, 0.03f, 0.006f));
                    Stamp(c, nose, 0.035f, "bruise_fresh", 0.8f);
                    s.noseBleedUntil = Time.time + 70f; s.nextNoseDrip = Time.time;
                    s.daze = Mathf.Max(s.daze, 0.8f);
                    Guard("face", () => c.brain.instance?.GetModule<BrainModuleExpression>(false)?.SetFaceExpression(FaceAnimator.Expression.Pain, 0, null));
                    Log($"{WhoIs(s)}: NOSE BROKEN");
                }
                // ---- teeth
                if (s.teethLost < 14 && severity >= 0.65f && Vector3.Distance(p, mouth) < 0.1f)      // 0.44: easier to knock out
                {
                    int n = Random.Range(1, severity >= 1.3f ? 5 : 3) + (severity >= 1.8f ? 1 : 0);
                    s.teethLost += n;
                    Sfx.PlayAt("NRE_Teeth", mouth, 1f);
                    Stamp(c, mouth, 0.03f, "blood_drip", 0.85f);
                    { var mt = Mouth(c); var nn = n; Guard("teeth50", () => TeethFx50(s, mt != null ? mt : part.transform, nn)); }
                    Vector3 outv = (ce.forward + Vector3.up * 0.3f).normalized;
                    SpawnGoreModels(mouth, outv + (p - mouth).normalized * -0.3f, "RE_Tooth", n, ToothMat(), 1.1f, c);
                    Guard("spit", () => SpitBlood(s, 1));
                    Sfx.Play("NRE_BoneCrunch", part.transform, 0.45f, 1.4f);
                    Log($"{WhoIs(s)}: {n} teeth knocked out ({s.teethLost} lost)");
                }
            }
            // ---- throat cut
            if (dt == DamageType.Slash && Has(t, RagdollPart.Type.Neck) && severity >= 0.6f && s.throatCutUntil <= 0f && !c.isKilled)
            {
                s.throatCutUntil = Time.time + 12f; s.nextSpurt = Time.time;
                s.throatPart = part; s.throatLocal = part.transform.InverseTransformPoint(p);
                s.bleedRate += 1.2f * bleedingRate;
                s.bloodInAirway = true; s.nextCoughTime = Time.time + 0.8f;
                Stamp(c, p, 0.05f, "laceration_deep", 0.95f);
                Sfx.Play("NRE_Squelch", part.transform, 0.8f);
                StartClutch(s, part, p, null, false, true);
                Guard("second", () => SecondHand(s));
                Speak(s, "choke");
                SetMelee(s, false, Time.time + 6f);
                Log($"{WhoIs(s)}: THROAT CUT");
            }
        }

        /// 0.1 s tick per NPC.
        void TickGore25(NpcState s, float now)
        {
            if (!uniqueGore) return;
            var c = s.creature;
            // arterial spurts in pulses, weakening; at 4 s he goes down to his knees
            if (s.throatCutUntil > 0f && s.throatPart != null)
            {
                if (now >= s.throatCutUntil || c.isKilled && now > s.throatCutUntil - 6f) { s.throatCutUntil = 0f; s.throatPart = null; }
                else if (now >= s.nextSpurt)
                {
                    float left = (s.throatCutUntil - now) / 12f;
                    s.nextSpurt = now + Mathf.Lerp(0.9f, 0.45f, left);                         // the heart slows down
                    Vector3 at = s.throatPart.transform.TransformPoint(s.throatLocal);
                    Guard("spurt", () => RageEngineScript.Blood(s.throatPart.transform));
                    if (Random.value < 0.5f) { var at2 = at; Guard("ground", () => GroundBlood(at2, "blood_ground_splat", Mathf.Lerp(0.15f, 0.3f, left), c)); }
                    if (Random.value < 0.25f) Speak(s, "choke");
                    if (now > s.throatCutUntil - 8f && !c.isKilled && c.ragdoll.state == Ragdoll.State.Standing) Sink(s, now);
                }
            }
            // blade pulled out of the gut / chest
            if (s.impaleItem != null && s.impalePart != null) { s.gutImpaled = Has(s.impalePart.type, RagdollPart.Type.Torso); s.gutPart = s.impalePart; }
            else if (s.gutImpaled)
            {
                s.gutImpaled = false;
                if (s.gutPart != null && !c.isKilled)
                {
                    Sfx.Play("NRE_Squelch", s.gutPart.transform, 0.9f);
                    for (int i = 0; i < 3; i++) Guard("blood", () => RageEngineScript.Blood(s.gutPart.transform));
                    var gp = s.gutPart.transform.position;
                    Guard("ground", () => GroundBlood(gp, "blood_pool", 0.3f, c));
                    s.bleedRate += 0.3f * bleedingRate;
                    var chest = ChestPart(c);
                    if (chest != null && chest.physicBody != null) chest.physicBody.AddForce(Vector3.down * 3f - c.transform.forward * 1.5f, ForceMode.Impulse);   // doubles over
                    Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Neck | RagdollPart.Type.Head, 0.4f, 0.8f);
                    Log($"{WhoIs(s)}: blade pulled out of the gut - gush");
                }
                s.gutPart = null;
            }
            TickGouge(s, now);
        }

        /// Eye gouging: an empty player hand's fingertip held in an eye socket.
        void TickGouge(NpcState s, float now)
        {
            var c = s.creature;
            var eyes = c.allEyes;
            var pc = Player.local != null ? Player.local.creature : null;
            if (eyes == null || pc == null || c.isKilled || c.ragdoll.headPart == null) return;
            if ((c.ragdoll.headPart.transform.position - pc.transform.position).sqrMagnitude > 4f) { s.gougeT = 0f; return; }
            bool any = false;
            foreach (var h in new[] { pc.handLeft, pc.handRight })
            {
                if (h == null || (h.grabbedHandle != null && !(h.grabbedHandle is HandleRagdoll))) continue;   // a weapon in the hand: not a thumb
                Vector3 tip = h.transform.position + h.PointDir.normalized * 0.09f;
                Vector3 thumb = h.transform.position + h.ThumbDir.normalized * 0.06f;
                for (int i = 0; i < eyes.Count && i < 8; i++)
                {
                    var e = eyes[i];
                    if (e == null || (s.gouged & (1 << i)) != 0) continue;
                    Vector3 ep = e.transform.position;
                    if ((tip - ep).sqrMagnitude > 0.035f * 0.035f && (thumb - ep).sqrMagnitude > 0.035f * 0.035f) continue;
                    any = true;
                    s.gougeT += 0.1f;
                    if (s.gougeT < 0.2f) { Scream(s, false); Flinch(s, ep + c.ragdoll.headPart.transform.forward * 0.3f, 0.6f); }
                    if (s.gougeT >= 0.6f) Gouge(s, i, e, h);
                    break;
                }
                if (any) break;
            }
            if (!any) s.gougeT = Mathf.Max(0f, s.gougeT - 0.2f);
        }

        void Gouge(NpcState s, int i, CreatureEye e, RagdollHand h)
        {
            var c = s.creature;
            s.gouged |= 1 << i; s.gougeT = 0f;
            if (s.eyeScale == null || s.eyeScale.Length != c.allEyes.Count)
            {
                s.eyeScale = new Vector3[c.allEyes.Count];
                for (int k = 0; k < c.allEyes.Count; k++) if (c.allEyes[k] != null) s.eyeScale[k] = c.allEyes[k].transform.localScale;
            }
            Vector3 ep = e.transform.position;
            e.transform.localScale = Vector3.zero;                                              // the eye is gone
            Guard("eye.hide", () => HideEye49(c, e, ep, c.ragdoll.headPart != null ? c.ragdoll.headPart.transform : null));
            Stamp(c, ep, 0.03f, "stab", 0.95f);
            Stamp(c, ep - Vector3.up * 0.03f, 0.04f, "blood_drip", 0.9f);
            Sfx.Play("NRE_Squelch", c.ragdoll.headPart.transform, 1f, 1.15f);
            Guard("blood", () => RageEngineScript.Blood(c.ragdoll.headPart.transform));
            Scream(s, true);
            s.bleedRate += 0.15f * bleedingRate;
            s.pain = Mathf.Min(1f, s.pain + 0.5f);
            s.defSkill *= 0.6f;                                                                  // can't see your hands coming on that side
            StartClutch(s, c.ragdoll.headPart, ep, null, false, false);
            s.blindEyes |= 1 << i;                                                               // 0.42: gone for good
            Guard("vision", () => UpdateVision(s));
            Guard("eye.hang", () => HangEye42(c.ragdoll.headPart.transform, ep, (ep - c.ragdoll.headPart.transform.position).normalized, s));
            if (AllBlind(s)) s.blind42 = true;
            Log($"{WhoIs(s)}: EYE GOUGED ({e.eyeTag})");
        }

        /// Put gouged eyes back when the body is recycled.
        static void RestoreGouged(NpcState s)
        {
            var c = s.creature;
            if (c != null) RestoreEyes49(c);
            if (s.eyeScale != null && c != null && c.allEyes != null)
                for (int k = 0; k < c.allEyes.Count && k < s.eyeScale.Length; k++) if (c.allEyes[k] != null && (s.gouged & (1 << k)) != 0) c.allEyes[k].transform.localScale = s.eyeScale[k];
            s.eyeScale = null; s.gouged = 0; s.gougeT = 0f;
        }
    }
}

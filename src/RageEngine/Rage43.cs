// Rage Engine 0.43 - new serums, super serums with powers, combinations, Deep Sleep, weapon proficiency and the
// Desperation Response.
//
// SERUMS        Iron Skin (blades glance off), Bloodhound (always knows where you are), Painless (feels nothing - then
//               all of it at once), Static Charge (hit him with metal and it bites back), Featherweight (fast, light, one
//               good hit sends him flying), Rot (he weakens and stains), Deep Sleep (out cold until you tap his head).
// SUPER SERUMS  Juggernaut, Berserker King, Wraith, Hivemind, Bone Lord, Magnet, Lazarus, Gravity Well, Mirror, Vampire,
//               Shockwave. Every power works through the game's own physics, AI and damage - no impossible abilities.
// COMBINATIONS  Painless + Berserker King, Iron Skin + Juggernaut, Featherweight + Wraith, Static Charge + Magnet.
// PROFICIENCY   every NPC is a Novice, Trained, Veteran or Master with weapons - independent of personality. It sets
//               parrying, attack tempo (within the game's own animation speeds) and how fast he recovers from an
//               interrupted attack. Returning NPCs keep theirs.
// DESPERATION   badly hurt NPCs vocalise, hesitate, back off, beg with their hands, signal "stop", call on allies, or
//               turn defiant - by personality, by whether they're alone, and by how bad it is. Real voices only: the
//               game's own and Rage Engine's recorded pain sounds; the begging is body language.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ------------------------------------------------------------------ settings
        public static ModOptionString[] despValues = { new ModOptionString("Off", null, "0"), new ModOptionString("Subtle", null, "1"), new ModOptionString("Normal", null, "2"), new ModOptionString("Intense", null, "3") };
        [ModOptionCategory("NPCs", 2), ModOptionOrder(16)]
        [ModOption("Desperation", "How often badly hurt NPCs beg, hesitate, back off, call for help or turn defiant (body language and real voices).", valueSourceName = nameof(despValues), defaultValueIndex = 2)]
        public static string desperation = "2";
        static float DespK => desperation == "0" ? 0f : desperation == "1" ? 0.5f : desperation == "3" ? 1.8f : 1f;

        static readonly object Hound43 = new object();

        static bool HasFx(NpcState s, Comp c) { for (int i = 0; i < s.bioFx.Count; i++) if (s.bioFx[i].comp == c) return true; return false; }
        static BioFx GetFx(NpcState s, Comp c) { for (int i = 0; i < s.bioFx.Count; i++) if (s.bioFx[i].comp == c) return s.bioFx[i]; return null; }

        // ================================================================== serums (0.1 s, from TickBio)
        void Serum43(NpcState s, BioFx f, float t, float k, float dt, float ramp, ref float tough, ref float pain, ref float balance, ref float speed, ref float dmgTaken,
                     ref float react, ref float painCap, ref float strength, ref float flinch, ref bool noQuit, ref bool angry)
        {
            var c = s.creature; float now = Time.time;
            switch (f.comp)
            {
                case Comp.IronSkin:
                    pain *= 0.7f; tough *= 1.2f;
                    if (bioVisuals && now >= s.bioNextPaint) { s.bioNextPaint = now + 2f; PaintAround(s, new Color(0.55f, 0.57f, 0.6f, 0.3f), 0.06f); }
                    break;
                case Comp.Bloodhound:
                {
                    speed *= 0.85f; noQuit = true;
                    var tg = PlayerTarget();
                    if (tg != null) { s.seenAt = now; s.seenPos = tg.position; s.lookAt = tg.position; }
                    if (!s.hound43) { s.hound43 = true; Guard("hound", () => { c.detectionFOVModifier.Remove(Hound43); c.detectionFOVModifier.Add(Hound43, 3f); }); }
                    break;
                }
                case Comp.Painless:
                    if (f.stage == 0)
                    {
                        painCap = 0f; pain *= 0.02f; flinch *= 0.05f; balance *= 1.6f;
                        if (t > f.dur - 0.6f)
                        {
                            // it wears off: every injury he picked up arrives at once
                            f.stage = 1; s.pain = 1f; Scream(s, true); AllowFall(s, 3f); Sink(s, now); s.stayDown = Mathf.Max(s.stayDown, 6f);
                            Log($"{WhoIs(s)}: Painless wears off - it all hits him at once");
                        }
                    }
                    break;
                case Comp.StaticCharge:
                    if (bioVisuals && Random.value < 0.06f && c.ragdoll.parts.Count > 0) { var p = c.ragdoll.parts[Random.Range(0, c.ragdoll.parts.Count)]; if (p != null) Puff(p.transform.position, new Color(0.4f, 0.8f, 1f), 3, 0.6f); }
                    break;
                case Comp.Featherweight:
                    speed *= 1.35f; balance *= 0.6f; react *= 1.2f;
                    break;
                case Comp.Rot:
                {
                    float r = Mathf.Clamp01(t / Mathf.Max(1f, f.dur));
                    strength *= 1f - 0.45f * r; speed *= 1f - 0.25f * r; s.bleedRate = Mathf.Max(s.bleedRate, 0.04f * k);
                    if (bioVisuals && now >= s.bioNextPaint) { s.bioNextPaint = now + 1.8f; PaintAround(s, new Color(0.2f, 0.24f, 0.06f, 0.4f), 0.04f + 0.05f * r); }
                    if (Random.value < 0.02f)
                        for (int i = 0; i < MaxTracked; i++)
                        {
                            var o = pool[i];
                            if (o == s || !o.active || o.creature == null || o.creature.isKilled) continue;
                            if ((o.creature.transform.position - c.transform.position).sqrMagnitude < 4f) { Speak(o, "choke"); break; }
                        }
                    break;
                }
                case Comp.DeepSleep:
                    if (f.stage == 0 && t > 3.5f) { f.stage = 1; s.deepSleep43 = true; Sleep(s, 600f); Log($"{WhoIs(s)}: Deep Sleep - out until somebody wakes him"); }
                    if (f.stage == 1 && !s.knockedOut) { f.stage = 2; f.dur = t; s.deepSleep43 = false; }      // woke up = done
                    speed *= 0.5f; react *= 0.5f;
                    break;
            }
        }

        // ================================================================== super serums (0.1 s, from SerumTick)
        void Super43(NpcState s, BioFx f, float t, float k, float dt, ref float tough, ref float pain, ref float balance, ref float speed, ref float dmgTaken,
                     ref float react, ref float painCap, ref float regen, ref float strength, ref float flinch, ref bool noQuit, ref bool angry)
        {
            var c = s.creature; float now = Time.time;
            var pc = Player.local != null ? Player.local.creature : null;
            float pd = pc != null ? Vector3.Distance(pc.transform.position, c.transform.position) : 99f;
            switch (f.comp)
            {
                case Comp.Juggernaut:
                    flinch *= 0.02f; balance *= 4f; tough *= 2.5f; dmgTaken *= 1f - 0.4f * Mathf.Min(1f, k); strength *= 1.4f; speed *= 0.95f; noQuit = true; painCap = Mathf.Min(painCap, 0.4f);
                    if (pd < 7f && pd > 2.5f && now >= s.next43) { s.next43 = now + Random.Range(5f, 9f); s.rushUntil = now + 2.5f; Speak(s, "attackmelee"); }     // the charge
                    if (HasFx(s, Comp.IronSkin)) { dmgTaken *= 0.7f; tough *= 1.3f; }                                                                  // combo: the tank
                    break;
                case Comp.BerserkerKing:
                {
                    s.rage43 = Mathf.Max(0f, s.rage43 - 0.01f * dt);
                    if (now < s.burnout43) { speed *= 0.6f; strength *= 0.7f; s.fatigue = Mathf.Max(s.fatigue, 0.8f); break; }
                    float r = s.rage43; angry = true; noQuit = true;
                    strength *= 1f + 1.2f * r; speed *= 1f + 0.45f * r; flinch *= 1f - 0.75f * r; painCap = Mathf.Min(painCap, 1f - 0.7f * r);
                    bool numb = HasFx(s, Comp.Painless);                                                                                            // combo: feels nothing, only gets faster
                    if (numb) { s.rage43 = Mathf.Min(1f, s.rage43 + 0.02f * dt); speed *= 1.1f; }
                    if (r >= 0.99f) { if (s.rageMaxAt43 <= 0f) { s.rageMaxAt43 = now; Scream(s, true); RageMoment("Berserker King"); } s.rushUntil = Mathf.Max(s.rushUntil, now + 0.3f); }
                    if (s.rageMaxAt43 > 0f && now - s.rageMaxAt43 > (numb ? 30f : 20f))
                    {
                        s.rageMaxAt43 = 0f; s.rage43 = 0f; s.burnout43 = now + 15f; AllowFall(s, 3f); Sink(s, now);
                        Log($"{WhoIs(s)}: burns out - collapses");
                        if (numb && Random.value < 0.5f) { s.finalKill = true; Guard("kill", () => c.Kill()); Log($"{WhoIs(s)}: his heart gives out"); }
                    }
                    break;
                }
                case Comp.Wraith:
                    speed *= 1.2f; react *= 1.4f; flinch *= 0.5f;
                    if (now >= s.next43 && pc != null && pd < 3f && c.ragdoll.state == Ragdoll.State.Standing && !s.knockedOut && IncomingSwing43(c)) Blink43(s, pc);
                    break;
                case Comp.Hivemind:
                    react *= 1.3f;
                    if (now >= s.next43)
                    {
                        s.next43 = now + 0.5f;
                        var tg = PlayerTarget();
                        bool knows = tg != null && now - s.seenAt < 3f;
                        for (int i = 0; i < MaxTracked; i++)
                        {
                            var o = pool[i];
                            if (o == s || !o.active || o.creature == null || o.creature.isKilled || o.creature.factionId != c.factionId) continue;
                            if ((o.creature.transform.position - c.transform.position).sqrMagnitude > 15f * 15f) continue;
                            o.bioAllyBoostUntil = now + 1f;
                            if (knows) { o.seenAt = now; o.seenPos = tg.position; o.lookAt = tg.position; }
                        }
                    }
                    break;
                case Comp.BoneLord:
                    regen += 0.5f * xRegen;
                    speed *= Mathf.Max(0.6f, 1f - 0.08f * f.stage);
                    if (now >= s.next43 && (s.brokenParts != 0 || s.leftArmDead || s.rightArmDead))
                    {
                        s.next43 = now + 4f;
                        int before = (int)s.brokenParts;
                        RestoreOneLimb(s);
                        if ((int)s.brokenParts != before || true)
                        {
                            f.stage++;
                            Sfx.Play("NRE_BoneCrunch", c.ragdoll.rootPart != null ? c.ragdoll.rootPart.transform : c.transform, 1f, 0.85f);
                            Scream(s, false);
                            Log($"{WhoIs(s)}: Bone Lord - a bone snaps back into place ({f.stage} so far)");
                        }
                    }
                    if (s.ribsBroken > 0 && now >= s.next43 - 2f && Random.value < 0.05f) s.ribsBroken--;
                    break;
                case Comp.Magnet:
                    strength *= 1.2f;
                    Guard("magnet", () => TickMagnet43(s, now));
                    break;
                case Comp.Lazarus:
                    regen += 0.3f * xRegen;
                    if (f.stage == 1 && !s.knockedOut)
                    {
                        f.stage = 2; s.drunk = 0f; s.rushUntil = now + 5f; s.pain = 0f; AddTag(s, Tag.Anger, 60f, 3f);
                        Scream(s, true); RageMoment("Lazarus");
                        Puff(c.transform.position + Vector3.up * 1.2f, new Color(1f, 0.95f, 0.7f), 24, 1.2f);
                        Log($"{WhoIs(s)}: LAZARUS - gets back up");
                    }
                    if (f.stage == 2) { angry = true; noQuit = true; strength *= 1.3f; painCap = Mathf.Min(painCap, 0.3f); }
                    break;
                case Comp.GravityWell:
                    tough *= 1.3f;
                    if (now >= s.next43) { s.next43 = now + 0.2f; Guard("gravity", () => Gravity43(s)); }
                    break;
                case Comp.Mirror:
                    dmgTaken *= 0.85f; tough *= 1.2f;
                    break;
                case Comp.Vampire:
                    speed *= 1.1f; strength *= 1.2f;
                    if (pc != null && pd < 1.6f && !Player.invincibility)
                    {
                        float drain = 2.5f * k * dt;
                        Guard("vampire", () => { pc.Damage(drain); c.Heal(drain * 3f); });
                        if (Random.value < 0.15f) { Haptics.Pulse(Side.Left, 0.3f, 1); Haptics.Pulse(Side.Right, 0.3f, 1); }
                    }
                    // feeds on the bleeding and the dead around him
                    if (now >= s.next43)
                    {
                        s.next43 = now + 1f;
                        for (int i = 0; i < MaxTracked; i++)
                        {
                            var o = pool[i];
                            if (o == s || !o.active || o.creature == null) continue;
                            if ((o.creature.transform.position - c.transform.position).sqrMagnitude > 9f) continue;
                            if (o.creature.isKilled || o.bleedRate > 0.1f) Guard("feed", () => c.Heal(4f));
                        }
                    }
                    break;
                case Comp.Shockwave:
                    strength *= 1.3f; balance *= 1.5f;
                    if (pc != null && pd < 3.5f && now >= s.next43 && c.ragdoll.state == Ragdoll.State.Standing) { s.next43 = now + Random.Range(7f, 10f); Guard("shockwave", () => Shockwave43(s, pc)); }
                    break;
            }
        }

        // ------------------------------------------------------------------ powers
        /// Something of the player's is swinging at him (a held weapon or a bare hand moving fast, close by).
        static bool IncomingSwing43(Creature c)
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) return false;
            var chest = ChestPart(c); Vector3 at = chest != null ? chest.transform.position : c.transform.position + Vector3.up * 1.2f;
            foreach (var h in new[] { pc.handLeft, pc.handRight })
            {
                if (h == null) continue;
                Vector3 p = h.transform.position; Vector3 v = h.physicBody != null ? h.physicBody.velocity : Vector3.zero;
                var it = h.grabbedHandle != null ? h.grabbedHandle.item : null;
                if (it != null && it.physicBody != null) { v = it.physicBody.velocity; p = it.transform.position; }
                if (v.magnitude > 3.2f && (p - at).sqrMagnitude < 1.4f * 1.4f && Vector3.Dot(v, at - p) > 0f) return true;
            }
            return false;
        }

        void Blink43(NpcState s, Creature pc)
        {
            var c = s.creature;
            bool light = HasFx(s, Comp.Featherweight);                                                           // combo: blinks more often
            s.next43 = Time.time + (light ? 3.5f : 6.5f);
            Vector3 pf = Vector3.ProjectOnPlane(Player.local.head != null ? Player.local.head.transform.forward : pc.transform.forward, Vector3.up).normalized;
            if (pf.sqrMagnitude < 0.01f) pf = pc.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, pf) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 to = pc.transform.position - pf * 1.7f + side * Random.Range(0.3f, 1f);
            if (Physics.Raycast(to + Vector3.up * 2f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore)) to = hit.point;
            else return;
            if (Physics.Linecast(pc.transform.position + Vector3.up * 1f, to + Vector3.up * 1f, out var wall, ~0, QueryTriggerInteraction.Ignore) && wall.collider.GetComponentInParent<Creature>() == null) return;   // not through walls
            Vector3 from = c.transform.position;
            Puff(from + Vector3.up * 1.1f, new Color(0.08f, 0.08f, 0.12f), 22, 1.4f);
            Quaternion rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(pc.transform.position - to, Vector3.up).normalized);
            Guard("blink", () => c.Teleport(to, rot));
            Puff(to + Vector3.up * 1.1f, new Color(0.08f, 0.08f, 0.12f), 22, 1.4f);
            s.rushUntil = Time.time + 1.2f;
            Log($"{WhoIs(s)}: WRAITH - blinks behind you");
        }

        void TickMagnet43(NpcState s, float now)
        {
            var c = s.creature;
            if (HasRealWeapon42(s) || c.ragdoll.state != Ragdoll.State.Standing) { s.magnetItem43 = null; return; }
            Side side = HandUsable(s, Side.Right) ? Side.Right : Side.Left;
            var hand = Hand(c, side);
            if (hand == null || hand.grabbedHandle != null || !HandUsable(s, side)) return;
            var it = s.magnetItem43;
            if (it == null || it.IsHanded() && (it.mainHandler == null || it.mainHandler.ragdoll == null || it.mainHandler.ragdoll.creature == null || !it.mainHandler.ragdoll.creature.isKilled))
            {
                s.magnetItem43 = null;
                if (now < s.magnetNext43) return;
                s.magnetNext43 = now + 1f;
                float best = 7f * 7f; bool sparky = HasFx(s, Comp.StaticCharge);                                   // combo: longer reach
                if (sparky) best = 11f * 11f;
                var all = Item.allActive; if (all == null) return;
                for (int i = 0; i < all.Count; i++)
                {
                    var x = all[i];
                    if (x == null || x.data == null || x.data.type != ItemData.Type.Weapon || x.holder != null || x.isTelekinesisGrabbed || x.physicBody == null || x.data.id == null || x.data.id.StartsWith("RE_")) continue;
                    if (x.IsHanded())
                    {
                        var hc = x.mainHandler != null && x.mainHandler.ragdoll != null ? x.mainHandler.ragdoll.creature : null;
                        if (hc == null || !hc.isKilled) continue;                                                     // rip it from a dead man's grip only
                    }
                    float d2 = (x.transform.position - hand.transform.position).sqrMagnitude;
                    if (d2 < best) { best = d2; it = x; }
                }
                s.magnetItem43 = it;
                if (it == null) return;
                if (it.IsHanded() && it.mainHandler != null) { var mh = it.mainHandler; Guard("magnet.rip", () => mh.UnGrab(false)); }
                Log($"{WhoIs(s)}: MAGNET - pulls a {it.data.id}");
            }
            Vector3 to = hand.transform.position - it.transform.position;
            if (to.magnitude < 0.35f)
            {
                var handle = side == Side.Right && it.mainHandleRight != null ? it.mainHandleRight : side == Side.Left && it.mainHandleLeft != null ? it.mainHandleLeft
                           : it.handles != null && it.handles.Count > 0 ? it.handles[0] : null;
                if (handle != null) Guard("magnet.grab", () => hand.Grab(handle, true));
                s.magnetItem43 = null;
                return;
            }
            it.physicBody.velocity = to.normalized * Mathf.Min(9f, 3f + to.magnitude * 2f) + Vector3.up * 0.4f;
            if (HasFx(s, Comp.StaticCharge) && Random.value < 0.3f) Puff(it.transform.position, new Color(0.4f, 0.8f, 1f), 2, 0.4f);
        }

        static readonly Collider[] grav43 = new Collider[40];
        void Gravity43(NpcState s)
        {
            var c = s.creature;
            var chest = ChestPart(c); Vector3 centre = chest != null ? chest.transform.position : c.transform.position + Vector3.up;
            int n = Physics.OverlapSphereNonAlloc(centre, 6f, grav43, ~0, QueryTriggerInteraction.Ignore);
            var seen = new HashSet<Rigidbody>();
            for (int i = 0; i < n; i++)
            {
                var rb = grav43[i] != null ? grav43[i].attachedRigidbody : null;
                if (rb == null || rb.isKinematic || !seen.Add(rb)) continue;
                if (rb.GetComponentInParent<Creature>() != null) continue;                                           // bodies stay put
                var it = rb.GetComponentInParent<Item>();
                if (it != null && it.IsHanded()) continue;
                Vector3 d = centre - rb.worldCenterOfMass; float m = d.magnitude;
                if (m < 0.6f) continue;
                float pull = (it != null && it.isFlying ? 14f : 5f) * Mathf.Clamp01(1f - m / 6.5f);
                rb.AddForce(d / m * pull, ForceMode.Acceleration);
            }
        }

        void Shockwave43(NpcState s, Creature pc)
        {
            var c = s.creature; Vector3 at = c.transform.position;
            Speak(s, "attackmelee");
            Puff(at + Vector3.up * 0.3f, new Color(1f, 0.6f, 0.2f), 30, 3f);
            int n = Physics.OverlapSphereNonAlloc(at, 4f, grav43, ~0, QueryTriggerInteraction.Ignore);
            var seen = new HashSet<Rigidbody>();
            for (int i = 0; i < n; i++)
            {
                var rb = grav43[i] != null ? grav43[i].attachedRigidbody : null;
                if (rb == null || rb.isKinematic || !seen.Add(rb)) continue;
                var cr = rb.GetComponentInParent<Creature>();
                if (cr == c || (cr != null && cr.isPlayer)) continue;
                Vector3 d = rb.worldCenterOfMass - at; d.y = Mathf.Max(d.y, 0.3f);
                rb.AddForce(d.normalized * 4.5f, ForceMode.VelocityChange);
            }
            // your weapons get torn at - you feel it in both hands
            foreach (var h in new[] { pc.handLeft, pc.handRight })
            {
                var it = h != null && h.grabbedHandle != null ? h.grabbedHandle.item : null;
                if (it != null && it.physicBody != null) it.physicBody.AddForce((it.transform.position - at).normalized * 5f, ForceMode.VelocityChange);
            }
            Haptics.Pulse(Side.Left, 1f, 3); Haptics.Pulse(Side.Right, 1f, 3);
            Log($"{WhoIs(s)}: SHOCKWAVE");
        }

        /// The Hivemind carrier is dead: the others lose the voice in their heads and panic.
        void HivemindDied43(NpcState s)
        {
            if (!HasFx(s, Comp.Hivemind) || s.creature == null) return;
            var c = s.creature; float now = Time.time;
            for (int i = 0; i < MaxTracked; i++)
            {
                var o = pool[i];
                if (o == s || !o.active || o.creature == null || o.creature.isKilled || o.creature.factionId != c.factionId) continue;
                if ((o.creature.transform.position - c.transform.position).sqrMagnitude > 20f * 20f) continue;
                AddTag(o, Tag.Fear, 10f, 1f); SetMelee(o, false, now + 3f); Scream(o, false);
            }
            Log($"{WhoIs(s)}: the Hivemind carrier is down - the rest panic");
        }

        /// From DeathRules27: true = Lazarus brings him back.
        bool Lazarus43(NpcState s)
        {
            var f = GetFx(s, Comp.Lazarus);
            if (f == null || f.stage != 0) return false;
            var c = s.creature;
            f.stage = 1;
            Revive(s, c.maxHealth * 0.85f);
            Sleep(s, 5f);
            s.eyesShutUntil = Time.time + 5f;
            Log($"{WhoIs(s)}: dies... (Lazarus)");
            return true;
        }

        // ================================================================== hits (every hit on a tracked NPC)
        void Bio43Hit(NpcState s, CollisionInstance hit, RagdollPart part, RagdollPart.Type t, float severity)
        {
            if (s.bioFx.Count == 0 && !s.deepSleep43) return;
            var c = s.creature; float now = Time.time;
            var dt = hit.damageStruct.damageType; float dmg = hit.damageStruct.damage;
            var pc = Player.local != null ? Player.local.creature : null;
            var pitem = PlayerItem43(hit);
            bool byPlayer = pitem != null || bareHit34;
            // Deep Sleep: a hit to the head wakes him (that's the "tap")
            if (s.deepSleep43 && s.knockedOut && Has(t, RagdollPart.Type.Head) && severity >= 0.05f) WakeDeep43(s, "a knock on the head");
            if (HasFx(s, Comp.IronSkin) && dt == DamageType.Slash && dmg > 0f && !c.isKilled)
            {
                float back = dmg * 0.67f;
                Guard("ironskin", () => c.Heal(back));
                if (Random.value < 0.5f) Puff(hit.contactPoint, new Color(0.75f, 0.75f, 0.8f), 4, 0.8f);
            }
            if (HasFx(s, Comp.StaticCharge) && pitem != null && pc != null && Random.value < 0.45f && !Player.invincibility)
            {
                Puff(hit.contactPoint, new Color(0.4f, 0.85f, 1f), 10, 1.6f);
                Guard("static", () => pc.Damage(3f));
                var hd = pitem.mainHandler; if (hd != null) Haptics.Pulse(hd.side, 1f, 4);
            }
            if (HasFx(s, Comp.Featherweight) && severity >= 0.4f && !c.isKilled)
            {
                Vector3 dir = hit.impactVelocity.sqrMagnitude > 0.01f ? hit.impactVelocity.normalized : -c.transform.forward;
                float dv = Mathf.Min(6f, 1.5f + severity * 2.5f);
                AllowFall(s, 2f);
                foreach (var rp in c.ragdoll.parts) if (rp != null && rp.physicBody != null) rp.physicBody.AddForce(dir * dv + Vector3.up * dv * 0.3f, ForceMode.VelocityChange);
            }
            if (HasFx(s, Comp.BerserkerKing) && now >= s.burnout43) s.rage43 = Mathf.Min(1f, s.rage43 + 0.08f + 0.05f * severity);
            if (HasFx(s, Comp.Mirror) && byPlayer && pc != null && dmg > 0f && !Player.invincibility)
            {
                float back = dmg * 0.3f;
                Guard("mirror", () => pc.Damage(back));
                Puff(hit.contactPoint, new Color(0.7f, 0.95f, 1f), 6, 1f);
            }
        }

        static Item PlayerItem43(CollisionInstance hit)
        {
            try
            {
                var it = hit.sourceCollider != null ? hit.sourceCollider.GetComponentInParent<Item>() : null;
                if (it == null) return null;
                var h = it.mainHandler;
                return h != null && h.ragdoll != null && h.ragdoll.creature != null && h.ragdoll.creature.isPlayer ? it : null;
            }
            catch { return null; }
        }

        void WakeDeep43(NpcState s, string why)
        {
            if (!s.deepSleep43) return;
            s.deepSleep43 = false;
            if (s.knockedOut) s.koWakeAt = Time.time;                                                        // TickKnockout brings him round
            var f = GetFx(s, Comp.DeepSleep); if (f != null) { f.stage = 2; f.dur = Time.time - f.start; }
            Log($"{WhoIs(s)}: woken from Deep Sleep ({why})");
        }

        // ================================================================== per-NPC 0.1 s: Deep Sleep tap, proficiency, desperation
        void Tick43(NpcState s, float now)
        {
            var c = s.creature;
            if (c.isKilled) return;
            // ---- Deep Sleep: a tap on the head with your hand wakes him
            if (s.deepSleep43 && s.knockedOut && c.ragdoll.headPart != null)
            {
                var pc = Player.local != null ? Player.local.creature : null;
                if (pc != null)
                {
                    Vector3 hp = c.ragdoll.headPart.transform.position;
                    foreach (var h in new[] { pc.handLeft, pc.handRight })
                    {
                        if (h == null || h.physicBody == null) continue;
                        if ((h.transform.position - hp).sqrMagnitude < 0.16f * 0.16f && h.physicBody.velocity.magnitude > 0.5f) { WakeDeep43(s, "a tap on the head"); break; }
                    }
                }
            }
            if (!s.deepSleep43 && s.hound43 && !HasFx(s, Comp.Bloodhound)) { s.hound43 = false; Guard("hound.off", () => c.detectionFOVModifier.Remove(Hound43)); }
            Guard("wear", () => Wear44(s, now));
            Guard("loadout", () => Loadout47(s, now));
            if (smart51) Guard("smart", () => Smart47(s, now));
            Guard("tick49", () => Tick49(s, now));
            Guard("tick50", () => Tick50(s, now));
            Guard("tick51", () => Tick51(s, now));
            Guard("prof", () => ProfTick43(s));
            Guard("desp", () => Desperation43(s, now));
        }

        // ================================================================== weapon proficiency
        static readonly string[] ProfTier = { "Novice", "Trained", "Veteran", "Master" };
        static string ProfName(float p) => ProfTier[p < 0.35f ? 0 : p < 0.6f ? 1 : p < 0.85f ? 2 : 3];

        void ProfOnTrack(NpcState s)
        {
            var rec = s.identity;
            float p;
            if (rec != null && rec.prof > 0) p = rec.prof / 100f;                                                // a returning NPC keeps his
            else
            {
                float r = Random.value;
                p = r < 0.3f ? Random.Range(0.1f, 0.35f) : r < 0.65f ? Random.Range(0.35f, 0.6f) : r < 0.9f ? Random.Range(0.6f, 0.85f) : Random.Range(0.85f, 1f);
                if (rec != null) { rec.prof = Mathf.Clamp(Mathf.RoundToInt(p * 100f), 1, 100); rosterDirty = true; }
            }
            s.prof43 = p;
            s.defSkill = Mathf.Clamp01(Mathf.Lerp(0.2f, 0.95f, p) + Random.Range(-0.05f, 0.05f));             // 0.43: parrying is skill, not personality
            s.profMelee43 = null; s.profHit43 = null;
            var pt = s.personality.type;
            s.will44 = Random.value < 0.2f + (s.brave ? 0.15f : 0f) + (pt == PersonalityType.Aggressive || pt == PersonalityType.Confident ? 0.1f : 0f) + 0.15f * p;
            Log($"{WhoIs(s)}: {(s.will44 ? "strong-willed, " : "")}{ProfName(p)} with weapons ({p:0.00}), {s.personality.type}");
        }

        /// Patches the game's melee tempo and hit recovery for his proficiency (re-applied if the brain is reloaded).
        void ProfTick43(NpcState s)
        {
            if (s.prof43 <= 0f) return;
            var inst = s.creature.brain != null ? s.creature.brain.instance : null;
            if (inst == null) return;
            var m = inst.GetModule<BrainModuleMelee>(false);
            if (m != null && m != s.profMelee43) { s.profMelee43 = m; s.profAnim43 = m.animationSpeedMultiplier; s.profDelay43 = m.nextAttackDelay; }
            if (m != null)
            {
                m.animationSpeedMultiplier = s.profAnim43 * Mathf.Lerp(0.93f, 1.08f, s.prof43) * WearAnim44(s);      // within the game's own animations; slower as he's hurt
                m.nextAttackDelay = s.profDelay43 * Mathf.Lerp(1.35f, 0.8f, s.prof43) * WearDelay44(s);
            }
            var h = inst.GetModule<BrainModuleHitReaction>(false);
            if (h != null && h != s.profHit43)
            {
                s.profHit43 = h; s.profRec43 = h.recoverySpeed;
                h.recoverySpeed = s.profRec43 * Mathf.Lerp(0.8f, 1.3f, s.prof43);                               // gets back into it faster after an interrupted attack
            }
        }

        static void ProfOnRelease(NpcState s)
        {
            if (s.profMelee43 != null) { s.profMelee43.animationSpeedMultiplier = s.profAnim43; s.profMelee43.nextAttackDelay = s.profDelay43; }
            if (s.profHit43 != null) s.profHit43.recoverySpeed = s.profRec43;
            s.profMelee43 = null; s.profHit43 = null; s.prof43 = 0f;
            VomitHoldOff44(s);
            var c = s.creature;
            if (c != null && s.wearOn44) Guard("wear.off", () => c.locomotion.globalMoveSpeedMultiplier.Remove(WearHandler));
            s.wearOn44 = false;
        }

        // ================================================================== desperation response
        void Desperation43(NpcState s, float now)
        {
            var c = s.creature;
            // end the current gesture
            if (s.despUntil43 > 0f && now >= s.despUntil43)
            {
                s.despUntil43 = 0f;
                if (!s.surrendered && !s.clutching) { var ik = c.ragdoll.ik; if (ik != null) foreach (var sd in new[] { Side.Left, Side.Right }) { ik.SetHandAnchor(sd, null); ik.SetHandState(sd, false, false); } }
            }
            if (s.despUntil43 > 0f) { HoldGesture43(s, now); return; }
            float K = DespK;
            if (K <= 0f || s.knockedOut || s.passedOut || s.surrendered || s.ally || s.bioNoQuit || Tank(s) || s.dying || now < s.despNext43) return;
            int injuries = CountBits((int)s.brokenParts) + (s.ribsBroken > 0 ? 1 : 0) + (s.leftArmDead || s.rightArmDead ? 1 : 0) + (s.blind42 ? 1 : 0);
            float distress = Mathf.Max(Mathf.Clamp01(s.pain * s.painSensitivity), Mathf.Clamp01((0.45f - HpFrac(c)) * 2.2f), Mathf.Clamp01(injuries * 0.3f));
            if (distress < 0.5f) return;
            s.despNext43 = now + Random.Range(4f, 9f) / K / Mathf.Lerp(0.8f, 1.4f, distress);
            var pc = Player.local != null ? Player.local.creature : null;
            float pd = pc != null ? Vector3.Distance(pc.transform.position, c.transform.position) : 99f;
            if (pd > 9f) return;
            NpcState ally = null; float ad = 64f;
            for (int i = 0; i < MaxTracked; i++)
            {
                var o = pool[i];
                if (o == s || !o.active || o.creature == null || o.creature.isKilled || o.creature.factionId != c.factionId) continue;
                float d2 = (o.creature.transform.position - c.transform.position).sqrMagnitude;
                if (d2 < ad) { ad = d2; ally = o; }
            }
            bool alone = ally == null, standing = c.ragdoll.state == Ragdoll.State.Standing;
            var type = s.personality.type;
            float r = Random.value;
            // personality picks the response
            string act;
            switch (type)
            {
                case PersonalityType.Aggressive: act = r < 0.55f ? "defiant" : r < 0.8f ? "voice" : "hesitate"; break;
                case PersonalityType.Confident: act = r < 0.4f ? "voice" : r < 0.7f ? "hesitate" : alone && distress > 0.8f ? "beg" : "defiant"; break;
                case PersonalityType.Nervous: act = r < 0.4f ? "beg" : r < 0.65f ? "stop" : r < 0.85f ? "voice" : "flee"; break;
                case PersonalityType.Cautious: act = r < 0.4f ? "backoff" : r < 0.7f ? "beg" : "hesitate"; break;
                case PersonalityType.Unpredictable: act = new[] { "beg", "stop", "voice", "defiant", "lunge", "backoff", "hesitate" }[Random.Range(0, 7)]; break;
                default: act = r < 0.35f ? "voice" : r < 0.65f ? "hesitate" : alone ? "beg" : "backoff"; break;
            }
            if (!alone && (act == "beg" || act == "stop") && Random.value < 0.6f) act = "callhelp";                // with friends near, he looks to them first
            if (!standing && (act == "stop" || act == "backoff" || act == "lunge")) act = Random.value < 0.5f ? "beg" : "voice";
            Vector3 away = pc != null ? Vector3.ProjectOnPlane(c.transform.position - pc.transform.position, Vector3.up).normalized : -c.transform.forward;
            switch (act)
            {
                case "voice":
                    if (Random.value < 0.6f) FearVoice49(s, true); else Speak(s, Random.value < 0.5f ? "grappled" : "choke");
                    break;
                case "hesitate":
                    SetMelee(s, false, now + Random.Range(1.2f, 2.8f) * Mathf.Lerp(0.8f, 1.3f, distress));
                    if (pc != null) Guard("flinch", () => Flinch(s, pc.transform.position + Vector3.up * 1.5f, 0.4f));
                    break;
                case "backoff":
                    SetMelee(s, false, now + 2f); ShoveBodyQuiet(s, away, 0.7f); Gesture43(s, 2, 1.6f);
                    break;
                case "beg":
                    SetMelee(s, false, now + 3f); Gesture43(s, 1, Random.Range(2f, 3.5f)); FearVoice49(s, true);
                    AddTag(s, Tag.Fear, 8f, 0.5f * K);                                                         // edges him toward surrender
                    Guard("face", () => c.brain.instance?.GetModule<BrainModuleExpression>(false)?.SetFaceExpression(FaceAnimator.Expression.Fear, 0, null));
                    break;
                case "stop":
                    SetMelee(s, false, now + 2.5f); Gesture43(s, 3, Random.Range(1.5f, 2.5f)); ShoveBodyQuiet(s, away, 0.4f); FearVoice49(s, true);
                    break;
                case "callhelp":
                    if (ally != null)
                    {
                        Speak(s, "taunt");
                        Vector3 toAlly = Vector3.ProjectOnPlane(ally.creature.transform.position - c.transform.position, Vector3.up).normalized;
                        ShoveBodyQuiet(s, toAlly, 0.6f);
                        AddTag(ally, Tag.Anger, 10f, 1f); ally.rushUntil = Mathf.Max(ally.rushUntil, now + 2f);
                    }
                    break;
                case "flee":
                    Panic(s, Random.Range(3f, 6f)); FearVoice49(s, true);
                    break;
                case "defiant":
                    Speak(s, Random.value < 0.5f ? "taunt" : "attackmelee"); AddTag(s, Tag.Anger, 6f, 1f);
                    if (pd < 4f) s.rushUntil = Mathf.Max(s.rushUntil, now + 1.5f);
                    break;
                case "lunge":
                    Scream(s, true); s.rushUntil = now + 2f; SetMelee(s, true, now + 2f);
                    break;
            }
            Log($"{WhoIs(s)}: desperation - {act} ({(alone ? "alone" : "with allies")}, distress {distress:0.00})");
        }

        /// pose 1 = hands out, begging; 2 = arms up covering; 3 = palms out "stop"
        void Gesture43(NpcState s, int pose, float seconds)
        {
            var c = s.creature;
            if (s.clutching || s.surrendered || c.ragdoll.ik == null || c.ragdoll.state != Ragdoll.State.Standing && pose != 1) return;
            s.despPose43 = pose; s.despUntil43 = Time.time + seconds;
            if (s.despL43 == null) { s.despL43 = new GameObject("RageEngine_DespL").transform; s.despR43 = new GameObject("RageEngine_DespR").transform; }
            HoldGesture43(s, Time.time);
        }

        void HoldGesture43(NpcState s, float now)
        {
            var c = s.creature; var ik = c.ragdoll.ik; var head = c.ragdoll.headPart;
            if (ik == null || head == null || s.despL43 == null || s.clutching || s.surrendered) return;
            Vector3 fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized, right = Vector3.Cross(Vector3.up, fwd);
            float shake = Mathf.Sin(now * 11f) * 0.012f;
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                if (Dead(s, side)) continue;
                var hand = Hand(c, side);
                if (hand != null && hand.grabbedHandle != null && s.despPose43 != 2 && s.despPose43 != 5) continue;               // still holding a weapon in that hand
                float sg = side == Side.Left ? -1f : 1f;
                var a = side == Side.Left ? s.despL43 : s.despR43;
                Vector3 h = head.transform.position;
                float beck = 0.5f + 0.5f * Mathf.Sin(now * 9f);                                              // 0.49: "come on" curl
                a.position = s.despPose43 == 1 ? h - Vector3.up * 0.35f + right * sg * 0.22f + fwd * 0.42f
                           : s.despPose43 == 2 ? h + Vector3.up * 0.05f + right * sg * 0.1f + fwd * 0.12f
                           : s.despPose43 == 4 ? h - Vector3.up * (0.3f + 0.04f * beck) + right * sg * 0.2f + fwd * (0.3f + 0.16f * beck)
                           : s.despPose43 == 5 ? h - Vector3.up * 0.32f + right * sg * 0.55f + fwd * 0.12f
                           : s.despPose43 == 6 ? h - Vector3.up * (0.3f + 0.12f * Mathf.Sin(now * 17f + sg)) + right * sg * (0.08f + 0.06f * Mathf.Sin(now * 13f)) + fwd * 0.2f
                           : h - Vector3.up * 0.15f + right * sg * 0.2f + fwd * 0.48f;
                if (s.despPose43 < 4) a.position += Vector3.up * shake;
                a.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                ik.SetHandAnchor(side, a); ik.SetHandState(side, true, true);
            }
        }
    }
}

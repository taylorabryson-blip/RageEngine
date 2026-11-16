// Rage Engine 0.12 - fisticuffs.
// What the game does on its own (from its behaviour tree "HumanUnarmed"): a disarmed NPC switches to the FLEE stance,
// looks for a weapon within 30 m and runs to it - and if there's none, it runs away. The game has no unarmed stance
// data and no punch animations for NPCs at all, which is why "fists up" never stuck in 0.11 (the tree reset it to
// Flee every update).
//
// Rage Engine's way: a disarmed NPC with no weapon close by gets an invisible, harmless "phantom" one-handed weapon in
// its hand. The game's AI now treats it as armed: it squares up and fights with its real one-handed melee animations -
// jabs and swings with an empty fist - and its real parry/defence moves. The phantom has no colliders and no damage;
// the punches land with the NPC's own fist, and Rage Engine turns a fist hitting you into punch damage + sound + buzz.
// The moment a real weapon is in reach they snatch it and the phantom is gone.
//   + they throw small loose objects (plates, bowls, cups, bottles...) at you when you're out of reach (game throw)
//   + barehanded forearm block when your hand/weapon comes in fast at their head
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const float FistThink = 0.25f;
        static readonly HashSet<Item> phantoms = new HashSet<Item>();
        static ItemData phantomData;
        static Creature hookedPlayer;
        static float nextPlayerHit;

        // ---------------- knockdown guard ----------------
        /// Lets the next knockdown of this NPC through (it's one Rage Engine or the player meant).
        internal static void AllowFall(NpcState s, float seconds) { if (s != null) s.allowFallUntil = Mathf.Max(s.allowFallUntil, Time.time + seconds); }

        /// Every frame: if the game drops a standing NPC on its own (an ordinary hit, a shove, a stagger that turned into
        /// a fall), put them straight back on their feet - unless Rage Engine or the player meant it.
        void GuardKnockdown(NpcState s)
        {
            var c = s.creature;
            var rag = c.ragdoll;
            if (rag == null) return;
            var st = rag.state;
            if (st == s.prevState) return;
            var prev = s.prevState;
            s.prevState = st;
            if (!blockCheapKnockdowns || prev != Ragdoll.State.Standing || (st != Ragdoll.State.Destabilized && st != Ragdoll.State.Inert)) return;
            float hpFrac = c.maxHealth > 1f ? c.currentHealth / c.maxHealth : 1f;
            bool hurt = hpFrac < 0.45f / Mathf.Sqrt(Tough33(s));                          // 0.33: healthy men don't go down
            if (c.isKilled || s.dying || (hurt && Time.time < s.allowFallUntil) || s.spineBroken || s.lostLeg || Has(s.brokenParts, LegParts)
                || s.knockedOutUntil > 0f || s.shockDeathAt > 0f) return;
            if (rag.handlers != null && rag.handlers.Count > 0) return;            // you're holding them: grabs and throws are yours
            // 0.28: anything that happens near you is yours too - spells (gravity push), kicks, shoves, other mods. This guard
            // was undoing them, which is why vanilla effects 'did nothing'. It now only catches trips far away from the fight.
            // 0.28.2: the guard is back (0.28 dropped it near you and NPCs fell over from everything). Two things still
            // get through: your spells (gravity push etc. - you were holding the cast button) and real power shots.
            if (Time.time - lastCastAt < 1.5f) return;
            if (hurt && Time.time - s.powerHitAt < 1f) return;
            if (Time.time - s.blockWindowStart > 3f) { s.blockWindowStart = Time.time; s.blockedFalls = 0; }
            if (++s.blockedFalls > 20) return;                                        // something keeps knocking them over: let it
            Guard("antifall", () => rag.SetState(Ragdoll.State.Standing));
            s.prevState = Ragdoll.State.Standing;
            Log($"{c.name}: blocked a cheap knockdown (kept on their feet)");
        }

        // ---------------- fisticuffs tick ----------------
        void TickFisticuffs(NpcState s)
        {
            var c = s.creature;
            if (c.isKilled || s.fleeing || c.ragdoll == null || c.brain == null || c.brain.instance == null || !c.brain.instance.isActive)
            { ReleaseGuard(s); DropPhantom(s); return; }
            HookPlayerFists();
            bool standing = c.ragdoll.state == Ragdoll.State.Standing;
            float now = Time.time;

            if (fistGuard && standing && s.punchPhase == 0 && (s.phantom == null || s.behavior == Behavior.Unarmed)) TickGuard(s, now);
            if (s.throwing) { TickThrow(s, now); return; }
            if (now < s.nextFistThink) return;
            s.nextFistThink = now + FistThink;

            // the phantom fell out of the hand (disarmed again, knocked down...): get rid of it
            if (s.phantom != null && !InHand(c, s.phantom)) DropPhantom(s);
            bool armed = RealWeapon(c, Side.Left) || RealWeapon(c, Side.Right);
            if (armed) { s.unarmedSince = 0f; DropPhantom(s); return; }
            if (!standing) return;
            if (s.unarmedSince <= 0f) { s.unarmedSince = now; return; }
            // 0.14: no hand that can hold anything (arms cut, grip muscles gone, agony): no weapons, no fists, no throws
            if (!HandUsable(s, Side.Left) && !HandUsable(s, Side.Right)) { DropPhantom(s); return; }
            float bare = now - s.unarmedSince;
            if (s.clutching && s.pain * painIntensity > 0.35f) return;           // busy holding a bad wound

            // 0.16: RNAS decides fists vs. fetching a weapon (RetrieveWeapon behavior grabs it, with a timeout and a
            // blacklist so they never loop on a weapon they can't get). Only an unarmed fighter throws things.
            TickStance(s, now);
            if (s.behavior != Behavior.Unarmed) return;
            ThinkPunch(s, now);

            // 3) throw something at you
            if (throwObjects && now >= s.nextThrow && bare > 1.5f)
            {
                var target = PlayerTarget();
                if (target == null) return;
                float d = Vector3.Distance(c.transform.position, target.position);
                if (d < 2.2f || d > 12f) return;
                s.nextThrow = now + Random.Range(1.5f, 3f);
                var it = FindLoose(c, 1.4f, false);
                if (it == null || !TryGrab(s, it)) return;
                s.throwing = true; s.throwItem = it;
                s.throwAt = now + 0.35f;
                s.nextThrow = now + Random.Range(5f, 9f);
                Log($"{c.name} picks up {it.data?.id} to throw");
            }
        }

        static bool InHand(Creature c, Item it)
        {
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                var h = Hand(c, side);
                if (h != null && h.grabbedHandle != null && h.grabbedHandle.item == it) return true;
            }
            return false;
        }

        /// A real weapon (not the phantom, not a grabbed body part) is in this hand.
        static bool RealWeapon(Creature c, Side side)
        {
            var h = Hand(c, side);
            if (h == null || h.grabbedHandle == null || h.grabbedHandle is HandleRagdoll) return false;
            var it = h.grabbedHandle.item;
            return it == null || !phantoms.Contains(it);
        }

        // ---------------- phantom weapon ----------------
        void EquipPhantom(NpcState s)
        {
            var c = s.creature;
            s.phantomAskedAt = Time.time;
            // 0.20 fix (game-breaking bug): never right after spawning (the NPC hasn't drawn its weapon yet), never
            // while a real weapon is in either hand
            if (Time.time - s.trackedAt < 8f || RealWeapon(c, Side.Left) || RealWeapon(c, Side.Right)) return;
            if (phantomData == null)
                phantomData = FindItem(d => d.type == ItemData.Type.Weapon && Contains(d.id, "dagger") && !Contains(d.id, "offhand") && !Contains(d.id, "throw"))
                              ?? FindItem(d => d.type == ItemData.Type.Weapon && Contains(d.id, "dagger"))
                              ?? FindItem(d => d.type == ItemData.Type.Weapon && Contains(d.id, "knife"));
            if (phantomData == null) { Log("fisticuffs: no dagger in the catalog to use as a phantom"); return; }
            Side side = HandUsable(s, Side.Right) ? Side.Right : Side.Left;
            if (!HandUsable(s, side)) return;
            var hand = Hand(c, side);
            if (hand == null) return;
            s.phantomPending = true;
            phantomData.SpawnAsync(item =>
            {
                s.phantomPending = false;
                if (item == null) return;
                if (s.creature != c || c.isKilled || RealWeapon(c, Side.Left) || RealWeapon(c, Side.Right) || hand.grabbedHandle != null)
                { Guard("despawn", () => item.Despawn()); return; }
                phantoms.Add(item);
                foreach (var r in item.GetComponentsInChildren<Renderer>(true)) r.enabled = false;          // invisible
                foreach (var col in item.GetComponentsInChildren<Collider>(true)) col.enabled = false;      // touches nothing
                var handle = side == Side.Right && item.mainHandleRight != null ? item.mainHandleRight
                           : side == Side.Left && item.mainHandleLeft != null ? item.mainHandleLeft
                           : item.handles != null && item.handles.Count > 0 ? item.handles[0] : null;
                if (handle == null) { DespawnPhantom(item); return; }
                bool ok = false;
                Guard("grab", () => { hand.Grab(handle, true); ok = hand.grabbedHandle != null; });
                if (!ok) { DespawnPhantom(item); return; }
                s.phantom = item;
                Log($"{c.name} puts up their fists (phantom {item.data?.id})");
            }, hand.transform.position, hand.transform.rotation, null, false);           // 0.20: NOT pooled - a pooled item gets parked at the map origin when released, dragging the arm there
        }

        static void DropPhantom(NpcState s)
        {
            var it = s.phantom;
            if (it == null) return;
            s.phantom = null;
            var c = s.creature;
            if (c != null)
                foreach (var side in new[] { Side.Left, Side.Right })
                {
                    var h = Hand(c, side);
                    if (h != null && h.grabbedHandle != null && h.grabbedHandle.item == it) Guard("grab", () => h.UnGrab(false));
                }
            DespawnPhantom(it);
        }

        static void DespawnPhantom(Item it)
        {
            phantoms.Remove(it);
            if (it == null) return;
            // 0.20: let go of it from EVERY hand first, then remove it
            Guard("ungrab-all", () =>
            {
                if (it.handlers != null) foreach (var h in it.handlers.ToArray()) if (h != null) h.UnGrab(false);
            });
            Guard("despawn", () => it.Despawn());
        }

        /// 0.20 watchdog: an NPC hand holding something that isn't actually at the hand (> 0.6 m away) is a broken
        /// grab - the item went somewhere and is dragging the arm with it. Let go immediately.
        static void HandWatchdog(NpcState s)
        {
            var c = s.creature;
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                var h = Hand(c, side);
                if (h == null || h.grabbedHandle == null) continue;
                var hd = h.grabbedHandle;
                if (Vector3.Distance(hd.transform.position, h.transform.position) < (hd is HandleRagdoll ? 1.0f : 1.5f)) { if (side == Side.Left) s.looseL = 0; else s.looseR = 0; continue; }   // 0.25.1: long handles (bats, spears) sit far from the hand
                if (side == Side.Left ? ++s.looseL < 3 : ++s.looseR < 3) continue;                                                   // must stay loose for 3 checks
                var item = hd.item;
                Log($"{c.name}: {(item != null && phantoms.Contains(item) ? "fist tool" : item?.data?.id)} came loose from the hand ({Vector3.Distance(hd.transform.position, h.transform.position):0.0} m) - released");
                Guard("ungrab", () => h.UnGrab(false));
                if (item != null && phantoms.Contains(item)) { if (s.phantom == item) s.phantom = null; DespawnPhantom(item); }
            }
        }

        // ---------------- their fists hitting you ----------------
        static void HookPlayerFists()
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null || pc == hookedPlayer || pc.ragdoll == null) return;
            if (hookedPlayer != null && hookedPlayer.ragdoll != null) Guard("events.contact", () => hookedPlayer.ragdoll.OnContactStartEvent -= OnPlayerContact);
            hookedPlayer = pc;
            Guard("events.contact", () => pc.ragdoll.OnContactStartEvent += OnPlayerContact);
        }

        static void OnPlayerContact(CollisionInstance hit, RagdollPart part)
        {
            if (!modEnabled || hit == null || hit.sourceCollider == null || instance == null) return;
            Guard("fists", () =>
            {
                var fist = hit.sourceCollider.GetComponentInParent<RagdollHand>();
                if (fist == null || fist.creature == null || fist.creature.isPlayer || fist.creature.isKilled) return;
                if (!instance.TryGet(fist.creature, out var s) || (s.phantom == null && s.punchPhase == 0 && Time.time > s.groundSwingUntil)) return;     // only barehanded fighters
                float speed = hit.impactVelocity.magnitude;
                if (speed < (s.punchPhase != 0 ? 1.2f : 2f) || Time.time < nextPlayerHit) return;
                s.punchLanded = true; s.hitsLanded++;
                nextPlayerHit = Time.time + 0.35f;
                var pc = Player.local.creature;
                float dmg = npcPunchDamage * Mathf.Clamp(speed / 4f, 0.6f, 1.5f) * (Has(part.type, RagdollPart.Type.Head) ? 1.3f : 1f);
                Guard("player.damage", () => pc.Damage(dmg));
                Sfx.PlayAt("NRE_Punch", hit.contactPoint, 1f);
                Haptics.Pulse(Side.Left, 0.7f, 1); Haptics.Pulse(Side.Right, 0.7f, 1);
                Log($"{fist.creature.name} punches you ({part.type}, {dmg:0.0} dmg)");
            });
        }

        // ---------------- shared helpers ----------------
        static Transform PlayerTarget()
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null || pc.ragdoll == null) return null;
            var t = pc.ragdoll.headPart != null ? pc.ragdoll.headPart : pc.ragdoll.rootPart;
            return t != null ? t.transform : pc.transform;
        }

        /// Nearest loose item within reach: weapon=true -> weapons only; false -> small throwable things (not weapons).
        static Item FindLoose(Creature c, float reach, bool weapon)
        {
            var list = Item.allActive;
            if (list == null) return null;
            Vector3 at = c.transform.position + Vector3.up * 0.9f;
            Item best = null; float bestD = reach * reach;
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                if (it == null || it.data == null || it.handles == null || it.handles.Count == 0 || phantoms.Contains(it)) continue;
                if (it.holder != null || it.isFlying || it.isBrokenPiece) continue;
                bool handed;
                try { handed = it.IsHanded(); } catch { continue; }                // no closure per item (0.17: less garbage)
                if (handed) continue;
                var t = it.data.type;
                if (weapon && it.data.id != null && (it.data.id.IndexOf("Throwable", System.StringComparison.OrdinalIgnoreCase) >= 0 || it.data.id.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                if (weapon ? t != ItemData.Type.Weapon
                           : t == ItemData.Type.Weapon || t == ItemData.Type.Shield || t == ItemData.Type.Body || t == ItemData.Type.Wardrobe
                             || t == ItemData.Type.Spell || t == ItemData.Type.Quiver) continue;
                if (!weapon && (it.physicBody == null || it.physicBody.rigidBody == null || it.physicBody.rigidBody.mass > 2.5f)) continue;
                if (tools.ContainsKey(it)) continue;                               // not our test pistol/dynamite
                Vector3 p = it.transform.position;
                if (Mathf.Abs(p.y - c.transform.position.y) > 1.6f) continue;
                float d = (p - at).sqrMagnitude;
                if (d < bestD) { bestD = d; best = it; }
            }
            return best;
        }

        bool TryGrab(NpcState s, Item it)
        {
            var c = s.creature;
            // 0.14: only a hand that can actually close (grip muscles intact, not in agony), is empty, and isn't
            // holding a wound
            bool Ok(Side sd) => HandUsable(s, sd) && Hand(c, sd) != null && Hand(c, sd).grabbedHandle == null && !(s.clutching && s.clutchSide == sd);
            Side side = Ok(Side.Right) ? Side.Right : Ok(Side.Left) ? Side.Left : (Side)(-1);
            if ((int)side < 0) return false;
            var hand = Hand(c, side);
            var handle = it.mainHandleRight != null && side == Side.Right ? it.mainHandleRight : it.mainHandleLeft != null && side == Side.Left ? it.mainHandleLeft : it.handles[0];
            s.lastGrabbed = it; s.lastGrabTime = Time.time;                        // its snap into the hand isn't a hit
            bool ok = false;
            Guard("grab", () => { hand.Grab(handle, true); ok = hand.grabbedHandle != null; });
            if (ok) s.throwSide = side;
            return ok;
        }

        void TickThrow(NpcState s, float now)
        {
            var c = s.creature;
            var it = s.throwItem;
            var hand = Hand(c, s.throwSide);
            if (it == null || hand == null || hand.grabbedHandle == null || hand.grabbedHandle.item != it || c.ragdoll.state != Ragdoll.State.Standing)
            { s.throwing = false; s.throwItem = null; return; }
            var target = PlayerTarget();
            if (target == null) { s.throwing = false; return; }
            if (now < s.throwAt) return;
            if (s.throwAt > 0f)
            {
                bool started = false;
                Guard("throw", () =>
                {
                    var th = c.brain.instance.GetModule<BrainModuleThrow>(false);
                    if (th != null) { th.StartThrow(target, false); started = true; }
                });
                s.throwAt = started ? -(now + 2.5f) : -now;
                if (started) { Log($"{c.name} throws (game throw)"); return; }
            }
            if (now < -s.throwAt) return;
            Vector3 from = hand.transform.position;
            Vector3 to = target.position;
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float dist = flat.magnitude;
            float speed = Mathf.Clamp(5f + dist * 0.9f, 6f, 14f);
            float time = dist / speed;
            Vector3 v = flat.normalized * speed + Vector3.up * ((to.y - from.y) / Mathf.Max(0.1f, time) + 0.5f * 9.81f * time);
            hand.UnGrab(true);
            if (it.physicBody != null)
            {
                it.physicBody.velocity = v;
                it.physicBody.rigidBody.angularVelocity = Random.insideUnitSphere * 8f;
            }
            s.throwing = false; s.throwItem = null;
            Log($"{c.name} throws {it.data?.id} (physics)");
        }

        // ---------------- barehanded blocking (before the phantom is in hand) ----------------
        void TickGuard(NpcState s, float now)
        {
            var c = s.creature;
            if (s.blockUntil > 0f && now >= s.blockUntil) { s.blockUntil = 0f; var hd = c.ragdoll.headPart; if (hd != null && s.stanceOn && s.punchPhase == 0) Guard("guard", () => RaiseGuard(s, hd)); }
            if (s.guardUntil > 0f && now >= s.guardUntil && !s.stanceOn) ReleaseGuard(s);
            // 0.25: a boxer in stance has his guard up all the time - he still has to actively block (this used to return here)
            if (now < s.nextGuard || (s.guardUntil > 0f && !s.stanceOn) || now < s.blockUntil) return;
            // 0.22: armed NPCs block with their FREE hand (the game parries with the weapon); both hands full = game only
            bool armedL = RealWeapon(c, Side.Left), armedR = RealWeapon(c, Side.Right);
            if (armedL && armedR) return;
            if (s.punchPhase != 0 || s.shocked || s.knockedOut || c.ragdoll.state != Ragdoll.State.Standing) return;
            var head = c.ragdoll.headPart;
            var chestG = ChestPart(c);
            var pc = Player.local != null ? Player.local.creature : null;
            if (head == null || pc == null) return;
            foreach (var ph in new[] { pc.handLeft, pc.handRight })
            {
                if (ph == null || ph.physicBody == null) continue;
                Vector3 v = ph.physicBody.velocity;
                if (ph.grabbedHandle != null && ph.grabbedHandle.item != null && ph.grabbedHandle.item.physicBody != null)
                    v = ph.grabbedHandle.item.physicBody.velocity;
                Vector3 to = head.transform.position - ph.transform.position;
                float d = to.magnitude;
                bool body = false;
                if (d > 1.3f || v.magnitude < 3f || Vector3.Dot(v.normalized, to / Mathf.Max(d, 0.01f)) < 0.6f)
                {
                    // 0.22: body shots too
                    if (chestG == null) continue;
                    to = chestG.transform.position - Vector3.up * 0.1f - ph.transform.position; d = to.magnitude;
                    if (d > 1.2f || v.magnitude < 3f || Vector3.Dot(v.normalized, to / Mathf.Max(d, 0.01f)) < 0.65f) continue;
                    body = true;
                }
                // 0.22: whether they react in time depends on skill, and on how punch-drunk / hurt / tired they are
                float react = s.defSkill * s.bioReact * (1f - 0.55f * Mathf.Clamp01(s.drunk)) * (1f - 0.35f * Mathf.Clamp01(s.pain * painIntensity)) * (s.ppCritical ? 0.3f : 1f)
                              * (s.stanceOn ? 1.25f : 1f);
                if (Random.value > Mathf.Clamp(react, 0.1f, 0.92f)) { s.nextGuard = now + 0.5f; return; }
                if (s.stanceOn && body && !armedL && !armedR) { BlockPunch(s, chestG, ph.transform.position, now); return; }
                if (armedL || armedR || body)
                {
                    RaiseGuard(s, head, body, armedL ? Side.Left : armedR ? Side.Right : (Side?)null);
                    return;
                }
                // 0.19: not always the same defence - cover up, lean back out of it, or duck under it
                float r = Random.value * (s.stanceOn ? 1f : 0.6f);
                if (s.stanceOn && r < 0.7f) { BlockPunch(s, head, ph.transform.position, now); return; }
                if (r < 0.6f || head.physicBody == null) RaiseGuard(s, head);
                else if (r < 0.8f) { head.physicBody.AddForce((-to.normalized + Vector3.up * 0.2f) * 3.5f, ForceMode.Impulse); ShoveBodyQuiet(s, -to, 0.7f); Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso, 0.35f, 0.3f); s.nextGuard = now + 1f; }
                else { head.physicBody.AddForce(Vector3.down * 4f + Vector3.Cross(Vector3.up, to.normalized) * 1.5f, ForceMode.Impulse); Loosen(s, RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso, 0.4f, 0.35f); s.coreUntil = Mathf.Max(s.coreUntil, now + 0.5f); s.nextGuard = now + 1f; }
                return;
            }
        }

        /// 0.25: an actual block - the glove on the side the punch comes from goes to meet it (between the fist and the
        /// spot it's aimed at), the other glove tucks in tight. Held a moment, then back to the normal guard.
        void BlockPunch(NpcState s, RagdollPart at, Vector3 fist, float now)
        {
            var c = s.creature;
            var ik = c.ragdoll.ik;
            if (ik == null || at == null || c.ragdoll.headPart == null) return;
            if (s.guardL == null) RaiseGuard(s, c.ragdoll.headPart);
            Vector3 fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 from = fist - at.transform.position;
            Side side = Vector3.Dot(from, right) >= 0f ? Side.Right : Side.Left;
            if (Dead(s, side) || RealWeapon(c, side) || (s.clutching && s.clutchSide == side)) side = Other(side);
            if (Dead(s, side) || RealWeapon(c, side)) return;
            var a = side == Side.Left ? s.guardL : s.guardR;
            var b = side == Side.Left ? s.guardR : s.guardL;
            var head = c.ragdoll.headPart.transform;
            a.SetParent(head, true);
            a.position = at.transform.position + Vector3.ClampMagnitude(from, 1f).normalized * 0.2f;
            a.rotation = Quaternion.LookRotation(Vector3.up, -from.normalized);
            ik.SetHandAnchor(side, a); ik.SetHandState(side, true, true);
            Side o = Other(side);
            if (!Dead(s, o) && !RealWeapon(c, o) && !(s.clutching && s.clutchSide == o))
            {
                b.SetParent(head, true);
                b.position = head.position + fwd * 0.12f + right * (o == Side.Left ? -0.06f : 0.06f) - Vector3.up * 0.03f;
                b.rotation = Quaternion.LookRotation(Vector3.up, -fwd);
                ik.SetHandAnchor(o, b); ik.SetHandState(o, true, true);
            }
            s.coreUntil = Mathf.Max(s.coreUntil, now + 0.3f);
            s.blockUntil = now + 0.25f;
            s.nextGuard = now + 0.2f;                                                      // ready for the next one in the combination
            s.blocks++;
        }

        void RaiseGuard(NpcState s, RagdollPart head, bool low = false, Side? skip = null)
        {
            var c = s.creature;
            var ik = c.ragdoll.ik;
            if (ik == null) return;
            if (s.guardL == null) { s.guardL = new GameObject("RageEngine_GuardL").transform; s.guardR = new GameObject("RageEngine_GuardR").transform; }
            Vector3 fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                if (Dead(s, side) || (s.clutching && s.clutchSide == side) || skip == side || RealWeapon(c, side)) continue;
                var a = side == Side.Left ? s.guardL : s.guardR;
                a.SetParent(head.transform, true);
                // 0.19 guard styles: 0 = high guard, 1 = tight "peek-a-boo" (gloves on the cheeks), 2 = lead hand low
                float sg = side == Side.Left ? -1f : 1f;
                a.position = s.guardStyle == 1 ? head.transform.position + fwd * 0.14f + right * sg * 0.06f - Vector3.up * 0.0f
                           : s.guardStyle == 2 && side == Side.Left ? head.transform.position + fwd * 0.25f + right * sg * 0.12f - Vector3.up * 0.32f
                           : head.transform.position + fwd * 0.22f + right * sg * 0.07f - Vector3.up * 0.02f;
                if (low) a.position += -Vector3.up * 0.38f + fwd * 0.04f;                          // 0.22: elbows in, covering the body
                a.rotation = Quaternion.LookRotation(Vector3.up, -fwd);
                ik.SetHandAnchor(side, a);
                ik.SetHandState(side, true, true);
            }
            s.guardUntil = Time.time + Random.Range(0.4f, 0.65f);
            s.nextGuard = s.guardUntil + Random.Range(0.6f, 1.4f);
        }

        static void ReleaseGuard(NpcState s)
        {
            if (s.guardUntil <= 0f) return;
            s.guardUntil = 0f;
            var ik = s.creature != null && s.creature.ragdoll != null ? s.creature.ragdoll.ik : null;
            if (ik == null) return;
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                if (s.clutching && s.clutchSide == side) continue;
                ik.SetHandAnchor(side, null);
                ik.SetHandState(side, false, false);
            }
        }
    }
}

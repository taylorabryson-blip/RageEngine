// Rage Engine 0.16 - RMSS + RNAS.
//
// RMSS (Rage Memory & State System): every NPC carries a fixed table of condition TAGS (no strings, no allocations).
//   Each tag has a shared definition (lifetime + stacking rule + default duration). An NPC only stores, per tag, when
//   it expires and how intense it is. Physical conditions (a dead arm, a blind eye, a pin) are PERSISTENT tags that are
//   synced from the systems that own them; reactions add TEMPORARY / REFRESHABLE / STACKING tags.
//
// RNAS (Rage Neural & Autonomic System): reads the tags -> works out what the NPC is physically CAPABLE of
//   (hands, arms, legs, sight, balance) -> picks ONE behavior by priority and owns it (a behavior token, so two
//   systems never fight over the same NPC):
//      Terminal > Pinned > Incapacitated > Recovering > Adrenaline > Blind > Disengage > Unarmed > RetrieveWeapon > Normal
//   It does not replace the game's combat AI. It only steps in where the current action is impossible or wrong:
//   movement (allowMove / speed), the game's melee switch, fists vs. fetching a weapon.
//   Re-evaluates on events (hit, weapon lost/gained, tag change) plus a cheap 0.5 s validation pass.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal enum Tag : byte
    {
        // temporary / refreshable
        Unbalanced, Staggered, Stunned, PainResponse, Agony, Recovering, Downed, AdrenalineActive, AdrenalineExhausted,
        Struggling, Pierced, WeaponRemoved, PinReleased, Anger, Fear,
        // persistent (synced from their owning systems)
        Weaponless, Blind, EyesShut, Impaled, PinnedToWorld,
        LeftArmUnavailable, RightArmUnavailable, LeftHandUnavailable, RightHandUnavailable,
        LeftArmImpaired, RightArmImpaired, LeftLegImpaired, RightLegImpaired, BothLegsUnavailable, SpineBroken,
        Terminal,
        Count
    }

    internal enum Life : byte { Temporary, Refreshable, Stacking, Persistent }
    internal enum StackRule : byte { RefreshDuration, ExtendDuration, IncreaseIntensity, IgnoreDuplicate, Replace }
    internal enum Ability : byte { Normal, Impaired, Unavailable }
    internal enum Cap : byte { HoldWeapon, UseHands, Move, RecoverBalance, ContinueCombat, Retreat, RetrieveWeapon, Defend, See, Count }
    internal enum Behavior : byte { Normal, RetrieveWeapon, Unarmed, Disengage, Blind, Surrender, Adrenaline, Recovering, Incapacitated, Pinned, Terminal }

    internal struct TagDef
    {
        public Life life; public StackRule rule; public float duration, maxIntensity;
        public TagDef(Life l, StackRule r, float d, float max = 1f) { life = l; rule = r; duration = d; maxIntensity = max; }
    }

    public sealed partial class RageEngineScript
    {
        // shared templates: loaded once, used by every NPC
        static readonly TagDef[] TagDefs = BuildTagDefs();
        static TagDef[] BuildTagDefs()
        {
            var d = new TagDef[(int)Tag.Count];
            var persistent = new TagDef(Life.Persistent, StackRule.IgnoreDuplicate, float.PositiveInfinity);
            for (int i = 0; i < d.Length; i++) d[i] = persistent;
            d[(int)Tag.Unbalanced] = new TagDef(Life.Refreshable, StackRule.RefreshDuration, 1.5f);
            d[(int)Tag.Staggered] = new TagDef(Life.Temporary, StackRule.IgnoreDuplicate, 1.0f);
            d[(int)Tag.Stunned] = new TagDef(Life.Temporary, StackRule.IgnoreDuplicate, 2.0f);
            d[(int)Tag.PainResponse] = new TagDef(Life.Stacking, StackRule.IncreaseIntensity, 4f, 3f);
            d[(int)Tag.Agony] = new TagDef(Life.Refreshable, StackRule.Replace, 5f);
            d[(int)Tag.Recovering] = new TagDef(Life.Refreshable, StackRule.RefreshDuration, 1.2f);
            d[(int)Tag.Downed] = new TagDef(Life.Refreshable, StackRule.RefreshDuration, 0.6f);
            d[(int)Tag.AdrenalineActive] = new TagDef(Life.Refreshable, StackRule.Replace, 10f);
            d[(int)Tag.AdrenalineExhausted] = new TagDef(Life.Temporary, StackRule.IgnoreDuplicate, 25f);
            d[(int)Tag.Struggling] = new TagDef(Life.Refreshable, StackRule.RefreshDuration, 1.0f);
            d[(int)Tag.Pierced] = new TagDef(Life.Refreshable, StackRule.RefreshDuration, 8f);
            d[(int)Tag.WeaponRemoved] = new TagDef(Life.Temporary, StackRule.Replace, 3f);
            d[(int)Tag.PinReleased] = new TagDef(Life.Temporary, StackRule.Replace, 3f);
            d[(int)Tag.Anger] = new TagDef(Life.Stacking, StackRule.IncreaseIntensity, 8f, 5f);
            d[(int)Tag.Fear] = new TagDef(Life.Stacking, StackRule.IncreaseIntensity, 5f, 5f);
            return d;
        }

        // ---------------- RMSS: per-NPC tag instances ----------------
        internal static bool HasTag(NpcState s, Tag t) => Time.time < s.tagUntil[(int)t];
        internal static float TagIntensity(NpcState s, Tag t) => HasTag(s, t) ? s.tagIntensity[(int)t] : 0f;

        /// Applies a tag by its definition's stacking rule. duration < 0 = the template's default.
        internal static void AddTag(NpcState s, Tag t, float duration = -1f, float intensity = 1f)
        {
            int i = (int)t;
            var def = TagDefs[i];
            float now = Time.time, dur = duration >= 0f ? duration : def.duration;
            bool had = now < s.tagUntil[i];
            if (!had) { s.tagUntil[i] = now + dur; s.tagIntensity[i] = Mathf.Min(intensity, def.maxIntensity); }
            else switch (def.rule)
            {
                case StackRule.IgnoreDuplicate: return;
                case StackRule.RefreshDuration: s.tagUntil[i] = Mathf.Max(s.tagUntil[i], now + dur); break;
                case StackRule.ExtendDuration: s.tagUntil[i] += dur; break;
                case StackRule.IncreaseIntensity: s.tagIntensity[i] = Mathf.Min(def.maxIntensity, s.tagIntensity[i] + intensity); s.tagUntil[i] = Mathf.Max(s.tagUntil[i], now + dur); break;
                case StackRule.Replace: s.tagUntil[i] = now + dur; s.tagIntensity[i] = Mathf.Min(intensity, def.maxIntensity); break;
            }
            if (!had) { s.rnasDirty = true; if (debugLog) Log($"{Name(s)} +{t}"); }
        }

        internal static void RemoveTag(NpcState s, Tag t)
        {
            int i = (int)t;
            if (Time.time >= s.tagUntil[i]) return;
            s.tagUntil[i] = 0f; s.tagIntensity[i] = 0f; s.rnasDirty = true;
            if (debugLog) Log($"{Name(s)} -{t}");
        }

        /// For persistent physical conditions owned by another system: on/off.
        static void SetTag(NpcState s, Tag t, bool on) { if (on) AddTag(s, t); else RemoveTag(s, t); }

        static void ClearTags(NpcState s)
        {
            for (int i = 0; i < s.tagUntil.Length; i++) { s.tagUntil[i] = 0f; s.tagIntensity[i] = 0f; }
            for (int i = 0; i < s.caps.Length; i++) s.caps[i] = Ability.Normal;
        }

        static string Name(NpcState s) => s.creature != null ? s.creature.name : "?";

        /// Mirrors the physical state owned by the injury / musculature / eye / impalement systems into persistent tags.
        static void SyncTags(NpcState s)
        {
            var c = s.creature;
            float now = Time.time;
            bool lArm = Dead(s, Side.Left), rArm = Dead(s, Side.Right);
            SetTag(s, Tag.LeftArmUnavailable, lArm);
            SetTag(s, Tag.RightArmUnavailable, rArm);
            SetTag(s, Tag.LeftHandUnavailable, lArm || Grip(s, Side.Left) < 0.3f);
            SetTag(s, Tag.RightHandUnavailable, rArm || Grip(s, Side.Right) < 0.3f);
            SetTag(s, Tag.LeftArmImpaired, !lArm && ArmStrength(s, Side.Left) < 0.7f);
            SetTag(s, Tag.RightArmImpaired, !rArm && ArmStrength(s, Side.Right) < 0.7f);
            float ll = LegStrength(s, Side.Left), rl = LegStrength(s, Side.Right);
            SetTag(s, Tag.LeftLegImpaired, ll < 0.6f || Has(s.limpParts, LeftLegParts));
            SetTag(s, Tag.RightLegImpaired, rl < 0.6f || Has(s.limpParts, RightLegParts));
            SetTag(s, Tag.BothLegsUnavailable, s.cantStand || (ll < 0.2f && rl < 0.2f) || s.spineBroken);
            SetTag(s, Tag.SpineBroken, s.spineBroken);
            SetTag(s, Tag.Blind, AllBlind(s));
            SetTag(s, Tag.EyesShut, now < s.eyesShutUntil);
            SetTag(s, Tag.Impaled, s.impaleItem != null);
            SetTag(s, Tag.PinnedToWorld, s.impaleItem != null && s.pinned);
            SetTag(s, Tag.Terminal, c.isKilled && !s.onAdrenaline);
            if (c.isKilled && !s.onAdrenaline) s.diedFinal = true;
            if (now < s.agonyUntil) AddTag(s, Tag.Agony, s.agonyUntil - now); else RemoveTag(s, Tag.Agony);
            if (Pumped(s)) AddTag(s, Tag.AdrenalineActive, Mathf.Max(0.5f, (s.onAdrenaline ? s.adrenalineEnd : s.rushUntil) - now));
            else RemoveTag(s, Tag.AdrenalineActive);
            bool weaponless = !RealWeapon(c, Side.Left) && !RealWeapon(c, Side.Right);
            if (!weaponless)
                foreach (var sd in new[] { Side.Right, Side.Left })
                {
                    var h = Hand(c, sd);
                    if (h != null && h.grabbedHandle != null && h.grabbedHandle.item != null && !phantoms.Contains(h.grabbedHandle.item)) { s.ownWeapon = h.grabbedHandle.item; break; }
                }
            if (weaponless && !HasTag(s, Tag.Weaponless)) AddTag(s, Tag.WeaponRemoved);
            SetTag(s, Tag.Weaponless, weaponless);
            if (c.ragdoll != null)
            {
                var st = c.ragdoll.state;
                if (!c.isKilled && st == Ragdoll.State.Destabilized) AddTag(s, Tag.Downed);
                if (s.balance < 0.6f) AddTag(s, Tag.Unbalanced);
            }
        }

        // ---------------- capabilities ----------------
        internal static Ability CapOf(NpcState s, Cap c) => s.caps[(int)c];

        static void EvaluateCapabilities(NpcState s)
        {
            bool lHand = !HasTag(s, Tag.LeftHandUnavailable), rHand = !HasTag(s, Tag.RightHandUnavailable);
            bool agony = HasTag(s, Tag.Agony) && !HasTag(s, Tag.AdrenalineActive);
            bool pinned = HasTag(s, Tag.PinnedToWorld);
            bool legsGone = HasTag(s, Tag.BothLegsUnavailable);
            bool legHurt = HasTag(s, Tag.LeftLegImpaired) || HasTag(s, Tag.RightLegImpaired);
            bool armHurt = HasTag(s, Tag.LeftArmImpaired) || HasTag(s, Tag.RightArmImpaired);
            bool blind = HasTag(s, Tag.Blind) || HasTag(s, Tag.EyesShut);
            bool terminal = HasTag(s, Tag.Terminal);

            Ability hands = terminal || agony || (!lHand && !rHand) ? Ability.Unavailable : (!lHand || !rHand || armHurt) ? Ability.Impaired : Ability.Normal;
            s.caps[(int)Cap.UseHands] = hands;
            s.caps[(int)Cap.HoldWeapon] = hands;
            s.caps[(int)Cap.Move] = terminal || pinned || legsGone ? Ability.Unavailable : legHurt || HasTag(s, Tag.AdrenalineExhausted) ? Ability.Impaired : Ability.Normal;
            s.caps[(int)Cap.RecoverBalance] = terminal || legsGone ? Ability.Unavailable : legHurt ? Ability.Impaired : Ability.Normal;
            s.caps[(int)Cap.See] = HasTag(s, Tag.Blind) ? Ability.Unavailable : blind || s.blindEyes != 0 ? Ability.Impaired : Ability.Normal;
            s.caps[(int)Cap.Retreat] = s.caps[(int)Cap.Move] == Ability.Unavailable || blind ? Ability.Unavailable : s.caps[(int)Cap.Move];
            s.caps[(int)Cap.RetrieveWeapon] = hands == Ability.Unavailable || s.caps[(int)Cap.Move] == Ability.Unavailable || blind ? Ability.Unavailable
                                             : hands == Ability.Impaired || s.caps[(int)Cap.Move] == Ability.Impaired ? Ability.Impaired : Ability.Normal;
            s.caps[(int)Cap.Defend] = hands == Ability.Unavailable ? Ability.Unavailable : hands;
            s.caps[(int)Cap.ContinueCombat] = terminal || agony || (hands == Ability.Unavailable && !HasTag(s, Tag.AdrenalineActive)) ? Ability.Unavailable
                                             : armHurt || legHurt ? Ability.Impaired : Ability.Normal;
        }

        // ---------------- arbitration ----------------
        static readonly object RnasMoveHandler = new object();
        static readonly object RibHandler = new object();

        void TickRnas(NpcState s, float dt)
        {
            var c = s.creature;
            if (c == null || c.ragdoll == null) return;
            float now = Time.time;
            SyncTags(s);
            bool validate = now >= s.rnasNext;
            if (!s.rnasDirty && !validate) { TickBehavior(s, now); return; }
            s.rnasDirty = false; s.rnasNext = now + 0.5f;
            EvaluateCapabilities(s);
            var want = Decide(s, now, out string why);
            // hysteresis: going UP the priority list happens at once; stepping DOWN waits a moment, so two
            // conditions near their thresholds can't flip the NPC back and forth
            if (want != s.behavior && (want > s.behavior || now - s.behaviorSince > 0.6f))
            {
                var old = s.behavior;
                ExitBehavior(s, old);
                s.behavior = want; s.behaviorSince = now;
                EnterBehavior(s, want);
                Log($"{WhoIs(s)}: RNAS {old} -> {want} ({why})");
                NoteSwitch(s, want);
            }
            TickBehavior(s, now);
        }

        Behavior Decide(NpcState s, float now, out string why)
        {
            var c = s.creature;
            if (HasTag(s, Tag.Terminal)) { why = "dead"; return Behavior.Terminal; }
            if (HasTag(s, Tag.PinnedToWorld)) { why = "pinned by an embedded weapon"; return Behavior.Pinned; }
            bool physicallyDown = HasTag(s, Tag.BothLegsUnavailable) || HasTag(s, Tag.SpineBroken);
            if (physicallyDown) { why = "can't stand (legs/spine)"; return Behavior.Incapacitated; }
            if (HasTag(s, Tag.Agony) && !HasTag(s, Tag.AdrenalineActive)) { why = "agony"; return Behavior.Incapacitated; }
            if (c.ragdoll.state == Ragdoll.State.Destabilized || c.ragdoll.state == Ragdoll.State.Inert) { why = "off balance / down"; return Behavior.Recovering; }
            if (HasTag(s, Tag.AdrenalineActive)) { why = "adrenaline"; return Behavior.Adrenaline; }
            if (s.surrendered) { why = "surrendered"; return Behavior.Surrender; }
            if (s.inCombat && WantsToSurrender(s)) { s.surrendered = true; why = "badly hurt, scared, out of fight"; return Behavior.Surrender; }
            if (CapOf(s, Cap.See) == Ability.Unavailable || HasTag(s, Tag.EyesShut)) { why = HasTag(s, Tag.Blind) ? "blind" : "eyes shut"; return Behavior.Blind; }
            if (s.fleeing && CapOf(s, Cap.Retreat) != Ability.Unavailable) { why = "fleeing"; return Behavior.Disengage; }
            if (HasTag(s, Tag.Weaponless) && now - s.trackedAt < 8f) { why = "just spawned - drawing a weapon"; return Behavior.Normal; }   // 0.20
            if (HasTag(s, Tag.Weaponless))
            {
                if (CapOf(s, Cap.UseHands) == Ability.Unavailable) { why = "no usable hands"; return Behavior.Disengage; }
                var w = RetrievableWeapon(s, now);
                if (w != null && CapOf(s, Cap.RetrieveWeapon) != Ability.Unavailable)
                { s.retrieveTarget = w; why = $"weapon {w.data?.id} is close and it's safe"; return Behavior.RetrieveWeapon; }
                why = "disarmed - fists"; return Behavior.Unarmed;
            }
            why = "armed"; return Behavior.Normal;
        }

        /// A loose weapon worth going for: close, not blacklisted after a failed try, and the player isn't on top of them.
        static Item RetrievableWeapon(NpcState s, float now)
        {
            var c = s.creature;
            if (!pickUpWeapons) return null;
            // 0.17: they go back for THEIR OWN weapon if it's close; any other weapon only if it's right at their feet.
            // Brawlers and aggressive types would rather just fight with their fists.
            Item w = null;
            var own = s.ownWeapon;
            if (own != null && own.gameObject.activeInHierarchy && own.mainHandler == null && own.holder == null
                && Vector3.Distance(c.transform.position, own.transform.position) < 3.5f) w = own;
            if (w == null) w = FindLoose(c, 1.2f, true);
            if (w == null || (w == s.badWeapon && now < s.badWeaponUntil)) return null;
            if ((s.brawler || s.personality.type == PersonalityType.Aggressive) && w != own) return null;
            if (s.retrieveTries >= 2 && now - s.retrieveFirstTry < 20f) return null;
            var p = PlayerTarget();
            if (p == null) return w;
            float dPlayer = Vector3.Distance(c.transform.position, p.position);
            float dWeapon = Vector3.Distance(c.transform.position, w.transform.position);
            return dWeapon < 1.3f || dPlayer > Mathf.Lerp(4.5f, 1.8f, s.trRisk) ? w : null;    // 0.19: risk-takers go for it closer to you      // going for it with you in their face = getting killed
        }

        void EnterBehavior(NpcState s, Behavior b)
        {
            var c = s.creature;
            float now = Time.time;
            switch (b)
            {
                case Behavior.RetrieveWeapon:
                    s.retrieveUntil = now + 3.5f;
                    if (s.retrieveTries == 0 || now - s.retrieveFirstTry > 20f) { s.retrieveFirstTry = now; s.retrieveTries = 0; }
                    s.retrieveTries++;
                    DropPhantom(s);
                    break;
                case Behavior.Unarmed:
                    if (s.phantom == null && !s.phantomPending && fistsWhenDisarmed) EquipPhantom(s);
                    AnnounceFists(s);
                    break;
                case Behavior.Blind:
                    s.blindMoveFlip = now;
                    break;
                case Behavior.Pinned:
                    Scream(s, true);
                    break;
                case Behavior.Adrenaline:
                    if (s.meleeOff) SetMelee(s, true, 0f);
                    break;
                case Behavior.Surrender:
                    EnterSurrender(s);
                    break;
            }
        }

        void ExitBehavior(NpcState s, Behavior b)
        {
            var c = s.creature;
            Guard("rnas-move", () =>
            {
                c.locomotion.allowMove = true;
                c.locomotion.globalMoveSpeedMultiplier.Remove(RnasMoveHandler);
            });
            if (b == Behavior.Pinned) AddTag(s, Tag.PinReleased);
            if (b == Behavior.Adrenaline && !c.isKilled) AddTag(s, Tag.AdrenalineExhausted);
            if (b == Behavior.Unarmed && !HasTag(s, Tag.Weaponless)) DropPhantom(s);
            if (b == Behavior.Surrender) ExitSurrender(s);
        }

        /// Keeps the chosen behavior applied (cheap; runs every tick).
        void TickBehavior(NpcState s, float now)
        {
            var c = s.creature;
            switch (s.behavior)
            {
                case Behavior.Pinned:
                    Guard("rnas-move", () => c.locomotion.allowMove = false);           // the game's own penetration joint holds the body; the AI must not walk
                    if (!Pumped(s)) SetMelee(s, false, now + 1f);                       // adrenaline may still swing - it can't walk away
                    AddTag(s, Tag.Struggling);
                    break;
                case Behavior.Incapacitated:
                    Guard("rnas-move", () => c.locomotion.allowMove = false);
                    SetMelee(s, false, now + 1f);
                    break;
                case Behavior.Blind:
                    // can't see: no confident running to an exit. Shuffles a few uncertain steps, stops, turns, listens.
                    if (now >= s.blindMoveFlip)
                    {
                        bool move = !s.blindMoving;
                        s.blindMoving = move;
                        s.blindMoveFlip = now + (move ? Random.Range(0.5f, 1.2f) : Random.Range(1.2f, 2.8f));
                        Guard("rnas-move", () =>
                        {
                            c.locomotion.allowMove = move;
                            c.locomotion.globalMoveSpeedMultiplier.Remove(RnasMoveHandler);
                            c.locomotion.globalMoveSpeedMultiplier.Add(RnasMoveHandler, 0.3f);
                        });
                    }
                    break;
                case Behavior.Disengage:
                    if (HasTag(s, Tag.Weaponless) && CapOf(s, Cap.UseHands) == Ability.Unavailable) DropPhantom(s);
                    break;
                case Behavior.RetrieveWeapon:
                    // the game's AI walks to it; we grab it when it's in reach. Didn't get there in time: give up on that one
                    // 0.17 fix: picking it up (or already holding it) is success, not "giving up" - and log once only
                    if (s.retrieveTarget == null || !HasTag(s, Tag.Weaponless) || (s.retrieveTarget.mainHandler != null && s.retrieveTarget.mainHandler.creature == c))
                    { s.retrieveTarget = null; s.rnasDirty = true; }
                    else if (s.retrieveTarget.mainHandler != null || now > s.retrieveUntil)
                    {
                        s.badWeapon = s.retrieveTarget; s.badWeaponUntil = now + 12f;
                        Log($"{c.name}: RNAS gives up on {s.retrieveTarget.data?.id} - fists");
                        s.retrieveTarget = null; s.rnasDirty = true;
                    }
                    else if (Vector3.Distance(c.transform.position, s.retrieveTarget.transform.position) < 1.5f && TryGrab(s, s.retrieveTarget))
                    { Log($"{c.name} picks its weapon back up ({s.retrieveTarget.data?.id})"); s.retrieveTarget = null; s.rnasDirty = true; }
                    break;
                case Behavior.Unarmed:
                    if (s.phantom == null && !s.phantomPending && fistsWhenDisarmed && now - s.phantomAskedAt > 3f) EquipPhantom(s);
                    break;
                case Behavior.Surrender:
                    TickSurrender(s, now);
                    break;
            }
            // 0.19: a path that keeps failing is paused briefly (owned here, so nothing else fights over allowMove)
            if ((s.behavior == Behavior.Normal || s.behavior == Behavior.Unarmed) && now < s.navPauseUntil) Guard("rnas-move", () => c.locomotion.allowMove = false);
            else if ((s.behavior == Behavior.Normal || s.behavior == Behavior.Unarmed) && s.navPauseUntil > 0f) { s.navPauseUntil = 0f; Guard("rnas-move", () => c.locomotion.allowMove = true); }
            LimbLook(s, now);
            if (HasTag(s, Tag.AdrenalineExhausted) && s.behavior != Behavior.Pinned && s.behavior != Behavior.Incapacitated && s.behavior != Behavior.Blind)
                Guard("rnas-move", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(RnasMoveHandler); c.locomotion.globalMoveSpeedMultiplier.Add(RnasMoveHandler, 0.75f); });
        }

        /// Readable dump of one NPC's condition (developer tools).
        static string Describe(NpcState s)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Name(s)).Append(" = ").Append(WhoIs(s)).Append(" [").Append(s.behavior).Append("] profile ").Append(s.adrenProfile)
              .Append(" durability ").Append(s.toughness.ToString("0.00")).Append(" pain ").Append(s.pain.ToString("0.00")).Append(" | tags:");
            for (int i = 0; i < (int)Tag.Count; i++) if (HasTag(s, (Tag)i)) sb.Append(' ').Append((Tag)i);
            sb.Append(" | caps:");
            for (int i = 0; i < (int)Cap.Count; i++) if (s.caps[i] != Ability.Normal) sb.Append(' ').Append((Cap)i).Append('=').Append(s.caps[i]);
            return sb.ToString();
        }
    
        /// 0.19 limbs show their state: a dead arm gets a dark ring painted at the elbow (once), and an impaired (not
        /// dead) arm that isn't needed is cradled against the body for a while when they're not fighting.
        void LimbLook(NpcState s, float now)
        {
            var c = s.creature;
            if (!s.limbPainted && (HasTag(s, Tag.LeftArmUnavailable) || HasTag(s, Tag.RightArmUnavailable)))
            {
                s.limbPainted = true;
                foreach (var p in c.ragdoll.parts)
                    if (p != null && Has(p.type, ArmParts) && IsLowerSegment(p) && Dead(s, Has(p.type, LeftArmParts) ? Side.Left : Side.Right))
                        Guard("limbpaint", () => SkinPaint.Paint(c, p.transform.position, 0.045f, new Color(0.3f, 0.08f, 0.22f, 0.6f), TexGen.Bruise()));
            }
            if (s.inCombat || s.clutching || s.punchPhase != 0 || s.stanceOn || s.behavior == Behavior.Surrender || c.ragdoll.state != Ragdoll.State.Standing)
            { if (s.cradling) EndCradle(s); return; }
            Side? hurt = HasTag(s, Tag.LeftArmImpaired) && !RealWeapon(c, Side.Left) ? Side.Left : HasTag(s, Tag.RightArmImpaired) && !RealWeapon(c, Side.Right) ? Side.Right : (Side?)null;
            if (hurt == null) { if (s.cradling) EndCradle(s); return; }
            if (s.cradling) { if (now > s.nextCradle) EndCradle(s); return; }
            if (now < s.nextCradle || c.ragdoll.ik == null) return;
            var chest = ChestPart(c);
            if (chest == null) return;
            if (s.cradleAnchor == null) s.cradleAnchor = new GameObject("RageEngine_Cradle").transform;
            s.cradleAnchor.SetParent(chest.transform, false);
            s.cradleAnchor.position = chest.transform.position - Vector3.up * 0.12f + c.transform.forward * 0.18f;
            s.cradleAnchor.rotation = Quaternion.LookRotation(c.transform.right * (hurt == Side.Left ? 1f : -1f), Vector3.up);
            c.ragdoll.ik.SetHandAnchor(hurt.Value, s.cradleAnchor);
            c.ragdoll.ik.SetHandState(hurt.Value, true, true);
            s.cradling = true; s.cradleSide = hurt.Value;
            s.nextCradle = now + Random.Range(3f, 6f);
        }

        void EndCradle(NpcState s)
        {
            var ik = s.creature != null && s.creature.ragdoll != null ? s.creature.ragdoll.ik : null;
            if (ik != null) { ik.SetHandAnchor(s.cradleSide, null); ik.SetHandState(s.cradleSide, false, false); }
            s.cradling = false; s.nextCradle = Time.time + Random.Range(4f, 9f);
        }
    }
}

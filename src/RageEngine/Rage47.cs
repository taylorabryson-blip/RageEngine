// Rage Engine 0.47 - spawn fix: NPCs spawned on top of each other (the book's spawn point) push apart hard as the
// physics separates them. That used to read as real hits - body slams, and the other NPC's weapon "stabbing" him -
// so freshly spawned NPCs bled, screamed and collapsed like they'd lost their bones. Now, for the first seconds after
// an NPC appears, nothing but you can hurt him or knock him about, and NPC bodies bumping each other never count.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const float SpawnGrace47 = 3.5f;

        /// true = ignore this hit/contact on s.
        bool Ignore47(NpcState s, CollisionInstance hit)
        {
            var col = hit != null ? hit.sourceCollider : null;
            var c = s.creature;
            Creature from = null; Item item = null;
            if (col != null)
            {
                item = col.GetComponentInParent<Item>();
                if (item != null)
                {
                    var h = item.mainHandler;
                    from = h != null && h.ragdoll != null ? h.ragdoll.creature : null;
                    if (from == null && item.handlers != null) foreach (var hh in item.handlers) if (hh != null && hh.creature != null) { from = hh.creature; break; }
                }
                else from = col.GetComponentInParent<Creature>();
            }
            if (from == c) return true;                                                           // his own weapon or body
            bool byPlayer = from != null && from.isPlayer || item != null && item.lastHandler != null && item.lastHandler.creature != null && item.lastHandler.creature.isPlayer;
            if (byPlayer) return false;
            float now = Time.time;
            bool fresh = now - s.trackedAt < SpawnGrace47;
            if (from != null && TryGet(from, out var o) && now - o.trackedAt < SpawnGrace47) fresh = true;
            if (fresh) return true;                                                                // spawn overlap / separation
            if (from != null && item == null && from.ragdoll != null && from.ragdoll.state == Ragdoll.State.Standing) return true;   // two NPCs bumping
            return false;
        }
    }
}

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ================================================================== settings
        public static ModOptionString[] modeValues = { new ModOptionString("Rage Engine + dynamic organs", null, "all"), new ModOptionString("Rage Engine (no organs)", null, "noorgans"), new ModOptionString("Dynamic organs only", null, "organs") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(1)]
        [ModOption("Mode", "Everything; Rage Engine without the organs falling out; or ONLY the dynamic organs (deep cuts spill guts and organs) with the rest of Rage Engine switched off.", valueSourceName = nameof(modeValues), defaultValueIndex = 0)]
        public static string rageMode = "all";
        internal static bool OrgansOn => rageMode != "noorgans";
        internal static bool OrgansOnly => rageMode == "organs";

        public static ModOptionString[] loadoutValues = { new ModOptionString("Game default", null, "0"), new ModOptionString("Mixed (some switch it up)", null, "1"), new ModOptionString("Anything in the game", null, "2") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(10)]
        [ModOption("NPC weapons", "What NPCs carry - in waves, dungeons, anywhere. Mixed: about 4 in 10 swap their faction's weapon for any melee weapon in the game (mods included). Anything: every NPC does.", valueSourceName = nameof(loadoutValues), defaultValueIndex = 1)]
        public static string npcWeapons = "1";

        // ================================================================== dynamic organs (also the whole of "organs only")
        static readonly Dictionary<Creature, float> gutted47 = new Dictionary<Creature, float>();   // creatures are pooled: forget after 3 min
        static readonly Dictionary<Creature, float> chestOpen47 = new Dictionary<Creature, float>();

        /// Organs-only mode: hits go straight here, nothing else in Rage Engine runs.
        void OrganOnlyHit47(CollisionInstance hit)
        {
            var part = hit.damageStruct.hitRagdollPart;
            if (part == null || part.ragdoll == null) return;
            var c = part.ragdoll.creature;
            if (c == null || c.isPlayer) return;
            var col = hit.sourceCollider; var it = col != null ? col.GetComponentInParent<Item>() : null;
            if (it != null && it.mainHandler != null && it.mainHandler.ragdoll != null && it.mainHandler.ragdoll.creature == c) return;   // his own weapon
            Organs47(null, c, part, hit, HitSeverity(hit));
        }

        /// Deep cuts and huge blows open the body: a low belly cut spills the guts (they hang from the wound), a deep chest
        /// cut drops lung / heart pieces, a crushing blast throws organs out. s may be null (organs-only mode).
        void Organs47(NpcState s, Creature c, RagdollPart part, CollisionInstance hit, float severity)
        {
            if (!OrgansOn || !Has(part.type, RagdollPart.Type.Torso)) return;
            var dt = hit.damageStruct.damageType;
            bool cut = dt == DamageType.Slash || dt == DamageType.Pierce && severity >= 1.2f;
            bool blast = dt == DamageType.Blunt && severity >= 3f;
            if (!cut && !blast) return;
            if (s != null && projectileHit34 && !blast) return;
            var chest = ChestPart(c) ?? part;
            Vector3 p = hit.contactPoint;
            bool low = Vector3.Dot(p - chest.transform.position, c.transform.up) <= 0.02f;
            Vector3 outDir = Vector3.ProjectOnPlane(p - chest.transform.position, c.transform.up);
            if (outDir.sqrMagnitude < 1e-4f) outDir = c.transform.forward; outDir.Normalize();
            if (cut && low && severity >= 0.9f && !(s != null ? s.gutsOut44 : gutted47.TryGetValue(c, out float gt) && Time.time - gt < 180f))
            {
                if (Random.value > 0.35f + 0.25f * (severity - 0.9f)) return;
                gutted47[c] = Time.time;
                if (s != null) s.gutsOut44 = true;
                SpillGuts47(s, c, part, p, severity);
                return;
            }
            if (cut && !low && severity >= 1.1f)
            {
                chestOpen47.TryGetValue(c, out float last);
                if (Time.time - last < 6f || Random.value > 0.3f + 0.2f * (severity - 1.1f)) return;
                chestOpen47[c] = Time.time;
                Stamp(c, p, 0.07f, "rib_wound", 1f);
                string o = Random.value < 0.6f ? "RE_Organ_Lung" : "RE_Organ_Heart";
                SpawnGoreModels(p, outDir + Vector3.down * 0.4f, o, 1, organMat44(o), 0.45f, c);
                Sfx.Play("NRE_GoreTear", part.transform, 1f); Sfx.Play("NRE_Squelch", part.transform, 0.8f, 0.9f);
                if (s != null) { s.bleedRate += 0.4f * bleedingRate; s.pain = 1f; Scream(s, true); }
                Log($"{c.name}: chest cut open - {o.Substring(9).ToLower()} out");
                return;
            }
            if (blast)
            {
                chestOpen47.TryGetValue(c, out float last);
                if (Time.time - last < 6f) return;
                chestOpen47[c] = Time.time;
                string[] all = { "RE_Organ_Gut", "RE_Organ_Liver", "RE_Organ_Kidney", "RE_Organ_Lung", "RE_Organ_Heart" };
                int n = perfMode == "p" ? 2 : 3;
                for (int i = 0; i < n; i++) { string o = all[Random.Range(0, all.Length)]; SpawnGoreModels(p, outDir + Random.insideUnitSphere * 0.5f, o, 1, organMat44(o), 0.9f, c); }
                Stamp(c, p, 0.09f, "torn_flesh", 1f);
                Sfx.Play("NRE_GoreTear", part.transform, 1f);
                Log($"{c.name}: blown open");
            }
        }

        void SpillGuts47(NpcState s, Creature c, RagdollPart part, Vector3 p, float severity)
        {
            int n = perfMode == "p" ? 6 : 10;
            while (guts44.Count > 0 && (guts44.Count >= 3 || guts44[0] == null)) { if (guts44[0] != null) Object.Destroy(guts44[0]); guts44.RemoveAt(0); }
            var root = new GameObject("RageEngine_Guts");
            var mesh = BoneModels.Get("RE_Organ_Gut"); var mat = organMat44("RE_Organ_Gut");
            var segs = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                var g = new GameObject("Seg"); g.transform.SetParent(root.transform, false);
                if (mesh != null) { g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                g.transform.localScale = Vector3.one * Random.Range(0.85f, 1.05f);
                segs[i] = g.transform;
            }
            var ch = root.AddComponent<GutChain44>();
            ch.anchor = part.transform; ch.local = part.transform.InverseTransformPoint(p); ch.segs = segs; ch.seg = 0.075f; ch.tearAt = Time.time + Random.Range(20f, 40f);
            guts44.Add(root); Object.Destroy(root, 90f);
            Stamp(c, p, 0.08f, "torn_flesh", 1f);
            Sfx.Play("NRE_GoreTear", part.transform, 1f); Sfx.Play("NRE_Squelch", part.transform, 0.9f);
            for (int i = 0; i < 3; i++) Guard("blood", () => RageEngineScript.Blood(part.transform));
            if (s != null)
            {
                s.bleedRate += 0.6f * bleedingRate; s.pain = 1f;
                StartClutch(s, part, p, null, false, true); if (s.clutching) s.clutchUntil = Time.time + 5f;
                Scream(s, true);
            }
            if (severity >= 1.5f) { string o = Random.value < 0.5f ? "RE_Organ_Liver" : "RE_Organ_Kidney"; SpawnGoreModels(p, c.transform.forward + Vector3.down * 0.3f, o, 1, organMat44(o), 0.4f, c); }
            Log($"{c.name}: GUTTED - his insides come out");
        }

        // ================================================================== NPC weapons (waves, dungeons, anywhere)
        static List<ItemData> melee47, oneHand47;
        static void BuildWeaponPool47()
        {
            melee47 = new List<ItemData>(); oneHand47 = new List<ItemData>();
            var list = Catalog.GetDataList(Category.Item);
            if (list == null) return;
            foreach (var cd in list)
            {
                if (!(cd is ItemData d) || d.type != ItemData.Type.Weapon || d.id == null || d.id.StartsWith("RE_") || d.moduleAI == null) continue;
                if (d.moduleAI.primaryClass != ItemModuleAI.WeaponClass.Melee) continue;
                string id = d.id.ToLowerInvariant();
                if (id.Contains("training") || id.Contains("test") || id.Contains("throw") || id.Contains("broken") || id.Contains("debug")) continue;
                melee47.Add(d);
                if (d.moduleAI.weaponHandling != ItemModuleAI.WeaponHandling.TwoHanded) oneHand47.Add(d);
            }
            Debug.Log($"[RageEngine] NPC weapons: {melee47.Count} melee weapons in the game ({oneHand47.Count} one-handed)");
        }

        void Loadout47(NpcState s, float now)
        {
            if (s.loadoutDone47 || npcWeapons == "0" || now - s.trackedAt < 1.2f) return;
            s.loadoutDone47 = true;
            var c = s.creature;
            if (c.isKilled || s.ally || c.ragdoll.state != Ragdoll.State.Standing) return;
            if (npcWeapons == "1" && Random.value > 0.4f) return;
            if (melee47 == null) BuildWeaponPool47();
            if (melee47 == null || melee47.Count == 0) return;
            var r = Hand(c, Side.Right); var l = Hand(c, Side.Left);
            var held = r != null && r.grabbedHandle != null ? r.grabbedHandle.item : null;
            if (held == null || held.data == null || held.data.type != ItemData.Type.Weapon) return;              // unarmed stays unarmed
            var pick = melee47[Random.Range(0, melee47.Count)];
            if (pick == held.data) return;
            bool twoHanded = pick.moduleAI != null && pick.moduleAI.weaponHandling == ItemModuleAI.WeaponHandling.TwoHanded;
            var offItem = l != null && l.grabbedHandle != null ? l.grabbedHandle.item : null;
            Swap47(s, r, Side.Right, held, pick);
            if (offItem != null && offItem.data != null && offItem.data.type == ItemData.Type.Weapon)
            {
                if (twoHanded) Guard("off.drop", () => { l.UnGrab(false); offItem.Despawn(); });
                else if (Random.value < 0.5f && oneHand47.Count > 0) Swap47(s, l, Side.Left, offItem, oneHand47[Random.Range(0, oneHand47.Count)]);
            }
        }

        void Swap47(NpcState s, RagdollHand hand, Side side, Item old, ItemData pick)
        {
            var c = s.creature;
            pick.SpawnAsync(item =>
            {
                if (item == null) return;
                if (s.creature != c || c.isKilled || hand == null) { Guard("despawn", () => item.Despawn()); return; }
                var handle = side == Side.Right && item.mainHandleRight != null ? item.mainHandleRight
                           : side == Side.Left && item.mainHandleLeft != null ? item.mainHandleLeft
                           : item.handles != null && item.handles.Count > 0 ? item.handles[0] : null;
                if (handle == null) { Guard("despawn", () => item.Despawn()); return; }
                Guard("swap", () =>
                {
                    if (hand.grabbedHandle != null) hand.UnGrab(false);
                    if (old != null) old.Despawn();
                    s.lastGrabbed = item; s.lastGrabTime = Time.time;
                    hand.Grab(handle, true);
                });
                Log($"{WhoIs(s)}: carries a {item.data?.id}");
            }, hand.transform.position, hand.transform.rotation, null, false);
        }

        // ================================================================== smarter NPCs
        void Smart47(NpcState s, float now)
        {
            var c = s.creature;
            if (c.isKilled || s.knockedOut || s.surrendered || s.ally || c.ragdoll.state != Ragdoll.State.Standing) return;
            // ---- skilled fighters step back out of a big incoming swing
            if (now >= s.nextDodge47 && s.prof43 >= 0.5f && IncomingSwing43(c))
            {
                s.nextDodge47 = now + Random.Range(1.5f, 3f);
                if (Random.value < (s.prof43 - 0.4f) * 0.6f * (1f - s.wear44 * 0.8f))
                {
                    var pc = Player.local != null ? Player.local.creature : null;
                    if (pc != null) { ShoveBodyQuiet(s, Vector3.ProjectOnPlane(c.transform.position - pc.transform.position, Vector3.up).normalized, 0.9f); Log($"{WhoIs(s)}: steps back out of your swing"); }
                }
            }
            // ---- disarmed: picks a weapon up off the floor if one's within reach
            if (now >= s.nextPickup47 && !HasRealWeapon42(s))
            {
                s.nextPickup47 = now + 1.5f;
                Side side = HandUsable(s, Side.Right) ? Side.Right : Side.Left;
                var hand = Hand(c, side);
                if (hand != null && hand.grabbedHandle == null && HandUsable(s, side) && Item.allActive != null)
                {
                    Item best = null; float bd = 1.9f * 1.9f;
                    foreach (var it in Item.allActive)
                    {
                        if (it == null || it.data == null || it.data.type != ItemData.Type.Weapon || it.IsHanded() || it.holder != null || it.isTelekinesisGrabbed || it.physicBody == null || it.data.id == null || it.data.id.StartsWith("RE_")) continue;
                        float d2 = (it.transform.position - c.transform.position).sqrMagnitude;
                        if (d2 < bd) { bd = d2; best = it; }
                    }
                    if (best != null) { s.magnetItem43 = best; Log($"{WhoIs(s)}: goes for the {best.data.id} on the floor"); }
                }
            }
            if (s.magnetItem43 != null && !HasFx(s, Comp.Magnet)) Guard("pickup", () => PickupStep47(s));
            // ---- tells his friends where you are
            if (now >= s.nextShare47 && now - s.seenAt < 1f)
            {
                s.nextShare47 = now + 2f;
                for (int i = 0; i < MaxTracked; i++)
                {
                    var o = pool[i];
                    if (o == s || !o.active || o.creature == null || o.creature.isKilled || o.creature.factionId != c.factionId || now - o.seenAt < 2f) continue;
                    if ((o.creature.transform.position - c.transform.position).sqrMagnitude > 12f * 12f) continue;
                    o.seenAt = now - 0.5f; o.seenPos = s.seenPos; o.lookAt = s.seenPos;
                }
            }
        }

        /// The weapon comes up into his hand like he reached down for it (slow, short range - not Magnet).
        void PickupStep47(NpcState s)
        {
            var it = s.magnetItem43; var c = s.creature;
            if (it == null || it.IsHanded() || it.holder != null || it.isTelekinesisGrabbed || HasRealWeapon42(s)) { s.magnetItem43 = null; return; }
            Side side = HandUsable(s, Side.Right) ? Side.Right : Side.Left;
            var hand = Hand(c, side);
            if (hand == null || hand.grabbedHandle != null) { s.magnetItem43 = null; return; }
            Vector3 to = hand.transform.position - it.transform.position;
            if (to.magnitude > 2.4f) { s.magnetItem43 = null; return; }
            if (to.magnitude < 0.3f)
            {
                var handle = side == Side.Right && it.mainHandleRight != null ? it.mainHandleRight : side == Side.Left && it.mainHandleLeft != null ? it.mainHandleLeft : it.handles != null && it.handles.Count > 0 ? it.handles[0] : null;
                if (handle != null) { s.lastGrabbed = it; s.lastGrabTime = Time.time; hand.Grab(handle, true); }
                s.magnetItem43 = null; return;
            }
            it.physicBody.velocity = to.normalized * 2.6f + Vector3.up * 0.3f;
            var chest = ChestPart(c);
            if (chest != null && chest.physicBody != null && to.y < -0.4f) chest.physicBody.AddForce(Vector3.down * 0.25f, ForceMode.VelocityChange);   // bends for it
        }
    }
}

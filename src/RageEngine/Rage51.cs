// Rage Engine 0.51
//   NPC SPAWNER      an item (book: Utilities -> "NPC Spawner"). Point it and pull the trigger: an NPC appears where you
//                    point. Alt-use (the other button) cycles: Enemy / Unarmed brawler / Super soldier / Ally.
//   HANGING JAWS     jaw hits add up; enough and the jaw breaks, more and it tears loose and hangs open, swinging.
//   KNIFE LINES      an NPC pulling a knife says something (voice-pack threat / taunt lines).
//   NPC LIFE         RDR2-style moments:
//                      personal space - bump into a calm NPC and he stumbles, turns and shoves you off
//                      aim at someone - point a weapon at a calm or beaten NPC and he puts his hands up and backs away
//                        (cocky ones square up instead)
//                      bodies         - NPCs who spot a fresh corpse stop and stare; friends of the dead get angry
//                      morale         - watching you do something brutal (head popped, spine ripped, punched through,
//                                       burned to ash) or watching friends die breaks the weak: they run or give up
//                      licking wounds - in a lull a hurt NPC looks down at his wound and holds it
//                      eye contact    - look at a calm NPC up close and he looks back and nods
//   SWITCHES         most systems can be turned off on their own (tabs: Rage / Gore / NPCs / You), so Rage Engine can be a
//                    light quality-of-life add-on next to other mods.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ThunderRoad;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ================================================================== switches (modular)
        [ModOptionCategory("NPCs", 2), ModOptionOrder(40)]
        [ModOption("NPC life (RDR2-style)", "Personal space, hands up when you aim at them, reacting to bodies, morale breaking, licking wounds, eye contact.", defaultValueIndex = 1)]
        public static bool npcLife51 = true;

        [ModOptionCategory("NPCs", 2), ModOptionOrder(41)]
        [ModOption("Keep NPCs standing", "Healthy NPCs stay on their feet (they only drop when dying or very low). Off = the game decides.", defaultValueIndex = 1)]
        public static bool keepStanding51 = true;

        [ModOptionCategory("NPCs", 2), ModOptionOrder(42)]
        [ModOption("Smart NPCs", "Dodging, picking weapons up off the floor, telling each other where you are.", defaultValueIndex = 1)]
        public static bool smart51 = true;

        [ModOptionCategory("NPCs", 2), ModOptionOrder(43)]
        [ModOption("Knife pulls", "Unarmed NPCs getting beaten pull a knife (and say something).", defaultValueIndex = 1)]
        public static bool knives51 = true;

        [ModOptionCategory("You", 3), ModOptionOrder(20)]
        [ModOption("Chokes", "Grab the throat to choke (two hands = faster).", defaultValueIndex = 1)]
        public static bool chokes51 = true;

        [ModOptionCategory("You", 3), ModOptionOrder(21)]
        [ModOption("Throat punches", "A solid hit to the throat makes him gag and clutch it.", defaultValueIndex = 1)]
        public static bool throatPunch51 = true;

        [ModOptionCategory("You", 3), ModOptionOrder(22)]
        [ModOption("Gravity slams", "Bodies thrown hard break bones and splatter on landing.", defaultValueIndex = 1)]
        public static bool slams51 = true;

        [ModOptionCategory("Gore", 1), ModOptionOrder(30)]
        [ModOption("Hanging jaws", "Enough damage to the jaw breaks it; more and it tears loose and hangs.", defaultValueIndex = 1)]
        public static bool hangJaws51 = true;

        [ModOptionCategory("Rage", 0), ModOptionOrder(50)]
        [ModOption("Map rescue", "On Rage maps, NPCs that can't reach you get moved to your floor.", defaultValueIndex = 1)]
        public static bool mapRescue51 = true;

        // ================================================================== voice lines by pattern (voice-pack aware)
        static readonly Dictionary<string, Regex> rx51 = new Dictionary<string, Regex>();
        static readonly Dictionary<(VoiceData, string), string[]> lines51 = new Dictionary<(VoiceData, string), string[]>();
        /// Plays a line from this NPC's voice pack whose id matches the pattern; else the fallback dialog id.
        internal static void SayLike51(NpcState s, string pattern, string fallback)
        {
            Guard("say51", () =>
            {
                var speak = s.creature.brain.instance?.GetModule<BrainModuleSpeak>(false);
                if (speak == null) return;
                var vd = speak.voiceData;
                if (vd != null && vd.dialogs != null)
                {
                    if (!rx51.TryGetValue(pattern, out var rx)) rx51[pattern] = rx = new Regex(pattern, RegexOptions.IgnoreCase);
                    if (!lines51.TryGetValue((vd, pattern), out var ids))
                    {
                        var l = new List<string>();
                        foreach (var d in vd.dialogs) if (d != null && !string.IsNullOrEmpty(d.id) && rx.IsMatch(d.id)) l.Add(d.id);
                        lines51[(vd, pattern)] = ids = l.ToArray();
                    }
                    if (ids.Length > 0) { speak.Play(ids[Random.Range(0, ids.Length)], true, false); return; }
                }
                if (!string.IsNullOrEmpty(fallback)) speak.Play(fallback, true, false);
            });
        }
        internal static void KnifeLine51(NpcState s) => SayLike51(s, "knife|blade|cut|stab|threat|insult|taunt|gonna|kill", "taunt");

        // ================================================================== jaw: damage adds up, breaks, then hangs
        void Jaw51(NpcState s, RagdollPart part, CollisionInstance hit, float severity)
        {
            var c = s.creature;
            if (!hangJaws51 || c.jaw == null || !Has(part.type, RagdollPart.Type.Head)) return;
            if (hit.damageStruct.damageType != DamageType.Blunt || severity < 0.25f) return;
            Vector3 p = hit.contactPoint;
            bool low = p.y < part.transform.position.y - 0.01f || (p - c.jaw.position).sqrMagnitude < 0.08f * 0.08f;
            if (!low) return;
            s.jawDmg51 += severity / Mathf.Max(0.7f, Tough33(s));
            if (!s.jawBroken && s.jawDmg51 >= 2.4f)
            {
                s.jawBroken = true; s.jawSkew = Random.Range(-12f, 12f); s.jawRest = c.jaw.localRotation;
                Guard("jaw", () => { var speak = c.brain.instance.GetModule<BrainModuleSpeak>(false); if (speak != null) speak.moveMouth = false; });
                Sfx.Play("NRE_BoneSnap_Jaw", c.jaw, 1f); Sfx.Play("NRE_BoneCrunch", c.jaw, 0.7f);
                s.pain = Mathf.Min(1f, s.pain + 0.5f);
                if (!ExternalGore) RageEngineScript.Blood(c.jaw);
                Log($"{WhoIs(s)}: JAW BROKEN (damage added up: {s.jawDmg51:0.0})");
            }
            if (s.jawBroken && !s.jawTorn && s.jawDmg51 >= 3.8f)
            {
                s.jawTorn = true;
                s.jawSkew = Mathf.Sign(s.jawSkew == 0f ? 1f : s.jawSkew) * Random.Range(8f, 14f);
                s.bleedRate += 0.4f * bleedingRate;
                Sfx.Play("NRE_GoreTear", c.jaw, 0.9f); Sfx.Play("NRE_BoneCrunch", c.jaw, 1f, 0.9f);
                if (!ExternalGore) { RageEngineScript.Blood(c.jaw); RageEngineScript.Blood(c.jaw); }
                Scream(s, true);
                Log($"{WhoIs(s)}: JAW HANGING LOOSE");
            }
        }

        // ================================================================== NPC life
        static float brutalAt51 = -100f; static string brutalWhy51; static Vector3 brutalPos51;
        static readonly Dictionary<Creature, float> deaths51 = new Dictionary<Creature, float>();
        internal static void Brutal51(string why)
        {
            brutalAt51 = Time.time; brutalWhy51 = why;
            var pc = Player.local != null ? Player.local.creature : null;
            brutalPos51 = pc != null ? pc.transform.position : Vector3.zero;
        }

        void Life51(NpcState s, float now)
        {
            if (!npcLife51) return;
            var c = s.creature;
            if (c.isKilled || s.knockedOut || s.passedOut || s.impaled49 || s.dying) return;
            var pc = Player.local != null ? Player.local.creature : null; if (pc == null) return;
            var head = c.ragdoll.headPart; if (head == null) return;
            bool standing = c.ragdoll.state == Ragdoll.State.Standing;
            Vector3 me = c.transform.position, you = pc.transform.position;
            float d = Vector3.Distance(me, you);
            Vector3 toYou = Vector3.ProjectOnPlane(you - me, Vector3.up).normalized;
            bool calm = !s.inCombat || s.ally || s.surrendered;

            // ---- personal space: you walk into him
            if (standing && d < 0.6f && now >= s.nextBump51 && !s.surrendered)
            {
                Vector3 pv = Vector3.zero; try { pv = pc.ragdoll.rootPart.physicBody.velocity; } catch { }
                if (Vector3.Dot(pv, -toYou) > 0.6f)
                {
                    s.nextBump51 = now + 2.5f; s.bumps51++;
                    ShoveBodyQuiet(s, -toYou, 0.45f);
                    s.lookAt = head.transform.position + toYou; s.lookUntil = now + 2f;
                    if (calm)
                    {
                        Guard("face", () => c.brain.instance?.GetModule<BrainModuleExpression>(false)?.SetFaceExpression(FaceAnimator.Expression.Angry, 0, null));
                        SayLike51(s, "bump|watch|hey|move|insult|annoy", s.bumps51 >= 2 ? "taunt" : null);
                        if (s.bumps51 >= 2 && !s.ally) { Gesture43(s, 5, 1.2f); AddTag(s, Tag.Anger, 6f, 0.6f); }
                        Log($"{WhoIs(s)}: bumped ({s.bumps51})");
                    }
                }
            }
            if (now >= s.nextLife51) s.nextLife51 = now + 0.25f; else return;

            // ---- you aim a weapon at him
            if (standing && d < 4.5f && !s.surrendered)
            {
                bool aimed = false;
                foreach (var h in new[] { pc.handLeft, pc.handRight })
                {
                    var it = h != null && h.grabbedHandle != null ? h.grabbedHandle.item : null;
                    if (it == null || it.data == null) continue;
                    Vector3 fwd = it.transform.forward, up = it.transform.up;
                    Vector3 to = (head.transform.position - it.transform.position).normalized;
                    if (Mathf.Max(Vector3.Dot(fwd, to), Vector3.Dot(up, to)) > 0.93f) { aimed = true; break; }
                }
                if (aimed) s.aimedFor51 += 0.25f; else s.aimedFor51 = Mathf.Max(0f, s.aimedFor51 - 0.5f);
                bool beaten = HpFrac(c) < 0.4f || !HasRealWeapon42(s);
                if (s.aimedFor51 >= 0.75f && (calm || beaten) && now >= s.nextAim51)
                {
                    s.nextAim51 = now + 6f;
                    var t = s.personality.type;
                    bool cocky = (t == PersonalityType.Aggressive || t == PersonalityType.Confident) && HpFrac(c) > 0.5f;
                    s.lookAt = pc.ragdoll.headPart != null ? pc.ragdoll.headPart.transform.position : you; s.lookUntil = now + 3f;
                    if (cocky && !s.ally) { Gesture43(s, 4, 1.8f); Speak(s, "taunt"); AddTag(s, Tag.Anger, 6f, 0.6f); Log($"{WhoIs(s)}: squares up to your weapon"); }
                    else
                    {
                        Gesture43(s, 1, 3f); SetMelee(s, false, now + 3f);
                        ShoveBodyQuiet(s, -toYou, 0.35f);
                        FearVoice49(s, true);
                        Log($"{WhoIs(s)}: hands up - you're aiming at him");
                    }
                }
            }

            // ---- brutality / friends dying: morale
            if (brutalAt51 > s.brutalSeen51 && now - brutalAt51 < 2f)
            {
                s.brutalSeen51 = brutalAt51;
                if (Vector3.Distance(me, brutalPos51) < 14f && Vector3.Dot(c.transform.forward, (brutalPos51 - me).normalized) > -0.2f)
                    Morale51(s, 0.45f, brutalWhy51);
            }
            if (now >= s.nextBody51)
            {
                s.nextBody51 = now + 1.5f;
                foreach (var kv in deaths51)
                {
                    var dc = kv.Key; if (dc == null || dc == c || now - kv.Value > 90f) continue;
                    if (s.bodiesSeen51 != null && s.bodiesSeen51.Contains(dc)) continue;
                    var dp = dc.ragdoll != null && dc.ragdoll.rootPart != null ? dc.ragdoll.rootPart.transform.position : dc.transform.position;
                    Vector3 v = dp - head.transform.position;
                    if (v.sqrMagnitude > 9f * 9f || Vector3.Dot(c.transform.forward, v.normalized) < 0.25f) continue;
                    if (Physics.Linecast(head.transform.position, dp + Vector3.up * 0.3f, out var blk, ~0, QueryTriggerInteraction.Ignore) && blk.collider.GetComponentInParent<Creature>() == null) continue;
                    (s.bodiesSeen51 ??= new HashSet<Creature>()).Add(dc);
                    s.lookAt = dp; s.lookUntil = now + 2.5f;
                    bool friend = TryGet(dc, out var ds) && ds.identity != null && s.identity != null && s.identity.friends != null && s.identity.friends.Contains(ds.identity.id);
                    if (friend)
                    {
                        AddTag(s, Tag.Anger, 20f, 0.9f); SayLike51(s, "friend|brother|avenge|bastard|kill|insult", "taunt");
                        Log($"{WhoIs(s)}: sees his friend's body - furious");
                    }
                    else if (now - kv.Value < 20f) { Morale51(s, 0.15f, "a body"); Speak(s, "alert"); }
                    break;
                }
            }

            // ---- lull: licks his wounds
            if (standing && s.wounds51 > 0 && d > 4f && now - s.lastHitTime > 5f && now >= s.nextLick51 && !s.clutching)
            {
                s.nextLick51 = now + Random.Range(9f, 16f);
                var wp = s.lastWoundPart51;
                if (wp != null)
                {
                    s.lookAt = wp.transform.position; s.lookUntil = now + 2.5f;
                    StartClutch(s, wp, wp.transform.position, null, false, false); if (s.clutching) s.clutchUntil = now + 2.5f;
                    if (Random.value < 0.5f) Speak(s, "pain");
                }
            }

            // ---- eye contact
            if (calm && standing && d < 3.2f && now >= s.nextNod51 && Player.local.head != null)
            {
                var ph = Player.local.head.transform;
                if (Vector3.Dot(ph.forward, (head.transform.position - ph.position).normalized) > 0.95f) s.lookedAt51 += 0.25f; else s.lookedAt51 = 0f;
                if (s.lookedAt51 >= 1.25f)
                {
                    s.lookedAt51 = 0f; s.nextNod51 = now + 20f;
                    s.lookAt = ph.position; s.lookUntil = now + 3f;
                    if (head.physicBody != null) head.physicBody.velocity += -c.transform.up * 0.6f + c.transform.forward * 0.2f;   // a nod
                    SayLike51(s, "greet|hello|hey|howdy|nod", null);
                }
            }
        }

        void Morale51(NpcState s, float hit, string why)
        {
            var t = s.personality.type;
            float k = t == PersonalityType.Nervous ? 1.5f : t == PersonalityType.Cautious ? 1.2f : t == PersonalityType.Aggressive ? 0.5f : t == PersonalityType.Confident ? 0.7f : 1f;
            if (Tank(s) || s.will44) k *= 0.5f;
            s.morale51 -= hit * k * (1.3f - HpFrac(s.creature) * 0.6f);
            Log($"{WhoIs(s)}: morale {s.morale51:0.00} ({why})");
            if (s.morale51 > 0f || s.ally) return;
            s.morale51 = 0.6f;
            if (Random.value < 0.5f || !HasRealWeapon42(s)) { Guard("surrender", () => EnterSurrender(s)); Log($"{WhoIs(s)}: GIVES UP ({why})"); }
            else { Panic(s, Random.Range(5f, 10f)); FearVoice49(s, true); Log($"{WhoIs(s)}: RUNS ({why})"); }
        }

        internal static void NoteDeath51(Creature c)
        {
            if (c == null || c.isPlayer) return;
            deaths51[c] = Time.time;
            if (deaths51.Count > 40) { var old = new List<Creature>(); foreach (var kv in deaths51) if (kv.Key == null || Time.time - kv.Value > 120f) old.Add(kv.Key); foreach (var o in old) deaths51.Remove(o); }
        }

        // ================================================================== NPC spawner item
        static readonly string[] spawnModes51 = { "Enemy", "Unarmed brawler", "Super soldier", "Ally" };
        static int spawnMode51;
        readonly bool[] useWas51 = new bool[2], altWas51 = new bool[2];
        static readonly HashSet<Item> dressed51 = new HashSet<Item>();
        static float nextSpawn51;

        void TickSpawner51()
        {
            var pc = Player.local != null ? Player.local.creature : null; if (pc == null) return;
            for (int k = 0; k < 2; k++)
            {
                var h = k == 0 ? pc.handLeft : pc.handRight;
                var it = h != null && h.grabbedHandle != null ? h.grabbedHandle.item : null;
                bool use = false, alt = false;
                if (it != null && it.data != null && it.data.id == "RE_NPCSpawner")
                {
                    if (!dressed51.Contains(it)) Guard("spawner.dress", () => DressSpawner51(it));
                    try { var ch = h.playerHand.controlHand; use = ch.usePressed; alt = ch.alternateUsePressed; } catch { }
                    if (alt && !altWas51[k])
                    {
                        spawnMode51 = (spawnMode51 + 1) % spawnModes51.Length;
                        Notify($"NPC Spawner: {spawnModes51[spawnMode51]}");
                        Haptics.Pulse(h.side, 0.4f, 1);
                    }
                    if (use && !useWas51[k] && Time.time >= nextSpawn51) { nextSpawn51 = Time.time + 0.6f; var it2 = it; var h2 = h; Guard("spawner.spawn", () => Spawn51(it2, h2)); }
                }
                useWas51[k] = use; altWas51[k] = alt;
            }
            if (Time.frameCount % 120 == 0) dressed51.RemoveWhere(x => x == null);
        }

        static Material spawnerBody51, spawnerGlow51, spawnerMetal51;
        static void DressSpawner51(Item it)
        {
            dressed51.Add(it);
            if (spawnerBody51 == null) { spawnerBody51 = Mats.Make(new Color(0.12f, 0.12f, 0.13f), 0.6f); spawnerGlow51 = Mats.Make(new Color(1f, 0.25f, 0.08f), 0.9f); try { spawnerGlow51.EnableKeyword("_EMISSION"); spawnerGlow51.SetColor("_EmissionColor", new Color(2.5f, 0.6f, 0.15f)); } catch { } }
            foreach (var r in it.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            var root = new GameObject("RageEngine_SpawnerModel").transform; root.SetParent(it.transform, false);
            root.localRotation = Quaternion.identity; root.localPosition = new Vector3(0f, -0.03f, 0f);
            bool modelled = false;
            foreach (var (mesh, m) in new[] { ("RE_Spawner_Body", spawnerBody51), ("RE_Spawner_Cage", spawnerMetal51 ??= Mats.Make(new Color(0.42f, 0.42f, 0.45f), 0.8f)), ("RE_Spawner_Core", spawnerGlow51) })
            {
                var mm = BoneModels.Get(mesh); if (mm == null) continue;
                var g = new GameObject(mesh); g.transform.SetParent(root, false);
                g.AddComponent<MeshFilter>().sharedMesh = mm;
                var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                modelled = true;
            }
            if (!modelled)                                                                         // models missing: simple fallback
            {
                GameObject Prim(PrimitiveType p, Vector3 pos, Vector3 scale, Material m)
                {
                    var g = GameObject.CreatePrimitive(p); Object.Destroy(g.GetComponent<Collider>());
                    g.transform.SetParent(root, false); g.transform.localPosition = pos; g.transform.localScale = scale;
                    g.GetComponent<Renderer>().sharedMaterial = m; return g;
                }
                Prim(PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(0.045f, 0.06f, 0.045f), spawnerBody51);
                Prim(PrimitiveType.Sphere, new Vector3(0f, 0.145f, 0f), new Vector3(0.05f, 0.05f, 0.05f), spawnerGlow51);
            }
            var lg = new GameObject("Glow"); lg.transform.SetParent(root, false); lg.transform.localPosition = new Vector3(0f, 0.124f, 0f);
            var L = lg.AddComponent<Light>(); L.type = LightType.Point; L.color = new Color(1f, 0.35f, 0.1f); L.range = 0.8f; L.intensity = 1.2f; L.shadows = LightShadows.None;
        }

        void Spawn51(Item it, RagdollHand h)
        {
            var pc = Player.local.creature;
            Vector3 from = it.transform.position, dir = it.transform.up;
            if (Vector3.Dot(dir, (pc.transform.position - from)) > 0f && Player.local.head != null) dir = Player.local.head.transform.forward;
            Vector3 pos;
            if (Physics.Raycast(from + dir * 0.2f, dir, out var hit, 14f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point - dir * 0.4f;
            else pos = from + Vector3.ProjectOnPlane(dir, Vector3.up).normalized * 4f;
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)) pos = g.point + Vector3.up * 0.05f;
            if ((pos - pc.transform.position).sqrMagnitude < 1.5f * 1.5f) pos = pc.transform.position + Vector3.ProjectOnPlane(dir, Vector3.up).normalized * 2f;
            var data = PickSoldier(out int tableFaction);
            if (data == null) { Notify("NPC Spawner: no NPC type found"); return; }
            int mode = spawnMode51;
            int hostile = HostileFaction();
            int faction = mode == 3 ? 2 : hostile >= 0 ? hostile : tableFaction >= 0 && tableFaction != 2 ? tableFaction : data.factionId != 2 ? data.factionId : -1;
            Vector3 look = pc.transform.position - pos; look.y = 0f;
            float rotY = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
            string id = data.id;
            data.SpawnAsync(pos, rotY, null, false, null, cr =>
            {
                if (cr == null) return;
                Guard("spawner.faction", () => { if (faction >= 0) cr.SetFaction(faction); });
                if (mode != 3) MakeHostile28(cr, faction);
                if (mode == 2) pendingSoldiers.Add(new PendingSoldier { c = cr, at = Time.time + 1.2f, comp = RandomSuperSerum() });
                if (mode == 1) disarm51.Add((cr, Time.time + 1.5f));
                Debug.Log($"[RageEngine] NPC Spawner: {id} as {spawnModes51[mode]} (faction {faction})");
            });
            Haptics.Pulse(h.side, 0.8f, 2);
            Sfx.Play("NRE_Punch", it.transform, 0.35f, 0.6f);
            Log($"NPC Spawner: {spawnModes51[mode]} at {pos}");
        }

        static readonly List<(Creature c, float at)> disarm51 = new List<(Creature, float)>();
        static void TickDisarm51()
        {
            for (int i = disarm51.Count - 1; i >= 0; i--)
            {
                var (c, at) = disarm51[i];
                if (c == null) { disarm51.RemoveAt(i); continue; }
                if (Time.time < at) continue;
                disarm51.RemoveAt(i);
                foreach (var nh in new[] { c.handLeft, c.handRight })
                {
                    var item = nh != null && nh.grabbedHandle != null ? nh.grabbedHandle.item : null;
                    if (item == null) continue;
                    var hh = nh; var ii = item;
                    Guard("disarm", () => { hh.UnGrab(false); ii.Despawn(); });
                }
            }
        }

        // ================================================================== per NPC / per frame
        void Tick51(NpcState s, float now)
        {
            Guard("life51", () => Life51(s, now));
        }

        void Frame51()
        {
            Guard("spawner51", TickSpawner51);
            if (disarm51.Count > 0) TickDisarm51();
        }
    }
}

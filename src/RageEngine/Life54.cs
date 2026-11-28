// Rage Engine 0.54 - injury ownership.
//
// The game pools NPC bodies. When an NPC dies or despawns, the same Creature object is handed out again later as
// somebody new. Rage Engine keeps a lot of per-NPC data (injuries, tags, timers, delayed effects). Before 0.54 some
// of it could survive onto the next person who got that body: a gouged eye that never came back, a jaw left hanging,
// a delayed death from a ripped-out heart killing the new NPC, an old wound you could still reach into.
//
// How 0.54 keeps every NPC's injuries his own:
//   1. Every life of every body has a number (LifeOf54). It goes up when the game despawns the body, or when the body
//      comes back with a new spawn time. Nothing is scanned: it's one dictionary lookup when somebody asks.
//   2. Everything that acts on an NPC later (a delayed kill, a queued serum, a disarm, a wound you can reach into, an
//      empty eye socket, a punch-through, a health bar) remembers the life it was made for. Once that life is over the
//      entry is dropped instead of being applied to whoever has the body now.
//   3. Every tracked NpcState slot has a generation number (gen54) that changes whenever the slot is given to a new
//      NPC or released, so anything holding on to an NpcState can tell it's still the same person.
//   4. When a life ends, every leftover tied to that body is cleaned up once, at a safe point in the frame (Flush54),
//      never from inside the game's own despawn call.
//   5. Reset now puts EVERY plain-data field of NpcState back to its starting value (Wipe54), so a field added in a
//      future version can't be forgotten. A self-test (Audit54) checks this at startup.
//   6. "Injury leak test" in the Quick action menu runs the whole cycle in game: spawn, injure, kill, despawn,
//      respawn, check the new NPC is clean, three times.
using System;
using System.Collections.Generic;
using System.Reflection;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ================================================================== life numbers
        sealed class Life54 { public int n = 1; public float spawn = -1f; public bool fresh = true; }
        static readonly Dictionary<Creature, Life54> lives54 = new Dictionary<Creature, Life54>();
        static readonly List<Creature> ended54 = new List<Creature>();          // bodies whose life ended, cleaned up at the next safe point
        static readonly List<Creature> flushing54 = new List<Creature>();
        static int genSeq54;

        /// The current life number of this body. Cheap: one lookup and a float compare.
        internal static int LifeOf54(Creature c)
        {
            if (c == null) return -1;
            if (!lives54.TryGetValue(c, out var l))
            {
                if (lives54.Count > 256) Sweep54();
                l = new Life54(); lives54[c] = l;
            }
            float st = 0f;
            try { st = c.spawnTime; } catch { }
            if (st <= 0f || st == l.spawn) return l.n;
            if (l.fresh) { l.spawn = st; l.fresh = false; return l.n; }                   // first look at this life: note when it began
            if (Mathf.Abs(st - l.spawn) < 1f) { l.spawn = st; return l.n; }               // same spawn, the time was only written late
            l.spawn = st; l.n++;                                                          // re-used without a despawn event: a new person
            if (!ended54.Contains(c)) ended54.Add(c);
            return l.n;
        }

        /// Is this body still in the life this was made for?
        internal static bool Same54(Creature c, int life) => life > 0 && c != null && LifeOf54(c) == life;

        /// Is this NpcState still the same NPC it was when gen was read?
        internal static bool Owns54(NpcState s, int gen) => s != null && s.active && s.creature != null && s.gen54 == gen;

        /// The game despawned this body: whoever was in it is gone.
        static void EndLife54(Creature c)
        {
            if (c == null) return;
            if (!lives54.TryGetValue(c, out var l)) { l = new Life54(); lives54[c] = l; }
            l.n++; l.fresh = true;
            if (!ended54.Contains(c)) ended54.Add(c);
        }

        /// Level unloads: every body is gone, start the books over.
        static void Unload54()
        {
            foreach (var l in lives54.Values) { l.n++; l.fresh = true; }                 // whatever was queued for anyone is void now
            ended54.Clear(); Sweep54();
        }

        static void Sweep54()
        {
            List<Creature> dead = null;
            foreach (var kv in lives54) if (kv.Key == null) (dead ??= new List<Creature>()).Add(kv.Key);
            if (dead != null) foreach (var d in dead) lives54.Remove(d);
        }

        // ================================================================== game events (they only take notes)
        void OnDespawn54(Creature c, EventTime t)
        {
            if (t != EventTime.OnStart || c == null) return;
            bool player = false; try { player = c.isPlayer; } catch { }
            if (player) return;
            EndLife54(c);                    // the actual clean-up waits for Flush54: we may be inside one of our own loops right now
        }

        void OnSpawn54(Creature c)
        {
            if (c == null) return;
            bool player = false; try { player = c.isPlayer; } catch { }
            if (player) return;
            LifeOf54(c);                     // notes the new spawn time (or spots a re-use the despawn event missed)
            nextScan = 0f;                   // and pick him up on the next frame instead of up to half a second later
        }

        // ================================================================== safe point, start of Update and LateUpdate
        void Flush54()
        {
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active) continue;
                var c = s.creature;
                if (c != null && LifeOf54(c) == s.life54) continue;
                // the body under this state belongs to somebody else now (or is gone): drop the old person's state
                Log($"{(c != null ? c.name : "?")}: body handed to a new NPC by the game - old injuries cleared (life {s.life54} -> {(c != null ? LifeOf54(c) : -1)})");
                Release(s);
                if (c != null && !ended54.Contains(c)) ended54.Add(c);
                nextScan = 0f;
            }
            if (ended54.Count == 0) return;
            flushing54.Clear(); flushing54.AddRange(ended54); ended54.Clear();
            for (int i = 0; i < flushing54.Count; i++) { var c = flushing54[i]; if (c != null) Guard("life.purge", () => Purge54(c)); }
            flushing54.Clear();
        }

        /// Everything tied to one body's previous life, cleaned up once.
        static void Purge54(Creature c)
        {
            // on the body itself
            Guard("purge.eyes", () => RestoreEyes49(c));
            Guard("purge.head", () =>
            {
                for (int i = crushedHeads.Count - 1; i >= 0; i--)
                {
                    var t = crushedHeads[i].t;
                    if (t == null) { crushedHeads.RemoveAt(i); continue; }
                    if (!t.IsChildOf(c.transform)) continue;
                    t.localScale = Vector3.one;                     // the tracked path restores the real scale first; this is the leftover case
                    crushedHeads.RemoveAt(i);
                }
            });
            Guard("purge.deform", () => HeadDeform.Restore(c));
            Guard("purge.skin", () => SkinPaint.Restore(c));
            Guard("purge.skincache", () => ForgetSkin(c));
            Guard("purge.eyelids", () =>
            {
                c.autoEyeClipsActive = true;
                if (c.allEyes != null) foreach (var e in c.allEyes) if (e != null) { e.closeAmount = 0f; e.SetClose(); }
            });
            // what the mod remembered about him
            gutted47.Remove(c); chestOpen47.Remove(c); takenFrom49.Remove(c); superDone50.Remove(c); deaths51.Remove(c); deadSince29.Remove(c);
            wave29.Remove(c);
            RemoveCorpse(c);
            if (corpseBlocks.TryGetValue(c, out var cb)) { if (cb.go != null) UnityEngine.Object.Destroy(cb.go); corpseBlocks.Remove(c); }
            if (bars52.TryGetValue(c, out var b)) { if (b.root != null) UnityEngine.Object.Destroy(b.root); bars52.Remove(c); }
        }

        // ================================================================== Reset that can't forget a field
        static FieldInfo[] wipeF54;
        static object[] wipeV54;
        static readonly HashSet<string> keep54 = new HashSet<string> { "gen54", "life54" };

        static bool Plain54(Type t) => t.IsValueType || t == typeof(string) || (t.IsArray && t.GetElementType().IsValueType);

        static void BuildWipe54()
        {
            var tmpl = new NpcState();
            var fs = new List<FieldInfo>(); var vs = new List<object>();
            foreach (var f in typeof(NpcState).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (keep54.Contains(f.Name) || !Plain54(f.FieldType)) continue;
                if (f.IsInitOnly && !f.FieldType.IsArray) continue;
                fs.Add(f); vs.Add(f.GetValue(tmpl));
            }
            wipeF54 = fs.ToArray(); wipeV54 = vs.ToArray();
        }

        /// Every number, flag, vector, enum, string and number-array of this state back to what a brand-new NpcState has.
        /// (Object references - meshes, anchors, lists - are owned resources: Release destroys or clears them.)
        static void Wipe54(NpcState s)
        {
            if (wipeF54 == null) BuildWipe54();
            for (int i = 0; i < wipeF54.Length; i++)
            {
                var f = wipeF54[i]; var v = wipeV54[i];
                if (v is Array tmpl)
                {
                    var cur = f.GetValue(s) as Array;
                    if (cur != null && cur.Length == tmpl.Length) Array.Copy(tmpl, cur, tmpl.Length);
                    else if (!f.IsInitOnly) f.SetValue(s, tmpl.Clone());
                }
                else if (!f.IsInitOnly) f.SetValue(s, v);
            }
        }

        // ================================================================== self-test of the reset
        /// Fills a spare state with junk, resets it, and compares it with a brand-new state reset the same way.
        /// Returns how many fields kept junk (0 = nothing can leak from one NPC to the next through a reused slot).
        internal static int Audit54(List<string> bad)
        {
            var dirty = new NpcState(); var clean = new NpcState();
            Poison54(dirty);
            var rs = UnityEngine.Random.state;
            UnityEngine.Random.InitState(54); Reset(dirty);
            UnityEngine.Random.InitState(54); Reset(clean);
            UnityEngine.Random.state = rs;
            int n = 0;
            foreach (var f in typeof(NpcState).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (keep54.Contains(f.Name) || !Plain54(f.FieldType)) continue;
                if (!Equal54(f.GetValue(dirty), f.GetValue(clean))) { n++; bad?.Add(f.Name); }
            }
            return n;
        }

        static void Poison54(NpcState s)
        {
            foreach (var f in typeof(NpcState).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (keep54.Contains(f.Name) || !Plain54(f.FieldType)) continue;
                var t = f.FieldType;
                try
                {
                    if (t.IsArray)
                    {
                        var arr = f.GetValue(s) as Array;
                        if (arr == null) { if (f.IsInitOnly) continue; arr = Array.CreateInstance(t.GetElementType(), 2); f.SetValue(s, arr); }
                        for (int i = 0; i < arr.Length; i++) { var j = Junk54(t.GetElementType(), arr.GetValue(i)); if (j != null) arr.SetValue(j, i); }
                    }
                    else if (!f.IsInitOnly) { var j = Junk54(t, f.GetValue(s)); if (j != null) f.SetValue(s, j); }
                }
                catch { }
            }
        }

        static object Junk54(Type t, object cur)
        {
            if (t == typeof(float)) return 777f;
            if (t == typeof(int)) return 7;
            if (t == typeof(bool)) return !(cur is bool b && b);
            if (t == typeof(string)) return "junk";
            if (t == typeof(Vector3)) return new Vector3(7f, 7f, 7f);
            if (t == typeof(Vector2)) return new Vector2(7f, 7f);
            if (t == typeof(Quaternion)) return new Quaternion(0.5f, 0.5f, 0.5f, 0.5f);
            if (t == typeof(Color)) return new Color(0.7f, 0.1f, 0.7f, 0.5f);
            if (t == typeof(double)) return 777d;
            if (t == typeof(long)) return 7L;
            if (t == typeof(byte)) return (byte)7;
            if (t.IsEnum) { var vals = Enum.GetValues(t); return vals.Length > 1 ? vals.GetValue(vals.Length - 1) : null; }
            return null;
        }

        static bool Equal54(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is Array x && b is Array y)
            {
                if (x.Length != y.Length) return false;
                for (int i = 0; i < x.Length; i++) if (!Equals(x.GetValue(i), y.GetValue(i))) return false;
                return true;
            }
            return a.Equals(b);
        }

        static void StartupAudit54()
        {
            var bad = new List<string>();
            int n = Audit54(bad);
            if (n == 0) Log("injury ownership: reset audit clean (every per-NPC field starts fresh)");
            else Debug.LogWarning("[RageEngine] reset audit: " + n + " field(s) keep data between NPCs: " + string.Join(", ", bad));
        }

        // ================================================================== in-game test: Quick action "Injury leak test"
        sealed class Test54
        {
            public int stage, cycle, passed, reused, cycles = 3;
            public float at, killAt;
            public Creature a, b; public CreatureData data; public Vector3 pos; public float rotY;
            public int lifeA;
            public readonly List<string> fails = new List<string>();
        }
        Test54 test54;

        void StartTest54()
        {
            if (test54 != null) { Notify("Injury leak test is already running"); return; }
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) { Notify("Injury leak test: load into a map first"); return; }
            var bad = new List<string>();
            int leaks = Audit54(bad);
            Log($"LEAK TEST: reset audit {(leaks == 0 ? "clean" : leaks + " field(s): " + string.Join(", ", bad))}");
            var data = PickSoldier(out _);
            if (data == null) { Notify("Injury leak test: no NPC type found"); return; }
            Vector3 fwd = Player.local.head != null ? Vector3.ProjectOnPlane(Player.local.head.transform.forward, Vector3.up).normalized : pc.transform.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 pos = pc.transform.position + fwd * 3f;
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)) pos = g.point + Vector3.up * 0.05f;
            test54 = new Test54 { data = data, pos = pos, rotY = Quaternion.LookRotation(-fwd).eulerAngles.y };
            if (leaks > 0) test54.fails.Add($"reset audit: {leaks} field(s) not reset ({string.Join(", ", bad)})");
            Notify("Injury leak test started - 3 rounds, about 20 seconds");
            SpawnTest54(true);
        }

        void SpawnTest54(bool first)
        {
            var t = test54; if (t == null) return;
            t.stage = first ? 1 : 4; t.at = Time.time;
            t.data.SpawnAsync(t.pos, t.rotY, null, true, null, cr =>
            {
                if (test54 != t || cr == null) return;
                Guard("test.faction", () => cr.SetFaction(2));                       // on your side: the test NPCs leave you alone
                if (first) t.a = cr; else t.b = cr;
            });
        }

        void TickTest54()
        {
            var t = test54; if (t == null) return;
            float now = Time.time;
            if (now - t.at > 20f) { t.fails.Add($"round {t.cycle + 1}: timed out at step {t.stage}"); FinishTest54(); return; }
            switch (t.stage)
            {
                case 1:   // A spawned and tracked: give him every kind of injury the mod has
                {
                    if (t.a == null || !TryGet(t.a, out var s)) return;
                    t.lifeA = s.life54;
                    InjureTest54(s);
                    t.stage = 2; t.at = now;
                    break;
                }
                case 2:   // kill him
                    if (now - t.at < 0.6f) return;
                    Guard("test.kill", () => t.a.Kill());
                    t.stage = 3; t.at = now;
                    break;
                case 3:   // despawn the body (it goes back to the game's pool), then ask for a new NPC of the same type
                    if (now - t.at < 0.8f) return;
                    var a = t.a;
                    Guard("test.despawn", () => a.Despawn(0f));
                    SpawnTest54(false);
                    break;
                case 4:   // B spawned and tracked: check he starts clean
                {
                    if (t.b == null || !TryGet(t.b, out var s)) return;
                    if (t.b == t.a) t.reused++;
                    CheckTest54(s, false);
                    t.stage = 5; t.at = now;
                    break;
                }
                case 5:   // wait past the delayed death that was queued for A, then check B again
                {
                    if (now < t.killAt + 1f) return;
                    if (t.b != null && TryGet(t.b, out var s)) CheckTest54(s, true);
                    else t.fails.Add($"round {t.cycle + 1}: new NPC was lost before the late check");
                    var b = t.b; t.b = null;
                    if (b != null) Guard("test.despawn", () => b.Despawn(0f));
                    t.cycle++;
                    if (t.cycle >= t.cycles) { FinishTest54(); return; }
                    t.a = null; t.stage = 6; t.at = now;
                    break;
                }
                case 6:   // next round
                    if (now - t.at < 0.8f) return;
                    SpawnTest54(true);
                    break;
            }
        }

        void InjureTest54(NpcState s)
        {
            var c = s.creature; var t = test54; float now = Time.time;
            int life = LifeOf54(c);
            // tags (all of them) and plain injury state
            for (int i = 0; i < s.tagUntil.Length; i++) { s.tagUntil[i] = now + 999f; s.tagIntensity[i] = 3f; }
            s.pain = 1f; s.bleedRate = 1f; s.brain50 = 5f; s.daze = 1f; s.jawDmg51 = 9f;
            // a broken leg (bone meshes, limp), a gouged eye (eye scale, empty socket, dangling eyeball), a hanging jaw
            var leg = PartOf(c, RagdollPart.Type.LeftLeg);
            if (leg != null) Guard("test.fracture", () => Fracture(s, leg, leg.transform.position, 1f, true));
            if (c.allEyes != null && c.allEyes.Count > 0 && c.allEyes[0] != null) { var e = c.allEyes[0]; Guard("test.gouge", () => Gouge(s, 0, e, null)); }
            if (c.jaw != null) { s.jawBroken = s.jawTorn = true; s.jawSkew = 12f; s.jawRest = c.jaw.localRotation; }
            // things remembered outside the NPC's own state, and things scheduled for later
            var chest = ChestPart(c);
            if (chest != null) Guard("test.wound", () => AddWound49(c, chest, chest.transform.position));
            takenFrom49[c] = 3; gutted47[c] = now; chestOpen47[c] = now; superDone50.Add(c);
            t.killAt = now + 3.5f;
            pendingKill49.Add((c, t.killAt, life));                              // a delayed death meant for A only
            disarm51.Add((c, t.killAt, life));
            Log($"LEAK TEST round {t.cycle + 1}: {WhoIs(s)} injured (tags, broken leg, gouged eye, hanging jaw, open wound, delayed death in 3.5 s)");
        }

        void CheckTest54(NpcState s, bool late)
        {
            var t = test54; var c = s.creature; float now = Time.time; int life = LifeOf54(c);
            var f = new List<string>();
            if (late) { if (c.isKilled) f.Add("the delayed death meant for the old NPC killed the new one"); }
            else
            {
                for (int i = 0; i < s.tagUntil.Length; i++) if ((Tag)i != Tag.Anger && s.tagUntil[i] > now) { f.Add("tag " + (Tag)i + " carried over"); break; }
                if (s.pain > 0.3f) f.Add("pain carried over");
                if (s.brokenParts != 0) f.Add("broken bones carried over");
                if (s.gouged != 0 || s.blindEyes != 0) f.Add("gouged eye carried over");
                if (s.jawBroken || s.jawTorn) f.Add("broken jaw carried over");
                if (s.brain50 > 0f) f.Add("brain damage carried over");
                if (c.allEyes != null) foreach (var e in c.allEyes) if (e != null && e.transform.localScale.sqrMagnitude < 1e-4f) { f.Add("an eye is still missing on the new body"); break; }
                foreach (var h in eyeHides49) if (h.c == c && h.life == life) { f.Add("empty eye socket carried over"); break; }
                foreach (var w in wounds49) if (w.c == c && w.life == life) { f.Add("open wound carried over"); break; }
                if (takenFrom49.ContainsKey(c) || gutted47.ContainsKey(c) || chestOpen47.ContainsKey(c) || superDone50.Contains(c)) f.Add("organ / serum records carried over");
                if (bars52.TryGetValue(c, out var b) && b.life != s.life54) f.Add("health bar carried over");
            }
            string round = $"round {t.cycle + 1}{(late ? " (late check)" : "")}";
            if (f.Count == 0) { if (late) t.passed++; Log($"LEAK TEST {round}: clean{(t.b == t.a ? " (same body re-used)" : " (game gave a different body)")}"); }
            else { t.fails.Add(round + ": " + string.Join(", ", f)); Log($"LEAK TEST {round}: FAILED - {string.Join(", ", f)}"); }
        }

        void FinishTest54()
        {
            var t = test54; test54 = null;
            if (t == null) return;
            if (t.a != null && t.a.isKilled) { var a = t.a; Guard("test.despawn", () => a.Despawn(0f)); }
            if (t.b != null) { var b = t.b; Guard("test.despawn", () => b.Despawn(0f)); }
            string msg = t.fails.Count == 0
                ? $"Injury leak test PASSED {t.passed}/{t.cycles} (body re-used {t.reused}x)"
                : $"Injury leak test FAILED: {string.Join(" | ", t.fails)}";
            Notify(msg);
            Log(msg);
        }
    }
}

// Rage Engine 0.54 - dungeon patrols, senses you can configure, noises NPCs go and check.
//
// PATROLS (Story Mode dungeons, NPCs the game has standing idle at a post): a short round near the post - walk a few
//   metres to a spot the navmesh can reach, stop, look around or turn to a nearby friend and gesture like they're
//   talking, go back, stand a while, repeat. Anything that puts the game's AI on alert (it sees you, gets hit, an ally
//   calls it) ends the round on the spot: the game's own combat takes over.
// NOISES (anywhere, idle NPCs only): fights, screams, deaths, things you throw hitting something, glass, gunshots and
//   the Noisemaker reach NPCs within earshot. An idle NPC walks to ROUGHLY where it came from - the farther away, the
//   rougher his guess - looks around for a few seconds and goes back. A noise never tells him where YOU are: he still
//   has to see you (sight and hearing are separate).
// LOSING YOU: if he was fighting or alerted and the game calms him back down, he goes to check the last place he saw
//   you before returning to his post.
// SENSES: "NPC field of view" and "NPC hearing" set the game's own detection (and the mod's sight / hearing checks)
//   narrower or wider, shorter or longer. "Game default" leaves the game's values untouched.
// COST: decisions 4x a second, a navmesh query only when a new spot is chosen, movement only for NPCs actually walking
//   (at most 6 at once), nothing for NPCs more than 45 m away.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        [ModOptionCategory("NPCs", 2), ModOptionOrder(46)]
        [ModOption("Dungeon patrols", "Story Mode dungeons: NPCs standing at their post walk short rounds, stop to look around or talk, then go back. Anything that alerts them ends it.", defaultValueIndex = 1)]
        public static bool patrols54 = true;

        [ModOptionCategory("NPCs", 2), ModOptionOrder(47)]
        [ModOption("Noises draw NPCs", "Idle NPCs walk over to check fights, things you throw, breaking glass and the Noisemaker. They only get a rough idea where it came from and still have to see you.", defaultValueIndex = 1)]
        public static bool noises54 = true;

        public static ModOptionString[] fovValues54 = { new ModOptionString("Game default", null, "game"), new ModOptionString("Narrow", null, "narrow"), new ModOptionString("Wide", null, "wide") };
        [ModOptionCategory("NPCs", 2), ModOptionOrder(48)]
        [ModOption("NPC field of view", "How wide NPCs see. Narrow (about 100 degrees) is easier to sneak past, Wide (about 160) is harder.", valueSourceName = nameof(fovValues54), defaultValueIndex = 0)]
        public static string fov54 = "game";

        public static ModOptionString[] hearValues54 = { new ModOptionString("Game default", null, "game"), new ModOptionString("Short", null, "short"), new ModOptionString("Long", null, "long") };
        [ModOptionCategory("NPCs", 2), ModOptionOrder(49)]
        [ModOption("NPC hearing", "How far NPCs hear fights and noises.", valueSourceName = nameof(hearValues54), defaultValueIndex = 0)]
        public static string hear54 = "game";

        /// Half-angle of the mod's own sight check (0.19 used 75).
        internal static float SightHalf54() => fov54 == "narrow" ? 50f : fov54 == "wide" ? 80f : 75f;
        /// Multiplier on how far the mod's noises carry.
        internal static float Hear54() => hear54 == "short" ? 0.6f : hear54 == "long" ? 1.5f : 1f;

        const int Post54 = 0, Walk54 = 1, Stop54 = 2, Home54 = 3, Check54 = 4, Look54 = 5;
        static bool dungeon54;
        static float nextDungeonCheck54, nextPatrolTick54;
        static NavMeshPath path54;                                                          // made on first use (Unity objects on the main thread)

        // ================================================================== tick
        void TickPatrol54()
        {
            float now = Time.time;
            TickThrown54(now);
            // walking NPCs get their move refreshed every frame (that's how the game's own AI drives it)
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active || !s.walking54 || s.move54 == null || s.creature == null) continue;
                var m = s.move54; var to = s.patTo54; float sp = s.pat54 == Check54 ? 0.5f : 0.3f;
                Guard("patrol.move", () => m.UpdateMoveCycle(to, 0.3f, 0.9f, sp, 0f, 360f, false, 1f, 2f));
            }
            if (now < nextPatrolTick54) return;
            nextPatrolTick54 = now + 0.25f;
            if (now >= nextDungeonCheck54) { nextDungeonCheck54 = now + 5f; bool d = false; Guard("dungeon54", () => d = Level.IsDungeon); dungeon54 = d; }
            var pc = Player.local != null ? Player.local.creature : null;
            int walking = 0;
            for (int i = 0; i < MaxTracked; i++) if (pool[i].active && pool[i].walking54) walking++;
            string key = fov54 + "|" + hear54;
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active || s.creature == null || s.life54 != LifeOf54(s.creature)) continue;
                var c = s.creature;
                if (c.isKilled) { StopWalk54(s); continue; }
                if (!Mods54(s)) continue;
                if (s.percKey54 != key) { s.percKey54 = key; Senses54(s); }
                if (pc != null && (c.transform.position - pc.transform.position).sqrMagnitude > 45f * 45f) { StopWalk54(s); continue; }
                Patrol54(s, now, ref walking);
            }
        }

        /// Sets the game's detection (field of view, hearing distance) from the two settings, from saved originals.
        static void Senses54(NpcState s)
        {
            if (s.det54 == null || s.detO54 == null) return;
            Guard("senses54", () =>
            {
                var d = s.det54; var o = s.detO54;
                d.sightDetectionHorizontalFov = fov54 == "narrow" ? Mathf.Min(o[0], 100f) : fov54 == "wide" ? Mathf.Max(o[0], 160f) : o[0];
                d.sightDetectionVerticalFov = o[1] * (fov54 == "narrow" ? 0.85f : fov54 == "wide" ? 1.15f : 1f);
                d.hearMaxDistance = o[2] * (hear54 == "short" ? 0.6f : hear54 == "long" ? 1.6f : 1f);
            });
        }

        /// The game's AI has him standing calmly (no target, not alerted) and nothing of ours is holding him.
        static bool Calm54(NpcState s, out bool alerted)
        {
            var c = s.creature; alerted = false;
            if (c.ragdoll == null || c.ragdoll.state != Ragdoll.State.Standing || s.ally || s.surrendered || s.knockedOut || s.passedOut
                || s.behavior != Behavior.Normal || s.pain > 0.6f || s.inCombat || now54() - s.lastHitTime < 5f) return false;
            int st = -1; bool target = false;
            Guard("calm54", () => { st = (int)c.brain.state; target = c.brain.currentTarget != null; });
            alerted = st == (int)Brain.State.Combat || st == (int)Brain.State.Alert || target;
            return st == (int)Brain.State.Idle && !target;
        }
        static float now54() => Time.time;

        void Patrol54(NpcState s, float now, ref int walking)
        {
            var c = s.creature;
            bool calm = Calm54(s, out bool alerted);
            // lost you: the game calmed him down again - go check where you were last seen
            if (s.alertWas54 && !alerted && calm && noises54 && s.seenAt > 0f && now - s.seenAt < 25f)
                Investigate54(s, s.seenPos, 1.5f, now, "lost you - searching where you were", ref walking);
            s.alertWas54 = alerted;
            if (!calm) { if (s.walking54 || s.pat54 != Post54) { StopWalk54(s); s.pat54 = Post54; s.patUntil54 = now + Random.Range(4f, 8f); } return; }
            if (!s.homeSet54) { s.home54 = c.transform.position; s.homeSet54 = true; s.pat54 = Post54; s.patUntil54 = now + Random.Range(3f, 12f); }
            // a noise: go and check roughly where it came from (sight is separate - this never finds YOU)
            if (noises54 && s.heardAt > s.heardSeen54 + 0.01f)
            {
                s.heardSeen54 = s.heardAt;
                float dist = Vector3.Distance(c.transform.position, s.heardPos);
                if (s.heardKind != Stim.NearMiss && dist > 2.5f && now - s.heardAt < 2f)
                    Investigate54(s, s.heardPos, Mathf.Clamp(dist * 0.3f, 0.7f, 5f), now, $"heard {(s.heardKind == Stim.Noise ? "a noise" : s.heardKind.ToString().ToLowerInvariant())} {dist:0}m away - going to check", ref walking);
            }
            // walking: arrived? stuck? taking too long?
            if (s.walking54)
            {
                Vector3 pos = c.transform.position;
                Vector3 flat = s.patTo54 - pos; flat.y = 0f;
                if ((pos - s.lastPos54).sqrMagnitude > 0.25f * 0.25f) { s.lastPos54 = pos; s.movedAt54 = now; }
                bool arrived = flat.sqrMagnitude < 1f, stuck = now - s.movedAt54 > 3f, late = now > s.patUntil54;
                if (!arrived && !stuck && !late) return;
                StopWalk54(s); walking--;
                switch (s.pat54)
                {
                    case Walk54: s.pat54 = Stop54; s.patUntil54 = now + Random.Range(3f, 8f); Ambient54(s, now); break;
                    case Check54: s.pat54 = Look54; s.patUntil54 = now + Random.Range(4f, 7f); s.nextLook54 = 0f; break;
                    default: s.pat54 = Post54; s.patUntil54 = now + Random.Range(8f, 20f); break;
                }
                return;
            }
            switch (s.pat54)
            {
                case Post54:     // standing at his post (the 0.19 idle life does the weight shifts and look-arounds)
                    if (now < s.patUntil54 || !patrols54 || !dungeon54) return;
                    if (walking >= 6) { s.patUntil54 = now + 2f; return; }
                    if (PickSpot54(s.home54, 3f, 8f, out var spot)) { Walk54To(s, spot, Walk54, now); walking++; }
                    else s.patUntil54 = now + Random.Range(6f, 12f);
                    return;
                case Stop54:     // a stop on the round: looking around / talking
                    if (now < s.patUntil54) { if (now >= s.nextLook54) { s.nextLook54 = now + Random.Range(1.2f, 2.5f); LookAround54(s, c.transform.position, now); } return; }
                    if (walking >= 6) { s.patUntil54 = now + 1f; return; }
                    Walk54To(s, s.home54, Home54, now); walking++;
                    return;
                case Look54:     // at the noise: look around, then head back
                    if (now < s.patUntil54) { if (now >= s.nextLook54) { s.nextLook54 = now + Random.Range(0.9f, 1.6f); LookAround54(s, s.patTo54, now); } return; }
                    if (debugLog) Log($"{WhoIs(s)}: nothing there - back to his post");
                    Walk54To(s, s.home54, Home54, now); walking++;
                    return;
                default: s.pat54 = Post54; s.patUntil54 = now + 5f; return;
            }
        }

        void Investigate54(NpcState s, Vector3 at, float err, float now, string why, ref int walking)
        {
            if (s.move54 == null || walking >= 6 && !s.walking54) return;
            Vector2 r = Random.insideUnitCircle * err;
            Vector3 guess = at + new Vector3(r.x, 0f, r.y);                                      // only a rough idea where it was
            if (!NavMesh.SamplePosition(guess, out var hit, 3f, NavMesh.AllAreas)) return;
            if (!s.homeSet54) { s.home54 = s.creature.transform.position; s.homeSet54 = true; }
            bool was = s.walking54;
            Walk54To(s, hit.position, Check54, now);
            if (!was) walking++;
            s.lookAt = at; s.lookUntil = now + 1.5f;
            Log($"{WhoIs(s)}: {why}");
        }

        static bool PickSpot54(Vector3 home, float min, float max, out Vector3 spot)
        {
            spot = home;
            if (path54 == null) path54 = new NavMeshPath();
            for (int tries = 0; tries < 4; tries++)
            {
                Vector2 d = Random.insideUnitCircle.normalized * Random.Range(min, max);
                if (!NavMesh.SamplePosition(home + new Vector3(d.x, 0f, d.y), out var hit, 2f, NavMesh.AllAreas)) continue;
                if (!NavMesh.CalculatePath(home, hit.position, NavMesh.AllAreas, path54) || path54.status != NavMeshPathStatus.PathComplete) continue;
                spot = hit.position; return true;
            }
            return false;
        }

        static void Walk54To(NpcState s, Vector3 to, int state, float now)
        {
            s.patTo54 = to; s.pat54 = state; s.walking54 = s.move54 != null;
            s.lastPos54 = s.creature.transform.position; s.movedAt54 = now;
            s.patUntil54 = now + 25f;
        }

        static void StopWalk54(NpcState s)
        {
            if (!s.walking54) return;
            s.walking54 = false;
            var m = s.move54;
            if (m != null) Guard("patrol.stop", () => m.StopMove());
        }

        static void LookAround54(NpcState s, Vector3 around, float now)
        {
            var head = s.creature.ragdoll != null ? s.creature.ragdoll.headPart : null;
            float h = head != null ? head.transform.position.y : around.y + 1.6f;
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(2f, 6f);
            s.lookAt = new Vector3(around.x + r.x, h + Random.Range(-0.4f, 0.2f), around.z + r.y);
            s.lookUntil = now + Random.Range(0.8f, 1.6f);
        }

        /// At a stop: if a calm friend stands close, they turn to each other and one of them talks with his hands.
        void Ambient54(NpcState s, float now)
        {
            var c = s.creature;
            for (int i = 0; i < MaxTracked; i++)
            {
                var o = pool[i];
                if (o == s || !o.active || o.creature == null || o.creature.isKilled || o.walking54 || o.creature.factionId != c.factionId) continue;
                if ((o.creature.transform.position - c.transform.position).sqrMagnitude > 3.5f * 3.5f) continue;
                var oh = o.creature.ragdoll != null ? o.creature.ragdoll.headPart : null; var sh = c.ragdoll.headPart;
                if (oh == null || sh == null) continue;
                s.lookAt = oh.transform.position; s.lookUntil = now + 3.5f;
                o.lookAt = sh.transform.position; o.lookUntil = now + 3.5f;
                if (Random.value < 0.6f) Gesture43(Random.value < 0.5f ? s : o, 5, 1.6f);
                s.nextLook54 = now + 3.6f;
                if (debugLog) Log($"{WhoIs(s)} stops to talk with {WhoIs(o)}");
                return;
            }
            s.nextLook54 = now;
        }

        // ================================================================== things you throw make noise
        sealed class Thrown54 { public Item it; public float until, speed; public int bounces; }
        static readonly List<Thrown54> thrown54 = new List<Thrown54>();
        readonly Item[] heldWas54 = new Item[2];

        void TickThrown54(float now)
        {
            if (!noises54) { thrown54.Clear(); return; }
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc != null)
                for (int k = 0; k < 2; k++)
                {
                    var h = k == 0 ? pc.handLeft : pc.handRight;
                    var it = h != null && h.grabbedHandle != null ? h.grabbedHandle.item : null;
                    var was = heldWas54[k];
                    if (was != null && it != was && was.physicBody != null && thrown54.Count < 6)
                        thrown54.Add(new Thrown54 { it = was, until = now + 4f, speed = 0f });      // let go of it: listen for where it lands
                    heldWas54[k] = it;
                }
            for (int i = thrown54.Count - 1; i >= 0; i--)
            {
                var t = thrown54[i];
                if (t.it == null || now > t.until || t.bounces >= 2 || t.it.physicBody == null || t.it.holder != null || t.it.IsHanded()) { thrown54.RemoveAt(i); continue; }
                float sp = t.it.physicBody.velocity.magnitude;
                if (t.speed > 3.5f && sp < t.speed * 0.45f)
                {   // it hit something
                    t.bounces++;
                    float mass = 1f; Guard("thrown.mass", () => mass = t.it.physicBody.mass);
                    Emit(t.it.transform.position, Mathf.Clamp(3f + mass * 2f + t.speed * 0.6f, 3f, 16f), Stim.Impact);
                }
                t.speed = sp;
            }
        }
    }
}

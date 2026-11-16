// Rage Engine 0.28 - playtest feedback after 0.26.
//   NOT TOO TANKY   your fists and feet now do real damage by how fast they land (a haymaker hurts, a tap doesn't),
//                   a heavy punch to the head of a hurt man puts him out or kills him, knockback is back to a middle
//                   setting, and the "keep them on their feet" guard no longer undoes knockdowns you caused (this is
//                   what was cancelling gravity pushes and other vanilla effects).
//   VITAL: BACK OF THE HEAD   blunt hits there make him punch-drunk twice as fast and enough of them kill.
//   EYES            enough blunt damage around an eye and it's gone.
//   GUT SHOTS       enough of them and he throws up, doubled over.
//   CHOKING ON BLOOD  blood in the airway while he's down or out: he gurgles, coughs it up, and can choke to death.
//   CONVULSIONS     punch-drunk AND in agony: he drops and convulses, helpless, for a few seconds.
//   CRAWLING AWAY   a downed man who's terrified or has no hands left to fight with drags himself away from you.
//   THE KNIFE       about half of them carry a short blade: disarmed with you in close, he pulls it.
//   MIXED WAVES     waves where every enemy has a different amount of HP (60 to 1500) and a slightly different size.
//   CHEATS SAVED    free climb, fall damage, infinite focus / imbue, invincibility are remembered between sessions.
using System.Collections.Generic;
using System.IO;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        [ModOptionCategory(C9, 9), ModOptionOrder(41)]
        [ModOption("Your punch damage", "Extra damage from your fists and feet for really fast punches. 0 = the game's own damage only.", defaultValueIndex = 2)]
        [ModOptionSlider, ModOptionFloatValues(0f, 3f, 0.25f)]
        public static float punchDamage = 0.5f;
        [ModOptionCategory(C9, 9), ModOptionOrder(42)]
        [ModOption("NPCs pull a knife", "ON: about half the NPCs carry a short blade and pull it when they've lost their weapon and you're close.", defaultValueIndex = 1)]
        public static bool pullKnife = true;
        [ModOptionCategory(C8, 8), ModOptionOrder(51)]
        [ModOption("Back of the head is vital", "ON: blunt hits to the back of the head daze twice as fast, and enough of them kill.", defaultValueIndex = 1)]
        public static bool occiputVital = true;
        [ModOptionCategory(C0, 0), ModOptionOrder(45)]
        [ModOption("Remember cheats", "ON: free climb, fall damage, infinite focus / imbue and invincibility are saved and put back next time you play.", defaultValueIndex = 1)]
        public static bool rememberCheats = true;

        // ------------------------------------------------------------------ your fists
        /// From Toughen: the player's bare hand or foot landed. Returns the new severity.
        float PunchPower28(NpcState s, CollisionInstance hit, float severity)
        {
            if (hit.damageStruct.damageType != DamageType.Blunt) return severity;
            if (!PlayerSource(hit, out var ppart, out var pitem, out float speed) || pitem != null || ppart == null) return severity;
            var c = s.creature;
            var part = hit.damageStruct.hitRagdollPart;
            bool head = part != null && Has(part.type, RagdollPart.Type.Head | RagdollPart.Type.Neck);
            bool tank = Tank(s);
            if (punchDamage > 0f && speed > 5f && !c.isKilled)                  // 0.28.2: normal punches are the game's own; only real power shots add
            {
                float extra = Mathf.Pow(speed - 5f, 1.4f) * 3f * punchDamage * (head ? 1.25f : 1f) * (tank ? 0.3f : 1f);
                extra = Mathf.Min(extra, c.maxHealth * 0.25f);
                // leave the killing blow to the rules below so it reads as a knockout / finisher, not a silent HP drain
                float room = c.currentHealth - 1f;
                if (room > 0f) Guard("punchdmg", () => c.Damage(Mathf.Min(extra, room)));
                if (speed > 5f && Time.time >= s.nextShrugLog) { s.nextShrugLog = Time.time + 1f; Log($"{WhoIs(s)}: your punch {speed:0.0} m/s -> +{extra:0} damage (HP {c.currentHealth:0}/{c.maxHealth:0})"); }
            }
            severity *= 1f + 0.05f * Mathf.Clamp(speed - 6f, 0f, 4f);
            if (speed >= 8f) s.powerHitAt = Time.time;                                 // a real power shot may put him down
            // the finisher: a real haymaker to the head of a hurt man
            if (!tank && head && speed >= 9f && !c.isKilled && !s.knockedOut)
            {
                float hp = HpFrac(c);
                if (hp <= 0.08f) { s.finalKill = true; Log($"{WhoIs(s)}: haymaker ({speed:0.0} m/s) - lights out for good"); Guard("kill", () => c.Kill()); }
                else if (hp <= 0.3f && Random.value < 0.6f) { AllowFall(s, 3f); Knockout(s, hit.impactVelocity, true); }
            }
            return severity;
        }

        /// True when this death came from a heavy blow of yours - those are never shrugged off with adrenaline.
        static bool HeavyPlayerBlow(CollisionInstance hit)
            => hit != null && hit.damageStruct.damageType == DamageType.Blunt && PlayerSource(hit, out _, out _, out float sp) && sp >= 5f;

        // ------------------------------------------------------------------ vital spots (from HandleHit, blunt)
        void Vitals28(NpcState s, CollisionInstance hit, RagdollPart part, float severity)
        {
            var c = s.creature;
            if (c.isKilled) return;
            var t = part.type;
            Vector3 p = hit.contactPoint;
            bool tank = Tank(s);
            // ---- back of the head
            if (occiputVital && !tank && Has(t, RagdollPart.Type.Head) && severity >= 0.5f)
            {
                var ce = c.centerEyes; var head = c.ragdoll.headPart;
                Vector3 fwd = ce != null ? ce.forward : head.transform.forward;
                if (Vector3.Dot((p - head.transform.position).normalized, fwd) < -0.35f)
                {
                    s.occiput += Mathf.Min(severity, 2.5f);
                    AddDrunk(s, t, severity);                                       // a second helping: dazed twice as fast
                    s.daze = Mathf.Max(s.daze, Mathf.Clamp01(severity * 0.6f));
                    if (s.occiput >= 3.2f || severity >= 1.9f)
                    {
                        s.finalKill = true; s.noGasp = true;
                        Sfx.PlayAt("NRE_BoneCrunch", p, 0.9f);
                        Stamp(c, p, 0.05f, "hematoma", 0.9f);
                        Log($"{WhoIs(s)}: blow to the back of the head - dead ({s.occiput:0.0})");
                        RageMoment("back of the head");
                        Guard("kill", () => c.Kill());
                        return;
                    }
                    if (s.occiput >= 1.6f && !s.knockedOut && Random.value < 0.5f) { AllowFall(s, 3f); Knockout(s, hit.impactVelocity, true); }
                }
            }
            // ---- eyes: blunt trauma right on one
            if (uniqueGore && Has(t, RagdollPart.Type.Head) && severity >= 1.1f && c.allEyes != null)
            {
                for (int i = 0; i < c.allEyes.Count && i < 2; i++)
                {
                    var e = c.allEyes[i];
                    if (e == null || (s.gouged & (1 << i)) != 0 || (e.transform.position - p).sqrMagnitude > 0.035f * 0.035f) continue;
                    s.eyeDmg[i] += severity;
                    if (s.eyeDmg[i] >= 2.6f)
                    {
                        Vector3 ep = e.transform.position;
                        Gouge(s, i, e, null);
                        SpawnGoreModels(ep, (ep - c.ragdoll.headPart.transform.position).normalized + Vector3.up * 0.2f, "RE_Eyeball", 1, ToothMat(), 0.9f, c);
                        Log($"{WhoIs(s)}: eye burst from the beating");
                    }
                    break;
                }
            }
            // ---- gut
            if (Has(t, RagdollPart.Type.Torso) && severity >= 0.6f && !tank)
            {
                var chest = ChestPart(c) ?? part;
                if (Vector3.Dot(p - chest.transform.position, c.transform.up) < -0.05f && Vector3.Dot(p - chest.transform.position, c.transform.forward) > -0.05f)
                {
                    s.gutShots += Mathf.Min(severity, 2f);
                    if (s.gutShots >= 4f && Time.time >= s.nextVomit) Vomit(s);
                }
            }
        }

        void Vomit(NpcState s)
        {
            var c = s.creature;
            s.gutShots = 1.5f; s.nextVomit = Time.time + 12f;
            s.vomitUntil = Time.time + 2.2f; s.nextVomitSpurt = 0f;
            s.daze = Mathf.Max(s.daze, 0.7f);
            Guard("vomit.stagger", () => Stagger(s, -c.transform.forward, BrainModuleHitReaction.PushBehaviour.Effect.StaggerMedium, RagdollPart.Type.Torso));
            Log($"{WhoIs(s)}: gut shots - he throws up");
        }

        // ------------------------------------------------------------------ 0.1 s tick
        void Tick28(NpcState s, float now)
        {
            var c = s.creature;
            if (c.isKilled) return;
            // ---- throwing up
            if (now < s.vomitUntil && now >= s.nextVomitSpurt)
            {
                s.nextVomitSpurt = now + 0.35f;
                var mouth = Mouth(c);
                if (mouth != null)
                {
                    Guard("vomit", () => Spray28(mouth.position, (c.transform.forward + Vector3.down * 0.7f).normalized, new Color(0.62f, 0.55f, 0.2f), 14, 1.6f));
                    Sfx.PlayAt("NRE_Squelch", mouth.position, 0.7f);
                    var chest = ChestPart(c);
                    if (chest != null && chest.physicBody != null && c.ragdoll.state == Ragdoll.State.Standing) chest.physicBody.AddForce((c.transform.forward * 0.8f + Vector3.down * 0.6f), ForceMode.VelocityChange);
                }
            }
            // ---- choking on his own blood: airway full while he's flat out
            bool flat = c.ragdoll.state != Ragdoll.State.Standing;
            if (s.bloodInAirway && flat && (s.knockedOut || s.cantStand || s.paralysed || s.throatCutUntil > now))
            {
                if (s.chokeBloodSince <= 0f) { s.chokeBloodSince = now; s.nextChokeCough = now + 1.5f; Log($"{WhoIs(s)}: choking on his own blood"); }
                if (now >= s.nextChokeCough)
                {
                    s.nextChokeCough = now + Random.Range(1.6f, 2.8f);
                    SpitBlood(s, 1);
                    var mouth = Mouth(c); if (mouth != null) Sfx.PlayAt("NRE_DeathGasp", mouth.position, 0.6f);
                    var chest = ChestPart(c);
                    if (chest != null && chest.physicBody != null) chest.physicBody.AddForce(Vector3.up * 0.9f + Random.insideUnitSphere * 0.3f, ForceMode.VelocityChange);
                    if (!Tank(s)) Guard("choke.dmg", () => c.Damage(Mathf.Max(2f, c.maxHealth * 0.035f)));
                }
                if (now - s.chokeBloodSince > 22f && !Tank(s)) { s.finalKill = true; Log($"{WhoIs(s)}: choked on his blood"); Guard("kill", () => c.Kill()); return; }
            }
            else s.chokeBloodSince = 0f;
            // ---- punch-drunk and in agony: down he goes, convulsing
            if (!s.knockedOut && !Tank(s) && s.drunk >= 1f && s.pain * painIntensity >= 0.85f && now >= s.nextConvulse && s.convulseUntil <= 0f)
            {
                s.convulseUntil = now + Random.Range(4f, 7f); s.nextConvulse = now + 25f;
                AllowFall(s, 8f);
                if (c.ragdoll.state == Ragdoll.State.Standing) Guard("convulse.fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
                Guard("convulse.drop", () => { foreach (Side sd in new[] { Side.Left, Side.Right }) { var h = Hand(c, sd); if (h != null && h.grabbedHandle != null) h.UnGrab(false); } });
                Scream(s, true);
                Log($"{WhoIs(s)}: dazed and in agony - collapses, convulsing");
            }
            if (s.convulseUntil > 0f)
            {
                if (now >= s.convulseUntil) s.convulseUntil = 0f;
                else
                {
                    s.stayDown = Mathf.Max(s.stayDown, 1f);
                    foreach (var p in c.ragdoll.parts)
                        if (p != null && p.physicBody != null && Has(p.type, ArmParts | LegParts | RagdollPart.Type.Torso) && Random.value < 0.6f)
                            p.physicBody.AddForce(Random.insideUnitSphere * Random.Range(0.6f, 1.8f), ForceMode.VelocityChange);
                }
            }
            // ---- the knife
            if (pullKnife && s.hasKnife && !s.knifeOut && !s.knifePending && c.ragdoll.state == Ragdoll.State.Standing && !s.knockedOut && !s.ally
                && s.unarmedSince > 0f && now - s.unarmedSince > 1.5f && now - s.trackedAt > 6f)
            {
                var pc = Player.local != null ? Player.local.creature : null;
                if (pc != null && (pc.transform.position - c.transform.position).sqrMagnitude < 3f * 3f) Guard("knife", () => PullKnife(s));
            }
        }

        static Material sprayMat28; static readonly Dictionary<int, Material> sprayMats28 = new Dictionary<int, Material>();
        /// A directed burst of coloured droplets (vomit).
        static void Spray28(Vector3 at, Vector3 dir, Color col, int count, float speed)
        {
            var sh = SkinPaint.BlendShader; if (sh == null) return;
            int key = Mathf.RoundToInt(col.r * 15f) | Mathf.RoundToInt(col.g * 15f) << 4 | Mathf.RoundToInt(col.b * 15f) << 8;
            if (!sprayMats28.TryGetValue(key, out var m) || m == null)
            {
                if (bloodDot == null) bloodDot = SoftDot();
                sprayMats28[key] = m = new Material(sh) { color = new Color(col.r, col.g, col.b, 1f), mainTexture = bloodDot, renderQueue = 3050 };
            }
            var go = new GameObject("RageEngine_Spray");
            go.transform.position = at; go.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir : Vector3.down);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = 0.3f; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed); main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
            main.startColor = Color.white; main.maxParticles = count; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 1.4f;
            var em = ps.emission; em.rateOverTime = 0f;
            var shp = ps.shape; shp.shapeType = ParticleSystemShapeType.Cone; shp.angle = 18f; shp.radius = 0.012f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
            ps.Emit(count);
            Object.Destroy(go, 1.6f);
        }

        // ------------------------------------------------------------------ the knife
        static ItemData knifeData;
        void PullKnife(NpcState s)
        {
            var c = s.creature;
            if (knifeData == null)
                knifeData = FindItem(d => d.type == ItemData.Type.Weapon && Contains(d.id, "dagger") && !Contains(d.id, "offhand") && !Contains(d.id, "throw") && !Contains(d.id, "RE_"))
                         ?? FindItem(d => d.type == ItemData.Type.Weapon && (Contains(d.id, "knife") || Contains(d.id, "dagger")) && !Contains(d.id, "RE_"));
            if (knifeData == null) { s.hasKnife = false; return; }
            Side side = HandUsable(s, Side.Right) ? Side.Right : Side.Left;
            if (!HandUsable(s, side)) return;
            DropPhantom(s);
            var hand = Hand(c, side);
            if (hand == null || hand.grabbedHandle != null) return;
            s.knifePending = true;
            knifeData.SpawnAsync(item =>
            {
                s.knifePending = false;
                if (item == null) return;
                if (s.creature != c || c.isKilled || hand.grabbedHandle != null) { Guard("despawn", () => item.Despawn()); return; }
                var handle = side == Side.Right && item.mainHandleRight != null ? item.mainHandleRight
                           : side == Side.Left && item.mainHandleLeft != null ? item.mainHandleLeft
                           : item.handles != null && item.handles.Count > 0 ? item.handles[0] : null;
                if (handle == null) { Guard("despawn", () => item.Despawn()); return; }
                bool ok = false;
                Guard("grab", () => { hand.Grab(handle, true); ok = hand.grabbedHandle != null; });
                if (!ok) { Guard("despawn", () => item.Despawn()); return; }
                s.knifeOut = true; s.unarmedSince = 0f;
                Sfx.Play("NRE_Swoosh", hand.transform, 0.6f, 1.3f);
                Speak(s, "attackmelee");
                Log($"{WhoIs(s)}: pulls a knife ({item.data?.id})");
            }, hand.transform.position, hand.transform.rotation, null, false);
        }

        // ------------------------------------------------------------------ mixed-toughness waves
        static readonly float[] MixedHp = { 60f, 100f, 150f, 250f, 400f, 650f, 1000f, 1500f };
        sealed class PendingBuild { public Creature c; public float at, hp, size; }
        static readonly List<PendingBuild> pendingBuilds = new List<PendingBuild>();

        /// Called by the wave spawners for "Mixed toughness": queue an HP and a size for this fresh spawn.
        static void QueueMixed(Creature cr)
        {
            pendingBuilds.Add(new PendingBuild { c = cr, at = Time.time + 1f, hp = MixedHp[Random.Range(0, MixedHp.Length)], size = Random.Range(0.93f, 1.08f) });
        }

        void TickMixed28()
        {
            if (pendingBuilds.Count == 0) return;
            float now = Time.time;
            for (int i = pendingBuilds.Count - 1; i >= 0; i--)
            {
                var p = pendingBuilds[i];
                if (p.c == null || p.c.isKilled || now > p.at + 15f) { pendingBuilds.RemoveAt(i); continue; }
                if (now < p.at || !TryGet(p.c, out var s)) continue;
                pendingBuilds.RemoveAt(i);
                s.hpOverride = p.hp; s.healthApplied = false; s.healthCheckAt = 0f;
                Guard("mixed.size", () => p.c.SetHeight(p.c.GetHeight() * p.size));
                Debug.Log($"[RageEngine] mixed wave: {p.c.name} -> {p.hp:0} HP, size x{p.size:0.00}");
            }
        }

        public static ModOptionString[] mixedSizes = { new ModOptionString("5", null, "5"), new ModOptionString("3", null, "3"), new ModOptionString("8", null, "8") };
        [ModOptionCategory(C0, 0), ModOptionOrder(46)]
        [ModOption("Mixed wave size", "How many enemies the mixed-toughness wave spawns.", valueSourceName = nameof(mixedSizes), defaultValueIndex = 0)]
        public static string mixedSize = "5";
        [ModOptionButton, ModOptionCategory(C0, 0), ModOptionOrder(47)]
        [ModOption("Spawn mixed-toughness wave", "Enemies in front of you, each with a different amount of HP (60 to 1500) and size - feel the difference.", valueSourceName = nameof(mapGo), saveValue = false)]
        public static void MixedWaveButton(string _) => Guard("mixedwave", () => instance?.SpawnMixedWave());

        void SpawnMixedWave()
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) return;
            int n = 5; int.TryParse(mixedSize, out n); n = Mathf.Clamp(n, 1, 8);
            LogFactions();
            int hostile = HostileFaction();
            Notify($"Mixed wave: {n} enemies (faction {FactionName28(hostile)})");
            Vector3 fwd = Vector3.ProjectOnPlane(Player.local.head != null ? Player.local.head.transform.forward : pc.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            for (int i = 0; i < n; i++)
            {
                var data = PickSoldier(out int tableFaction);
                if (data == null) { Notify("Rage Engine: couldn't find any enemy type to spawn."); return; }
                Vector3 pos = pc.transform.position + fwd * (6f + (i % 2) * 1.5f) + right * ((i - (n - 1) * 0.5f) * 1.7f);
                if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var hg, 8f, ~0, QueryTriggerInteraction.Ignore)) pos = hg.point + Vector3.up * 0.05f;
                int faction = hostile >= 0 ? hostile : tableFaction >= 0 && tableFaction != 2 ? tableFaction : data.factionId != 2 && data.factionId >= 0 ? data.factionId : -1;      // 0.28.2: always an aggressive faction first
                Vector3 look = pc.transform.position - pos; look.y = 0f;
                data.SpawnAsync(pos, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : -fwd).eulerAngles.y, null, false, null, cr =>
                {
                    if (cr == null) return;
                    Guard("mixed.faction", () => { if (faction >= 0 && faction != 2) cr.SetFaction(faction); });
                    MakeHostile28(cr, faction);
                    QueueMixed(cr);
                });
            }
        }

        // ------------------------------------------------------------------ cheats remembered
        static float nextCheatCheck; static bool cheatsApplied; static string cheatSnap;
        static string CheatPath => string.IsNullOrEmpty(modFolder) ? null : Path.Combine(modFolder, "RageEngine_cheats.txt");
        static string CheatSnapshot() => $"freeClimb={(RagdollHandClimb.climbFree ? 1 : 0)}\nfallDamage={(Player.fallDamage ? 1 : 0)}\ninfiniteFocus={(Mana.infiniteFocus ? 1 : 0)}\ninfiniteImbue={(Imbue.infiniteImbue ? 1 : 0)}\ninvincibility={(Player.invincibility ? 1 : 0)}\n";

        static void TickCheats28()
        {
            if (!rememberCheats) return;
            float now = Time.unscaledTime;
            if (now < nextCheatCheck) return;
            nextCheatCheck = now + 2f;
            if (Player.local == null || Player.local.creature == null) return;      // wait until you're actually in a level
            var path = CheatPath; if (path == null) return;
            if (!cheatsApplied)
            {
                cheatsApplied = true;
                try
                {
                    if (File.Exists(path))
                    {
                        foreach (var line in File.ReadAllLines(path))
                        {
                            int eq = line.IndexOf('='); if (eq <= 0) continue;
                            bool on = line.Substring(eq + 1).Trim() == "1";
                            switch (line.Substring(0, eq))
                            {
                                case "freeClimb": GameManager.SetFreeClimb(on); break;
                                case "fallDamage": GameManager.SetPlayerFallDamage(on); break;
                                case "infiniteFocus": GameManager.SetInfiniteFocus(on); break;
                                case "infiniteImbue": GameManager.SetInfiniteImbue(on); break;
                                case "invincibility": GameManager.SetPlayerInvincibility(on); break;
                            }
                        }
                        Debug.Log("[RageEngine] cheats restored: " + CheatSnapshot().Replace('\n', ' '));
                    }
                }
                catch (System.Exception e) { Debug.Log("[RageEngine] cheats restore failed: " + e.Message); }
                cheatSnap = CheatSnapshot();
                return;
            }
            string snap = CheatSnapshot();
            if (snap == cheatSnap) return;
            cheatSnap = snap;
            try { File.WriteAllText(path, snap); } catch (System.Exception e) { Debug.Log("[RageEngine] cheats save failed: " + e.Message); }
        }
    }
}

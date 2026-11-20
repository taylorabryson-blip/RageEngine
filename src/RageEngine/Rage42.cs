// Rage Engine 0.42 - player feedback round:
//   EYES          a hard blow, a blade tip or a thumb in the eye pops it: the eyeball hangs out of the socket on its
//                 nerve, swings, then tears off and drops. A popped eye is gone for good; both gone = blind for good
//                 (he loses you unless you're on top of him, swings wild at anything close, keeps a hand on his face).
//   JAW           a broken jaw gets held: a hand goes up to it again and again.
//   DAGGERS       an unarmed NPC who's taking a beating sometimes pulls a dagger from his belt (no Rage Engine fist
//                 fighting - so it never fights with fist-fighting mods).
//   BONE BREAKS   more reactions: cradling the arm, staring at it, grabbing the leg, gritting it out, pain vomiting,
//                 passing out from the pain.
//   WAVES         an NPC Rage Engine keeps alive after the game counted him dead goes back into the wave's count,
//                 so breaking bones / dying moments / adrenaline no longer pull extra enemies in.
//                 "Max NPCs at once": game default, or your own number.
//   SFX           mod sound effects are gore only now (bones, flesh, blood, teeth, eyes, glass) - no more punch,
//                 body-hit, whoosh or thud sounds over the game's own.
//   STABILITY     a feature that keeps throwing errors is switched off for a while instead of erroring every frame.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    /// An eyeball hanging out of its socket on the optic nerve: a little pendulum that follows the head, then tears off.
    public sealed class EyeDangle42 : MonoBehaviour
    {
        public Transform head; public Vector3 socketLocal; public float len = 0.06f; public float dropAt; public LineRenderer nerve;
        Vector3 pos, vel, lastAnchor; bool started, dropped;

        void Start() { pos = transform.position; if (head != null) lastAnchor = head.TransformPoint(socketLocal); started = true; }

        void Update()
        {
            if (!started || dropped) return;
            float dt = Mathf.Min(Time.deltaTime, 0.04f);
            if (head == null || !head.gameObject.activeInHierarchy) { Drop(Vector3.zero); return; }
            Vector3 anchor = head.TransformPoint(socketLocal);
            Vector3 anchorVel = (anchor - lastAnchor) / Mathf.Max(dt, 0.0001f); lastAnchor = anchor;
            if (Time.time >= dropAt || anchorVel.sqrMagnitude > 7f * 7f) { Drop(anchorVel); return; }      // a hard jerk of the head tears it off
            vel += Physics.gravity * dt;
            vel *= 0.985f;
            pos += vel * dt;
            Vector3 d = pos - anchor; float m = d.magnitude;
            if (m > len) { Vector3 n = d / m; pos = anchor + n * len; vel -= Vector3.Project(vel, n); }
            // keep it out of the skull
            Vector3 hc = head.position; Vector3 fromC = pos - hc; float r = 0.085f;
            if (fromC.sqrMagnitude < r * r) { pos = hc + fromC.normalized * r; vel = Vector3.ProjectOnPlane(vel, fromC.normalized); }
            transform.position = pos;
            if ((anchor - pos).sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(pos - anchor);
            if (nerve != null) { nerve.SetPosition(0, anchor); nerve.SetPosition(1, pos); }
        }

        void Drop(Vector3 v)
        {
            dropped = true;
            if (nerve != null) Destroy(nerve.gameObject);
            RageEngineScript.Guard("eye.drop", () =>
            {
                var sc = gameObject.AddComponent<SphereCollider>(); sc.radius = 0.012f / Mathf.Max(0.001f, transform.lossyScale.x);
                var rb = gameObject.AddComponent<Rigidbody>(); rb.mass = 0.02f; rb.drag = 0.4f; rb.angularDrag = 0.4f;
                rb.velocity = vel * 0.6f + v * 0.4f;
                rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.VelocityChange);
                Sfx.PlayAt("NRE_Squelch", transform.position, 0.45f);
            });
            Destroy(gameObject, 45f);
        }
    }

    /// Batwing / swinging doors: a creak when they swing hard.
    public sealed class DoorCreak42 : MonoBehaviour
    {
        Rigidbody rb; float next;
        void Start() { rb = GetComponent<Rigidbody>(); }
        void FixedUpdate()
        {
            if (rb == null || Time.time < next) return;
            float w = rb.angularVelocity.magnitude;
            if (w > 2.2f) { next = Time.time + 0.7f; Sfx.PlayAt("NRE_DoorCreak", transform.position, Mathf.Clamp01(0.35f + w * 0.06f)); }
        }
    }

    public sealed partial class RageEngineScript
    {
        // ------------------------------------------------------------------ settings
        public static ModOptionString[] maxNpcValues =
        {
            new ModOptionString("Game default", null, "0"), new ModOptionString("1", null, "1"), new ModOptionString("2", null, "2"), new ModOptionString("3", null, "3"),
            new ModOptionString("4", null, "4"), new ModOptionString("5", null, "5"), new ModOptionString("6", null, "6"), new ModOptionString("8", null, "8"),
            new ModOptionString("10", null, "10"), new ModOptionString("12", null, "12"), new ModOptionString("15", null, "15"),
        };
        [ModOptionCategory("Rage", 0), ModOptionOrder(9)]
        [ModOption("Max NPCs at once", "How many wave enemies can be alive at the same time. Game default = what the wave says. NPCs Rage Engine keeps alive (dying moments, adrenaline, passing out) always count toward it.", valueSourceName = nameof(maxNpcValues), defaultValueIndex = 0)]
        public static string maxNpcs = "0";
        internal static int MaxNpcs42 { get { int.TryParse(maxNpcs, out int n); return Mathf.Clamp(n, 0, 30); } }

        // ------------------------------------------------------------------ gore-only sounds
        static readonly HashSet<string> nonGore42 = new HashSet<string> { "NRE_Punch", "NRE_BodyHit", "NRE_ThrowHit", "NRE_HeavyImpact", "NRE_Swoosh", "NRE_BodyFall", "NRE_MetalBang" };
        internal static bool GoreSound42(string key) => !nonGore42.Contains(key);

        // ------------------------------------------------------------------ error storms
        static readonly Dictionary<string, int> errCount42 = new Dictionary<string, int>();
        static readonly Dictionary<string, float> errMuteUntil42 = new Dictionary<string, float>();
        static float errWindow42;
        /// true = skip this feature for now (it has been throwing over and over)
        internal static bool Muted42(string feature) => errMuteUntil42.Count > 0 && errMuteUntil42.TryGetValue(feature, out float u) && Time.unscaledTime < u;
        internal static void NoteError42(string feature)
        {
            float now = Time.unscaledTime;
            if (now > errWindow42) { errWindow42 = now + 10f; errCount42.Clear(); }
            errCount42.TryGetValue(feature, out int n); errCount42[feature] = ++n;
            if (n == 40) { errMuteUntil42[feature] = now + 20f; Debug.LogWarning($"[RageEngine] '{feature}' keeps failing - paused for 20 s"); }
        }

        // ------------------------------------------------------------------ hits
        /// From HandleHit, after the other gore.
        void Gore42Hit(NpcState s, CollisionInstance hit, RagdollPart part, RagdollPart.Type t, float severity)
        {
            var c = s.creature;
            float now = Time.time;
            var dt = hit.damageStruct.damageType;
            // a beating while unarmed (counts toward pulling a knife)
            if (severity >= 0.08f)
            {
                if (now - s.beatAt42 > 8f) s.beatHits42 = 0;
                s.beatHits42++; s.beatAt42 = now;
            }
            if (uniqueGore && part != null && Has(t, RagdollPart.Type.Neck)) Guard("neckgrab", () => NeckGrab44(s, part, hit, severity));
            if (part != null && Has(t, RagdollPart.Type.Torso)) Guard("organs", () => Organs47(s, c, part, hit, severity));
            if (!uniqueGore || part == null || c.allEyes == null || !Has(t, RagdollPart.Type.Head)) return;
            Vector3 p = hit.contactPoint;
            for (int i = 0; i < c.allEyes.Count && i < 2; i++)
            {
                var e = c.allEyes[i];
                if (e == null || (s.gouged & (1 << i)) != 0) continue;
                float dist = Vector3.Distance(e.transform.position, p);
                if (dt == DamageType.Pierce && dist < 0.04f && severity >= 0.3f) { PopEye42(s, i, !projectileHit34, "a blade in the eye"); return; }
                if (dt == DamageType.Slash && dist < 0.035f && severity >= 0.5f) { PopEye42(s, i, false, "cut across the eye"); return; }
                if (dt == DamageType.Blunt && dist < 0.045f && severity >= 1.0f)
                {
                    s.eyeDmg[i] += severity * (bareHit34 ? 0.8f : 1f);
                    if (s.eyeDmg[i] >= 1.9f * Mathf.Sqrt(Tough33(s))) { PopEye42(s, i, true, "beaten out"); return; }
                }
            }
        }

        static Material eyeWhite42, irisMat42, nerveMat42;
        /// The eye is gone for good. hang = it comes out of the socket on the nerve.
        internal void PopEye42(NpcState s, int i, bool hang, string why)
        {
            var c = s.creature;
            var e = c.allEyes != null && i < c.allEyes.Count ? c.allEyes[i] : null;
            var head = c.ragdoll != null ? c.ragdoll.headPart : null;
            if (e == null || head == null || (s.gouged & (1 << i)) != 0) return;
            if (s.eyeScale == null || s.eyeScale.Length != c.allEyes.Count)
            {
                s.eyeScale = new Vector3[c.allEyes.Count];
                for (int k = 0; k < c.allEyes.Count; k++) if (c.allEyes[k] != null) s.eyeScale[k] = c.allEyes[k].transform.localScale;
            }
            s.gouged |= 1 << i;
            s.blindEyes |= 1 << i;
            Vector3 ep = e.transform.position;
            Vector3 outv = (ep - head.transform.position).normalized;
            e.transform.localScale = Vector3.zero;
            Guard("eye.hide", () => HideEye49(c, e, ep, head.transform));                              // 0.49: the socket is really empty
            Stamp(c, ep, 0.03f, "stab", 0.95f);
            Stamp(c, ep - Vector3.up * 0.035f, 0.04f, "blood_drip", 0.9f);
            Sfx.Play("NRE_Squelch", head.transform, 1f, 1.25f);
            Sfx.Play("NRE_GoreTear", head.transform, 0.5f, 1.3f);
            Guard("blood", () => RageEngineScript.Blood(head.transform));
            s.bleedRate += 0.15f * bleedingRate;
            s.pain = Mathf.Min(1f, s.pain + 0.6f);
            s.defSkill *= 0.6f;
            Scream(s, true);
            StartClutch(s, head, ep, null, false, false);
            Guard("vision", () => UpdateVision(s));
            Guard("eyes", () => { if (!livingEyes.Contains(s)) livingEyes.Add(s); });
            if (hang) Guard("eye.hang", () => HangEye42(head.transform, ep, outv, s));
            bool blind = AllBlind(s);
            if (blind) { s.blind42 = true; SetMelee(s, false, Time.time + 4f); }
            Log($"{WhoIs(s)}: EYE {(hang ? "POPPED OUT" : "DESTROYED")} ({why}){(blind ? " - BLIND" : "")}");
        }

        static readonly List<GameObject> eyes42 = new List<GameObject>();
        void HangEye42(Transform head, Vector3 ep, Vector3 outv, NpcState s)
        {
            var mesh = BoneModels.Get("RE_Eyeball");
            while (eyes42.Count > 0 && (eyes42.Count >= 8 || eyes42[0] == null)) { if (eyes42[0] != null) Object.Destroy(eyes42[0]); eyes42.RemoveAt(0); }
            var go = new GameObject("RageEngine_Eyeball");
            go.transform.position = ep + outv * 0.02f;
            if (eyeWhite42 == null) eyeWhite42 = Mats.Make(new Color(0.92f, 0.88f, 0.84f), 0.75f);
            if (irisMat42 == null) irisMat42 = Mats.Make(new Color(0.18f, 0.12f, 0.08f), 0.8f);
            if (nerveMat42 == null) nerveMat42 = Mats.Make(new Color(0.45f, 0.05f, 0.05f), 0.6f);
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = eyeWhite42; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                float sz = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
                if (sz > 0.0001f) go.transform.localScale = Vector3.one * (0.025f / sz);
            }
            else
            {
                var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(sph.GetComponent<Collider>());
                sph.transform.SetParent(go.transform, false); sph.transform.localScale = Vector3.one * 0.025f; sph.GetComponent<MeshRenderer>().sharedMaterial = eyeWhite42;
            }
            // the iris, facing away from the nerve
            var iris = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(iris.GetComponent<Collider>());
            iris.name = "Iris"; iris.transform.SetParent(go.transform, false);
            float inv = 1f / Mathf.Max(0.0001f, go.transform.localScale.x);
            iris.transform.localScale = new Vector3(0.012f, 0.012f, 0.005f) * inv; iris.transform.localPosition = Vector3.forward * 0.0115f * inv;
            iris.GetComponent<MeshRenderer>().sharedMaterial = irisMat42;
            var lineGo = new GameObject("RageEngine_Nerve");
            var lr = lineGo.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.useWorldSpace = true; lr.startWidth = 0.006f; lr.endWidth = 0.004f; lr.sharedMaterial = nerveMat42;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.SetPosition(0, ep); lr.SetPosition(1, go.transform.position);
            var d = go.AddComponent<EyeDangle42>();
            d.head = head; d.socketLocal = head.InverseTransformPoint(ep); d.nerve = lr; d.len = Random.Range(0.045f, 0.07f);
            d.dropAt = Time.time + Random.Range(6f, 18f);
            eyes42.Add(go); eyes42.Add(lineGo);
            Object.Destroy(lineGo, 60f);
        }

        // ------------------------------------------------------------------ 0.1 s tick
        void Tick42(NpcState s, float now)
        {
            var c = s.creature;
            if (c.isKilled || s.knockedOut || s.passedOut) return;
            var head = c.ragdoll != null ? c.ragdoll.headPart : null;
            // ---- empty hands? (fist fighting is off since 0.36, so nothing else tracks it - the knife needs it)
            if (!fistFighting)
            {
                if (HasRealWeapon42(s)) s.unarmedSince = 0f;
                else if (s.unarmedSince <= 0f && c.ragdoll.state == Ragdoll.State.Standing) s.unarmedSince = now;
            }
            // ---- the broken jaw gets held
            if (s.jawBroken && c.jaw != null && head != null && now >= s.nextJawHold42 && !s.clutching && c.ragdoll.state == Ragdoll.State.Standing)
            {
                s.nextJawHold42 = now + Random.Range(5f, 10f);
                Vector3 jp = c.jaw.position + (c.centerEyes != null ? c.centerEyes.forward : c.transform.forward) * 0.03f - Vector3.up * 0.02f;
                StartClutch(s, head, jp, null, false, false);
                if (s.clutching) s.clutchUntil = now + Random.Range(2.5f, 4.5f);
                if (Random.value < 0.4f && !Silent(s)) Speak(s, "muffled");
            }
            // ---- blind: wild swings at anything close, a hand to the face, no parrying
            if (s.blind42)
            {
                var pc = Player.local != null ? Player.local.creature : null;
                bool close = pc != null && (pc.transform.position - c.transform.position).sqrMagnitude < 1.4f * 1.4f;
                if (close && now >= s.nextBlindSwing42) { s.nextBlindSwing42 = now + Random.Range(1.2f, 2.5f); SetMelee(s, true, now + 1f); }
                else if (!close && now >= s.meleeOffUntil - 0.2f) SetMelee(s, false, now + 2f);
                Guard("blind.parry", () => { var d = c.brain.instance?.GetModule<BrainModuleDefense>(false); if (d != null) d.parryEnabled = false; });
                if (head != null && !s.clutching && now >= s.nextJawHold42 && Random.value < 0.15f)
                {
                    s.nextJawHold42 = now + Random.Range(3f, 6f);
                    var ce = c.centerEyes != null ? c.centerEyes.position : head.transform.position;
                    StartClutch(s, head, ce, null, false, false);
                }
            }
        }

        static bool HasRealWeapon42(NpcState s) => s.creature != null && (RealWeapon(s.creature, Side.Left) || RealWeapon(s.creature, Side.Right));

        // ------------------------------------------------------------------ more bone-break reactions
        internal void BoneReact42(NpcState s, RagdollPart part, Vector3 point, bool leg)
        {
            var c = s.creature;
            if (c.isKilled || s.knockedOut) return;
            float now = Time.time;
            float r = Random.value;
            bool tough = Tank(s) || Tough33(s) >= 2.5f;
            if (tough && r < 0.45f)
            {
                // grits his teeth and comes at you
                s.rushUntil = now + 3f; s.pain = Mathf.Min(s.pain, 0.6f);
                if (!Silent(s)) Speak(s, "attackmelee");
                Log($"{WhoIs(s)}: grits it out");
                return;
            }
            if (knockouts && passOut && !Tank(s) && r < 0.08f + 0.12f * (1f - HpFrac(c)))
            {
                PassOut(s, c.transform.forward * -1f, "the pain of the break");
                return;
            }
            if (!projectileHit34 && r < 0.2f) { s.nextVomit = Mathf.Min(s.nextVomit, now + Random.Range(1f, 2.5f)); s.gutShots = Mathf.Max(s.gutShots, 3f); }    // sick from the pain
            if (leg)
            {
                // down on the ground, holding the leg
                AllowFall(s, 2f);
                StartClutch(s, part, point, null, false, false);
                if (s.clutching) s.clutchUntil = now + Random.Range(3f, 5f);
                s.stayDown = Mathf.Max(s.stayDown, Random.Range(2f, 5f));
                Log($"{WhoIs(s)}: grabs the broken leg");
            }
            else
            {
                // cradles the arm with the good hand, stares at it
                StartClutch(s, part, point, null, false, false);
                if (s.clutching) s.clutchUntil = now + Random.Range(2.5f, 5f);
                s.lookAt = point; s.gazeTarget = point;
                Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Neck, 0.5f, 1.2f);
                Log($"{WhoIs(s)}: cradles the broken arm");
            }
            if (!Silent(s) && Random.value < 0.5f) Speak(s, "choke");
        }

        // ------------------------------------------------------------------ waves: revived NPCs count, max at once
        static readonly Dictionary<WaveData, int> waveMaxOrig42 = new Dictionary<WaveData, int>();
        static readonly Dictionary<WaveData.WaveFaction, int> facMaxOrig42 = new Dictionary<WaveData.WaveFaction, int>();
        static float nextWave42;

        void TickWaves42()
        {
            float now = Time.time;
            if (now < nextWave42) return;
            nextWave42 = now + 0.5f;
            var list = WaveSpawner.instances;
            if (list == null) return;
            int cap = MaxNpcs42;
            for (int w = 0; w < list.Count; w++)
            {
                var ws = list[w];
                if (ws == null) continue;
                // remember which spawner each of our NPCs belongs to
                if (ws.spawnedCreatures != null)
                    for (int k = 0; k < ws.spawnedCreatures.Count; k++)
                    {
                        var cr = ws.spawnedCreatures[k];
                        if (cr != null && !cr.isKilled && TryGet(cr, out var st)) st.wave42 = ws;
                    }
                var data = ws.currentLoadedData;
                if (data == null) continue;
                if (!waveMaxOrig42.ContainsKey(data)) waveMaxOrig42[data] = data.totalMaxAlive;
                int want = cap > 0 ? cap : waveMaxOrig42[data];
                if (data.totalMaxAlive != want) data.totalMaxAlive = want;
                if (data.factions != null)
                    foreach (var f in data.factions)
                    {
                        if (f == null) continue;
                        if (!facMaxOrig42.ContainsKey(f)) facMaxOrig42[f] = f.factionMaxAlive;
                        int fw = cap > 0 ? cap : facMaxOrig42[f];
                        if (f.factionMaxAlive != fw) f.factionMaxAlive = fw;
                    }
            }
        }

        /// The game counted this NPC dead and Rage Engine is bringing him back: put him back in his wave's count so the
        /// wave doesn't send a replacement on top of him.
        static void RejoinWave42(NpcState s)
        {
            var c = s.creature; var ws = s.wave42;
            if (c == null || ws == null || !ws.isRunning || ws.spawnedCreatures == null) return;
            if (ws.spawnedCreatures.Contains(c)) return;
            ws.spawnedCreatures.Add(c);
            var g = c.spawnGroup;
            if (g != null && ws.groupAliveCounts != null)
            {
                ws.groupAliveCounts.TryGetValue(g, out int n);
                ws.groupAliveCounts[g] = n + 1;
            }
            Log($"{WhoIs(s)}: back in the wave count");
        }

        /// How many enemies (not you, not allies) are alive right now.
        static int AliveEnemies42()
        {
            int n = 0; var all = Creature.allActive;
            if (all == null) return 0;
            for (int i = 0; i < all.Count; i++) { var c = all[i]; if (c != null && !c.isPlayer && !c.isKilled && c.factionId != 2) n++; }
            return n;
        }

        /// For Rage Engine's own spawn buttons: how many more may come in.
        internal static int SpawnRoom42(int asked)
        {
            int cap = MaxNpcs42;
            if (cap <= 0) return asked;
            return Mathf.Clamp(cap - AliveEnemies42(), 0, asked);
        }
    }
}

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        /// 0.42: the 20 Hz per-NPC update, every step on its own (one failing step no longer skips the rest), and no
        /// closure allocated per step per NPC (that was ~10,000 small allocations a second with a full wave - GC hitches).
        void TickNpc42(NpcState s, float dt)
        {
            float tn = Time.time;
            if (On("balance")) try { TickBalance(s, dt); } catch (System.Exception e) { Fail42("balance", e); }
            if (On("muscles")) try { TickMuscles(s); } catch (System.Exception e) { Fail42("muscles", e); }
            if (On("injuries")) try { TickInjuries(s, dt); } catch (System.Exception e) { Fail42("injuries", e); }
            if (On("wounds")) try { TickWounds(s, dt); } catch (System.Exception e) { Fail42("wounds", e); }
            if (On("musculature")) try { TickMusculature(s, dt); } catch (System.Exception e) { Fail42("musculature", e); }
            if (On("director")) try { TickDirector(s, dt); } catch (System.Exception e) { Fail42("director", e); }
            if (On("rnas")) try { TickRnas(s, dt); } catch (System.Exception e) { Fail42("rnas", e); }
            if (On("adapt")) try { TickAdapt(s, dt); } catch (System.Exception e) { Fail42("adapt", e); }
            if (On("awareness")) try { TickAwareness(s, dt); } catch (System.Exception e) { Fail42("awareness", e); }
            if (On("vanilla")) try { TickVanilla(s, tn); } catch (System.Exception e) { Fail42("vanilla", e); }
            if (s.creature == null) return;
            if (On("drunk")) try { TickDrunk(s, tn); } catch (System.Exception e) { Fail42("drunk", e); }
            if (On("steady")) try { TickSteady(s, tn); } catch (System.Exception e) { Fail42("steady", e); }
            if (On("pressure")) try { TickPressure(s, tn); } catch (System.Exception e) { Fail42("pressure", e); }
            if (On("sink")) try { TickSink(s, tn); } catch (System.Exception e) { Fail42("sink", e); }
            if (On("blood")) try { TickBlood(s, tn); } catch (System.Exception e) { Fail42("blood", e); }
            if (On("gore25t")) try { TickGore25(s, tn); } catch (System.Exception e) { Fail42("gore25t", e); }
            if (On("lab")) try { TickLab(s, tn); } catch (System.Exception e) { Fail42("lab", e); }
            if (On("takedowns")) try { TickTakedowns(s, tn); } catch (System.Exception e) { Fail42("takedowns", e); }
            if (On("integrity")) try { TickIntegrity(s, tn); } catch (System.Exception e) { Fail42("integrity", e); }
            if (On("defense")) try { TickDefense(s, tn); } catch (System.Exception e) { Fail42("defense", e); }
            if (On("toxin")) try { TickToxin(s, tn); } catch (System.Exception e) { Fail42("toxin", e); }
            if (On("bio")) try { TickBio(s, tn); } catch (System.Exception e) { Fail42("bio", e); }
            if (On("serum25")) try { TickSerum25(s, tn); } catch (System.Exception e) { Fail42("serum25", e); }
            if (On("groundfight")) try { TickGroundFight(s, tn); } catch (System.Exception e) { Fail42("groundfight", e); }
            if (On("salts")) try { TickSalts(s, tn); } catch (System.Exception e) { Fail42("salts", e); }
            if (On("tick28")) try { Tick28(s, tn); } catch (System.Exception e) { Fail42("tick28", e); }
            if (On("tick42")) try { Tick42(s, tn); } catch (System.Exception e) { Fail42("tick42", e); }
            if (On("tick43")) try { Tick43(s, tn); } catch (System.Exception e) { Fail42("tick43", e); }
            if (On("knockout")) try { TickKnockout(s, tn); } catch (System.Exception e) { Fail42("knockout", e); }
            if (On("pullout")) try { TickPullOut(s, tn); } catch (System.Exception e) { Fail42("pullout", e); }
            if (On("invite")) try { MaybeInviteFistfight(s, tn); } catch (System.Exception e) { Fail42("invite", e); }
            if (On("group")) try { TickGroup(s, tn); } catch (System.Exception e) { Fail42("group", e); }
            if (On("charge")) try { TickCharge(s, tn); } catch (System.Exception e) { Fail42("charge", e); }
            if (On("footwork")) try { TickArmedFootwork(s, tn); } catch (System.Exception e) { Fail42("footwork", e); }
            if (On("pointat")) try { TickPointAt(s, tn); } catch (System.Exception e) { Fail42("pointat", e); }
            if (On("stumble")) try { TickStumble(s, tn); } catch (System.Exception e) { Fail42("stumble", e); }
            if (On("dazed")) try { TickDazed(s, tn); } catch (System.Exception e) { Fail42("dazed", e); }
            if (On("settle")) try { TickSettle(s, tn); } catch (System.Exception e) { Fail42("settle", e); }
            if (On("navigation")) try { TickNavigation(s, dt, tn); } catch (System.Exception e) { Fail42("navigation", e); }
            if (On("choke")) try { TickChoke(s, dt); } catch (System.Exception e) { Fail42("choke", e); }
            if (On("trauma")) try { TickTrauma(s, dt); } catch (System.Exception e) { Fail42("trauma", e); }
            if (On("dying")) try { TickDying(s); } catch (System.Exception e) { Fail42("dying", e); }
            if (On("bruises")) try { TickBruises(s); } catch (System.Exception e) { Fail42("bruises", e); }
            if (On("forcephysic")) try { TickPhysics(s); } catch (System.Exception e) { Fail42("forcephysic", e); }
            if (On("fists")) try { TickFisticuffs(s); } catch (System.Exception e) { Fail42("fists", e); }
        }
    }
}

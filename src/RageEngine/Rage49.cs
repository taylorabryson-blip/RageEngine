// Rage Engine 0.49
//   RIP IT OUT       cut him open, put your hand in the slit and grab: chest -> heart or lung, belly -> liver / stomach /
//                    guts, back -> his spine. You hold it; let go to drop or throw it.
//   PUNCH THROUGH    a fast enough punch to the chest goes clean through (a hole front and back). He hangs on your arm,
//                    grabs at it, gasps; pull your arm back out and he dies.
//   ORGANS           come out of the wound where they actually sit (heart left chest, lungs high, liver right side,
//                    kidneys low back, guts low belly) and slide out instead of flying.
//   TAUNTS           healthy, cocky NPCs square up: "come on" beckon, arms out, the game's taunt voice (any voice pack's
//                    taunt lines play here too).
//   FEAR VOICES      begging / fleeing / surrendering NPCs use any surrender, beg, mercy, panic or fear lines that their
//                    voice pack has (voice-pack compatible), then the game's own panic scream.
//   OTHER GORE MODS  Gore tab -> "Gore & blood from: my other gore mod": Rage Engine stops all its blood, wounds, chunks
//                    and organs so your blood/gore mod does that part; the rest of Rage Engine keeps running.
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
        // ================================================================== settings
        public static ModOptionString[] goreSrcValues = { new ModOptionString("Rage Engine", null, "rage"), new ModOptionString("My other gore / blood mod", null, "other") };
        [ModOptionCategory("Gore", 1), ModOptionOrder(0)]
        [ModOption("Gore & blood from", "Rage Engine: all of Rage Engine's blood, wounds, chunks and organs. My other gore mod: Rage Engine turns its gore off so your blood/gore mod handles it - NPC behaviour, standing, AI, waves, maps and the rest still run.", valueSourceName = nameof(goreSrcValues), defaultValueIndex = 0)]
        public static string goreSource = "rage";
        internal static bool ExternalGore => goreSource == "other";

        [ModOptionCategory("You", 3), ModOptionOrder(10)]
        [ModOption("Rip organs out by hand", "Cut him open, put your hand in the wound and grab: chest = heart or lung, belly = liver, stomach, guts, back = spine.", defaultValueIndex = 1)]
        public static bool ripOrgans49 = true;

        [ModOptionCategory("You", 3), ModOptionOrder(11)]
        [ModOption("Punch through chest", "A really fast punch to the chest goes through. He hangs on your arm until you pull it out.", defaultValueIndex = 1)]
        public static bool punchThrough49 = true;

        [ModOptionCategory("NPCs", 2), ModOptionOrder(20)]
        [ModOption("Taunts", "Cocky NPCs square up, beckon you in and taunt (voice packs' taunt lines play too).", defaultValueIndex = 1)]
        public static bool taunts49 = true;

        [ModOptionCategory("NPCs", 2), ModOptionOrder(21)]
        [ModOption("Fear & surrender voices", "Begging, fleeing and surrendering NPCs use the surrender / beg / panic lines of whatever voice pack they have.", defaultValueIndex = 1)]
        public static bool fearVoices49 = true;

        // ================================================================== organs: where they sit
        /// 0 chest, 1 belly, 2 back
        static int Region49(Creature c, RagdollPart chest, Vector3 p)
        {
            Vector3 d = p - chest.transform.position;
            if (Vector3.Dot(d, c.transform.forward) < -0.03f) return 2;
            return Vector3.Dot(d, c.transform.up) <= 0.02f ? 1 : 0;
        }

        static string OrganAt49(Creature c, RagdollPart chest, Vector3 p, int region)
        {
            Vector3 d = p - chest.transform.position;
            float side = Vector3.Dot(d, c.transform.right);                    // >0 = his right
            float up = Vector3.Dot(d, c.transform.up);
            switch (region)
            {
                case 0: return side < 0.04f && up < 0.12f && Random.value < 0.7f ? "RE_Organ_Heart" : "RE_Organ_Lung";
                case 2: return up > -0.05f ? "RE_Organ_Lung" : side < -0.07f && up > -0.12f ? "RE_Organ_Spleen" : "RE_Organ_Kidney";
                default:
                    if (up < -0.2f && Mathf.Abs(side) < 0.07f) return "RE_Organ_Bladder";
                    if (side > 0.03f && up > -0.08f) return "RE_Organ_Liver";
                    if (side < -0.09f && up > -0.06f) return Random.value < 0.6f ? "RE_Organ_Spleen" : "RE_Organ_Stomach";
                    if (side < -0.02f && up > -0.08f) return "RE_Organ_Stomach";
                    return Random.value < 0.6f ? "RE_Organ_Intestine" : "RE_Organ_Gut";
            }
        }

        /// An organ that slides out of the wound (not flung across the room). Returns it.
        static GameObject Organ49(string name, Vector3 at, Vector3 outward, Creature c, float push)
        {
            var mesh = BoneModels.Get(name) ?? BoneModels.Get(name == "RE_Organ_Stomach" || name == "RE_Organ_Intestine" ? "RE_Organ_Gut" : "RE_Organ_Kidney");
            if (mesh == null) return null;
            while (goreModels.Count > 0 && (goreModels.Count >= PerfGorePieces || goreModels[0] == null)) { if (goreModels[0] != null) Object.Destroy(goreModels[0]); goreModels.RemoveAt(0); }
            var go = new GameObject("RageEngine_Organ");
            go.transform.position = at + outward * 0.02f;
            go.transform.rotation = Quaternion.LookRotation(outward, Random.onUnitSphere);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = organMat44(name); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var bc = go.AddComponent<BoxCollider>(); bc.size = mesh.bounds.size; bc.center = mesh.bounds.center;
            if (c != null && c.ragdoll != null) foreach (var b in c.ragdoll.GetComponentsInChildren<Collider>()) if (b != null) Physics.IgnoreCollision(bc, b, true);
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.3f; rb.drag = 0.6f; rb.angularDrag = 2f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.velocity = outward * push + Vector3.down * 0.3f;
            rb.angularVelocity = Random.insideUnitSphere * 2f;
            goreModels.Add(go); Object.Destroy(go, 90f);
            return go;
        }

        // ================================================================== open wounds you can reach into
        sealed class Wound49 { public Creature c; public RagdollPart part; public Vector3 local; public int region; public float at; public int taken; public int life; }   // 0.54: life = whose wound
        static readonly List<Wound49> wounds49 = new List<Wound49>();
        static readonly Dictionary<Creature, int> takenFrom49 = new Dictionary<Creature, int>();   // bit 1 heart, 2 spine

        internal static void AddWound49(Creature c, RagdollPart part, Vector3 p)
        {
            if (!ripOrgans49 || c == null || part == null) return;
            var chest = ChestPart(c) ?? part;
            int region = Region49(c, chest, p);
            int life = LifeOf54(c);
            foreach (var w in wounds49)
                if (w.c == c && w.life == life && w.part == part && (w.part.transform.TransformPoint(w.local) - p).sqrMagnitude < 0.06f * 0.06f) { w.at = Time.time; return; }
            if (wounds49.Count >= 24) wounds49.RemoveAt(0);
            wounds49.Add(new Wound49 { c = c, part = part, local = part.transform.InverseTransformPoint(p), region = region, at = Time.time, life = life });
        }

        sealed class Held49 { public GameObject go; public RagdollHand hand; public float at; }
        readonly Held49[] held49 = new Held49[2];
        readonly bool[] gripWas49 = new bool[2];

        void TickRip49()
        {
            for (int i = wounds49.Count - 1; i >= 0; i--)
            {
                var w = wounds49[i];
                if (w.c == null || w.part == null || Time.time - w.at > 180f || w.taken >= 3 || !Same54(w.c, w.life)) wounds49.RemoveAt(i);   // 0.54: his body went to someone else
            }
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) return;
            for (int k = 0; k < 2; k++)
            {
                var hand = k == 0 ? pc.handLeft : pc.handRight;
                if (hand == null) continue;
                bool grip = false;
                try { grip = hand.playerHand != null && hand.playerHand.controlHand != null && hand.playerHand.controlHand.gripPressed; } catch { }
                bool pressed = grip && !gripWas49[k];
                gripWas49[k] = grip;
                var h = held49[k];
                if (h != null)
                {
                    if (h.go == null) { held49[k] = null; continue; }
                    if (!grip) { Release49(k); continue; }
                    if (Time.time - h.at < 0.5f && hand.grabbedHandle is HandleRagdoll) { var hh = hand; Guard("ungrab", () => hh.UnGrab(false)); }   // the game grabbed his body too
                    continue;
                }
                if (!pressed || !ripOrgans49 || wounds49.Count == 0) continue;
                Vector3 palm = hand.grip != null ? hand.grip.position : hand.transform.position;
                Wound49 best = null; float bd = 0.13f * 0.13f;
                foreach (var w in wounds49)
                {
                    if (!Same54(w.c, w.life)) continue;                                 // 0.54
                    float d2 = (w.part.transform.TransformPoint(w.local) - palm).sqrMagnitude;
                    if (d2 < bd) { bd = d2; best = w; }
                }
                if (best == null) continue;
                var gh = hand.grabbedHandle;
                if (gh != null && !(gh is HandleRagdoll hr && hr.ragdollPart != null && hr.ragdollPart.ragdoll != null && hr.ragdollPart.ragdoll.creature == best.c)) continue;
                var bw = best; var bh = hand;
                Guard("rip", () => Rip49(bw, bh, k));
            }
        }

        void Rip49(Wound49 w, RagdollHand hand, int k)
        {
            var c = w.c;
            var chest = ChestPart(c) ?? w.part;
            Vector3 p = w.part.transform.TransformPoint(w.local);
            takenFrom49.TryGetValue(c, out int taken);
            string name;
            bool spine = false;
            if (w.region == 2 && (taken & 2) == 0) { spine = true; name = "RE_Bone_Vertebra"; }
            else
            {
                name = OrganAt49(c, chest, p, w.region == 2 ? 0 : w.region);
                if (name == "RE_Organ_Heart" && (taken & 1) != 0) name = "RE_Organ_Lung";
            }
            if (hand.grabbedHandle != null) hand.UnGrab(false);
            GameObject go;
            if (spine)
            {
                go = new GameObject("RageEngine_Spine");
                var vm = BoneModels.Get("RE_Bone_Vertebra"); var mat = BoneMat();
                for (int i = 0; i < 9; i++)
                {
                    var v = new GameObject("V"); v.transform.SetParent(go.transform, false);
                    v.transform.localPosition = Vector3.up * (i * 0.035f - 0.14f);
                    v.transform.localRotation = Quaternion.Euler(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                    v.transform.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.8f, i / 8f);
                    if (vm != null) { v.AddComponent<MeshFilter>().sharedMesh = vm; var vr = v.AddComponent<MeshRenderer>(); vr.sharedMaterial = mat; vr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                }
                var cord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(cord.GetComponent<Collider>());
                cord.transform.SetParent(go.transform, false); cord.transform.localPosition = Vector3.up * 0.0f + Vector3.forward * -0.012f; cord.transform.localScale = new Vector3(0.012f, 0.17f, 0.012f);
                cord.GetComponent<Renderer>().sharedMaterial = organMat44("RE_Organ_Gut");
                var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(0.05f, 0.33f, 0.05f);
                foreach (var b in c.ragdoll.GetComponentsInChildren<Collider>()) if (b != null) Physics.IgnoreCollision(bc, b, true);
                var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.6f;
                goreModels.Add(go); Object.Destroy(go, 120f);
                takenFrom49[c] = taken | 2;
            }
            else
            {
                go = Organ49(name, p, (p - chest.transform.position).normalized, c, 0f);
                if (go == null) return;
                if (name == "RE_Organ_Heart") takenFrom49[c] = taken | 1;
            }
            if (go == null) return;
            // in your hand
            var rbH = go.GetComponent<Rigidbody>(); if (rbH != null) rbH.isKinematic = true;
            foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;
            go.transform.SetParent(hand.transform, true);
            Vector3 palm = hand.grip != null ? hand.grip.position : hand.transform.position;
            go.transform.position = palm;
            held49[k] = new Held49 { go = go, hand = hand, at = Time.time };
            w.taken++;
            // the body
            Stamp(c, p, 0.08f, "torn_flesh", 1f);
            for (int i = 0; i < 4; i++) Guard("blood", () => RageEngineScript.Blood(w.part.transform));
            Sfx.Play("NRE_GoreTear", w.part.transform, 1f); Sfx.Play("NRE_Squelch", w.part.transform, 1f, 0.85f);
            if (spine) Sfx.Play("NRE_SpineSnap", w.part.transform, 1f);
            if (breakHaptics) Haptics.Pulse(hand.side, 1f, 3);
            TryGet(c, out var s);
            if (s != null) { s.pain = 1f; s.bleedRate += 1.2f * bleedingRate; }
            if (spine || name == "RE_Organ_Heart")
            {
                if (s != null) { s.finalKill = true; s.dying = false; s.noGasp = spine; if (!spine) Scream(s, true); AllowFallAlways(s, 5f); }
                if (spine) { if (!c.isKilled) Guard("kill", () => c.Kill()); }
                else pendingKill49.Add((c, Time.time + Random.Range(0.8f, 2f), LifeOf54(c)));
            }
            else if (s != null)
            {
                Scream(s, true);
                if (name == "RE_Organ_Lung") Speak(s, "choke");
                StartClutch(s, w.part, p, null, false, true); if (s.clutching) s.clutchUntil = Time.time + 4f;
                if (Random.value < 0.35f) pendingKill49.Add((c, Time.time + Random.Range(4f, 9f), LifeOf54(c)));
            }
            RageMoment(spine ? "spine ripped out" : "organ ripped out");
            Log($"{c.name}: {(spine ? "SPINE" : name.Substring(9).ToUpper())} RIPPED OUT by hand");
        }

        static Material boneMat49;
        static Material BoneMat() => boneMat49 != null ? boneMat49 : boneMat49 = Mats.Make(new Color(0.88f, 0.84f, 0.74f), 0.4f);

        void Release49(int k)
        {
            var h = held49[k]; held49[k] = null;
            if (h == null || h.go == null) return;
            Vector3 v = Vector3.zero;
            try { if (h.hand != null && h.hand.physicBody != null) v = h.hand.physicBody.velocity; } catch { }
            h.go.transform.SetParent(null, true);
            foreach (var col in h.go.GetComponentsInChildren<Collider>()) col.enabled = true;
            var rb = h.go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = false; rb.velocity = v; rb.angularVelocity = Random.insideUnitSphere * 3f; }
        }

        static readonly List<(Creature c, float at, int life)> pendingKill49 = new List<(Creature, float, int)>();   // 0.54: life = who it was meant for
        static void TickPendingKill49()
        {
            for (int i = pendingKill49.Count - 1; i >= 0; i--)
            {
                var (c, at, life) = pendingKill49[i];
                if (c == null || c.isKilled || !Same54(c, life)) { pendingKill49.RemoveAt(i); continue; }   // 0.54: never kills the next NPC in that body
                if (Time.time < at) continue;
                pendingKill49.RemoveAt(i);
                var cc = c; Guard("kill", () => cc.Kill());
            }
        }

        // ================================================================== punch through the chest
        sealed class Impale49
        {
            public NpcState s; public Creature c; public RagdollHand hand; public RagdollPart chest; public int gen, life;   // 0.54
            public Vector3 frontLocal, normalLocal, offsetInHand;
            public float start; public bool done; public float restoreAt;
            public readonly List<(Collider a, Collider b)> ignored = new List<(Collider, Collider)>();
            public Transform graspL, graspR;
        }
        static readonly List<Impale49> impales49 = new List<Impale49>();

        /// Called from the hit (Combat25Hit) for a player's fist to the torso.
        bool TryPunchThrough49(NpcState s, RagdollPart ppart, RagdollPart part, Vector3 point, Vector3 dir, float speed)
        {
            if (!punchThrough49 || s.impaled49 || speed < punchThroughSpeed || Tank(s) || s.bioNoQuit) return false;
            var pc = ppart.ragdoll.creature;
            var hand = ppart as RagdollHand;
            if (hand == null && pc != null)
                hand = pc.handLeft != null && pc.handRight != null
                     ? ((pc.handLeft.transform.position - point).sqrMagnitude < (pc.handRight.transform.position - point).sqrMagnitude ? pc.handLeft : pc.handRight)
                     : pc.handRight ?? pc.handLeft;
            if (hand == null || hand.grabbedHandle != null) return false;
            var c = s.creature;
            var chest = ChestPart(c) ?? part;
            var im = new Impale49 { s = s, c = c, hand = hand, chest = chest, start = Time.time, gen = s.gen54, life = LifeOf54(c) };
            Vector3 n = -dir.normalized;
            im.frontLocal = chest.transform.InverseTransformPoint(point);
            im.normalLocal = chest.transform.InverseTransformDirection(n);
            im.offsetInHand = hand.transform.InverseTransformPoint(chest.transform.position);
            // your arm passes through his body
            var mine = new List<Collider>();
            foreach (var rp in pc.ragdoll.parts)
            {
                if (rp == null) continue;
                bool same = hand.side == Side.Left ? Has(rp.type, RagdollPart.Type.LeftHand | RagdollPart.Type.LeftArm) : Has(rp.type, RagdollPart.Type.RightHand | RagdollPart.Type.RightArm);
                if (same) mine.AddRange(rp.GetComponentsInChildren<Collider>(true));
            }
            foreach (var rp in c.ragdoll.parts)
            {
                if (rp == null || !Has(rp.type, RagdollPart.Type.Torso | RagdollPart.Type.Neck | ArmParts)) continue;
                foreach (var b in rp.GetComponentsInChildren<Collider>(true))
                    foreach (var a in mine) if (a != null && b != null) { Physics.IgnoreCollision(a, b, true); im.ignored.Add((a, b)); }
            }
            // the holes: a ring of torn flesh front and back with your arm through it
            Vector3 back = chest.transform.position + dir.normalized * 0.13f;
            Hole49(chest.transform, point, n);
            Hole49(chest.transform, back, -n);
            if (!ExternalGore)
            {
                Stamp(c, point, 0.08f, "laceration_deep", 1f); Stamp(c, back, 0.1f, "laceration_deep", 1f);
                for (int i = 0; i < 6; i++) Guard("blood", () => RageEngineScript.Blood(chest.transform));
                Guard("ground", () => GroundBlood(back + dir * 0.4f, "blood_ground_splat", 0.5f, c));
            }
            Sfx.Play("NRE_RibCrack", chest.transform, 1f); Sfx.Play("NRE_Squelch", chest.transform, 1f, 0.8f); Sfx.Play("NRE_GoreTear", chest.transform, 1f);
            if (breakHaptics) Haptics.Pulse(hand.side, 1f, 4);
            s.impaled49 = true; s.pain = 1f; s.finalKill = true; s.noGasp = false;
            SetMelee(s, false, Time.time + 99f);
            Guard("loco", () => { if (c.locomotion != null) c.locomotion.allowMove = false; });
            Scream(s, true);
            // his hands go to your arm
            im.graspL = new GameObject("RageEngine_GraspL").transform; im.graspR = new GameObject("RageEngine_GraspR").transform;
            foreach (var sd in new[] { Side.Left, Side.Right })
            {
                var nh = Hand(c, sd); if (nh != null && nh.grabbedHandle != null) { var hh = nh; Guard("drop", () => hh.UnGrab(false)); }
                var ik = c.ragdoll.ik; var a = sd == Side.Left ? im.graspL : im.graspR;
                if (ik != null && !Dead(s, sd)) Guard("ik", () => { ik.SetHandAnchor(sd, a); ik.SetHandState(sd, true, false); });
            }
            impales49.Add(im);
            RageMoment("punch-through");
            Log($"{WhoIs(s)}: PUNCHED THROUGH - hanging on your arm ({speed:0.0} m/s)");
            return true;
        }

        static Material holeFlesh49, holeDark49;
        static readonly List<GameObject> holes49 = new List<GameObject>();
        static void Hole49(Transform parent, Vector3 at, Vector3 outward)
        {
            if (holeFlesh49 == null) { holeFlesh49 = Mats.Make(new Color(0.55f, 0.07f, 0.07f), 0.85f); holeDark49 = Mats.Make(new Color(0.12f, 0.01f, 0.01f), 0.6f); }
            while (holes49.Count >= 12) { if (holes49[0] != null) Object.Destroy(holes49[0]); holes49.RemoveAt(0); }
            var go = new GameObject("RageEngine_Hole");
            go.transform.SetParent(parent, false);
            go.transform.position = at + outward * 0.006f;
            go.transform.rotation = Quaternion.LookRotation(outward);
            var ring = new GameObject("Ring"); ring.transform.SetParent(go.transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = Torus49();
            var rr = ring.AddComponent<MeshRenderer>(); rr.sharedMaterial = holeFlesh49; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(go.transform, false); disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localPosition = Vector3.forward * -0.012f; disc.transform.localScale = new Vector3(0.1f, 0.012f, 0.1f);
            disc.GetComponent<Renderer>().sharedMaterial = holeDark49;
            holes49.Add(go); Object.Destroy(go, 240f);
        }

        static Mesh torus49;
        static Mesh Torus49()
        {
            if (torus49 != null) return torus49;
            const int seg = 24, side = 10; const float R = 0.06f, r = 0.02f;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg; Vector3 ca = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int j = 0; j <= side; j++)
                {
                    float b = j * Mathf.PI * 2f / side;
                    float jag = 1f + 0.18f * Mathf.Sin(a * 7f + j) * Mathf.Sin(a * 3f);   // torn, not a perfect donut
                    v.Add(ca * (R + r * jag * Mathf.Cos(b)) + Vector3.forward * (r * 0.8f * Mathf.Sin(b)));
                }
            }
            for (int i = 0; i < seg; i++) for (int j = 0; j < side; j++)
                {
                    int a0 = i * (side + 1) + j, a1 = a0 + side + 1;
                    t.Add(a0); t.Add(a1); t.Add(a0 + 1); t.Add(a0 + 1); t.Add(a1); t.Add(a1 + 1);
                }
            torus49 = new Mesh { name = "RE_Hole" }; torus49.SetVertices(v); torus49.SetTriangles(t, 0); torus49.RecalculateNormals(); torus49.RecalculateBounds();
            return torus49;
        }

        void TickImpale49()
        {
            for (int i = impales49.Count - 1; i >= 0; i--)
            {
                var im = impales49[i];
                if (im.done)
                {
                    if (Time.time < im.restoreAt) continue;
                    foreach (var (a, b) in im.ignored) if (a != null && b != null) Physics.IgnoreCollision(a, b, false);
                    if (im.graspL != null) Object.Destroy(im.graspL.gameObject); if (im.graspR != null) Object.Destroy(im.graspR.gameObject);
                    impales49.RemoveAt(i); continue;
                }
                var c = im.c;
                if (c != null && (!Same54(c, im.life) || !Owns54(im.s, im.gen)))
                {   // 0.54: his body went to someone else - let go without touching the new NPC
                    foreach (var (a, b) in im.ignored) if (a != null && b != null) Physics.IgnoreCollision(a, b, false);
                    if (im.graspL != null) Object.Destroy(im.graspL.gameObject); if (im.graspR != null) Object.Destroy(im.graspR.gameObject);
                    impales49.RemoveAt(i); continue;
                }
                if (c == null || im.hand == null || im.chest == null || im.s.creature != c) { Finish49(im, false); continue; }
                Vector3 front = im.chest.transform.TransformPoint(im.frontLocal), n = im.chest.transform.TransformDirection(im.normalLocal);
                Vector3 palm = im.hand.transform.position;
                float age = Time.time - im.start;
                // pulled your arm out (back past the entry hole) -> he dies
                if (age > 0.5f && Vector3.Dot(palm - front, n) > 0.06f) { Finish49(im, true); continue; }
                if (c.isKilled || age > 20f) { Finish49(im, !c.isKilled); continue; }
                // he hangs on your arm: the chest follows your fist
                Vector3 target = im.hand.transform.TransformPoint(im.offsetInHand);
                if (im.chest.physicBody != null)
                {
                    Vector3 d = target - im.chest.transform.position;
                    im.chest.physicBody.velocity = Vector3.ClampMagnitude(d * 14f, 6f) + Random.insideUnitSphere * 0.15f;   // the shudder
                }
                // his hands clutch your forearm
                Vector3 up = im.hand.transform.position - n * 0.12f;
                Vector3 r = Vector3.Cross(n, Vector3.up).normalized;
                if (im.graspL != null) { im.graspL.position = up + r * 0.04f + Vector3.up * 0.02f; im.graspL.rotation = Quaternion.LookRotation(-n); }
                if (im.graspR != null) { im.graspR.position = up - r * 0.04f - n * 0.05f; im.graspR.rotation = Quaternion.LookRotation(-n); }
                if (!ExternalGore && Random.value < Time.deltaTime * 2f) Guard("blood", () => RageEngineScript.Blood(im.chest.transform));
                if (Random.value < Time.deltaTime * 0.6f) Speak(im.s, "choke");
            }
        }

        void Finish49(Impale49 im, bool ripOut)
        {
            im.done = true; im.restoreAt = Time.time + 1.2f;
            var s = im.s; var c = im.c;
            if (s != null) s.impaled49 = false;
            if (c == null) return;
            var ik = c.ragdoll != null ? c.ragdoll.ik : null;
            if (ik != null) foreach (var sd in new[] { Side.Left, Side.Right }) { var side = sd; Guard("ik", () => { ik.SetHandAnchor(side, null); ik.SetHandState(side, false, false); }); }
            Guard("loco", () => { if (c.locomotion != null) c.locomotion.allowMove = true; });
            if (ripOut && im.chest != null)
            {
                Vector3 front = im.chest.transform.TransformPoint(im.frontLocal), n = im.chest.transform.TransformDirection(im.normalLocal);
                if (!ExternalGore)
                {
                    Guard("spray", () => Spray28(front, n + Vector3.down * 0.2f, new Color(0.45f, 0.02f, 0.02f), 60, 3.5f));
                    for (int k = 0; k < 6; k++) Guard("blood", () => RageEngineScript.Blood(im.chest.transform));
                    Guard("ground", () => GroundBlood(front + n * 0.3f, "blood_ground_splat", 0.7f, c));
                }
                Sfx.Play("NRE_GoreTear", im.chest.transform, 1f); Sfx.Play("NRE_Squelch", im.chest.transform, 1f, 0.75f);
                if (im.hand != null && breakHaptics) Haptics.Pulse(im.hand.side, 1f, 3);
                if (s != null) { s.finalKill = true; s.dying = false; AllowFallAlways(s, 5f); }
                if (!c.isKilled) Guard("kill", () => c.Kill());
                Log($"{c.name}: you pull your arm out - he drops");
            }
        }

        // ================================================================== taunts
        void Taunt49(NpcState s, float now)
        {
            var c = s.creature;
            if (!taunts49 || now < s.nextTaunt49 || s.fleeing || s.surrendered || s.ally || s.knockedOut || s.impaled49 || s.clutching
                || c.ragdoll.state != Ragdoll.State.Standing || HpFrac(c) < 0.5f || now - s.seenAt > 2f) return;
            var pc = Player.local != null ? Player.local.creature : null; if (pc == null) return;
            float d = Vector3.Distance(pc.transform.position, c.transform.position);
            if (d < 1.8f || d > 6.5f) return;
            var type = s.personality.type;
            float chance = type == PersonalityType.Aggressive ? 0.55f : type == PersonalityType.Confident ? 0.45f : type == PersonalityType.Unpredictable ? 0.35f : s.brawler ? 0.4f : 0.12f;
            s.nextTaunt49 = now + Random.Range(7f, 16f);
            if (Random.value > chance) return;
            Speak(s, "taunt");
            bool armed = HasRealWeapon42(s);
            int pose = armed ? 4 : (Random.value < 0.5f ? 4 : 5);           // 4 = "come on" beckon, 5 = arms out "hit me"
            SetMelee(s, false, now + 1.8f);
            Gesture43(s, pose, Random.Range(1.6f, 2.4f));
            Guard("face", () => c.brain.instance?.GetModule<BrainModuleExpression>(false)?.SetFaceExpression(FaceAnimator.Expression.Angry, 0, null));
            AddTag(s, Tag.Anger, 6f, 0.5f);
            Log($"{WhoIs(s)}: taunts you ({(pose == 4 ? "come on" : "hit me")})");
        }

        // ================================================================== fear / surrender voices (voice-pack aware)
        static readonly Regex fearRx49 = new Regex("surrender|beg|mercy|plead|please|spare|yield|panic|fear|scared|afraid|flee|cower|terror|scream", RegexOptions.IgnoreCase);
        static readonly Dictionary<VoiceData, string[]> fearIds49 = new Dictionary<VoiceData, string[]>();

        internal static void FearVoice49(NpcState s, bool force = false)
        {
            if (!fearVoices49) { Scream(s, force); return; }
            if (!force && Time.time < s.nextFearVoice49) return;
            s.nextFearVoice49 = Time.time + Random.Range(2.5f, 5f);
            bool played = false;
            Guard("fearvoice", () =>
            {
                var speak = s.creature.brain.instance?.GetModule<BrainModuleSpeak>(false);
                var vd = speak != null ? speak.voiceData : null;
                if (vd != null && vd.dialogs != null)
                {
                    if (!fearIds49.TryGetValue(vd, out var ids))
                    {
                        var l = new List<string>();
                        foreach (var dl in vd.dialogs) if (dl != null && !string.IsNullOrEmpty(dl.id) && fearRx49.IsMatch(dl.id)) l.Add(dl.id);
                        fearIds49[vd] = ids = l.ToArray();
                        if (ids.Length > 0) Debug.Log($"[RageEngine] voice '{vd.id}': fear/surrender lines -> {string.Join(", ", ids)}");
                    }
                    if (ids.Length > 0) { speak.Play(ids[Random.Range(0, ids.Length)], true, false); played = true; return; }
                }
                var fear = s.creature.brain.instance?.GetModule<BrainModuleFear>(false);
                if (speak != null && fear != null && !string.IsNullOrEmpty(fear.panicScreamDialogID)) { speak.Play(fear.panicScreamDialogID, true, false); played = true; }
            });
            if (!played) Scream(s, force);
        }

        // ================================================================== per-NPC (from Tick43) and per-frame
        void Tick49(NpcState s, float now)
        {
            var c = s.creature;
            if (c.isKilled) return;
            Taunt49(s, now);
            MapReach49(s, now);
            if ((s.fleeing || s.surrendered) && !s.knockedOut && now >= s.nextFearVoice49) FearVoice49(s);
        }

        void Frame49()
        {
            if (impales49.Count > 0) Guard("impale", TickImpale49);
            Guard("rip", TickRip49);
            if (pendingKill49.Count > 0) TickPendingKill49();
            Guard("music50", TickWaveMusic50);
            Frame51();
        }
    }
}

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ================================================================== maps: NPCs that can't reach you
        /// On a Rage Engine map: an NPC on another floor with no walkable path to you for ~6 s is moved to a spawn
        /// point on your floor, out of your face. Stops NPCs being stranded up top or down below.
        static readonly UnityEngine.AI.NavMeshPath path49 = new UnityEngine.AI.NavMeshPath();
        void MapReach49(NpcState s, float now)
        {
            if (!mapRescue51 || map26Root == null || map26Spawns.Count == 0 || now < s.nextReach49) return;
            s.nextReach49 = now + 2f;
            var c = s.creature;
            if (c.isKilled || s.knockedOut || s.ally || s.impaled49 || c.ragdoll.state != Ragdoll.State.Standing) { s.stuckReach49 = 0; return; }
            var pc = Player.local != null ? Player.local.creature : null; if (pc == null) return;
            Vector3 me = c.transform.position, you = pc.transform.position;
            if (me.y < map26Origin.y - 20f) return;                                         // not on the map
            float dy = Mathf.Abs(me.y - you.y);
            if (dy < 2f) { s.stuckReach49 = 0; return; }
            bool ok = false;
            try
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(me, out var a, 2f, UnityEngine.AI.NavMesh.AllAreas) && UnityEngine.AI.NavMesh.SamplePosition(you, out var b, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    ok = UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, path49) && path49.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
            }
            catch { ok = true; }
            if (ok) { s.stuckReach49 = 0; return; }
            if (++s.stuckReach49 < 3) return;
            s.stuckReach49 = 0;
            Vector3 best = Vector3.zero; float bd = float.MaxValue;
            foreach (var sp in map26Spawns)
            {
                Vector3 w = map26Origin + sp;
                if (Mathf.Abs(w.y - you.y) > 1.6f) continue;
                float d = Vector3.Distance(w, you);
                if (d < 6f) continue;
                float score = Mathf.Abs(d - 10f) + Random.value * 3f;
                if (score < bd) { bd = score; best = w; }
            }
            if (bd == float.MaxValue) return;
            var dest = best + Vector3.up * 0.1f; var rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(you - best, Vector3.up).normalized + Vector3.forward * 1e-4f);
            Guard("reach.tp", () => c.Teleport(dest, rot));
            Log($"{WhoIs(s)}: no way to reach you - moved to your floor");
        }
    }
}

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        // ================================================================== an empty eye socket
        /// 0.49: the eyeball itself is gone from the socket - every eye transform and eye bone on that side is collapsed
        /// (re-applied every frame, the animator can't put it back) and a dark wet hole sits where it was.
        sealed class EyeHide49 { public Creature c; public List<(Transform t, Vector3 scale)> parts = new List<(Transform, Vector3)>(); public GameObject hole; public int life; }   // 0.54: life = whose eye
        static readonly List<EyeHide49> eyeHides49 = new List<EyeHide49>();
        static Material socketMat49;

        internal static void HideEye49(Creature c, CreatureEye e, Vector3 ep, Transform head)
        {
            if (c == null || e == null) return;
            var h = new EyeHide49 { c = c, life = LifeOf54(c) };
            var seen = new HashSet<Transform>();
            void Add(Transform t) { if (t != null && seen.Add(t)) h.parts.Add((t, t.localScale)); }
            Add(e.transform);
            if (e.eyeParts != null)
                foreach (var p in e.eyeParts)
                {
                    if (p == null || p.transform == null) continue;
                    string n = (p.name ?? p.transform.name ?? "").ToLowerInvariant();
                    if (n.Contains("lid") || n.Contains("lash") || n.Contains("brow")) continue;
                    Add(p.transform);
                }
            // eye bones of the head mesh on this side (some heads skin the eyeball to a bone, not the CreatureEye)
            foreach (var sm in c.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sm == null || sm.bones == null) continue;
                foreach (var b in sm.bones)
                {
                    if (b == null) continue;
                    string n = b.name.ToLowerInvariant();
                    if (!n.Contains("eye") || n.Contains("lid") || n.Contains("lash") || n.Contains("brow")) continue;
                    if ((b.position - ep).sqrMagnitude < 0.025f * 0.025f) Add(b);
                }
            }
            if (head != null)
            {
                if (socketMat49 == null) socketMat49 = Mats.Make(new Color(0.16f, 0.01f, 0.015f), 0.9f);
                var hole = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(hole.GetComponent<Collider>());
                hole.name = "RageEngine_EmptySocket";
                hole.transform.SetParent(head, true);
                Vector3 outv = (ep - head.position).normalized;
                hole.transform.position = ep - outv * 0.004f;
                hole.transform.rotation = Quaternion.LookRotation(outv);
                hole.transform.localScale = Vector3.one;
                Vector3 ls = head.lossyScale; float k = 1f / Mathf.Max(0.0001f, (ls.x + ls.y + ls.z) / 3f);
                hole.transform.localScale = new Vector3(0.024f, 0.02f, 0.012f) * k;
                hole.GetComponent<Renderer>().sharedMaterial = socketMat49;
                hole.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                h.hole = hole;
            }
            eyeHides49.Add(h);
            foreach (var (t, _) in h.parts) t.localScale = Vector3.zero;
        }

        static void LateUpdateEyeHides49()
        {
            for (int i = eyeHides49.Count - 1; i >= 0; i--)
            {
                var h = eyeHides49[i];
                if (h.c == null) { if (h.hole != null) Object.Destroy(h.hole); eyeHides49.RemoveAt(i); continue; }
                if (!Same54(h.c, h.life))
                {   // 0.54: the body belongs to somebody new - give the eye back instead of hiding it on him
                    foreach (var (t, sc) in h.parts) if (t != null) t.localScale = sc;
                    if (h.hole != null) Object.Destroy(h.hole);
                    eyeHides49.RemoveAt(i); continue;
                }
                foreach (var (t, _) in h.parts) if (t != null && t.localScale != Vector3.zero) t.localScale = Vector3.zero;
            }
        }

        /// Body recycled: put the eyes back, remove the holes.
        internal static void RestoreEyes49(Creature c)
        {
            for (int i = eyeHides49.Count - 1; i >= 0; i--)
            {
                var h = eyeHides49[i];
                if (h.c != c) continue;
                foreach (var (t, sc) in h.parts) if (t != null) t.localScale = sc;
                if (h.hole != null) Object.Destroy(h.hole);
                eyeHides49.RemoveAt(i);
            }
        }
    }
}

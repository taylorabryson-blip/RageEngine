// Nomad Rage Engine 0.23 - TOXINS: coated blades and what each poison does to a body.
//   Get a coated weapon two ways:
//     - Item book > "Rage Engine Toxins": seven daggers, each permanently coated.
//     - Mod Options > Rage Engine: Toxins: coat the weapon in your hand (6 hits, then wipe it again).
//   A coated weapon shows it: a glossy bead of the toxin on the tip that keeps dripping.
//   Cuts and stabs deliver a dose (more hits = more dose = faster). Each toxin does exactly what it says:
//     NEUROTOXIN (purple)  skin turns purple from the wound outward, lips go blue-purple, he grabs his throat, chokes
//                          and gasps, staggers, collapses convulsing, then suffocates.
//     PARALYTIC  (yellow)  tremors, then the arms go dead (weapon drops), then the legs fold. Down, awake, can't move.
//     HEMOTOXIN  (crimson) blood won't clot: heavy bleeding, coughing blood, bleeding from the eyes, bruises bloom
//                          all over the body.
//     SEDATIVE   (blue)    drowsy, heavy eyelids, stumbling like a drunk, then out cold for a while (never lethal).
//     ACID       (green)   the wound burns and keeps eating outward: spreading burns, screaming, clutching it.
//     BERSERK    (red)     rage serum: no pain, faster, relentless... then a hard crash.
//     TERROR     (white)   panic: drops everything, screams, runs; cornered he cowers and begs.
//   "Toxins can kill" (Deaths) - off = the lethal ones stop at collapse and wear off.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal enum Tox { None = -1, Neurotoxin, Paralytic, Hemotoxin, Sedative, Acid, Berserk, Terror }

    public sealed partial class RageEngineScript
    {
        const string CTX = "Rage Engine: Toxins";
        static readonly string[] ToxName = { "Neurotoxin", "Paralytic", "Hemotoxin", "Sedative", "Acid", "Berserk", "Terror" };
        static readonly Color[] ToxColor =
        {
            new Color(0.45f, 0.1f, 0.7f), new Color(0.95f, 0.85f, 0.1f), new Color(0.5f, 0.02f, 0.05f), new Color(0.15f, 0.35f, 0.95f),
            new Color(0.35f, 0.95f, 0.15f), new Color(1f, 0.08f, 0.05f), new Color(0.92f, 0.95f, 1f),
        };

        [ModOptionCategory(CD, 2), ModOptionOrder(6)]
        [ModOption("Toxins can kill", "ON: neurotoxin suffocates, acid eats them, paralytic stops their breathing. OFF: they collapse and the toxin wears off.", defaultValueIndex = 1)]
        public static bool toxinsKill = true;

        // 0.25.1: coat buttons removed from the menu - use the toxin oils / toxin daggers in the item book
        public static void CoatNeuro(string _) => CoatHeld(Tox.Neurotoxin);
        // 0.25.1: coat buttons removed from the menu - use the toxin oils / toxin daggers in the item book
        public static void CoatPara(string _) => CoatHeld(Tox.Paralytic);
        // 0.25.1: coat buttons removed from the menu - use the toxin oils / toxin daggers in the item book
        public static void CoatHemo(string _) => CoatHeld(Tox.Hemotoxin);
        // 0.25.1: coat buttons removed from the menu - use the toxin oils / toxin daggers in the item book
        public static void CoatSed(string _) => CoatHeld(Tox.Sedative);
        // 0.25.1: coat buttons removed from the menu - use the toxin oils / toxin daggers in the item book
        public static void CoatAcid(string _) => CoatHeld(Tox.Acid);
        // 0.25.1: coat buttons removed from the menu - use the toxin oils / toxin daggers in the item book
        public static void CoatBers(string _) => CoatHeld(Tox.Berserk);
        // 0.25.1: coat buttons removed from the menu - use the toxin oils / toxin daggers in the item book
        public static void CoatTerror(string _) => CoatHeld(Tox.Terror);

        sealed class Coat { public Tox tox; public int charges; public Transform bead; public float nextDrip; public Vector3 tipLocal; }
        static readonly Dictionary<Item, Coat> coats = new Dictionary<Item, Coat>();
        static float nextCoatScan;

        static void CoatHeld(Tox t) => Guard("coat", () =>
        {
            var pc = Player.local != null ? Player.local.creature : null;
            if (pc == null) return;
            foreach (var h in new[] { pc.handRight, pc.handLeft })
            {
                var it = h != null && h.grabbedHandle != null ? h.grabbedHandle.item : null;
                if (it == null) continue;
                CoatItem(it, t, 6);
                Log($"coated {it.data?.id} in {ToxName[(int)t]}");
                return;
            }
            Log("coat: hold a weapon first");
        });

        static void CoatItem(Item it, Tox t, int charges)
        {
            if (coats.TryGetValue(it, out var old) && old.bead != null) Object.Destroy(old.bead.gameObject);
            var c = new Coat { tox = t, charges = charges };
            // the tip: the far end of the weapon along its pointing direction
            var fwdT = it.flyDirRef != null ? it.flyDirRef : it.transform;
            Vector3 fwd = fwdT.forward;
            Bounds b = default; bool any = false;
            foreach (var r in it.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r.name.StartsWith("RageEngine_")) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            Vector3 tip = any ? b.center + fwd * (Mathf.Abs(fwd.x) * b.extents.x + Mathf.Abs(fwd.y) * b.extents.y + Mathf.Abs(fwd.z) * b.extents.z) * 0.92f : it.transform.position + fwd * 0.2f;
            c.tipLocal = it.transform.InverseTransformPoint(tip);
            var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(bead.GetComponent<Collider>());
            bead.name = "RageEngine_ToxinBead";
            bead.transform.SetParent(it.transform, true);
            bead.transform.position = tip - fwd * 0.03f;
            bead.transform.rotation = Quaternion.LookRotation(fwd);
            var ls = it.transform.lossyScale;
            bead.transform.localScale = new Vector3(0.016f / Mathf.Max(0.01f, ls.x), 0.016f / Mathf.Max(0.01f, ls.y), 0.075f / Mathf.Max(0.01f, ls.z));
            bead.GetComponent<MeshRenderer>().sharedMaterial = Mats.Make(ToxColor[(int)t], 0.95f);
            c.bead = bead.transform;
            coats[it] = c;
        }

        /// Every frame-ish: book daggers coat themselves; coated tips drip.
        static void TickCoats()
        {
            float now = Time.time;
            if (now >= nextCoatScan)
            {
                nextCoatScan = now + 1f;
                var all = Item.allActive;
                if (all != null)
                    foreach (var it in all)
                    {
                        if (it == null || it.data == null || coats.ContainsKey(it)) continue;
                        string id = it.data.id;
                        if (id == null || !id.StartsWith("RE_Toxin_")) continue;
                        for (int i = 0; i < ToxName.Length; i++)
                            if (id == "RE_Toxin_" + ToxName[i]) { CoatItem(it, (Tox)i, int.MaxValue); break; }
                    }
                var dead = new List<Item>();
                foreach (var kv in coats) if (kv.Key == null) dead.Add(kv.Key);
                foreach (var d in dead) coats.Remove(d);
            }
            foreach (var kv in coats)
            {
                var it = kv.Key; var c = kv.Value;
                if (it == null || c.bead == null || now < c.nextDrip) continue;
                c.nextDrip = now + Random.Range(0.7f, 1.6f);
                var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(drop.GetComponent<Collider>());
                drop.name = "RageEngine_ToxinDrip";
                drop.transform.position = it.transform.TransformPoint(c.tipLocal);
                drop.transform.localScale = Vector3.one * Random.Range(0.006f, 0.01f);
                drop.GetComponent<MeshRenderer>().sharedMaterial = Mats.Make(ToxColor[(int)c.tox], 0.95f);
                var rb = drop.AddComponent<Rigidbody>(); rb.mass = 0.001f; rb.velocity = Vector3.down * 0.2f;
                Object.Destroy(drop, 1.2f);
            }
        }

        /// From HandleHit: a cut/stab from a coated weapon delivers a dose.
        void ToxinHit(NpcState s, CollisionInstance hit, RagdollPart part)
        {
            var type = hit.damageStruct.damageType;
            if (type != DamageType.Slash && type != DamageType.Pierce) return;
            var col = hit.sourceCollider;
            var it = col != null ? col.GetComponentInParent<Item>() : null;
            if (it == null || !coats.TryGetValue(it, out var c)) return;
            if (c.charges != int.MaxValue && --c.charges <= 0) { coats.Remove(it); if (c.bead != null) Object.Destroy(c.bead.gameObject); }
            Envenom(s, c.tox, type == DamageType.Pierce ? 1.2f : 0.8f, part, hit.contactPoint);
        }

        void Envenom(NpcState s, Tox t, float dose, RagdollPart part, Vector3 point)
        {
            var c = s.creature;
            if (c.isKilled) return;
            if (s.tox != (int)t) { s.tox = (int)t; s.toxStart = Time.time; s.toxDose = 0f; s.toxStage = 0; s.toxNext = 0f; s.toxFlags = 0; }
            s.toxDose = Mathf.Min(3f, s.toxDose + dose);
            s.toxPart = part; s.toxLocal = part.transform.InverseTransformPoint(point);
            Log($"{WhoIs(s)} poisoned: {ToxName[(int)t]} (dose {s.toxDose:0.0})");
            if (t == Tox.Hemotoxin) s.bleedRate += 0.35f * dose * bleedingRate;
            if (t == Tox.Acid) Stamp(c, point, 0.04f, "burn");
            MarkInjected(s, ToxColor[(int)t], point);
        }

        static readonly object ToxHandler = new object();

        void TickToxin(NpcState s, float now)
        {
            if (s.tox < 0) return;
            var c = s.creature;
            if (c.isKilled) { s.tox = -1; return; }
            float speed = 0.6f + 0.4f * s.toxDose;                            // more dose, faster
            float t = (now - s.toxStart) * speed / Mathf.Lerp(1f, 1.6f, s.hpK);   // big HP NPCs fight it longer
            var tox = (Tox)s.tox;
            Vector3 wound = s.toxPart != null ? s.toxPart.transform.TransformPoint(s.toxLocal) : c.transform.position + Vector3.up;
            var head = c.ragdoll.headPart;
            bool fx = now >= s.toxNext;
            if (fx) s.toxNext = now + 1.4f;

            switch (tox)
            {
                case Tox.Neurotoxin:
                    if (fx && t > 1.5f)
                    {
                        // purple spreading out from the wound, and the lips/face going blue-purple
                        float r = Mathf.Min(0.12f, 0.03f + t * 0.006f);
                        Guard("tox.paint", () => SkinPaint.Paint(c, wound + Random.insideUnitSphere * r, r, new Color(0.38f, 0.12f, 0.6f, 0.45f), GoreTex.Pick("bruise_deep") ?? TexGen.Bruise(), false, Random.Range(0f, 360f)));
                        if (t > 4f && c.jaw != null) Guard("tox.lips", () => SkinPaint.Paint(c, c.jaw.position, 0.04f, new Color(0.35f, 0.15f, 0.55f, 0.5f), TexGen.Bruise()));
                    }
                    if (t > 4f && s.toxStage < 1)
                    {
                        s.toxStage = 1;
                        var neck = PartOf(c, RagdollPart.Type.Neck);
                        if (neck != null) StartClutch(s, neck, neck.transform.position, null, false, true);
                        Speak(s, "choke");
                        Log($"{WhoIs(s)} is CHOKING - neurotoxin");
                    }
                    if (s.toxStage >= 1 && s.toxStage < 2)
                    {
                        SetMelee(s, false, now + 0.5f);
                        s.drunk = Mathf.Max(s.drunk, Mathf.Min(1.1f, (t - 4f) * 0.08f));
                        if (fx && Random.value < 0.3f) Speak(s, "choke");
                    }
                    if (t > 12f && s.toxStage < 2) { s.toxStage = 2; s.cantStand = true; Sink(s, now); Log($"{WhoIs(s)} collapses, convulsing"); }
                    if (s.toxStage == 2) Convulse(s, 0.6f);
                    if (t > 26f && s.toxStage < 3) { s.toxStage = 3; if (toxinsKill) { s.finalKill = true; s.noGasp = false; Guard("kill", () => c.Kill()); Log($"{WhoIs(s)} suffocates - neurotoxin"); } }
                    if (!toxinsKill && t > 45f) EndToxin(s, "the neurotoxin wears off");
                    break;

                case Tox.Paralytic:
                    if (t > 2f && t < 6f) Convulse(s, 0.15f);                                           // tremors
                    if (t > 4f && s.toxStage < 1) { s.toxStage = 1; s.weakArmLUntil = s.weakArmRUntil = now + 999f; Log($"{WhoIs(s)}: arms going numb - paralytic"); }
                    if (t > 7f && s.toxStage < 2) { s.toxStage = 2; KillArm(s, Side.Left); KillArm(s, Side.Right); Log($"{WhoIs(s)}: ARMS PARALYSED"); }
                    if (t > 10f && s.toxStage < 3) { s.toxStage = 3; s.buckleLegs |= LegParts; s.buckleUntil = float.MaxValue; }
                    if (t > 13f && s.toxStage < 4) { s.toxStage = 4; s.cantStand = true; Sink(s, now); Log($"{WhoIs(s)}: FULLY PARALYSED - awake, can't move"); }
                    if (s.toxStage >= 4 && fx && Random.value < 0.08f) Speak(s, "muffled");
                    if (t > 40f && s.toxStage < 5) { s.toxStage = 5; if (toxinsKill) { s.finalKill = true; Guard("kill", () => c.Kill()); Log($"{WhoIs(s)}: breathing stops - paralytic"); } }
                    break;

                case Tox.Hemotoxin:
                    if (fx)
                    {
                        if (t > 3f && Random.value < 0.5f) SpitBlood(s, 1);
                        if (t > 5f && head != null) { Stamp(c, head.transform.position + head.transform.forward * 0.08f + Vector3.up * 0.03f, 0.03f, "blood_drip", 0.85f, false, 0f); s.eyeBloodUntil = now + 4f; }
                        if (t > 4f)
                        {
                            var parts = c.ragdoll.parts;
                            var p = parts[Random.Range(0, parts.Count)];
                            if (p != null) Stamp(c, p.transform.position + Random.insideUnitSphere * 0.05f, 0.04f, Random.value < 0.5f ? "bruise_deep" : "hematoma", 0.6f);
                        }
                        s.bleedRate = Mathf.Max(s.bleedRate, 0.25f * s.toxDose * bleedingRate);
                    }
                    if (t > 8f && s.toxStage < 1) { s.toxStage = 1; Log($"{WhoIs(s)} is bleeding out from everywhere - hemotoxin"); }
                    break;

                case Tox.Sedative:
                    s.drunk = Mathf.Min(1.4f, s.drunk + 0.012f * speed);
                    if (fx && t > 3f) s.eyesShutUntil = now + Mathf.Clamp(t * 0.05f, 0.2f, 1.0f);    // heavy eyelids
                    if (t > 12f && s.toxStage < 1 && !s.knockedOut)
                    {
                        s.toxStage = 1;
                        s.knockedOut = true; s.koWakeAt = now + 35f; s.koLimbs = s.limpParts;
                        s.eyesShutUntil = float.MaxValue;
                        Guard("brain", () => c.brain.Stop());
                        AllowFall(s, 3f); Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
                        Log($"{WhoIs(s)} falls ASLEEP - sedative");
                    }
                    if (s.toxStage >= 1 && !s.knockedOut) EndToxin(s, "wakes from the sedative");
                    break;

                case Tox.Acid:
                    if (fx)
                    {
                        float r = Mathf.Min(0.1f, 0.03f + t * 0.005f);
                        Stamp(c, wound + Random.insideUnitSphere * r, r * 0.8f, Random.value < 0.6f ? "burn" : "torn_flesh", 0.9f);
                        s.pain = 1f;
                        if (Random.value < 0.35f) Scream(s, false);
                        if (Random.value < 0.15f && s.toxPart != null) SpawnChunks(wound, (wound - s.toxPart.transform.position).normalized, 1, 1f, 0.5f, c);
                        if (!s.clutching && s.toxPart != null) StartClutch(s, s.toxPart, wound, null, false, false);
                        float dmg = 1.6f * s.toxDose;
                        if (!toxinsKill) dmg = Mathf.Min(dmg, Mathf.Floor(c.currentHealth - 1f));
                        if (dmg >= 1f) Guard("acid", () => c.Damage(dmg));
                    }
                    if (t > 30f) EndToxin(s, "the acid has burned out");
                    break;

                case Tox.Berserk:
                    if (s.toxStage < 1)
                    {
                        s.toxStage = 1; s.rushUntil = now + 30f; s.pain = 0f;
                        Guard("speed", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(ToxHandler); c.locomotion.globalMoveSpeedMultiplier.Add(ToxHandler, 1.35f); });
                        Scream(s, true);
                        Log($"{WhoIs(s)} goes BERSERK");
                    }
                    if (s.toxStage == 1) { s.pain = 0f; if (fx && head != null) Guard("flush", () => SkinPaint.Paint(c, head.transform.position, 0.06f, new Color(0.8f, 0.1f, 0.1f, 0.2f), TexGen.Bruise())); }
                    if (t > 30f && s.toxStage < 2)
                    {
                        s.toxStage = 2; s.drunk = 1.2f; s.fatigue = 1f;
                        Guard("speed", () => { c.locomotion.globalMoveSpeedMultiplier.Remove(ToxHandler); c.locomotion.globalMoveSpeedMultiplier.Add(ToxHandler, 0.6f); });
                        Log($"{WhoIs(s)} CRASHES after the rage serum");
                    }
                    if (t > 48f) EndToxin(s, "the crash passes");
                    break;

                case Tox.Terror:
                    if (s.toxStage < 1)
                    {
                        s.toxStage = 1; s.fleeing = true; s.rnasDirty = true;
                        foreach (var sd in new[] { Side.Left, Side.Right }) { var h = Hand(c, sd); if (h != null && h.grabbedHandle != null) Guard("drop", () => h.UnGrab(false)); }
                        Scream(s, true);
                        Guard("face", () => c.brain.instance.GetModule<BrainModuleExpression>(false)?.SetFaceExpression(FaceAnimator.Expression.Fear, 0, null));
                        Log($"{WhoIs(s)} is TERRIFIED - drops everything and runs");
                    }
                    SetMelee(s, false, now + 0.5f);
                    if (fx) Convulse(s, 0.08f);                                                         // shaking
                    if (fx && Random.value < 0.2f) Scream(s, false);
                    if (t > 40f) { s.fleeing = false; EndToxin(s, "calms down"); }
                    break;
            }
        }

        void EndToxin(NpcState s, string why)
        {
            Log($"{WhoIs(s)} {why}");
            s.tox = -1;
            var c = s.creature;
            Guard("speed", () => c.locomotion.globalMoveSpeedMultiplier.Remove(ToxHandler));
        }

        static void Convulse(NpcState s, float k)
        {
            var parts = s.creature.ragdoll.parts;
            for (int i = 0; i < 3; i++)
            {
                var p = parts[Random.Range(0, parts.Count)];
                if (p != null && p.physicBody != null) p.physicBody.AddForce(Random.insideUnitSphere * 2.5f * k, ForceMode.VelocityChange);
            }
        }

        static RagdollPart PartOf(Creature c, RagdollPart.Type t)
        {
            foreach (var p in c.ragdoll.parts) if (p != null && p.type == t) return p;
            return null;
        }

        /// 0.23: bigger HP = tougher NPC: more pain tolerance, stronger grip and muscles, steadier, slower to poison.
        static void ScaleByHp(NpcState s)
        {
            if (s.hpScaled) return;
            var c = s.creature;
            if (c == null || c.maxHealth <= 1f) return;
            s.hpScaled = true;
            float k = Mathf.InverseLerp(150f, 2500f, c.maxHealth);
            s.hpK = k;
            if (k <= 0.01f) return;
            s.toughness *= 1f + 1.1f * k;                    // 0.28: between 0.25 (0.9) and 0.26 (1.6)
            s.painSensitivity *= 1f - 0.5f * k;
            s.balanceStability *= 1f + 0.4f * k;
            Log($"{c.name}: {c.maxHealth:0} HP - toughness x{1f + 0.9f * k:0.00}, pain x{1f - 0.4f * k:0.00}, grip +{k * 50f:0}%");
        }
    }
}

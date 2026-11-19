// Nomad Rage Engine 0.22 - gore content + physics polish.
//   GORE TEXTURES (Textures/Gore/*.png, 42 stamps): every wound is classified and painted with its own texture:
//     slash -> laceration (deep cuts: laceration_deep), pierce -> stab wound, bullet -> entry + exit, heavy blunt ->
//     split skin / crush wound, open fracture -> open fracture wound, skidding along the floor -> abrasion,
//     blood runs down from wounds (drips), sprays onto skin and clothes (splatter), pools on the ground under bleeders.
//   BRUISING: per-spot damage tracking - contusion -> deep contusion -> hematoma (the skin SWELLS: real mesh
//     deformation) -> split skin. Each stage has its own textures.
//   SKULL: integrity per NPC. Heavy blunt hits to the head crack it (fracture lines painted, dent) and enough on a
//     cracked skull - or one massive blow from something heavy - smashes it: skull fragments, brain matter and teeth.
//   BONES: new models for ribs, hand bones, vertebrae and the jaw.
//   PHYSICS: no snap-back after hard hits (the body's anchor follows where the hit put it), trips on the run never
//     break legs, vanilla parrying tuned per NPC (and weakened by injuries / punch-drunk), free hands block punches
//     to the head AND body - armed NPCs too.
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    internal static class GoreTex
    {
        static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
        static bool loaded;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                string dir = System.IO.Path.Combine(RageEngineScript.modFolder ?? "", "Textures", "Gore");
                if (!System.IO.Directory.Exists(dir)) { RageEngineScript.Log("gore textures: folder missing"); return; }
                foreach (var f in System.IO.Directory.GetFiles(dir, "*.png"))
                {
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                    if (t.LoadImage(System.IO.File.ReadAllBytes(f))) { t.name = System.IO.Path.GetFileNameWithoutExtension(f); tex[t.name] = t; }
                }
                RageEngineScript.Log($"gore textures loaded: {tex.Count}");
            }
            catch (System.Exception e) { RageEngineScript.Log("gore textures: " + e.Message); }
        }

        /// A random variant: Pick("laceration") -> laceration_1..4. Null if none.
        public static Texture2D Pick(string prefix)
        {
            Load();
            Texture2D found = null; int n = 0;
            foreach (var kv in tex)
                if (kv.Key.StartsWith(prefix + "_") && char.IsDigit(kv.Key[prefix.Length + 1]) && Random.Range(0, ++n) == 0) found = kv.Value;
            return found;
        }

        public static Texture2D Get(string name) { Load(); return tex.TryGetValue(name, out var t) ? t : null; }

        static readonly Dictionary<string, Texture2D> meshTex = new Dictionary<string, Texture2D>();
        public static Texture2D MeshTex(string file)
        {
            if (meshTex.TryGetValue(file, out var t)) return t;
            t = null;
            try
            {
                string p = System.IO.Path.Combine(RageEngineScript.modFolder ?? "", "Textures", file);
                if (System.IO.File.Exists(p)) { t = new Texture2D(2, 2); t.LoadImage(System.IO.File.ReadAllBytes(p)); }
            }
            catch { }
            return meshTex[file] = t;
        }
    }

    public sealed partial class RageEngineScript
    {
        // ======================================================== wound painting
        static readonly Color Full = new Color(1f, 1f, 1f, 0.95f);

        /// Paints a texture stamp (its own colours) at a random angle. Falls back silently.
        static bool Stamp(Creature c, Vector3 at, float radius, string prefix, float alpha = 0.95f, bool cloth = false, float rot = float.NaN)
        {
            if (bareHit34) return false;                                                       // 0.40: fists leave no painted circles
            if (!bruiseMarks && prefix.StartsWith("bruise")) return false;
            var t = GoreTex.Pick(prefix);
            if (t == null) return false;
            bool ok = false;
            Guard("stamp", () => ok = SkinPaint.Paint(c, at, radius, new Color(1f, 1f, 1f, alpha), t, cloth, float.IsNaN(rot) ? Random.Range(0f, 360f) : rot));
            return ok;
        }

        /// Called for every damaging hit: paint the right kind of wound and remember it (it keeps bleeding/dripping).
        void PaintWound(NpcState s, CollisionInstance hit, RagdollPart part, float severity)
        {
            if (!goreTextures) return;
            var c = s.creature;
            var type = hit.damageStruct.damageType;
            float dmg = hit.damageStruct.damage;
            Vector3 p = hit.contactPoint;
            string kind = null; float r = 0.03f;
            if (type == DamageType.Slash && dmg >= woundThreshold * 0.4f) { kind = dmg >= woundThreshold * 1.6f ? "laceration_deep" : "laceration"; r = Mathf.Clamp(0.03f + dmg * 0.0012f, 0.03f, 0.075f); }
            else if (type == DamageType.Pierce && dmg >= woundThreshold * 0.3f) { kind = "stab"; r = Mathf.Clamp(0.02f + dmg * 0.0006f, 0.02f, 0.04f); }
            else if (type == DamageType.Blunt && severity >= 1.6f && !Has(part.type, RagdollPart.Type.Torso)) { kind = "split_skin"; r = 0.028f; }
            if (kind == null) return;
            if (Stamp(c, p, r, kind))
            {
                if (s.woundSpots.Count >= 12) s.woundSpots.RemoveAt(0);
                s.woundSpots.Add(new WoundSpot { part = part, local = part.transform.InverseTransformPoint(p), born = Time.time, heavy = dmg >= woundThreshold });
                // blood on the clothes around it, splatter on the skin nearby
                Stamp(c, p, r * 1.6f, "blood_splatter", 0.7f, true);
                if (Random.value < 0.6f) Stamp(c, p + Random.insideUnitSphere * 0.03f, r * 1.3f, "blood_splatter", 0.55f);
            }
        }

        // ======================================================== bruises: classified stages + swelling
        internal sealed class BruiseSpot { public RagdollPart part; public Vector3 local; public float dmg; public int stage, swells; }
        internal sealed class WoundSpot { public RagdollPart part; public Vector3 local; public float born, nextDrip; public bool heavy; }

        /// Returns true if handled (painted with the stage textures).
        bool BruiseStage(NpcState s, RagdollPart part, Vector3 point, Vector3 normal, float sev)
        {
            var c = s.creature;
            BruiseSpot spot = null;
            foreach (var b in s.bruiseSpots)
                if (b.part == part && (part.transform.TransformPoint(b.local) - point).sqrMagnitude < 0.055f * 0.055f) { spot = b; break; }
            if (spot == null)
            {
                if (s.bruiseSpots.Count >= 24) s.bruiseSpots.RemoveAt(0);
                spot = new BruiseSpot { part = part, local = part.transform.InverseTransformPoint(point) };
                s.bruiseSpots.Add(spot);
            }
            spot.dmg += sev;
            int stage = spot.dmg >= 3.6f ? 3 : spot.dmg >= 2.2f ? 2 : spot.dmg >= 1.1f ? 1 : 0;
            string tex = stage == 0 ? "bruise_fresh" : stage == 1 ? "bruise_deep" : "hematoma";
            float rad = Mathf.Clamp(0.022f * (0.8f + spot.dmg * 0.5f), 0.022f, 0.065f);
            bool painted = Stamp(c, point, rad, tex, Mathf.Clamp(0.45f + 0.18f * sev, 0.45f, 0.9f));
            if (!painted) return false;
            if (stage > spot.stage)
            {
                spot.stage = stage;
                string what = stage == 1 ? "deep contusion" : stage == 2 ? "HEMATOMA - swelling up" : "skin SPLIT open";
                Log($"{WhoIs(s)}: {part.type} {what} ({spot.dmg:0.0})");
            }
            // swelling: real mesh deformation outward (hematoma), at most twice per spot
            if (false && stage >= 2 && spot.swells < 2 && s.swellCount < 10)     // 0.28.3: no swelling lumps
            {
                var bone = part.meshBone != null ? part.meshBone : part.bone != null ? part.bone.mesh : null;
                if (bone != null)
                {
                    spot.swells++; s.swellCount++;
                    bool head = Has(part.type, RagdollPart.Type.Head);
                    Guard("swell", () => HeadDeform.Dent(c, bone, point, normal, head ? 0.03f : 0.04f, head ? 0.0045f : 0.006f));
                }
            }
            if (stage >= 3 && spot.stage == 3 && spot.swells <= 2)
            {
                Stamp(c, point, 0.028f, "split_skin");
                RageEngineScript.Blood(part.transform);
                s.bleedRate += 0.05f * bleedingRate;
                spot.swells = 3;
            }
            return true;
        }

        // ======================================================== blood: drips, pools on the ground
        static Material bloodDecalMat;
        static readonly List<GameObject> bloodDecals = new List<GameObject>();
        static Mesh quadMesh;

        static Mesh Quad()
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh { name = "RageEngine_Quad" };
            quadMesh.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quadMesh.RecalculateNormals(); quadMesh.RecalculateBounds();
            return quadMesh;
        }

        static Material DecalMat(Texture2D t)
        {
            var sh = SkinPaint.BlendShader;
            if (sh == null || t == null) return null;
            return new Material(sh) { mainTexture = t, color = Color.white, renderQueue = 2460 };
        }

        /// A blood decal on the ground under 'from' (raycast down). Returns the decal (to grow) or null.
        static GameObject GroundBlood(Vector3 from, string prefix, float size, Creature ignore)
        {
            if (!groundBlood) return null;
            var t = GoreTex.Pick(prefix);
            var mat = DecalMat(t);
            if (mat == null) return null;
            if (!Physics.Raycast(from + Vector3.up * 0.1f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) return null;
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) return null;                  // not on bodies / props
            while (bloodDecals.Count > 0 && (bloodDecals.Count >= PerfDecals || bloodDecals[0] == null)) { if (bloodDecals[0] != null) Object.Destroy(bloodDecals[0]); bloodDecals.RemoveAt(0); }
            var go = new GameObject("RageEngine_BloodDecal");
            go.transform.position = hit.point + hit.normal * 0.004f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bloodDecals.Add(go);
            Object.Destroy(go, 240f);
            return go;
        }

        /// 0.1 s tick: wounds drip down the body; bleeders leave drops and a growing pool where they stand / lie.
        void TickBlood(NpcState s, float now)
        {
            var c = s.creature;
            if (!goreTextures) return;
            bool bleeding = s.bleedRate > 0.03f || (s.woundSpots.Count > 0 && now - s.woundSpots[s.woundSpots.Count - 1].born < 40f);
            if (!bleeding) return;
            // drips running down from wounds
            foreach (var w in s.woundSpots)
            {
                if (w.part == null || now < w.nextDrip || now - w.born > 90f) continue;
                w.nextDrip = now + Random.Range(2f, 4f);                                       // 0.41: more bleeding
                Vector3 at = w.part.transform.TransformPoint(w.local) - Vector3.up * 0.035f;
                Stamp(c, at, w.heavy ? 0.045f : 0.032f, "blood_drip", 0.85f, false, 0f);
                Stamp(c, at - Vector3.up * 0.06f, w.heavy ? 0.06f : 0.04f, "blood_drip", 0.75f, false, 0f);
                if (w.heavy) Stamp(c, at - Vector3.up * 0.05f, 0.05f, "blood_drip", 0.7f, true, 0f);
            }
            // drops / pool on the ground
            if (now < s.nextGroundBlood || s.bleedRate < 0.03f) return;
            s.nextGroundBlood = now + Mathf.Lerp(1.6f, 0.45f, Mathf.Clamp01(s.bleedRate));
            var root = c.ragdoll.rootPart;
            Vector3 at2 = root != null ? root.transform.position : c.transform.position;
            bool down = c.ragdoll.state != Ragdoll.State.Standing || c.isKilled;
            if (s.pool != null && (s.pool.transform.position - at2).sqrMagnitude < (down ? 0.6f * 0.6f : 0.35f * 0.35f))
            {
                float cur = s.pool.transform.localScale.x;
                float max = down ? 2.2f : 0.9f;
                if (cur < max) s.pool.transform.localScale = Vector3.one * Mathf.Min(max, cur + 0.05f + 0.12f * s.bleedRate);
                return;
            }
            s.pool = GroundBlood(at2 + Random.insideUnitSphere * 0.1f, down ? "blood_pool" : "blood_ground_splat", down ? 0.35f : 0.28f, c);
        }

        // ======================================================== skull
        const float SkullIntegrity = 100f;

        /// Heavy blunt hit to the head: crack the skull, or smash it. Returns true if it did either.
        bool SkullHit(NpcState s, CollisionInstance hit, RagdollPart head, float dmg, Vector3 dir)
        {
            if (!headCrush || s.headCrushed) return false;
            var col = hit.sourceCollider;
            bool fist = col != null && col.GetComponentInParent<RagdollHand>() != null;
            if (fist) return false;                                                            // fists never smash skulls
            float mass = col != null && col.attachedRigidbody != null ? col.attachedRigidbody.mass : 1f;
            float massF = Mathf.Clamp(mass / 2.5f, 0.4f, 2.5f);                               // a hammer/mace hits much harder than a dagger pommel
            float speed = hit.impactVelocity.magnitude;
            float blow = dmg * massF * Mathf.Clamp(speed / 6f, 0.5f, 1.6f) * headCrushEase;
            // 0.25: a downed head has the floor behind it - the blow has nowhere to go (stomps, hammer on a head on the ground)
            bool onFloor = false;
            if (s.creature.ragdoll.state != Ragdoll.State.Standing)
                foreach (var rh in Physics.RaycastAll(head.transform.position, Vector3.down, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var rpp = rh.collider != null ? rh.collider.GetComponentInParent<RagdollPart>() : null;
                    if (rpp == null || rpp.ragdoll != s.creature.ragdoll) { onFloor = true; break; }
                }
            if (onFloor) blow *= 1.6f;
            if (blow < 12f || projectileHit34 || !faceCave) return false;
            float before = s.skull;
            s.skull -= blow;
            Vector3 p = hit.contactPoint;
            if (before > 55f && s.skull <= 55f)
            {
                // cracked: fracture lines + a dent; he's in a bad way
                Stamp(s.creature, p, 0.05f, "skull_fracture");
                var bone = head.meshBone != null ? head.meshBone : head.bone != null ? head.bone.mesh : null;
                if (bone != null) Guard("dent", () => HeadDeform.Dent(s.creature, bone, p, (head.transform.position - p).normalized, 0.04f, 0.007f));
                Sfx.Play("NRE_BoneCrunch", head.transform, 0.9f);
                SpawnGoreModels(p, -dir + Vector3.up * 0.4f, "RE_Skull_Frag", 1, SkullMat(), 0.35f, s.creature);
                s.daze = Mathf.Max(s.daze, 1.2f); s.drunk = Mathf.Max(s.drunk, 0.9f);
                s.bleedRate += 0.15f * bleedingRate;
                RageEngineScript.Blood(head.transform);
                Log($"{WhoIs(s)}: SKULL CRACKED (blow {blow:0})");
            }
            // smash: one massive blow, or a heavy blow on an already-cracked skull
            if (s.skull <= 0f && blow >= 20f || blow >= 95f)   // 0.25: easier (was 30 / 140)
            {
                CrushHead(s, head, p, dir, Mathf.Max(headCrushForce, blow / 12f));
                return true;
            }
            return s.skull <= 55f;
        }

        static Material skullMat, brainMat, toothMat;
        static Material SkullMat() => skullMat != null ? skullMat : skullMat = Mats.MakeTextured(GoreTex.MeshTex("RE_Skull.png") ?? TexGen.Bone(), null, Color.white, 0.35f, false);
        static Material BrainMat() => brainMat != null ? brainMat : brainMat = Mats.MakeTextured(GoreTex.MeshTex("RE_Brain.png"), null, Color.white, 0.75f, false);
        static Material ToothMat() => toothMat != null ? toothMat : toothMat = Mats.Make(new Color(0.93f, 0.9f, 0.8f), 0.5f);

        static readonly List<GameObject> goreModels = new List<GameObject>();

        /// Throws real modelled pieces (skull fragments, brain, teeth) with physics. names: prefix, picks _1.._N.
        internal static void SpawnGoreModels(Vector3 point, Vector3 dir, string prefix, int count, Material mat, float speed, Creature c)
        {
            var bodyCols = new List<Collider>();
            if (c != null && c.ragdoll != null) bodyCols.AddRange(c.ragdoll.GetComponentsInChildren<Collider>());
            for (int i = 0; i < count; i++)
            {
                if (!faceCave && (prefix.StartsWith("RE_Skull") || prefix.StartsWith("RE_Brain") || prefix.StartsWith("RE_Jaw"))) return;      // 0.42: teeth and eyes always
                Mesh m = BoneModels.Get(prefix) ?? BoneModels.Get(prefix + "_" + Random.Range(1, 7)) ?? BoneModels.Get(prefix + "_1");
                if (m == null) return;
                while (goreModels.Count > 0 && (goreModels.Count >= PerfGorePieces || goreModels[0] == null)) { if (goreModels[0] != null) Object.Destroy(goreModels[0]); goreModels.RemoveAt(0); }
                var go = new GameObject("RageEngine_GorePiece");
                go.transform.position = point + Random.insideUnitSphere * 0.03f;
                go.transform.rotation = Random.rotation;
                go.transform.localScale = Vector3.one * Random.Range(0.75f, 1.15f);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var bc = go.AddComponent<BoxCollider>(); bc.size = m.bounds.size; bc.center = m.bounds.center;
                foreach (var b in bodyCols) if (b != null) Physics.IgnoreCollision(bc, b, true);
                var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.05f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.AddForce((dir.normalized + Random.insideUnitSphere * 0.6f).normalized * speed * Random.Range(2f, 5f) + Vector3.up * Random.Range(0.5f, 2f), ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
                goreModels.Add(go);
                Object.Destroy(go, 60f);
            }
        }

        /// Extra gore on a smashed skull: fragments, brain, teeth, blood on the ground.
        void SkullSmashExtras(NpcState s, RagdollPart head, Vector3 point, Vector3 dir)
        {
            var c = s.creature;
            Vector3 outv = (point - head.transform.position + dir * 0.3f).normalized;
            SpawnGoreModels(point, outv + Vector3.up * 0.3f, "RE_Skull_Frag", Random.Range(4, 7), SkullMat(), 1f, c);
            SpawnGoreModels(point, outv, "RE_Brain", Random.Range(2, 4), BrainMat(), 0.8f, c);
            if (c.jaw != null) SpawnGoreModels(c.jaw.position, -head.transform.up + outv, "RE_Tooth", Random.Range(2, 5), ToothMat(), 0.9f, c);
            if (Random.value < 0.4f && c.jaw != null) SpawnGoreModels(c.jaw.position, outv, "RE_Jaw_Frag", 1, SkullMat(), 0.5f, c);
            Stamp(c, point, 0.07f, "crush_wound");
            Stamp(c, point, 0.08f, "skull_fracture");
            GroundBlood(head.transform.position, "blood_ground_splat", 0.9f, c);
        }

        // ======================================================== physics polish
        /// No snap-back: for half a second after a hit, the body's anchor (locomotion) follows where the ragdoll went.
        static void FollowBody(NpcState s)
        {
            var c = s.creature;
            if (Time.time > s.followUntil || c.ragdoll.state != Ragdoll.State.Standing || c.locomotion == null || c.locomotion.physicBody == null) return;
            var root = c.ragdoll.rootPart;
            if (root == null) return;
            Vector3 off = root.transform.position - c.transform.position; off.y = 0f;
            Vector3 d = off - s.followBase;
            if (d.sqrMagnitude < 0.04f * 0.04f) return;
            c.locomotion.physicBody.velocity += Vector3.ClampMagnitude(d * 9f, 3f) * Time.deltaTime * 8f;
        }

        void StartFollow(NpcState s, float severity)
        {
            var c = s.creature;
            var root = c.ragdoll.rootPart;
            if (root == null || c.ragdoll.state != Ragdoll.State.Standing) return;
            if (Time.time > s.followUntil) { Vector3 off = root.transform.position - c.transform.position; off.y = 0f; s.followBase = off; }
            s.followUntil = Time.time + Mathf.Lerp(0.35f, 0.8f, Mathf.Clamp01(severity / 2f));
            // hard hits: the upper body stays loose a little longer, so it doesn't spring straight back
            if (severity > 1.1f) Loosen(s, RagdollPart.Type.Torso | RagdollPart.Type.Head | RagdollPart.Type.Neck | ArmParts, Mathf.Clamp(0.25f + 0.15f * severity, 0.3f, 0.6f), 0.5f + 0.25f * Mathf.Min(severity, 2.5f));
        }

        /// Contact with the level (floor, walls, stairs) - a trip, not a weapon.
        static bool EnvironmentHit(CollisionInstance hit)
        {
            var col = hit.sourceCollider;
            if (col == null) return true;
            // 0.25: a corpse, a dropped weapon or a chair on the floor is something you trip over, not something that hits you
            var it = col.GetComponentInParent<Item>();
            if (it != null)
            {
                if (it.handlers != null && it.handlers.Count > 0) return false;                 // somebody is swinging it
                var irb = col.attachedRigidbody;
                return irb == null || irb.velocity.magnitude < 4f;                              // lying there (not thrown)
            }
            var rp = col.GetComponentInParent<RagdollPart>();
            if (rp != null) { var cr = rp.ragdoll != null ? rp.ragdoll.creature : null; return cr == null || cr.isKilled; }
            var rb = col.attachedRigidbody;
            return rb == null || rb.isKinematic;
        }

        // ---- vanilla parrying, tuned per NPC and by their condition
        void DefenseOnTrack(NpcState s)
        {
            var c = s.creature;
            s.defSkill = Mathf.Clamp01(Random.Range(0.35f, 0.95f) + (s.personality.type == PersonalityType.Cautious ? 0.15f : s.personality.type == PersonalityType.Aggressive ? -0.1f : 0f));
            var d = c.brain?.instance?.GetModule<BrainModuleDefense>(false);
            if (d == null) return;
            s.defOrig = new[] { d.aboutToBeHitTiming, d.parryDetectionRadius, d.positionSpeed, d.rotationSpeed, d.parryAdjustSpeed, d.armSpringMultiplier };
            s.defPatched = true;
            ApplyDefense(s, d, 1f);
        }

        static void ApplyDefense(NpcState s, BrainModuleDefense d, float fit)
        {
            var o = s.defOrig;
            float k = 1f + s.defSkill * fit;
            d.aboutToBeHitTiming = o[0] * Mathf.Lerp(0.8f, 1.5f, s.defSkill * fit);
            d.parryDetectionRadius = o[1] * Mathf.Lerp(0.9f, 1.3f, s.defSkill * fit);
            d.positionSpeed = o[2] * Mathf.LerpUnclamped(0.5f, 1.8f, s.defSkill * fit);
            d.rotationSpeed = o[3] * Mathf.LerpUnclamped(0.5f, 1.8f, s.defSkill * fit);
            d.parryAdjustSpeed = o[4] * Mathf.Lerp(0.6f, 1.6f, s.defSkill * fit);
            d.armSpringMultiplier = o[5] * Mathf.Lerp(0.6f, 1.4f, fit) * (k > 0 ? 1f : 1f);
        }

        void TickDefense(NpcState s, float now)
        {
            if (!s.defPatched || now < s.nextDefense) return;
            s.nextDefense = now + 1f;
            var d = s.creature.brain?.instance?.GetModule<BrainModuleDefense>(false);
            if (d == null) return;
            float arms = Mathf.Max(Dead(s, Side.Left) ? 0f : ArmStrength(s, Side.Left), Dead(s, Side.Right) ? 0f : ArmStrength(s, Side.Right));
            float fit = Mathf.Clamp(s.bioReact * (now < s.bioAllyBoostUntil ? 1.3f : 1f) * arms * (1f - 0.5f * Mathf.Clamp01(s.drunk)) * (1f - 0.35f * Mathf.Clamp01(s.pain * painIntensity)) * (s.ppCritical ? 0.4f : 1f), 0.15f, 1.8f);
            ApplyDefense(s, d, fit);
        }

        static void DefenseOnRelease(NpcState s)
        {
            if (!s.defPatched) return;
            s.defPatched = false;
            var d = s.creature?.brain?.instance?.GetModule<BrainModuleDefense>(false);
            if (d == null || s.defOrig == null) return;
            var o = s.defOrig;
            d.aboutToBeHitTiming = o[0]; d.parryDetectionRadius = o[1]; d.positionSpeed = o[2]; d.rotationSpeed = o[3]; d.parryAdjustSpeed = o[4]; d.armSpringMultiplier = o[5];
        }
    }
}

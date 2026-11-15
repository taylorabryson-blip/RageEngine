// 0.25.3: the "Rage Engine: Toys" menu is gone (no more spawn buttons); the code stays so nothing else breaks.
// Nomad Rage Engine - test tools for trying the physics: a pistol and dynamite, spawned from
// Mod Options > Nomad Rage Engine > Test Tools. No custom 3D models are needed: they reuse game items
// (a dagger tinted gunmetal = pistol, a potion bottle tinted red = dynamite).
//   Pistol:   hold it like a gun (blade = barrel), pull the TRIGGER to fire. Real hitscan bullet: damage through the
//             game's own damage system (so blood, wounds, bones, bruises, pain all react), a physical bullet impulse,
//             gunshot + flesh impact sounds, recoil buzz.
//   Dynamite: press the TRIGGER to light the fuse (sizzle), throw it; 3.5 s later it explodes: everyone near the
//             blast is thrown by real explosion force (a "super heavy force" - this is what knocks people down),
//             takes damage by distance, loose objects fly.
using System;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const string C7 = "Rage Engine: Developer", CT = "Rage Engine: Toys";
        internal enum ToolKind { Pistol, Dynamite, Bat, Pipe, NailBat, Crowbar, Bottle, BottleBroken, PoolCue, Salts }
        static bool LongTool(ToolKind k) => k == ToolKind.Bat || k == ToolKind.Pipe || k == ToolKind.NailBat || k == ToolKind.Crowbar || k == ToolKind.PoolCue || k == ToolKind.Bottle;
        static bool BladeTool(ToolKind k) => k == ToolKind.BottleBroken || k == ToolKind.Salts;

        public static ModOptionString[] spawnButton = { new ModOptionString("Spawn", null, "spawn") };

        public static void SpawnTestPistol(string _) => Guard("tools", () => SpawnTool(ToolKind.Pistol));

        public static void SpawnTestDynamite(string _) => Guard("tools", () => SpawnTool(ToolKind.Dynamite));

        public static void SpawnBat(string _) => Guard("tools", () => SpawnTool(ToolKind.Bat));

        public static void SpawnPipe(string _) => Guard("tools", () => SpawnTool(ToolKind.Pipe));

        public static float pistolDamage = 25f;

        public static float bulletForce = 6f;

        public static float dynamitePower = 1f;

        static readonly Dictionary<Item, ToolKind> tools = new Dictionary<Item, ToolKind>();
        sealed class Fuse { public Item item; public float end; }
        static readonly List<Fuse> fuses = new List<Fuse>();
        static float nextShot;

        // ---------------- spawning ----------------
        static void SpawnTool(ToolKind kind) => SpawnTool(kind, null, null);
        internal static void SpawnTool(ToolKind kind, Vector3? at, System.Action<Item> then)
        {
            var data = LongTool(kind)
                ? FindItem(d => d.type == ItemData.Type.Weapon && (Contains(d.id, "club") || Contains(d.id, "mace")) && !Contains(d.id, "throw"))
                  ?? FindItem(d => d.type == ItemData.Type.Weapon && (Contains(d.id, "hammer") || Contains(d.id, "staff")))
                  ?? FindItem(d => d.type == ItemData.Type.Weapon)
                : kind == ToolKind.Pistol || BladeTool(kind)
                ? FindItem(d => d.type == ItemData.Type.Weapon && Contains(d.id, "dagger"))
                  ?? FindItem(d => d.type == ItemData.Type.Weapon && Contains(d.id, "knife"))
                  ?? FindItem(d => d.type == ItemData.Type.Weapon)
                : FindItem(d => d.type == ItemData.Type.Potion)
                  ?? FindItem(d => Contains(d.id, "bottle") || Contains(d.id, "flask"))
                  ?? FindItem(d => d.type == ItemData.Type.Food || d.type == ItemData.Type.Misc);
            if (data == null) { Debug.LogWarning("[RageEngine] test tools: no suitable base item found"); return; }

            var cam = Player.local != null && Player.local.head != null && Player.local.head.cam != null ? Player.local.head.cam.transform : null;
            Vector3 pos = at ?? (cam != null ? cam.position + cam.forward * 0.45f - Vector3.up * 0.25f : Vector3.zero);
            Quaternion rot = cam != null ? Quaternion.LookRotation(cam.forward, Vector3.up) : Quaternion.identity;
            data.SpawnAsync(item =>
            {
                if (item == null) return;
                tools[item] = kind;
                item.OnHeldActionEvent += (hand, handle, action) => Guard("tools", () => OnToolAction(item, kind, hand, action));
                if (kind == ToolKind.Pistol) item.OnGrabEvent += (handle, hand) => Guard("grip", () => FitToHand(item, kind, hand));
                bool dressed = false;
                Guard("model", () => dressed = Dress(item, kind));
                if (!dressed) Guard("tint", () => Tint(item, kind == ToolKind.Pistol ? new Color(0.18f, 0.18f, 0.2f) : kind == ToolKind.Dynamite ? new Color(0.75f, 0.08f, 0.05f) : new Color(0.6f, 0.42f, 0.25f)));
                Log($"spawned {kind} from '{data.id}'");
                if (then != null) Guard("tool.then", () => then(item));
            }, pos, rot, null, false);                                                     // 0.20: not pooled (its looks are changed)
        }

        // 0.20: the real models (Models/RE_Prop_*.obj + Textures/RE_Prop_*.png) on the base item; its own look hidden.
        static readonly Dictionary<string, Material> propMats = new Dictionary<string, Material>();
        static bool Dress(Item item, ToolKind kind)
        {
            string n = kind.ToString();
            var mesh = BoneModels.Get("RE_Prop_" + n);
            if (mesh == null) return false;
            if (!propMats.TryGetValue(n, out var mat))
            {
                Texture2D tex = null;
                try
                {
                    string p = System.IO.Path.Combine(modFolder ?? "", "Textures", "RE_Prop_" + n + ".png");
                    if (System.IO.File.Exists(p)) { tex = new Texture2D(2, 2); tex.LoadImage(System.IO.File.ReadAllBytes(p)); }
                }
                catch { }
                mat = Mats.MakeTextured(tex, null, Color.white, kind == ToolKind.Pistol || kind == ToolKind.Pipe || kind == ToolKind.Crowbar || kind == ToolKind.Bottle || kind == ToolKind.BottleBroken ? 0.6f : 0.25f, false);
                propMats[n] = mat;
            }
            foreach (var r in item.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var grip = item.mainHandleRight != null ? item.mainHandleRight.transform : item.handles != null && item.handles.Count > 0 ? item.handles[0].transform : item.transform;
            Vector3 fwd = (item.flyDirRef != null ? item.flyDirRef : item.transform).forward;
            if (LongTool(kind) || BladeTool(kind))
            {
                // 0.21: lie along the base weapon's handle axis, pointing to its far (heavy) end, so what you see is what hits
                Bounds b = default; bool any = false;
                foreach (var r in item.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                Vector3 axis = grip.up;
                if (any && Vector3.Dot(b.center - grip.position, axis) < 0f) axis = -axis;
                fwd = axis;
            }
            Vector3 up = Vector3.ProjectOnPlane(grip.forward, fwd);
            if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(grip.right, fwd);
            var go = new GameObject("RageEngine_Model_" + n);
            go.transform.SetParent(item.transform, false);
            go.transform.position = kind == ToolKind.Dynamite ? item.transform.position : grip.position;
            go.transform.rotation = Quaternion.LookRotation(fwd, up.normalized);
            var ls = item.transform.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            Log($"dressed {item.data?.id} as the {n} model");
            return true;
        }

        // 0.21: the pistol is fitted to the hand that picks it up - barrel along the hand's pointing direction, slide
        // up on the thumb side, its grip in the palm. Re-fitted on every grab (left or right hand).
        static readonly Vector3 PistolGripLocal = new Vector3(0f, -0.055f, -0.01f);
        static void FitToHand(Item item, ToolKind kind, RagdollHand hand)
        {
            if (kind != ToolKind.Pistol || hand == null) return;
            var model = item.transform.Find("RageEngine_Model_Pistol");
            if (model == null) return;
            Vector3 point = hand.PointDir, thumb = hand.ThumbDir;
            if (point.sqrMagnitude < 0.01f || thumb.sqrMagnitude < 0.01f) return;
            var rot = Quaternion.LookRotation(point.normalized, thumb.normalized) * Quaternion.Euler(-12f, 0f, 0f);   // wrist cocked slightly up
            Vector3 palm = hand.grip != null ? hand.grip.position : hand.transform.position;
            model.rotation = rot;
            model.position = palm - rot * PistolGripLocal;
            Log($"pistol fitted to the {hand.side} hand");
        }

        static ItemData FindItem(Func<ItemData, bool> pred)
        {
            var list = Catalog.GetDataList(Category.Item);
            if (list == null) return null;
            foreach (var cd in list) if (cd is ItemData d && pred(d)) return d;
            return null;
        }

        static bool Contains(string s, string sub) => s != null && s.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;

        static void Tint(Item item, Color col)
        {
            foreach (var r in item.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial == null) continue;
                var m = new Material(r.sharedMaterial);
                BoneMesh.Recolour(m, col, 0.6f);
                r.material = m;
            }
        }

        static void OnToolAction(Item item, ToolKind kind, RagdollHand hand, Interactable.Action action)
        {
            if (action == Interactable.Action.Grab) { Guard("grip", () => FitToHand(item, kind, hand)); return; }
            if (action != Interactable.Action.UseStart) return;
            if (kind == ToolKind.Pistol) Fire(item, hand);
            else if (kind == ToolKind.Dynamite) Light(item, hand);
        }

        // ---------------- pistol ----------------
        static void Fire(Item item, RagdollHand hand)
        {
            if (Time.time < nextShot) return;
            nextShot = Time.time + 0.18f;
            var model = item.transform.Find("RageEngine_Model_Pistol");                    // 0.21: shoots where the modelled barrel points
            var muzzle = model != null ? model : item.flyDirRef != null ? item.flyDirRef : item.transform;
            Vector3 dir = muzzle.forward;
            Vector3 origin = (model != null ? model.position : item.transform.position) + dir * 0.15f;
            Sfx.Play("NRE_Gunshot", item.transform, 1f);
            Emit(item.transform.position, 35f, Stim.Gunshot);
            if (hand != null) Haptics.Pulse(hand.side, 1f, 2);
            if (item.physicBody != null) item.physicBody.AddForce(-dir * 1.5f + Vector3.up * 1f, ForceMode.VelocityChange);   // recoil

            // bullet: thin sphere cast; ignores the gun itself and the player's own body/held items
            RagdollPart target = null; Vector3 tp = origin + dir * 60f, tn = -dir; float wallDist = 120f; Rigidbody wallRb = null;
            var hits = Physics.SphereCastAll(origin, 0.025f, dir, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var hitItem = h.collider.GetComponentInParent<Item>();
                if (hitItem == item) continue;
                if (hitItem != null && hitItem.mainHandler != null && hitItem.mainHandler.creature != null && hitItem.mainHandler.creature.isPlayer) continue;
                var rp = h.collider.GetComponentInParent<RagdollPart>();
                var c = rp != null && rp.ragdoll != null ? rp.ragdoll.creature : null;
                if (c != null && c.isPlayer) continue;
                Vector3 hp = h.distance <= 0f ? origin : h.point;
                if (c != null) { target = rp; tp = hp; tn = h.normal; break; }
                wallDist = h.distance; tp = hp; tn = h.normal; wallRb = h.rigidbody; break;
            }
            // aim assist: a body part within 25 cm of the bullet's path counts as hit (VR aiming is hard)
            if (target == null) target = AimAssist(origin, dir, wallDist, ref tp, ref tn);
            Guard("fx", () => MuzzleFx(origin, tp));
            if (target != null) Bullet(target.ragdoll.creature, target, tp, tn, dir);
            else if (wallRb != null) wallRb.AddForceAtPosition(dir * 4f, tp, ForceMode.Impulse);
            Log($"BANG -> {(target != null ? target.ragdoll.creature.name + " " + target.type : "missed")}");
        }

        static RagdollPart AimAssist(Vector3 origin, Vector3 dir, float maxDist, ref Vector3 point, ref Vector3 normal)
        {
            RagdollPart best = null; float bestD = 0.25f;
            var list = Creature.allActive;
            if (list == null) return null;
            foreach (var c in list)
            {
                if (c == null || c.isPlayer || c.ragdoll == null || c.ragdoll.parts == null) continue;
                foreach (var p in c.ragdoll.parts)
                {
                    if (p == null || p.isSliced) continue;
                    Vector3 v = p.transform.position - origin;
                    float t = Vector3.Dot(v, dir);
                    if (t < 0.2f || t > maxDist) continue;
                    float d = (v - dir * t).magnitude;
                    if (d < bestD) { bestD = d; best = p; point = origin + dir * t; normal = -dir; }
                }
            }
            return best;
        }

        static void MuzzleFx(Vector3 from, Vector3 to)
        {
            var flash = new GameObject("RageEngine_Muzzle");
            flash.transform.position = from;
            var l = flash.AddComponent<Light>();
            l.type = LightType.Point; l.range = 4f; l.intensity = 4f; l.color = new Color(1f, 0.75f, 0.35f);
            UnityEngine.Object.Destroy(flash, 0.06f);
            var tr = new GameObject("RageEngine_Tracer");
            var lr = tr.AddComponent<LineRenderer>();
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) lr.sharedMaterial = new Material(sh);
            lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, to);
            lr.startWidth = 0.012f; lr.endWidth = 0.004f;
            lr.startColor = new Color(1f, 0.9f, 0.5f, 0.9f); lr.endColor = new Color(1f, 0.8f, 0.4f, 0.2f);
            UnityEngine.Object.Destroy(tr, 0.05f);
        }

        static void Bullet(Creature c, RagdollPart rp, Vector3 point, Vector3 normal, Vector3 dir)
        {
            // full-body physics on, so the bullet actually moves the body
            if (instance != null) instance.PhysicalNow(c, 4f);
            var ds = new DamageStruct
            {
                damageType = DamageType.Pierce, damage = pistolDamage, pushLevel = 1,
                hitRagdollPart = rp, penetration = DamageStruct.Penetration.Skewer, penetrationDeepReached = true
            };
            var ci = new CollisionInstance { damageStruct = ds, contactPoint = point, contactNormal = normal, impactVelocity = dir * 12f };
            float before = -1f, after = -1f;
            Guard("health", () => before = c.currentHealth);
            bool head = (rp.type & (RagdollPart.Type.Head | RagdollPart.Type.Neck)) != 0;
            float dmg = pistolDamage * (head ? 2.5f : 1f);
            lastHandled = null;
            if (!c.isKilled) Guard("gun.damage", () => c.Damage(ci));
            Guard("health", () => after = c.currentHealth);
            // the game ignored it (no damage taken): apply it directly
            if (!c.isKilled && before >= 0f && after >= before - 0.01f) Guard("gun.damage2", () => c.Damage(dmg));
            if (lastHandled != ci && instance != null) Guard("hit", () => instance.HandleHit(ci));

            // knockback: the hit part snaps back, the body rocks; it's partial ragdoll, not a flying cartoon
            float f = bulletForce * (c.isKilled ? 1.6f : 1f);
            if (rp.physicBody != null) rp.physicBody.AddForceAtPosition(dir * f, point, ForceMode.VelocityChange);
            if (rp.parentPart != null && rp.parentPart.physicBody != null) rp.parentPart.physicBody.AddForce(dir * f * 0.4f, ForceMode.VelocityChange);
            var root = c.ragdoll.rootPart;
            if (root != null && root.physicBody != null) root.physicBody.AddForce(Vector3.ProjectOnPlane(dir, Vector3.up) * f * 0.15f, ForceMode.VelocityChange);
            if (instance != null && instance.TryGet(c, out var s) && !c.isKilled)
            {
                Loosen(s, LimbOf(rp.type) != 0 ? LimbOf(rp.type) | RagdollPart.Type.Torso : RagdollPart.Type.Torso | RagdollPart.Type.Neck | RagdollPart.Type.Head, 0.9f, 0.7f);
                s.balance = Mathf.Max(MinBalance, s.balance - 0.25f);
                s.pain = Mathf.Min(1f, s.pain + 0.4f);
            }
            Sfx.PlayAt("NRE_BulletHit", point, 1f);
            Guard("bulletpaint", () => { Stamp(c, point, 0.022f, "gunshot_entry"); Stamp(c, point + dir * 0.22f, 0.04f, "gunshot_exit"); Stamp(c, point, 0.04f, "blood_splatter", 0.7f, true); GroundBlood(point + dir * 0.6f, "blood_ground_splat", 0.5f, c); });
            var wound = new GameObject("RageEngine_BulletWound").transform;
            wound.SetParent(rp.transform, false);
            wound.position = point;
            RageEngineScript.Blood(wound);
            UnityEngine.Object.Destroy(wound.gameObject, 20f);
            Log($"BULLET -> {c.name} {rp.type} hp {before:0}->{after:0} force {f:0}");
        }

        // ---------------- dynamite ----------------
        static void Light(Item item, RagdollHand hand)
        {
            foreach (var f in fuses) if (f.item == item) return;
            fuses.Add(new Fuse { item = item, end = Time.time + 3.5f });
            Sfx.Play("NRE_Fuse", item.transform, 0.9f);
            if (hand != null) Haptics.Pulse(hand.side, 0.5f, 1);
            Log("dynamite lit");
        }

        static void TickTools()
        {
            for (int i = fuses.Count - 1; i >= 0; i--)
            {
                var f = fuses[i];
                if (f.item == null) { fuses.RemoveAt(i); continue; }
                if (Time.time < f.end) continue;
                fuses.RemoveAt(i);
                Vector3 pos = f.item.transform.position;
                tools.Remove(f.item);
                Guard("tools", () => f.item.Despawn());
                Explode(pos);
            }
        }

        static void Explode(Vector3 pos)
        {
            float power = Mathf.Max(0.25f, dynamitePower);
            float radius = 7f * Mathf.Sqrt(power);
            Sfx.PlayAt("NRE_Explosion", pos, 1f);
            Emit(pos, 40f, Stim.Explosion);
            foreach (var side in new[] { Side.Left, Side.Right }) Haptics.Pulse(side, 1f, 3);

            var cols = Physics.OverlapSphere(pos, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var done = new HashSet<Creature>();
            var pushed = new HashSet<Rigidbody>();
            // creatures first: knock them off their feet, damage by distance
            foreach (var col in cols)
            {
                var rp = col.GetComponentInParent<RagdollPart>();
                var c = rp != null && rp.ragdoll != null ? rp.ragdoll.creature : null;
                if (c == null || c.isPlayer || !done.Add(c)) continue;
                var root = c.ragdoll.rootPart != null ? c.ragdoll.rootPart.transform.position : c.transform.position;
                float f = 1f - Vector3.Distance(root, pos) / radius;
                if (f <= 0f) continue;
                if (instance != null) instance.PhysicalNow(c, 5f);
                if (instance != null && instance.TryGet(c, out var fs)) AllowFall(fs, 3f);
                if (f > 0.15f) Guard("fall", () => c.ragdoll.SetState(Ragdoll.State.Destabilized));
                if (f > 0.6f && instance != null && instance.TryGet(c, out var gs)) Guard("gore", () => instance.TearFlesh(gs, rp, rp.transform.position, (rp.transform.position - pos).normalized, 4f * f * power));
                if (instance != null && instance.TryGet(c, out var s)) { s.balance = MinBalance; s.pain = Mathf.Min(1f, s.pain + f); s.lastHitDir = (root - pos).normalized; }
                float dmg = 90f * f * f * power;
                if (!c.isKilled) Guard("blast.damage", () => c.Damage(dmg));
                Log($"BLAST -> {c.name} f={f:0.00} dmg={dmg:0}");
            }
            // then throw every loose body part and object
            foreach (var col in cols)
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic || !pushed.Add(rb)) continue;
                var rp = col.GetComponentInParent<RagdollPart>();
                if (rp != null && rp.ragdoll != null && rp.ragdoll.creature != null && rp.ragdoll.creature.isPlayer) continue;
                rb.AddExplosionForce(14f * power, pos, radius, 1.2f, ForceMode.VelocityChange);
            }
        }
    }
}

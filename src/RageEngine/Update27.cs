// Rage Engine 0.27
//   WEAPONS       Mod Options > Rage Engine: Weapons: baseball bat, nail bat, lead pipe, crowbar, pool cue, whiskey
//                 bottle (smashes on a head and leaves you holding the jagged neck) - real models on game weapons.
//   SMELLING SALTS (the surprise) hold the capsule under a knocked-out NPC's nose for a second and he comes round.
//   PASSING OUT   a beaten NPC sometimes just passes out instead of dying: at low HP, and sometimes at the killing blow
//                 itself (blunt only, never a super soldier, once per NPC). He lies there breathing for 15-30 s.
//   3-D SWELLING  solid knots rise where you hit: a lump that grows with every blow to the same spot, flushed red then
//                 purple. Faces, arms, legs, ribs. (Separate objects - they show even where mesh deformation can't.)
//   SUPER SOLDIERS fatal attacks are never negated any more (no adrenaline, no dying-moments reprieve) - except
//                 head shots: the head is not a weak point on a super serum, he gets back up from those (not from a
//                 decapitation).
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        const string CW = "Rage Engine: Weapons";
        public static ModOptionString[] weaponNames =
        {
            new ModOptionString("Baseball bat", null, "Bat"), new ModOptionString("Nail bat", null, "NailBat"), new ModOptionString("Lead pipe", null, "Pipe"),
            new ModOptionString("Crowbar", null, "Crowbar"), new ModOptionString("Pool cue", null, "PoolCue"), new ModOptionString("Whiskey bottle", null, "Bottle"),
            new ModOptionString("Broken bottle", null, "BottleBroken"), new ModOptionString("Smelling salts", null, "Salts"),
        };
        [ModOptionCategory(CW, 13), ModOptionOrder(0)]
        [ModOption("Weapon", "Pick one, then press Spawn it. It appears in front of you.", valueSourceName = nameof(weaponNames), defaultValueIndex = 0)]
        public static string weaponPick = "Bat";
        [ModOptionButton, ModOptionCategory(CW, 13), ModOptionOrder(1)]
        [ModOption("Spawn it", "Spawns the picked weapon in front of you.", valueSourceName = nameof(mapGo), saveValue = false)]
        public static void SpawnWeaponButton(string _) => Guard("weapon", () => { if (System.Enum.TryParse<ToolKind>(weaponPick, out var k)) SpawnTool(k, null, null); });

        [ModOptionCategory(C8, 8), ModOptionOrder(50)]
        [ModOption("Passing out", "ON: beaten NPCs sometimes pass out instead of dying (blunt damage, never super soldiers).", defaultValueIndex = 1)]
        public static bool passOut = true;
        [ModOptionCategory(C4, 4), ModOptionOrder(34)]
        [ModOption("3D swelling", "ON: solid lumps and knots rise where you hit, growing with every blow to the same spot.", defaultValueIndex = 1)]
        public static bool knots3d = true;

        // ------------------------------------------------------------------ weapons hitting NPCs
        /// From HandleHit: special weapons.
        void ToolHit27(NpcState s, CollisionInstance hit, RagdollPart part, float severity)
        {
            if (!PlayerSource(hit, out _, out var item, out float speed) || item == null) return;
            if (!tools.TryGetValue(item, out var kind)) return;
            var c = s.creature;
            Vector3 p = hit.contactPoint;
            if (kind == ToolKind.Bottle && speed > 3.2f)
            {
                // smash
                Sfx.PlayAt("NRE_GlassBreak", p, 0.9f);
                Guard("shards", () => Puff(p, new Color(0.55f, 0.85f, 0.6f), 16, 1.6f));
                Stamp(c, p, 0.035f, "laceration", 0.9f); Blood(part.transform);
                s.bleedRate += 0.12f * bleedingRate;
                if (Has(part.type, RagdollPart.Type.Head)) { s.daze = Mathf.Max(s.daze, 1f); if (!c.isKilled && Random.value < 0.35f) Knockout(s, hit.impactVelocity, true); }
                var holder = item.mainHandler;
                Vector3 at = item.transform.position; Quaternion rot = item.transform.rotation;
                tools.Remove(item);
                if (holder != null) Guard("ungrab", () => holder.UnGrab(false));
                Guard("despawn", () => item.Despawn());
                SpawnTool(ToolKind.BottleBroken, at, ni =>
                {
                    if (holder == null || ni == null) return;
                    var h = ni.GetMainHandle(holder.side);
                    if (h != null) holder.Grab(h, true, false);
                });
                Log($"{WhoIs(s)}: bottle smashed over his {part.type}");
            }
            else if (kind == ToolKind.NailBat && speed > 2.5f)
            {
                Stamp(c, p, 0.03f, "laceration", 0.9f); Stamp(c, p + Random.onUnitSphere * 0.02f, 0.02f, "blood_drip", 0.85f);
                Blood(part.transform);
                s.bleedRate += 0.1f * bleedingRate; s.pain = Mathf.Min(1f, s.pain + 0.2f);
            }
            else if (kind == ToolKind.BottleBroken && speed > 1.5f)
            {
                Stamp(c, p, 0.04f, "laceration", 0.95f); s.bleedRate += 0.18f * bleedingRate;
            }
        }

        /// 0.1 s tick: smelling salts under a knocked-out nose.
        void TickSalts(NpcState s, float now)
        {
            if (!s.knockedOut || s.creature.isKilled || tools.Count == 0) return;
            var ce = s.creature.centerEyes; if (ce == null) return;
            Vector3 nose = ce.position - ce.up * 0.045f + ce.forward * 0.07f;
            bool near = false;
            foreach (var kv in tools) if (kv.Value == ToolKind.Salts && kv.Key != null && kv.Key.mainHandler != null && (kv.Key.transform.position - nose).sqrMagnitude < 0.2f * 0.2f) { near = true; break; }
            if (!near) { s.saltsT = 0f; return; }
            s.saltsT += 0.1f;
            if (s.saltsT < 0.9f) return;
            s.saltsT = 0f;
            s.koDeathAt = 0f; s.finalKill = false; s.noGasp = false;
            s.koWakeAt = now;                                                   // TickKnockout brings him round
            Sfx.PlayAt("NRE_BreathCritical", nose, 0.9f);
            Log($"{WhoIs(s)}: smelling salts - he comes round");
        }

        // ------------------------------------------------------------------ passing out
        void PassOut(NpcState s, Vector3 dir, string why)
        {
            var c = s.creature;
            if (s.knockedOut || c.isKilled) return;
            bool kk = knockoutKills; knockoutKills = false;
            try { Knockout(s, dir, true); } finally { knockoutKills = kk; }
            if (!s.knockedOut) return;
            s.passedOut = true;
            s.koWakeAt = Time.time + Random.Range(15f, 30f); s.koDeathAt = 0f; s.finalKill = false;
            s.bleedRate *= 0.3f;
            Log($"{WhoIs(s)}: PASSES OUT ({why})");
        }

        /// From HandleHit: low on HP and still taking a beating -> lights out.
        void MaybePassOut(NpcState s, CollisionInstance hit, float severity)
        {
            if (!passOut || s.passedOut || s.creature.isKilled || Tank(s) || severity < 0.6f) return;
            if (hit.damageStruct.damageType != DamageType.Blunt) return;
            if (HpFrac(s.creature) > 0.25f || Random.value > 0.3f) return;
            Guard("passout.heal", () => s.creature.Heal(s.creature.maxHealth * 0.1f));
            PassOut(s, hit.impactVelocity, "beaten down");
        }

        /// From HandleKill: true if this death was turned into something else.
        bool DeathRules27(NpcState s, Creature victim, CollisionInstance hit)
        {
            if (s == null || s.finalKill) return false;
            var head = victim.ragdoll != null ? victim.ragdoll.headPart : null;
            if (head != null && head.isSliced) return false;
            var part = hit != null ? hit.damageStruct.hitRagdollPart : null;
            bool headHit = part != null && Has(part.type, RagdollPart.Type.Head);
            if (Tank(s))
            {
                // the head is not a weak point on a super serum
                if (headHit && s.headSaves < 4)
                {
                    s.headSaves++;
                    Revive(s, Mathf.Max(25f, victim.maxHealth * (0.4f - 0.08f * s.headSaves)));
                    s.pain = 0f; s.rushUntil = Time.time + 3f;
                    Log($"{WhoIs(s)}: head shot #{s.headSaves} - not a weak point on a super serum, he gets back up");
                    return true;
                }
                return false;
            }
            // an ordinary man beaten to death sometimes just passes out
            if (passOut && !s.passedOut && !s.dismembered && hit != null && hit.damageStruct.damageType == DamageType.Blunt && !headHit && Random.value < 0.3f)
            {
                Revive(s, Mathf.Max(12f, victim.maxHealth * 0.15f));
                Vector3 dir = hit.impactVelocity;
                PassOut(s, dir, "instead of dying");
                return s.knockedOut;
            }
            return false;
        }

        // ------------------------------------------------------------------ 3-D swelling
        public sealed class Knot { public GameObject go; public RagdollPart part; public float dmg; public bool logged; }
        static Mesh knotMesh;

        /// From HandleHit (blunt): raise / grow a lump where it landed.
        void Swell27(NpcState s, RagdollPart part, Vector3 point, float severity)
        {
            if (!knots3d || severity < 0.55f || part == null) return;
            var c = s.creature;
            Knot k = null;
            foreach (var x in s.knots) if (x.go != null && x.part == part && (x.go.transform.position - point).sqrMagnitude < 0.06f * 0.06f) { k = x; break; }
            if (k == null)
            {
                if (s.knots.Count >= 12) return;
                if (!SkinAt(c, point, 0.07f, out var sp, out var sn, out bool covered) || covered) return;
                if (knotMesh == null)
                {
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    knotMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                    Object.Destroy(tmp);
                }
                var go = new GameObject("RageEngine_Knot");
                go.transform.SetParent(part.transform, true);
                go.transform.position = sp - sn * 0.004f;
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, sn);
                go.AddComponent<MeshFilter>().sharedMesh = knotMesh;
                var r = go.AddComponent<MeshRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                k = new Knot { go = go, part = part };
                s.knots.Add(k);
            }
            k.dmg += Mathf.Min(severity, 2f);
            bool head = Has(part.type, RagdollPart.Type.Head);
            bool limb = Has(part.type, ArmParts | LegParts);
            float g = Mathf.Clamp01(k.dmg / 5f);
            float w = Mathf.Lerp(head ? 0.022f : 0.03f, head ? 0.05f : limb ? 0.075f : 0.085f, g);       // across
            float h = Mathf.Lerp(0.008f, head ? 0.022f : 0.03f, g);                                       // how far it stands out
            var ls = part.transform.lossyScale;
            // limbs swell along the limb
            Vector3 size = new Vector3(w, h, limb ? w * 1.5f : w);
            k.go.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, ls.x), size.y / Mathf.Max(0.01f, ls.y), size.z / Mathf.Max(0.01f, ls.z));
            Color skin = Mats.SkinOf(c);
            Color col = Color.Lerp(Color.Lerp(skin, new Color(0.62f, 0.2f, 0.22f), 0.35f + 0.25f * g), new Color(0.3f, 0.12f, 0.3f), Mathf.Clamp01(g * 1.4f - 0.5f));
            k.go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Make(col, 0.35f);
            if (k.dmg >= 2.5f && !k.logged) { k.logged = true; Log($"{WhoIs(s)}: a knot the size of {(g > 0.7f ? "a fist" : "an egg")} on his {part.type}"); }
        }

        static void ClearKnots(NpcState s)
        {
            foreach (var k in s.knots) if (k.go != null) Object.Destroy(k.go);
            s.knots.Clear();
        }
    }
}

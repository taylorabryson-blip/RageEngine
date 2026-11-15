// Nomad Rage Engine - version-tolerant access to the game (reflection, cached).
// Members are found by NAME at runtime: a field or a property, any extra optional parameters filled with their
// defaults. So differences between game versions (e.g. SetState(state) vs SetState(state, bool)) can't break
// loading: a member that truly doesn't exist just makes that one feature switch itself off (logged once).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RageEngine
{
    public static class G
    {
        const BindingFlags Inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags Stat = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        static readonly Dictionary<string, Type> types = new Dictionary<string, Type>();
        static readonly Dictionary<string, MemberInfo> members = new Dictionary<string, MemberInfo>();
        static readonly Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>();
        static readonly HashSet<string> warned = new HashSet<string>();

        public static void Warn(string what)
        {
            if (warned.Add(what)) Debug.LogWarning("[RageEngine] unavailable in this game version, feature disabled: " + what);
        }

        // ---------- types ----------
        public static Type T(string fullName)
        {
            if (types.TryGetValue(fullName, out var t)) return t;
            t = Type.GetType(fullName + ", ThunderRoad");
            if (t == null)
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    if ((t = asm.GetType(fullName)) != null) break;
            if (t == null) Warn("type " + fullName);
            types[fullName] = t;
            return t;
        }

        public static object Enum(string enumType, string value)
        {
            var t = T(enumType);
            if (t == null) return null;
            try { return System.Enum.Parse(t, value); } catch { Warn(enumType + "." + value); return null; }
        }

        // ---------- fields / properties ----------
        static MemberInfo Member(Type type, string name, bool isStatic)
        {
            string key = (isStatic ? "S:" : "I:") + type.FullName + "." + name;
            if (members.TryGetValue(key, out var m)) return m;
            var flags = isStatic ? Stat : Inst;
            for (var t = type; t != null && m == null; t = t.BaseType)
                m = (MemberInfo)t.GetField(name, flags | BindingFlags.DeclaredOnly) ?? t.GetProperty(name, flags | BindingFlags.DeclaredOnly);
            if (m == null) Warn(type.Name + "." + name);
            members[key] = m;
            return m;
        }

        public static object Get(object o, string name)
        {
            if (o == null) return null;
            var m = Member(o.GetType(), name, false);
            try
            {
                if (m is FieldInfo f) return f.GetValue(o);
                if (m is PropertyInfo p) return p.GetValue(o, null);
            }
            catch (Exception e) { Warn(name + ": " + e.GetType().Name); }
            return null;
        }

        public static T Get<T>(object o, string name, T fallback = default)
        {
            var v = Get(o, name);
            if (v is T t) return t;
            if (v != null && typeof(T) != typeof(object))
                try { return (T)Convert.ChangeType(v, typeof(T)); } catch { }
            return fallback;
        }

        public static object GetStatic(Type type, string name)
        {
            if (type == null) return null;
            var m = Member(type, name, true);
            try
            {
                if (m is FieldInfo f) return f.GetValue(null);
                if (m is PropertyInfo p) return p.GetValue(null, null);
            }
            catch (Exception e) { Warn(name + ": " + e.GetType().Name); }
            return null;
        }

        public static void Set(object o, string name, object value)
        {
            if (o == null) return;
            var m = Member(o.GetType(), name, false);
            try
            {
                if (m is FieldInfo f) f.SetValue(o, Convert.ChangeType(value, f.FieldType));
                else if (m is PropertyInfo p && p.CanWrite) p.SetValue(o, Convert.ChangeType(value, p.PropertyType), null);
            }
            catch (Exception e) { Warn("set " + name + ": " + e.GetType().Name); }
        }

        // ---------- methods ----------
        static bool Fits(ParameterInfo[] ps, object[] args)
        {
            if (ps.Length < args.Length) return false;
            for (int i = 0; i < ps.Length; i++)
            {
                var pt = ps[i].ParameterType;
                if (i < args.Length)
                {
                    var a = args[i];
                    if (a == null) { if (pt.IsValueType) return false; }
                    else if (!pt.IsInstanceOfType(a)) return false;
                }
                else if (!ps[i].IsOptional && !IsParams(ps[i])) return false;
            }
            return true;
        }

        static bool IsParams(ParameterInfo p) => p.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0;

        static object[] Fill(ParameterInfo[] ps, object[] args)
        {
            if (ps.Length == args.Length) return args;
            var full = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                if (i < args.Length) full[i] = args[i];
                else if (IsParams(ps[i])) full[i] = Array.CreateInstance(ps[i].ParameterType.GetElementType(), 0);
                else full[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
            }
            return full;
        }

        static MethodInfo Find(Type type, string name, object[] args, bool isStatic, Type[] generic)
        {
            var sb = new System.Text.StringBuilder(isStatic ? "S:" : "I:").Append(type.FullName).Append('.').Append(name);
            if (generic != null) foreach (var g in generic) sb.Append('<').Append(g.FullName).Append('>');
            foreach (var a in args) sb.Append('|').Append(a == null ? "null" : a.GetType().FullName);
            string key = sb.ToString();
            if (methods.TryGetValue(key, out var mi)) return mi;

            MethodInfo best = null; int bestExtra = int.MaxValue;
            foreach (var m in type.GetMethods(isStatic ? Stat : Inst))
            {
                if (m.Name != name) continue;
                var cand = m;
                if (generic != null)
                {
                    if (!m.IsGenericMethodDefinition || m.GetGenericArguments().Length != generic.Length) continue;
                    try { cand = m.MakeGenericMethod(generic); } catch { continue; }
                }
                else if (m.IsGenericMethodDefinition) continue;
                var ps = cand.GetParameters();
                if (!Fits(ps, args)) continue;
                int extra = ps.Length - args.Length;
                if (extra < bestExtra) { best = cand; bestExtra = extra; }
            }
            if (best == null) Warn(type.Name + "." + name + "(" + args.Length + " args)");
            methods[key] = best;
            return best;
        }

        public static object Call(object o, string name, params object[] args) => CallG(o, name, null, args);

        public static object CallG(object o, string name, Type[] generic, params object[] args)
        {
            if (o == null) return null;
            var m = Find(o.GetType(), name, args, false, generic);
            if (m == null) return null;
            try { return m.Invoke(o, Fill(m.GetParameters(), args)); }
            catch (TargetInvocationException e) { Debug.LogWarning("[RageEngine] " + name + " threw " + e.InnerException?.GetType().Name); return null; }
            catch (Exception e) { Warn(name + ": " + e.GetType().Name); return null; }
        }

        public static object CallStatic(Type type, string name, Type[] generic, params object[] args)
        {
            if (type == null) return null;
            var m = Find(type, name, args, true, generic);
            if (m == null) return null;
            try { return m.Invoke(null, Fill(m.GetParameters(), args)); }
            catch (TargetInvocationException e) { Debug.LogWarning("[RageEngine] " + name + " threw " + e.InnerException?.GetType().Name); return null; }
            catch (Exception e) { Warn(name + ": " + e.GetType().Name); return null; }
        }

        // ---------- game-specific helpers ----------
        static Type tCreature, tCatalog, tEffectData, tPlayerControl;
        public static Type CreatureT => tCreature ?? (tCreature = T("ThunderRoad.Creature"));

        public static IList AllCreatures() => GetStatic(CreatureT, "allActive") as IList;

        /// Plays a catalog effect (sound, mesh, particles) attached to a transform. Missing effects are skipped.
        public static void PlayEffect(string id, Transform at)
        {
            if (at == null || string.IsNullOrEmpty(id)) return;
            tCatalog = tCatalog ?? T("ThunderRoad.Catalog");
            tEffectData = tEffectData ?? T("ThunderRoad.EffectData");
            if (tCatalog == null || tEffectData == null) return;
            var data = CallStatic(tCatalog, "GetData", new[] { tEffectData }, id, false);
            if (data == null) return;
            var inst = Call(data, "Spawn", at);
            if (inst != null) Call(inst, "Play");
        }

        /// Controller vibration on the player's hand (0..1+).
        public static void Haptic(object side, float intensity)
        {
            tPlayerControl = tPlayerControl ?? T("ThunderRoad.PlayerControl");
            if (tPlayerControl == null || side == null) return;
            var hand = CallStatic(tPlayerControl, "GetHand", null, side);
            if (hand != null) Call(hand, "HapticShort", intensity);
        }

        /// RagdollPart.Type flags as int (Head=1, Neck=2, Torso=4, LeftArm=8, RightArm=16, LeftHand=32, RightHand=64,
        /// LeftLeg=128, RightLeg=256, LeftFoot=512, RightFoot=1024 per BasSDK).
        public static int PartType(object part)
        {
            var v = Get(part, "type");
            try { return v == null ? 0 : Convert.ToInt32(v); } catch { return 0; }
        }

        public static string RagdollState(object creature) => Get(Get(creature, "ragdoll"), "state")?.ToString() ?? "";
        public static void SetRagdollState(object creature, string state)
        {
            var e = Enum("ThunderRoad.Ragdoll+State", state);
            if (e != null) Call(Get(creature, "ragdoll"), "SetState", e);
        }
    }

    /// RagdollPart.Type flag values (BasSDK).
    public static class P
    {
        public const int Head = 1, Neck = 2, Torso = 4, LeftArm = 8, RightArm = 16, LeftHand = 32, RightHand = 64,
                         LeftLeg = 128, RightLeg = 256, LeftFoot = 512, RightFoot = 1024;
        public const int Arms = LeftArm | RightArm, Hands = LeftHand | RightHand, Legs = LeftLeg | RightLeg | LeftFoot | RightFoot;
    }
}

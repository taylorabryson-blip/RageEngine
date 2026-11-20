// Rage Engine 0.34 - per-feature switches (player feedback) and a performance mode for older Quests.
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        [ModOptionCategory("NPCs", 2), ModOptionOrder(3)]
        [ModOption("Mod voices", "ON: Rage Engine's screams, pain groans and gasps. OFF: only the game's own voices.", defaultValueIndex = 1)]
        public static bool modVoices = true;

        public static bool fistFighting = false;                                  // 0.36: fisticuffs removed - unarmed NPCs are the game's again

        [ModOptionCategory("Gore", 1), ModOptionOrder(11)]
        [ModOption("Bones through skin", "ON: weapons, falls and crushing blows can push broken bones out. Never from punches or kicks.", defaultValueIndex = 1)]
        public static bool bonesOutOpt = true;

        [ModOptionCategory("Gore", 1), ModOptionOrder(12)]
        [ModOption("Face cave-in & skull bits", "ON: repeated blunt blows cave the face in and a crushed skull throws bits. Never from one shot. OFF: none of it, no bits.", defaultValueIndex = 1)]
        public static bool faceCaveOpt = true;

        [ModOptionCategory("NPCs", 2), ModOptionOrder(13)]
        [ModOption("Knockouts & passing out", "ON: big head shots can knock them out, and a man beaten half to death may pass out. OFF: never.", defaultValueIndex = 1)]
        public static bool knockouts = true;

        [ModOptionCategory("Gore", 1), ModOptionOrder(14)]
        [ModOption("Bruise marks", "ON: painted bruises and black eyes on the skin. OFF: no marks (wounds and blood still show).", defaultValueIndex = 0)]
        public static bool bruiseMarks = false;

        public static ModOptionString[] perfValues = { new ModOptionString("Quality (Quest 3)", null, "q"), new ModOptionString("Balanced", null, "b"), new ModOptionString("Performance (Quest 2 / older)", null, "p") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(15)]
        [ModOption("Performance", "Performance = fewer NPCs get the full Rage Engine treatment, fewer sounds, gore pieces and blood decals at once, bodies clean up faster. Use it on Quest 2.", valueSourceName = nameof(perfValues), defaultValueIndex = 1)]
        public static string perfMode = "b";

        internal static int PerfTracked => perfMode == "p" ? 6 : perfMode == "q" ? 20 : 12;
        internal static int PerfVoices => perfMode == "p" ? 6 : perfMode == "q" ? 14 : 10;
        internal static int PerfGorePieces => perfMode == "p" ? 12 : perfMode == "q" ? 60 : 30;
        internal static int PerfDecals => perfMode == "p" ? 25 : perfMode == "q" ? 70 : 45;
        internal static int PerfPainted => perfMode == "p" ? 3 : perfMode == "q" ? 6 : 4;
        internal static float PerfBodyFade => perfMode == "p" ? 60f : 0f;

        /// Head or neck cut off, or dead: no more voice.
        internal static bool Silent(NpcState s)
        {
            var c = s != null ? s.creature : null;
            if (c == null || c.isKilled || s.dying) return true;
            if (c.ragdoll == null) return false;
            foreach (var p in c.ragdoll.parts)
                if (p != null && p.isSliced && (p.type == RagdollPart.Type.Head || p.type == RagdollPart.Type.Neck)) return true;
            return false;
        }

        // the last hit HandleHit is working on
        static bool bareRaw34, projRaw34; static int hitFrame34 = -1;
        /// true only while the hit that set it is being handled (same frame)
        internal static bool bareHit34 => bareRaw34 && Time.frameCount == hitFrame34;
        internal static bool projectileHit34 => projRaw34 && Time.frameCount == hitFrame34;
        static void ClassifyHit34(CollisionInstance hit)
        {
            bool bareHit34 = false, projectileHit34 = false;
            hitFrame34 = Time.frameCount;
            try { Classify34(hit, ref bareHit34, ref projectileHit34); } finally { bareRaw34 = bareHit34; projRaw34 = projectileHit34; }
        }
        static void Classify34(CollisionInstance hit, ref bool bareHit34, ref bool projectileHit34)
        {
            var col = hit != null ? hit.sourceCollider : null;
            if (col == null) return;
            var it = col.GetComponentInParent<Item>();
            if (it != null) { projectileHit34 = it.mainHandler == null; return; }          // flying: arrow, bullet, thrown thing
            bareHit34 = col.GetComponentInParent<RagdollPart>() != null;                   // a fist, a foot, a body
        }
    }
}

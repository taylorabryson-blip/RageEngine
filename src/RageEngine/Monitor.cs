// Rage Engine 0.19 - diagnostics. With Debug log on, every 30 s Player.log gets one line:
//   NPCs tracked, Rage Engine's own CPU time per frame (average and worst), RNAS behavior changes, RNAS conflicts
//   (an NPC flipping behavior 6+ times in 5 s = two conditions fighting - logged with the sequence), navigation
//   failures, physics corrections, glancing hits filtered.
using System.Diagnostics;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        static long perfTicks, perfPeak; static int perfFrames;
        static int rnasSwitches, rnasConflicts, navFailures, physicsFixes;
        static float nextMonitor;

        public override void ScriptUpdate()
        {
            base.ScriptUpdate();
            long t0 = Stopwatch.GetTimestamp();
            UpdateCore();
            long dt = Stopwatch.GetTimestamp() - t0;
            perfTicks += dt; perfFrames++; if (dt > perfPeak) perfPeak = dt;
            if (Time.time >= nextMonitor) { nextMonitor = Time.time + 30f; ReportMonitor(); }
        }

        void ReportMonitor()
        {
            if (!debugLog || perfFrames == 0) { perfTicks = perfPeak = 0; perfFrames = 0; return; }
            double toMs = 1000.0 / Stopwatch.Frequency;
            int n = 0, glance = 0;
            for (int i = 0; i < MaxTracked; i++) if (pool[i].active) { n++; glance += pool[i].glancing; pool[i].glancing = 0; }
            Log($"MONITOR: {n} NPCs | Rage Engine {perfTicks * toMs / perfFrames:0.00} ms/frame avg, {perfPeak * toMs:0.00} ms worst | RNAS changes {rnasSwitches}, conflicts {rnasConflicts} | nav fails {navFailures} | physics fixes {physicsFixes} | glancing hits {glance}");
            perfTicks = perfPeak = 0; perfFrames = 0; rnasSwitches = rnasConflicts = navFailures = physicsFixes = 0;
        }

        /// Called on every RNAS behavior change: catches two systems fighting over an NPC.
        static void NoteSwitch(NpcState s, Behavior to)
        {
            rnasSwitches++;
            float now = Time.time;
            s.switchLog[s.switchIdx % s.switchLog.Length] = now;
            s.switchSeq[s.switchIdx % s.switchSeq.Length] = to;
            s.switchIdx++;
            if (s.switchIdx < 6) return;
            float oldest = s.switchLog[(s.switchIdx - 6) % s.switchLog.Length];
            if (now - oldest < 5f && now >= s.nextConflictLog)
            {
                s.nextConflictLog = now + 10f;
                rnasConflicts++;
                var sb = new System.Text.StringBuilder();
                for (int k = 6; k >= 1; k--) sb.Append(s.switchSeq[(s.switchIdx - k) % s.switchSeq.Length]).Append(k > 1 ? " > " : "");
                Log($"RNAS CONFLICT on {WhoIs(s)}: {sb} within 5 s");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Simple performance profiler to help diagnose lag spikes
    /// </summary>
    public static class QuestProfiler
    {
        private static readonly Dictionary<string, List<long>> measurements = new Dictionary<string, List<long>>();
        private const int MaxMeasurements = 100;
        private static ICoreServerAPI sapi;
        private static long lastLogMs = 0;
        private const long LogIntervalMs = 10000; // Log every 10 seconds

        public static bool Enabled { get; set; } = false;

        public static void Initialize(ICoreServerAPI api)
        {
            sapi = api;
        }

        public static Stopwatch StartMeasurement(string name)
        {
            if (!Enabled) return null;
            var sw = new Stopwatch();
            sw.Start();
            return sw;
        }

        public static void EndMeasurement(string name, Stopwatch sw)
        {
            if (!Enabled) return;
            if (sw == null) return;
            sw.Stop();
            
            long elapsedTicks = sw.ElapsedTicks;
            long elapsedMs = sw.ElapsedMilliseconds;

            lock (measurements)
            {
                if (!measurements.TryGetValue(name, out var list))
                {
                    list = new List<long>();
                    measurements[name] = list;
                }
                list.Add(elapsedMs);
                
                // Keep only last N measurements
                while (list.Count > MaxMeasurements)
                {
                    list.RemoveAt(0);
                }
            }

            // Log if significant lag detected (>50ms)
            if (elapsedMs > 50)
            {
                sapi?.Logger?.Warning("[QuestProfiler] {0} took {1}ms", name, elapsedMs);
            }
        }

        public static void LogStats()
        {
            if (!Enabled) return;
            if (sapi == null) return;
            
            long now = sapi.World.ElapsedMilliseconds;
            if (now - lastLogMs < LogIntervalMs) return;
            lastLogMs = now;

            lock (measurements)
            {
                foreach (var kvp in measurements)
                {
                    if (kvp.Value.Count == 0) continue;
                    
                    long sum = 0;
                    long max = 0;
                    foreach (var ms in kvp.Value)
                    {
                        sum += ms;
                        if (ms > max) max = ms;
                    }
                    double avg = sum / (double)kvp.Value.Count;
                    
                    if (max > 10) // Only log if there's noticeable lag
                    {
                        sapi.Logger.Notification("[QuestProfiler] {0}: avg={1:F2}ms, max={2}ms, samples={3}",
                            kvp.Key, avg, max, kvp.Value.Count);
                    }
                }
            }
        }

        public static void Clear()
        {
            lock (measurements)
            {
                measurements.Clear();
            }
        }
    }
}

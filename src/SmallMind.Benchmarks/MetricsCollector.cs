using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Collects performance metrics during benchmark execution.
    /// </summary>
    internal sealed class MetricsCollector
    {
        private long _allocatedBytesStart;
        private int _gen0Start;
        private int _gen1Start;
        private int _gen2Start;
        private long _workingSetStart;
        private long _workingSetPeak;

        /// <summary>
        /// Start collecting metrics.
        /// </summary>
        public void Start()
        {
            // Force GC to get clean baseline
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

            _allocatedBytesStart = GC.GetAllocatedBytesForCurrentThread();
            _gen0Start = GC.CollectionCount(0);
            _gen1Start = GC.CollectionCount(1);
            _gen2Start = GC.CollectionCount(2);
            
            using var process = Process.GetCurrentProcess();
            _workingSetStart = process.WorkingSet64;
            _workingSetPeak = _workingSetStart;
        }

        /// <summary>
        /// Update peak memory usage.
        /// </summary>
        public void UpdatePeak()
        {
            using var process = Process.GetCurrentProcess();
            var current = process.WorkingSet64;
            if (current > _workingSetPeak)
                _workingSetPeak = current;
        }

        /// <summary>
        /// Stop collecting and return metrics.
        /// </summary>
        public (long allocatedBytes, int gen0, int gen1, int gen2, long peakRSS, long managedHeap) Stop()
        {
            UpdatePeak();

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - _allocatedBytesStart;
            int gen0 = GC.CollectionCount(0) - _gen0Start;
            int gen1 = GC.CollectionCount(1) - _gen1Start;
            int gen2 = GC.CollectionCount(2) - _gen2Start;
            long peakRSS = _workingSetPeak;
            long managedHeap = GC.GetTotalMemory(forceFullCollection: false);

            return (allocatedBytes, gen0, gen1, gen2, peakRSS, managedHeap);
        }

        /// <summary>
        /// Get current GC heap size.
        /// </summary>
        public static long GetManagedHeapSize()
        {
            return GC.GetTotalMemory(forceFullCollection: false);
        }

        /// <summary>
        /// Get GC memory info (requires .NET 7+).
        /// </summary>
        public static (long heapSize, long fragmentedBytes, long totalCommitted) GetGCMemoryInfo()
        {
            try
            {
                var info = GC.GetGCMemoryInfo();
                return (info.HeapSizeBytes, info.FragmentedBytes, info.TotalCommittedBytes);
            }
            catch
            {
                return (0, 0, 0);
            }
        }
    }
}

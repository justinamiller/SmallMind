using System;
using System.Diagnostics;

namespace SmallMind.Benchmarks.Diagnostics
{
    /// <summary>
    /// Tracks memory usage during benchmarks.
    /// </summary>
    internal sealed class MemoryTracker
    {
        private long _startWorkingSet;
        private long _peakWorkingSet;
        private long _startManagedMemory;
        private long _peakManagedMemory;
        private int _startGen0;
        private int _startGen1;
        private int _startGen2;

        /// <summary>
        /// Start tracking memory.
        /// </summary>
        public void Start()
        {
            // Force GC to get clean baseline
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

            _startWorkingSet = GetWorkingSet();
            _peakWorkingSet = _startWorkingSet;
            _startManagedMemory = GC.GetTotalMemory(false);
            _peakManagedMemory = _startManagedMemory;
            _startGen0 = GC.CollectionCount(0);
            _startGen1 = GC.CollectionCount(1);
            _startGen2 = GC.CollectionCount(2);
        }

        /// <summary>
        /// Update peak memory metrics.
        /// </summary>
        public void UpdatePeak()
        {
            long currentWorkingSet = GetWorkingSet();
            if (currentWorkingSet > _peakWorkingSet)
                _peakWorkingSet = currentWorkingSet;

            long currentManaged = GC.GetTotalMemory(false);
            if (currentManaged > _peakManagedMemory)
                _peakManagedMemory = currentManaged;
        }

        /// <summary>
        /// Get snapshot of current memory state.
        /// </summary>
        public MemorySnapshot GetSnapshot()
        {
            UpdatePeak();

            return new MemorySnapshot
            {
                WorkingSetBytes = GetWorkingSet(),
                PeakWorkingSetBytes = _peakWorkingSet,
                WorkingSetDeltaBytes = _peakWorkingSet - _startWorkingSet,
                ManagedMemoryBytes = GC.GetTotalMemory(false),
                PeakManagedMemoryBytes = _peakManagedMemory,
                ManagedMemoryDeltaBytes = _peakManagedMemory - _startManagedMemory,
                Gen0Collections = GC.CollectionCount(0) - _startGen0,
                Gen1Collections = GC.CollectionCount(1) - _startGen1,
                Gen2Collections = GC.CollectionCount(2) - _startGen2
            };
        }

        /// <summary>
        /// Get current process working set in bytes.
        /// </summary>
        private static long GetWorkingSet()
        {
            using var process = Process.GetCurrentProcess();
            return process.WorkingSet64;
        }

        /// <summary>
        /// Get current managed heap size in bytes.
        /// </summary>
        public static long GetManagedMemory()
        {
            return GC.GetTotalMemory(false);
        }

        /// <summary>
        /// Force a full GC and return the memory after collection.
        /// </summary>
        public static long GetManagedMemoryAfterGC()
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            return GC.GetTotalMemory(false);
        }
    }

    /// <summary>
    /// Snapshot of memory state at a point in time.
    /// </summary>
    internal sealed class MemorySnapshot
    {
        public long WorkingSetBytes { get; init; }
        public long PeakWorkingSetBytes { get; init; }
        public long WorkingSetDeltaBytes { get; init; }
        public long ManagedMemoryBytes { get; init; }
        public long PeakManagedMemoryBytes { get; init; }
        public long ManagedMemoryDeltaBytes { get; init; }
        public int Gen0Collections { get; init; }
        public int Gen1Collections { get; init; }
        public int Gen2Collections { get; init; }

        public override string ToString()
        {
            return $"WS: {WorkingSetBytes / 1024 / 1024}MB (peak: {PeakWorkingSetBytes / 1024 / 1024}MB), " +
                   $"Managed: {ManagedMemoryBytes / 1024 / 1024}MB (peak: {PeakManagedMemoryBytes / 1024 / 1024}MB), " +
                   $"GC: Gen0={Gen0Collections}, Gen1={Gen1Collections}, Gen2={Gen2Collections}";
        }
    }
}

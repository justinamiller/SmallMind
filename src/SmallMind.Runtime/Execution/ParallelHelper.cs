namespace SmallMind.Runtime.Execution
{
    /// <summary>
    /// Helper for controlled parallelization based on runtime options.
    /// Respects deterministic mode and parallelization thresholds.
    /// </summary>
    internal static class ParallelHelper
    {
        /// <summary>
        /// Executes a parallel for loop with runtime options control.
        /// Respects DeterministicMode (forces single-threaded) and ParallelizationThreshold.
        /// </summary>
        /// <param name="fromInclusive">Start index (inclusive)</param>
        /// <param name="toExclusive">End index (exclusive)</param>
        /// <param name="body">Action to execute for each index</param>
        /// <param name="options">Runtime options controlling parallelization</param>
        public static void For(int fromInclusive, int toExclusive, Action<int> body, RuntimeOptions? options = null)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            int workSize = toExclusive - fromInclusive;

            // Deterministic mode or options not provided - force single-threaded
            if (options == null || options.DeterministicMode || workSize < options.ParallelizationThreshold)
            {
                for (int i = fromInclusive; i < toExclusive; i++)
                {
                    body(i);
                }
            }
            else
            {
                // Parallel execution with degree of parallelism control
                Parallel.For(fromInclusive, toExclusive,
                    new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism },
                    body);
            }
        }

        /// <summary>
        /// Executes a parallel for loop with local state.
        /// Respects DeterministicMode (forces single-threaded) and ParallelizationThreshold.
        /// </summary>
        public static void For<TLocal>(
            int fromInclusive,
            int toExclusive,
            Func<TLocal> localInit,
            Func<int, ParallelLoopState, TLocal, TLocal> body,
            Action<TLocal> localFinally,
            RuntimeOptions? options = null)
        {
            if (localInit == null)
                throw new ArgumentNullException(nameof(localInit));
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (localFinally == null)
                throw new ArgumentNullException(nameof(localFinally));

            int workSize = toExclusive - fromInclusive;

            // Deterministic mode or small work - force single-threaded
            if (options == null || options.DeterministicMode || workSize < options.ParallelizationThreshold)
            {
                var local = localInit();
                for (int i = fromInclusive; i < toExclusive; i++)
                {
                    local = body(i, null!, local);
                }
                localFinally(local);
            }
            else
            {
                // Parallel execution
                Parallel.For(fromInclusive, toExclusive,
                    new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism },
                    localInit,
                    body,
                    localFinally);
            }
        }

        /// <summary>
        /// Determines if parallelization should be used for the given work size.
        /// </summary>
        public static bool ShouldParallelize(int workSize, RuntimeOptions? options = null)
        {
            if (options == null)
                return false;

            if (options.DeterministicMode)
                return false;

            return workSize >= options.ParallelizationThreshold;
        }
    }
}

using SmallMind.Abstractions;

namespace SmallMind.Engine
{
    /// <summary>
    /// Single entry point for creating a SmallMind inference engine.
    /// This is the stable public API contract.
    /// </summary>
    public static class SmallMind
    {
        /// <summary>
        /// Creates a new SmallMind inference engine with the specified options.
        /// </summary>
        /// <param name="options">Engine options. If null, uses defaults.</param>
        /// <returns>A SmallMind engine instance.</returns>
        public static ISmallMindEngine Create(SmallMindOptions? options = null)
        {
            return new SmallMindEngine(options ?? new SmallMindOptions());
        }
    }
}

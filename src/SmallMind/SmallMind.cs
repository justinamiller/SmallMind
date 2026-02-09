namespace SmallMind
{
    /// <summary>
    /// Single entry point for creating a SmallMind inference engine.
    /// This is the only public factory - all consumers must use this.
    /// </summary>
    public static class SmallMindFactory
    {
        /// <summary>
        /// Creates a new SmallMind inference engine with the specified options.
        /// Validates options and loads the model.
        /// </summary>
        /// <param name="options">Engine options.</param>
        /// <returns>A SmallMind engine instance.</returns>
        /// <exception cref="InvalidOptionsException">Thrown when options are invalid.</exception>
        /// <exception cref="ModelLoadFailedException">Thrown when model fails to load.</exception>
        /// <exception cref="UnsupportedModelFormatException">Thrown when model format is not supported.</exception>
        public static ISmallMindEngine Create(SmallMindOptions options)
        {
            return new Internal.SmallMindEngineAdapter(options);
        }
    }
}

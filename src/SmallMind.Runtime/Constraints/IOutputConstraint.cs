namespace SmallMind.Runtime.Constraints
{
    /// <summary>
    /// Interface for output constraints that guide structured generation.
    /// Implementations enforce specific output formats (JSON, regex patterns, etc).
    /// </summary>
    public interface IOutputConstraint
    {
        /// <summary>
        /// Determines whether a candidate token is allowed given the current generation state.
        /// </summary>
        /// <param name="generatedSoFar">The text generated so far.</param>
        /// <param name="candidateTokenId">The ID of the candidate token being evaluated.</param>
        /// <param name="candidateTokenText">The text representation of the candidate token.</param>
        /// <returns>True if the token is allowed, false if it should be masked.</returns>
        bool IsTokenAllowed(string generatedSoFar, int candidateTokenId, string candidateTokenText);

        /// <summary>
        /// Determines whether the generated text is complete and valid according to the constraint.
        /// </summary>
        /// <param name="generatedSoFar">The text generated so far.</param>
        /// <returns>True if generation can stop with valid output, false otherwise.</returns>
        bool IsComplete(string generatedSoFar);

        /// <summary>
        /// Gets a human-readable description of this constraint.
        /// </summary>
        string ConstraintDescription { get; }
    }
}

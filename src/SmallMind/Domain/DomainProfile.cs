using System;
using SmallMind.Domain.Policies;
using SmallMind.Core.Validation;

namespace SmallMind.Domain
{
    /// <summary>
    /// Defines the complete domain profile for bounded reasoning.
    /// Specifies all constraints and policies for safe, predictable generation.
    /// </summary>
    public class DomainProfile
    {
        /// <summary>
        /// Gets or sets the name of the domain profile.
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Gets or sets the version of the domain profile.
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets the maximum number of input tokens allowed.
        /// Default is 512 tokens.
        /// </summary>
        public int MaxInputTokens { get; set; } = 512;

        /// <summary>
        /// Gets or sets the maximum number of output tokens to generate.
        /// Default is 256 tokens.
        /// </summary>
        public int MaxOutputTokens { get; set; } = 256;

        /// <summary>
        /// Gets or sets the maximum execution time allowed.
        /// If null, no time limit is enforced.
        /// Default is 30 seconds.
        /// </summary>
        public TimeSpan? MaxExecutionTime { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the maximum working set size in megabytes (soft limit).
        /// If null, no memory limit is enforced.
        /// Note: This is advisory and may not be strictly enforced.
        /// </summary>
        public int? MaxWorkingSetMB { get; set; }

        /// <summary>
        /// Gets or sets the policy for allowed tokens.
        /// </summary>
        public AllowedTokenPolicy AllowedTokens { get; set; } = AllowedTokenPolicy.Default();

        /// <summary>
        /// Gets or sets the output format and validation policy.
        /// </summary>
        public OutputPolicy Output { get; set; } = OutputPolicy.Default();

        /// <summary>
        /// Gets or sets the sampling policy.
        /// </summary>
        public SamplingPolicy Sampling { get; set; } = SamplingPolicy.Default();

        /// <summary>
        /// Gets or sets the provenance and explainability policy.
        /// </summary>
        public ProvenancePolicy Provenance { get; set; } = ProvenancePolicy.Default();

        /// <summary>
        /// Gets or sets the safety policy.
        /// </summary>
        public SafetyPolicy Safety { get; set; } = SafetyPolicy.Default();

        /// <summary>
        /// Validates the domain profile configuration.
        /// Ensures all policies are valid and consistent.
        /// </summary>
        public void Validate()
        {
            Guard.NotNullOrWhiteSpace(Name, nameof(Name));
            Guard.NotNullOrWhiteSpace(Version, nameof(Version));
            Guard.GreaterThan(MaxInputTokens, 0, nameof(MaxInputTokens));
            Guard.GreaterThan(MaxOutputTokens, 0, nameof(MaxOutputTokens));

            if (MaxExecutionTime.HasValue)
            {
                Guard.GreaterThan(MaxExecutionTime.Value, TimeSpan.Zero, nameof(MaxExecutionTime));
            }

            if (MaxWorkingSetMB.HasValue)
            {
                Guard.GreaterThan(MaxWorkingSetMB.Value, 0, nameof(MaxWorkingSetMB));
            }

            // Validate all sub-policies
            Guard.NotNull(AllowedTokens, nameof(AllowedTokens));
            Guard.NotNull(Output, nameof(Output));
            Guard.NotNull(Sampling, nameof(Sampling));
            Guard.NotNull(Provenance, nameof(Provenance));
            Guard.NotNull(Safety, nameof(Safety));

            Output.Validate();
            Sampling.Validate();
            Provenance.Validate();
            Safety.Validate();

            // Cross-policy validation
            if (Safety.RequireCitations && !Provenance.EnableProvenance)
            {
                throw new DomainPolicyViolationException(
                    "Safety policy requires citations but provenance is not enabled",
                    "SafetyPolicy.RequireCitations");
            }
        }

        /// <summary>
        /// Creates a default domain profile with safe, balanced settings.
        /// </summary>
        /// <returns>A default domain profile.</returns>
        public static DomainProfile Default()
        {
            return new DomainProfile
            {
                Name = "Default",
                Version = "1.0"
            };
        }

        /// <summary>
        /// Creates a strict domain profile with maximum safety constraints.
        /// </summary>
        /// <returns>A strict domain profile.</returns>
        public static DomainProfile Strict()
        {
            return new DomainProfile
            {
                Name = "Strict",
                Version = "1.0",
                MaxInputTokens = 256,
                MaxOutputTokens = 128,
                MaxExecutionTime = TimeSpan.FromSeconds(15),
                Sampling = SamplingPolicy.CreateDeterministic(),
                Safety = SafetyPolicy.Strict(),
                Provenance = ProvenancePolicy.Enabled()
            };
        }

        /// <summary>
        /// Creates a permissive domain profile with minimal constraints.
        /// </summary>
        /// <returns>A permissive domain profile.</returns>
        public static DomainProfile Permissive()
        {
            return new DomainProfile
            {
                Name = "Permissive",
                Version = "1.0",
                MaxInputTokens = 2048,
                MaxOutputTokens = 1024,
                MaxExecutionTime = TimeSpan.FromMinutes(5),
                Safety = SafetyPolicy.Permissive()
            };
        }

        /// <summary>
        /// Creates a domain profile optimized for JSON output.
        /// </summary>
        /// <returns>A JSON-optimized domain profile.</returns>
        public static DomainProfile JsonOutput()
        {
            return new DomainProfile
            {
                Name = "JsonOutput",
                Version = "1.0",
                Output = OutputPolicy.JsonOnly(),
                Sampling = SamplingPolicy.CreateDeterministic()
            };
        }
    }
}

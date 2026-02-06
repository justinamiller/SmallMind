using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmallMind.Core.Validation;

namespace SmallMind.Domain
{
    /// <summary>
    /// Extension methods for configuring Domain-Bound Reasoning services.
    /// </summary>
    public static class DomainReasoningServiceExtensions
    {
        /// <summary>
        /// Adds SmallMind Domain-Bound Reasoning services to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureDomain">Optional action to configure a default domain profile.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSmallMindDomainReasoning(
            this IServiceCollection services,
            Action<DomainProfile>? configureDomain = null)
        {
            Guard.NotNull(services, nameof(services));

            // Register IDomainReasoner as scoped (one per request/scope)
            // Note: The actual implementation requires TransformerModel and ITokenizer
            // These should be registered separately by the consuming application
            services.TryAddScoped<IDomainReasoner>(sp =>
            {
                // Try to resolve dependencies from the service provider
                var model = sp.GetService<Core.TransformerModel>();
                var tokenizer = sp.GetService<Text.ITokenizer>();

                if (model == null || tokenizer == null)
                {
                    throw new InvalidOperationException(
                        "Domain reasoning requires TransformerModel and ITokenizer to be registered. " +
                        "Please register these dependencies before calling AddSmallMindDomainReasoning.");
                }

                // Get block size from model options if available, otherwise use default
                var blockSize = 128; // Default block size
                var modelOptions = sp.GetService<Microsoft.Extensions.Options.IOptions<Configuration.ModelOptions>>();
                if (modelOptions?.Value?.BlockSize > 0)
                {
                    blockSize = modelOptions.Value.BlockSize;
                }

                var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<DomainReasoner>>();
                return new DomainReasoner(model, tokenizer, blockSize, logger);
            });

            // Optionally register a default domain profile
            if (configureDomain != null)
            {
                var defaultProfile = new DomainProfile();
                configureDomain(defaultProfile);
                defaultProfile.Validate();
                services.TryAddSingleton(defaultProfile);
            }

            return services;
        }

        /// <summary>
        /// Adds SmallMind Domain-Bound Reasoning services with a specific domain profile.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="domainProfile">The domain profile to register.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSmallMindDomainReasoning(
            this IServiceCollection services,
            DomainProfile domainProfile)
        {
            Guard.NotNull(services, nameof(services));
            Guard.NotNull(domainProfile, nameof(domainProfile));

            domainProfile.Validate();

            // Add domain reasoning services
            AddSmallMindDomainReasoning(services);

            // Register the specific domain profile
            services.AddSingleton(domainProfile);

            return services;
        }
    }
}

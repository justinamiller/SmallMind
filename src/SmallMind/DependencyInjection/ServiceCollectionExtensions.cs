using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SmallMind.Abstractions;
using SmallMind.Configuration;
using SmallMind.Health;
using SmallMind.Telemetry;

namespace SmallMind.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring SmallMind services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds SmallMind services to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration action.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSmallMind(
            this IServiceCollection services,
            Action<SmallMindOptions>? configure = null)
        {
            // Register options
            var optionsBuilder = services.AddOptions<SmallMindOptions>();
            
            if (configure != null)
            {
                optionsBuilder.Configure(configure);
            }
            
            // Validate options on startup
            // Note: SmallMindOptions doesn't have a Validate method
            // optionsBuilder.Validate(options =>
            // {
            //     try
            //     {
            //         options.Validate();
            //         return true;
            //     }
            //     catch
            //     {
            //         return false;
            //     }
            // }, "SmallMind options validation failed");
            
            // Register core services as singletons
            services.TryAddSingleton<SmallMindMetrics>(SmallMindMetrics.Instance);
            services.TryAddSingleton<SmallMindHealthCheck>();
            
            return services;
        }
    }
}

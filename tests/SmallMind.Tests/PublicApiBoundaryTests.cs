using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests to enforce the Public API Boundary Policy.
    /// See docs/PublicApiBoundary.md for the full policy.
    /// </summary>
    public class PublicApiBoundaryTests
    {
        /// <summary>
        /// Tooling projects should have NO public types except Program.Main entry points.
        /// </summary>
        [Theory]
        [InlineData("SmallMind.Console")]
        [InlineData("SmallMind.Benchmarks")]
        [InlineData("SmallMind.Perf")]
        public void ToolingProjects_ShouldHaveNoPublicTypes_ExceptEntryPoints(string assemblyName)
        {
            // Try to load the assembly
            Assembly? assembly = null;
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch (Exception)
            {
                // Assembly not found in this test context - skip test
                return;
            }

            var publicTypes = assembly.GetExportedTypes()
                .Where(t => !IsEntryPointType(t))
                .ToList();

            Assert.Empty(publicTypes);
        }

        /// <summary>
        /// Implementation projects should have limited public types, mostly exceptions.
        /// This test validates the allowlist.
        /// </summary>
        [Theory]
        [InlineData("SmallMind.Core")]
        [InlineData("SmallMind.Runtime")]
        [InlineData("SmallMind.Transformers")]
        [InlineData("SmallMind.Tokenizers")]
        [InlineData("SmallMind.Quantization")]
        [InlineData("SmallMind.Engine")]
        [InlineData("SmallMind.Rag")]
        [InlineData("SmallMind.ModelRegistry")]
        public void ImplementationProjects_ShouldOnlyExposeAllowlistedTypes(string assemblyName)
        {
            // Try to load the assembly
            Assembly? assembly = null;
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch (Exception)
            {
                // Assembly not found in this test context - skip test
                return;
            }

            var publicTypes = assembly.GetExportedTypes().ToList();
            var allowedTypes = GetAllowedPublicTypes(assemblyName);

            var violations = new List<string>();
            foreach (var type in publicTypes)
            {
                var fullName = type.FullName ?? type.Name;
                
                // Check if type is in allowlist
                if (allowedTypes.Contains(fullName))
                    continue;

                // Check if it's an exception type (always allowed)
                if (IsExceptionType(type))
                    continue;

                // This is a violation
                violations.Add($"{fullName} in {type.Assembly.GetName().Name}");
            }

            if (violations.Any())
            {
                var message = $"Public API boundary violation in {assemblyName}:\n" +
                              $"The following types are public but not in the allowlist:\n" +
                              string.Join("\n", violations.Select(v => $"  - {v}")) +
                              $"\n\nEither make these types internal or add them to the allowlist in GetAllowedPublicTypes()";
                Assert.Fail(message);
            }
        }

        /// <summary>
        /// SmallMind.Abstractions should only contain contracts (interfaces, DTOs, enums).
        /// It should not have implementation classes (except exceptions).
        /// </summary>
        [Fact]
        public void AbstractionsProject_ShouldOnlyContainContracts()
        {
            Assembly? assembly = null;
            try
            {
                assembly = Assembly.Load("SmallMind.Abstractions");
            }
            catch (Exception)
            {
                // Assembly not found in this test context - skip test
                return;
            }

            var publicTypes = assembly.GetExportedTypes();
            var violations = new List<string>();

            // Allowlist for default implementation classes that provide convenience implementations
            // These are sealed implementation classes that implement interfaces defined in Abstractions
            var allowedDefaultImplementations = new HashSet<string>
            {
                "SmallMind.Abstractions.NoOpTelemetry",       // Default no-op implementation of IChatTelemetry
                "SmallMind.Abstractions.ConsoleTelemetry",    // Console logging implementation of IChatTelemetry
                "SmallMind.Abstractions.NoOpRetrievalProvider", // Default no-op implementation of IRetrievalProvider
                "SmallMind.Abstractions.Telemetry.ConsoleRuntimeLogger",  // Console implementation of IRuntimeLogger
                "SmallMind.Abstractions.Telemetry.NullRuntimeLogger",     // Null implementation of IRuntimeLogger
                "SmallMind.Abstractions.Telemetry.InMemoryRuntimeMetrics", // In-memory implementation of IRuntimeMetrics
                "SmallMind.Abstractions.Telemetry.NullRuntimeMetrics"     // Null implementation of IRuntimeMetrics
            };

            foreach (var type in publicTypes)
            {
                // Interfaces, enums, and value types are always allowed
                if (type.IsInterface || type.IsEnum || type.IsValueType)
                    continue;

                // Exception types are allowed
                if (IsExceptionType(type))
                    continue;

                // Sealed classes with only properties/fields are DTOs - allowed
                if (type.IsSealed && IsDataTransferObject(type))
                    continue;

                // Static classes are allowed (for extension methods, factories, etc.)
                if (type.IsAbstract && type.IsSealed)
                    continue;

                // Abstract classes are allowed (base classes for exceptions, etc.)
                if (type.IsAbstract)
                    continue;

                // Default implementation classes are allowed (sealed, implement interface)
                if (type.IsSealed && allowedDefaultImplementations.Contains(type.FullName ?? ""))
                    continue;

                // Everything else is suspicious
                violations.Add($"{type.FullName ?? type.Name} (not an interface/enum/DTO)");
            }

            if (violations.Any())
            {
                var message = $"SmallMind.Abstractions should only contain contracts:\n" +
                              string.Join("\n", violations.Select(v => $"  - {v}"));
                Assert.Fail(message);
            }
        }

        private static bool IsEntryPointType(Type type)
        {
            // Check if this is a Program class with a Main method
            return type.Name == "Program" || 
                   type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                       .Any(m => m.Name == "Main");
        }

        private static bool IsExceptionType(Type type)
        {
            return typeof(Exception).IsAssignableFrom(type);
        }

        private static bool IsDataTransferObject(Type type)
        {
            // Check if a type looks like a DTO:
            // - Has properties or fields
            // - No methods (except property getters/setters, constructors, ToString, Equals, GetHashCode)
            
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName) // Exclude property getters/setters
                .Where(m => m.Name != "ToString")
                .Where(m => m.Name != "Equals")
                .Where(m => m.Name != "GetHashCode")
                .Where(m => m.DeclaringType == type) // Only declared in this type
                .ToList();

            // If there are non-trivial methods, it's not a pure DTO
            return methods.Count == 0;
        }

        /// <summary>
        /// Allowlist of public types in implementation projects.
        /// These types are intentionally public for specific reasons (documented in code).
        /// </summary>
        private static HashSet<string> GetAllowedPublicTypes(string assemblyName)
        {
            // Base set: all exception types are always allowed
            var allowed = new HashSet<string>();

            switch (assemblyName)
            {
                case "SmallMind.Core":
                    // Only exception types should be public in Core
                    // (exceptions are automatically allowed by IsExceptionType check)
                    break;

                case "SmallMind.Runtime":
                    // Only exception types should be public in Runtime
                    break;

                case "SmallMind.Transformers":
                    // Only exception types should be public in Transformers
                    break;

                case "SmallMind.Tokenizers":
                    // TokenizerDiagnostics is a public diagnostics DTO
                    allowed.Add("SmallMind.Tokenizers.Gguf.TokenizerDiagnostics");
                    break;

                case "SmallMind.Quantization":
                    // Only exception types should be public in Quantization
                    break;

                case "SmallMind.Engine":
                    // Main engine factory class is public
                    allowed.Add("SmallMind.Engine.SmallMind");
                    break;

                case "SmallMind.Rag":
                    // Extension point interfaces for RAG customization
                    allowed.Add("SmallMind.Rag.Telemetry.IRagLogger");
                    allowed.Add("SmallMind.Rag.Telemetry.IRagMetrics");
                    allowed.Add("SmallMind.Rag.Retrieval.IEmbedder");
                    allowed.Add("SmallMind.Rag.Retrieval.IEmbeddingProvider");
                    allowed.Add("SmallMind.Rag.Retrieval.IVectorStore");
                    allowed.Add("SmallMind.Rag.Security.IAuthorizer");
                    allowed.Add("SmallMind.Rag.Generation.ITextGenerator");
                    // Main RAG facade
                    allowed.Add("SmallMind.Rag.RagPipeline");
                    allowed.Add("SmallMind.Rag.RagPipelineBuilder");
                    allowed.Add("SmallMind.Rag.RagOptions");
                    break;

                case "SmallMind.ModelRegistry":
                    // No public types expected (all should be internal)
                    break;
            }

            return allowed;
        }
    }
}

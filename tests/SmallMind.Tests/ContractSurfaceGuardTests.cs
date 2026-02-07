using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using SmallMind.Abstractions;
using SmallMind.Core.Core;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests to prevent accidental growth of the public API surface.
    /// Guards the stable contract defined in SmallMind.Abstractions.
    /// </summary>
    public class ContractSurfaceGuardTests
    {
        /// <summary>
        /// Allowlist of expected public types in SmallMind.Abstractions.
        /// Any new public type MUST be added here with justification.
        /// </summary>
        private static readonly HashSet<string> AllowedPublicTypes = new HashSet<string>
        {
            // Main interfaces
            "SmallMind.Abstractions.ISmallMindEngine",
            "SmallMind.Abstractions.IModelHandle",
            "SmallMind.Abstractions.IChatSession",
            "SmallMind.Abstractions.IRagEngine",
            "SmallMind.Abstractions.IRagIndex",

            // Request DTOs
            "SmallMind.Abstractions.ModelLoadRequest",
            "SmallMind.Abstractions.GenerationRequest",
            "SmallMind.Abstractions.ChatMessage",
            "SmallMind.Abstractions.RagBuildRequest",
            "SmallMind.Abstractions.RagAskRequest",

            // Result DTOs
            "SmallMind.Abstractions.GenerationResult",
            "SmallMind.Abstractions.TokenEvent",
            "SmallMind.Abstractions.RagAnswer",
            "SmallMind.Abstractions.RagCitation",
            "SmallMind.Abstractions.ModelInfo",
            "SmallMind.Abstractions.RagIndexInfo",  // RAG index metadata
            "SmallMind.Abstractions.SessionInfo",   // Chat session metadata

            // Options DTOs
            "SmallMind.Abstractions.SmallMindOptions",
            "SmallMind.Abstractions.GenerationOptions",
            "SmallMind.Abstractions.SessionOptions",
            "SmallMind.Abstractions.EngineCapabilities",
            "SmallMind.Abstractions.OutputConstraints",  // Output constraints for workflow

            // Enums
            "SmallMind.Abstractions.GenerationMode",
            "SmallMind.Abstractions.ChatRole",
            "SmallMind.Abstractions.TokenEventKind",

            // Exceptions
            "SmallMind.Abstractions.SmallMindException",
            "SmallMind.Abstractions.UnsupportedModelException",
            "SmallMind.Abstractions.UnsupportedGgufTensorException",
            "SmallMind.Abstractions.ContextLimitExceededException",
            "SmallMind.Abstractions.BudgetExceededException",
            "SmallMind.Abstractions.RagInsufficientEvidenceException",
            "SmallMind.Abstractions.SecurityViolationException",
        };

        /// <summary>
        /// Allowlist of expected public types in SmallMind.Engine namespace (static factory only).
        /// </summary>
        private static readonly HashSet<string> AllowedEnginePublicTypes = new HashSet<string>
        {
            "SmallMind.Engine.SmallMind"  // Static factory class
        };

        [Fact]
        public void SmallMindAbstractions_OnlyContainsAllowedPublicTypes()
        {
            // Arrange
            var assembly = typeof(ISmallMindEngine).Assembly;
            var publicTypes = assembly.GetTypes()
                .Where(t => t.IsPublic && t.Namespace?.StartsWith("SmallMind.Abstractions") == true)
                .Select(t => t.FullName)
                .OrderBy(name => name)
                .ToList();

            // Act - find unexpected types
            var unexpectedTypes = publicTypes
                .Where(name => !AllowedPublicTypes.Contains(name))
                .ToList();

            // Assert
            Assert.Empty(unexpectedTypes);

            // Also check that we haven't removed expected types
            var missingTypes = AllowedPublicTypes
                .Where(name => !publicTypes.Contains(name))
                .ToList();

            Assert.Empty(missingTypes);
        }

        [Fact]
        public void SmallMindAbstractions_AllPublicTypesAreNotCompilerGenerated()
        {
            // Arrange
            var assembly = typeof(ISmallMindEngine).Assembly;
            var publicTypes = assembly.GetTypes()
                .Where(t => t.IsPublic && t.Namespace?.StartsWith("SmallMind.Abstractions") == true)
                .ToList();

            // Act - check for compiler-generated types
            var compilerGeneratedTypes = new List<string>();
            foreach (var type in publicTypes)
            {
                var compilerGenerated = type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>();
                if (compilerGenerated != null)
                {
                    compilerGeneratedTypes.Add(type.FullName!);
                }
            }

            // Assert - all public types should be hand-written (not compiler-generated)
            Assert.Empty(compilerGeneratedTypes);
        }

        [Fact]
        public void SmallMindAbstractions_AllPublicInterfacesFollowNamingConvention()
        {
            // Arrange
            var assembly = typeof(ISmallMindEngine).Assembly;
            var publicInterfaces = assembly.GetTypes()
                .Where(t => t.IsPublic && 
                           t.IsInterface && 
                           t.Namespace?.StartsWith("SmallMind.Abstractions") == true)
                .ToList();

            // Act - check naming
            var badlyNamedInterfaces = publicInterfaces
                .Where(t => !t.Name.StartsWith("I"))
                .Select(t => t.FullName)
                .ToList();

            // Assert - all interfaces should start with 'I'
            Assert.Empty(badlyNamedInterfaces);
        }

        [Fact]
        public void SmallMindAbstractions_AllPublicExceptionsInheritFromSmallMindException()
        {
            // Arrange
            var assembly = typeof(ISmallMindEngine).Assembly;
            var publicExceptions = assembly.GetTypes()
                .Where(t => t.IsPublic && 
                           typeof(Exception).IsAssignableFrom(t) &&
                           t != typeof(SmallMindException) &&
                           t.Namespace?.StartsWith("SmallMind.Abstractions") == true)
                .ToList();

            // Act - check inheritance
            var nonCompliantExceptions = publicExceptions
                .Where(t => !typeof(SmallMindException).IsAssignableFrom(t))
                .Select(t => t.FullName)
                .ToList();

            // Assert - all exceptions should inherit from SmallMindException
            Assert.Empty(nonCompliantExceptions);
        }

        [Fact]
        public void SmallMindEngine_OnlyExposesStaticFactoryClass()
        {
            // Arrange - try to find SmallMind.Engine assembly
            var engineAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "SmallMind.Engine");

            if (engineAssembly == null)
            {
                // Engine assembly not loaded, skip test
                return;
            }

            var publicTypes = engineAssembly.GetTypes()
                .Where(t => t.IsPublic && t.Namespace?.StartsWith("SmallMind.Engine") == true)
                .Select(t => t.FullName)
                .OrderBy(name => name)
                .ToList();

            // Act - find unexpected types
            var unexpectedTypes = publicTypes
                .Where(name => !AllowedEnginePublicTypes.Contains(name))
                .ToList();

            // Assert - only SmallMind static factory should be public
            Assert.Empty(unexpectedTypes);
        }

        [Fact]
        public void SmallMindAbstractions_PublicDtosAreSealed()
        {
            // Arrange
            var assembly = typeof(ISmallMindEngine).Assembly;
            var publicClasses = assembly.GetTypes()
                .Where(t => t.IsPublic && 
                           t.IsClass && 
                           !t.IsAbstract &&
                           t.Namespace?.StartsWith("SmallMind.Abstractions") == true)
                .ToList();

            // Act - check for sealed
            var unsealedClasses = publicClasses
                .Where(t => !t.IsSealed && !typeof(Exception).IsAssignableFrom(t))
                .Select(t => t.FullName)
                .ToList();

            // Assert - DTOs should be sealed to prevent inheritance
            // (Exceptions can be left unsealed for custom exception hierarchies)
            Assert.Empty(unsealedClasses);
        }

        [Fact]
        public void SmallMindAbstractions_EnumsAreProperlyDefined()
        {
            // Arrange
            var assembly = typeof(ISmallMindEngine).Assembly;
            var publicEnums = assembly.GetTypes()
                .Where(t => t.IsPublic && 
                           t.IsEnum && 
                           t.Namespace?.StartsWith("SmallMind.Abstractions") == true)
                .ToList();

            // Assert - check that we have expected enums
            Assert.Contains(publicEnums, e => e.Name == "GenerationMode");
            Assert.Contains(publicEnums, e => e.Name == "ChatRole");
            Assert.Contains(publicEnums, e => e.Name == "TokenEventKind");

            // Check that enums don't have [Flags] unless appropriate
            foreach (var enumType in publicEnums)
            {
                var hasFlagsAttribute = enumType.GetCustomAttribute<FlagsAttribute>() != null;
                
                // Our current enums should NOT be flags
                // If we add flags enums in the future, update this test
                Assert.False(hasFlagsAttribute, 
                    $"Enum {enumType.Name} has [Flags] attribute unexpectedly. " +
                    "If this is intentional, update the test.");
            }
        }

        [Fact]
        public void SmallMindAbstractions_NoPublicFields()
        {
            // Arrange
            var assembly = typeof(ISmallMindEngine).Assembly;
            var publicTypes = assembly.GetTypes()
                .Where(t => t.IsPublic && t.Namespace?.StartsWith("SmallMind.Abstractions") == true)
                .ToList();

            // Act - find public fields
            var typesWithPublicFields = new List<string>();
            foreach (var type in publicTypes)
            {
                var publicFields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(f => !f.IsLiteral) // Exclude const fields
                    .Where(f => f.Name != "value__") // Exclude enum value__ field
                    .Where(f => !IsReadonlyStructField(type, f)) // Exclude readonly fields in readonly structs
                    .ToList();

                if (publicFields.Any())
                {
                    typesWithPublicFields.Add($"{type.FullName}: {string.Join(", ", publicFields.Select(f => f.Name))}");
                }
            }

            // Assert - no public fields (use properties instead)
            // Note: Enum value__ fields are automatically excluded
            // Note: readonly struct fields are allowed for performance optimization
            Assert.Empty(typesWithPublicFields);
        }

        private static bool IsReadonlyStructField(Type type, FieldInfo field)
        {
            // Allow public readonly fields in readonly structs (performance optimization)
            if (!type.IsValueType)
                return false;

            // Check if the field is readonly
            if (!field.IsInitOnly)
                return false;

            // Check if the type is a readonly struct (has IsReadOnlyAttribute)
            var isReadOnlyStruct = type.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "IsReadOnlyAttribute");

            return isReadOnlyStruct;
        }

        [Fact]
        public void VersionAssemblyAttribute_IsPresent()
        {
            // Arrange
            var assembly = typeof(ISmallMindEngine).Assembly;

            // Act
            var version = assembly.GetName().Version;

            // Assert - assembly should have version
            Assert.NotNull(version);
            Assert.True(version.Major >= 0);
        }

        [Fact]
        public void SmallMindAbstractions_CountPublicTypes()
        {
            // Arrange
            var assembly = typeof(ISmallMindEngine).Assembly;
            var publicTypeCount = assembly.GetTypes()
                .Count(t => t.IsPublic && t.Namespace?.StartsWith("SmallMind.Abstractions") == true);

            // Assert - document the current count (this test will fail if we add types)
            // Update the expected count when adding new types is justified
            Assert.Equal(AllowedPublicTypes.Count, publicTypeCount);
        }
    }
}

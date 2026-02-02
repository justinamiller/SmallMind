using System;
using System.IO;
using Xunit;
using SmallMind.Runtime.PretrainedModels;

namespace SmallMind.Tests.PretrainedModels
{
    public class PretrainedPackTests
    {
        private static string GetProjectRoot()
        {
            // Navigate up from bin/Debug/net10.0 to the project root
            var currentDir = Directory.GetCurrentDirectory();
            var dirInfo = new DirectoryInfo(currentDir);
            
            // Go up until we find the solution directory (contains data/ and src/)
            while (dirInfo != null && !Directory.Exists(Path.Combine(dirInfo.FullName, "data", "pretrained")))
            {
                dirInfo = dirInfo.Parent;
            }
            
            if (dirInfo == null)
            {
                throw new DirectoryNotFoundException("Could not find project root with data/pretrained directory");
            }
            
            return dirInfo.FullName;
        }

        private static string TestDataPath => Path.Combine(GetProjectRoot(), "data", "pretrained");

        [Fact]
        public void LoadRegistry_ValidPath_LoadsSuccessfully()
        {
            // Arrange
            var registryPath = Path.Combine(TestDataPath, "registry.json");
            
            // Act
            var registry = PretrainedRegistry.Load(registryPath);
            
            // Assert
            Assert.NotNull(registry);
            Assert.NotEmpty(registry.Packs);
            Assert.True(registry.Packs.Count >= 4, "Expected at least 4 packs including ITIL v4");
        }

        [Fact]
        public void LoadRegistry_FindsPacks_ByIdCorrectly()
        {
            // Arrange
            var registryPath = Path.Combine(TestDataPath, "registry.json");
            var registry = PretrainedRegistry.Load(registryPath);
            
            // Act
            var sentimentPack = registry.FindPack("sm.pretrained.sentiment.v1");
            var classificationPack = registry.FindPack("sm.pretrained.classification.v1");
            var financePack = registry.FindPack("sm.pretrained.finance.v1");
            var itilPack = registry.FindPack("sm.pretrained.itil_v4_mastery.v1");
            
            // Assert
            Assert.NotNull(sentimentPack);
            Assert.Equal("sentiment", sentimentPack.Domain);
            Assert.NotNull(classificationPack);
            Assert.Equal("classification", classificationPack.Domain);
            Assert.NotNull(financePack);
            Assert.Equal("finance", financePack.Domain);
            Assert.True(financePack.RagEnabled);
            Assert.NotNull(itilPack);
            Assert.Equal("itil_v4", itilPack.Domain);
            Assert.True(itilPack.RagEnabled);
            Assert.Equal("knowledge-pack", itilPack.Type);
        }

        [Fact]
        public void LoadPack_SentimentPack_LoadsCorrectly()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "sentiment");
            
            // Act
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotNull(pack);
            Assert.Equal("sm.pretrained.sentiment.v1", pack.Manifest.Id);
            Assert.Equal("sentiment", pack.Manifest.Domain);
            Assert.NotEmpty(pack.Samples);
            Assert.Equal(30, pack.Samples.Count);
            Assert.NotEmpty(pack.EvaluationLabels);
            Assert.Contains("positive", pack.Categories);
            Assert.Contains("negative", pack.Categories);
            Assert.Contains("neutral", pack.Categories);
        }

        [Fact]
        public void LoadPack_ClassificationPack_LoadsCorrectly()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "classification");
            
            // Act
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotNull(pack);
            Assert.Equal("sm.pretrained.classification.v1", pack.Manifest.Id);
            Assert.Equal("classification", pack.Manifest.Domain);
            Assert.NotEmpty(pack.Samples);
            Assert.Equal(30, pack.Samples.Count);
            Assert.Equal(4, pack.Categories.Count);
            Assert.Contains("Technology", pack.Categories);
            Assert.Contains("Sports", pack.Categories);
            Assert.Contains("Politics", pack.Categories);
            Assert.Contains("Entertainment", pack.Categories);
        }

        [Fact]
        public void LoadPack_FinancePack_LoadsRagDocuments()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "finance");
            
            // Act
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotNull(pack);
            Assert.Equal("sm.pretrained.finance.v1", pack.Manifest.Id);
            Assert.True(pack.Manifest.Rag?.Enabled);
            Assert.NotEmpty(pack.RagDocumentPaths);
            Assert.Equal(5, pack.RagDocumentPaths.Count);
            
            // Verify all RAG documents exist
            foreach (var docPath in pack.RagDocumentPaths)
            {
                Assert.True(File.Exists(docPath), $"RAG document not found: {docPath}");
            }
        }

        [Fact]
        public void LoadFromJsonl_ValidFile_LoadsSamples()
        {
            // Arrange
            var jsonlPath = Path.Combine(TestDataPath, "sentiment", "task", "inputs.jsonl");
            
            // Act
            var samples = DatasetLoader.LoadFromJsonl(jsonlPath);
            
            // Assert
            Assert.NotEmpty(samples);
            Assert.Equal(30, samples.Count);
            
            // Verify first sample has all fields
            var firstSample = samples[0];
            Assert.NotNull(firstSample.Id);
            Assert.NotEmpty(firstSample.Id);
            Assert.NotNull(firstSample.Task);
            Assert.NotEmpty(firstSample.Text);
            Assert.NotEmpty(firstSample.Label);
        }

        [Fact]
        public void LoadFromJsonl_WithExpectedLabels_ValidatesSamples()
        {
            // Arrange
            var jsonlPath = Path.Combine(TestDataPath, "sentiment", "task", "inputs.jsonl");
            var expectedLabels = new[] { "positive", "negative", "neutral" };
            
            // Act
            var samples = DatasetLoader.LoadFromJsonl(jsonlPath, expectedLabels);
            
            // Assert
            Assert.NotEmpty(samples);
            
            // Verify all samples have valid labels
            foreach (var sample in samples)
            {
                Assert.Contains(sample.Label, expectedLabels, StringComparer.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void LoadLabeledData_AutoDetectsJsonl_ByExtension()
        {
            // Arrange
            var jsonlPath = Path.Combine(TestDataPath, "sentiment", "task", "inputs.jsonl");
            
            // Act
            var samples = DatasetLoader.LoadLabeledData(jsonlPath);
            
            // Assert
            Assert.NotEmpty(samples);
            Assert.Equal(30, samples.Count);
        }

        [Fact]
        public void LoadLabeledData_LegacyFormat_StillWorks()
        {
            // Arrange
            var legacyPath = Path.Combine(TestDataPath, "sentiment", "sample-sentiment.txt");
            
            // Act
            var samples = DatasetLoader.LoadLabeledData(legacyPath);
            
            // Assert
            Assert.NotEmpty(samples);
            // Legacy files should still load
            Assert.True(samples.Count > 0);
        }

        [Fact]
        public void PackManifest_HasRequiredFields()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "sentiment");
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotNull(pack.Manifest.Id);
            Assert.NotEmpty(pack.Manifest.Id);
            Assert.NotNull(pack.Manifest.Domain);
            Assert.NotEmpty(pack.Manifest.Domain);
            Assert.NotNull(pack.Manifest.IntendedUse);
            Assert.NotEmpty(pack.Manifest.IntendedUse);
            Assert.NotNull(pack.Manifest.Source);
            Assert.NotEmpty(pack.Manifest.Source.License);
        }

        [Fact]
        public void GetSummary_ReturnsFormattedSummary()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "sentiment");
            var pack = PretrainedPack.Load(packPath);
            
            // Act
            var summary = pack.GetSummary();
            
            // Assert
            Assert.NotNull(summary);
            Assert.Contains(pack.Manifest.Id, summary);
            Assert.Contains(pack.Manifest.Domain, summary);
            Assert.Contains(pack.Samples.Count.ToString(), summary);
        }

        [Fact]
        public void LoadPack_ItilV4Pack_LoadsCorrectly()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "itil_v4_mastery");
            
            // Act
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotNull(pack);
            Assert.Equal("sm.pretrained.itil_v4_mastery.v1", pack.Manifest.Id);
            Assert.Equal("itil_v4", pack.Manifest.Domain);
            Assert.Equal("knowledge-pack", pack.Manifest.Type);
            Assert.True(pack.Manifest.Rag?.Enabled);
            
            // Verify RAG configuration
            Assert.NotNull(pack.Manifest.Rag);
            Assert.Equal(20, pack.Manifest.Rag.DocumentCount);
            Assert.Equal("hybrid", pack.Manifest.Rag.IndexType);
        }

        [Fact]
        public void LoadPack_ItilV4Pack_LoadsAllDocuments()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "itil_v4_mastery");
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotEmpty(pack.RagDocumentPaths);
            Assert.Equal(20, pack.RagDocumentPaths.Count);
            
            // Verify all expected documents exist
            var expectedDocs = new[]
            {
                "001_foundations.md",
                "010_service_value_system.md",
                "020_guiding_principles.md",
                "030_four_dimensions.md",
                "040_service_value_chain.md",
                "050_practices_overview.md",
                "060_incident_management.md",
                "070_problem_management.md",
                "080_change_enablement.md",
                "090_service_request_management.md",
                "100_service_catalog_management.md",
                "110_service_level_management.md",
                "120_monitoring_and_event_management.md",
                "130_release_management.md",
                "140_configuration_management.md",
                "150_knowledge_management.md",
                "160_continual_improvement.md",
                "170_metrics_kpis_okrs.md",
                "180_common_pitfalls_anti_patterns.md",
                "190_mappings_itil_to_ops.md"
            };
            
            foreach (var expectedDoc in expectedDocs)
            {
                var docPath = pack.RagDocumentPaths.Find(p => p.EndsWith(expectedDoc));
                Assert.NotNull(docPath);
                Assert.True(File.Exists(docPath), $"Document not found: {expectedDoc}");
            }
        }

        [Fact]
        public void LoadPack_ItilV4Pack_LoadsQueries()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "itil_v4_mastery");
            var queriesPath = Path.Combine(packPath, "task", "queries.jsonl");
            
            // Act
            Assert.True(File.Exists(queriesPath), "Queries file should exist");
            var queries = DatasetLoader.LoadFromJsonl(queriesPath);
            
            // Assert
            Assert.NotEmpty(queries);
            Assert.True(queries.Count >= 40, $"Expected at least 40 queries, found {queries.Count}");
            
            // Verify query structure
            var firstQuery = queries[0];
            Assert.NotNull(firstQuery.Id);
            Assert.NotNull(firstQuery.Task);
            Assert.NotNull(firstQuery.Text);
            
            // Verify task types
            var docqaTasks = queries.FindAll(q => q.Task == "docqa");
            var consultTasks = queries.FindAll(q => q.Task == "structured_consult");
            
            Assert.NotEmpty(docqaTasks);
            Assert.NotEmpty(consultTasks);
        }

        [Fact]
        public void LoadPack_ItilV4Pack_HasExpectedEvaluationFile()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "itil_v4_mastery");
            var expectedPath = Path.Combine(packPath, "eval", "expected.jsonl");
            
            // Act & Assert
            Assert.True(File.Exists(expectedPath), "Expected evaluation file should exist");
            
            // Verify file has content (expected.jsonl has different schema, not loadable via DatasetLoader)
            var lines = File.ReadAllLines(expectedPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            Assert.NotEmpty(lines);
            Assert.True(lines.Count >= 40, $"Expected at least 40 evaluation entries, found {lines.Count}");
            
            // Verify first line is valid JSON with expected schema
            var firstLine = lines[0];
            Assert.Contains("\"id\":", firstLine);
            Assert.Contains("expected_keywords", firstLine);
        }

        [Fact]
        public void LoadPack_ItilV4Pack_HasScoringMethodology()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "itil_v4_mastery");
            var scoringPath = Path.Combine(packPath, "eval", "scoring.md");
            
            // Act & Assert
            Assert.True(File.Exists(scoringPath), "Scoring methodology should exist");
            var scoringContent = File.ReadAllText(scoringPath);
            Assert.NotEmpty(scoringContent);
            
            // Verify key sections exist
            Assert.Contains("Citation", scoringContent);
            Assert.Contains("docqa", scoringContent);
            Assert.Contains("structured_consult", scoringContent);
        }

        [Fact]
        public void LoadPack_ItilV4Pack_HasScenarioConfigurations()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "itil_v4_mastery");
            var docqaScenarioPath = Path.Combine(packPath, "scenarios", "docqa.json");
            var consultScenarioPath = Path.Combine(packPath, "scenarios", "structured_consult.json");
            
            // Act & Assert
            Assert.True(File.Exists(docqaScenarioPath), "Document Q&A scenario should exist");
            Assert.True(File.Exists(consultScenarioPath), "Structured consult scenario should exist");
            
            // Verify scenarios are valid JSON
            var docqaContent = File.ReadAllText(docqaScenarioPath);
            var consultContent = File.ReadAllText(consultScenarioPath);
            Assert.NotEmpty(docqaContent);
            Assert.NotEmpty(consultContent);
            
            // Verify key fields in scenarios
            Assert.Contains("scenario_id", docqaContent);
            Assert.Contains("citation", docqaContent);
            Assert.Contains("output_schema", consultContent);
            Assert.Contains("json", consultContent);
        }

        [Fact]
        public void LoadPack_ItilV4Pack_HasDeterministicSettings()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "itil_v4_mastery");
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotNull(pack.Manifest.RecommendedSettings);
            Assert.True(pack.Manifest.RecommendedSettings.Deterministic);
            Assert.Equal(8192, pack.Manifest.RecommendedSettings.ContextTokens);
        }

        [Fact]
        public void LoadPack_ItilV4Pack_HasProvenanceDocumentation()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "itil_v4_mastery");
            var provenancePath = Path.Combine(packPath, "PROVENANCE.md");
            
            // Act & Assert
            Assert.True(File.Exists(provenancePath), "PROVENANCE.md should exist");
            var provenanceContent = File.ReadAllText(provenancePath);
            Assert.NotEmpty(provenanceContent);
            
            // Verify key content
            Assert.Contains("original work", provenanceContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("MIT", provenanceContent);
            Assert.Contains("copyrighted", provenanceContent, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void LoadPack_ItilV4Pack_DocumentsHaveExpectedStructure()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "itil_v4_mastery");
            var pack = PretrainedPack.Load(packPath);
            var foundationsDoc = pack.RagDocumentPaths.Find(p => p.EndsWith("001_foundations.md"));
            
            // Act
            Assert.NotNull(foundationsDoc);
            var content = File.ReadAllText(foundationsDoc);
            
            // Assert - verify expected sections
            Assert.Contains("## Purpose", content);
            Assert.Contains("## Common Implementation Do", content);  // Matches "Common Implementation Do's"
            Assert.Contains("## Common Implementation Don", content); // Matches "Common Implementation Don'ts"
            Assert.Contains("## Common Q&A", content);
            Assert.Contains("## Cross-Links", content);
        }

        [Fact]
        public void LoadPack_ItilV4Pack_HasRagIndexMetadata()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "itil_v4_mastery");
            var indexMetadataPath = Path.Combine(packPath, "rag", "index", "metadata.json");
            
            // Act & Assert
            Assert.True(File.Exists(indexMetadataPath), "RAG index metadata should exist");
            var metadataContent = File.ReadAllText(indexMetadataPath);
            Assert.NotEmpty(metadataContent);
            
            // Verify key fields
            Assert.Contains("documents", metadataContent);
            Assert.Contains("total_documents", metadataContent);
            Assert.Contains("chunking_config", metadataContent);
            Assert.Contains("retrieval_config", metadataContent);
        }
    }
}

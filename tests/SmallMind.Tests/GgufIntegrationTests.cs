using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SmallMind.Tests
{
    /// <summary>
    /// Integration tests for GGUF model download, import, and inference.
    /// These tests require real model downloads and are gated by environment variable.
    /// Set SMALLMIND_TEST_DOWNLOADS=1 to enable these tests.
    /// </summary>
    public class GgufIntegrationTests : IDisposable
    {
        private readonly string _testDir;
        private readonly bool _testsEnabled;

        public GgufIntegrationTests()
        {
            _testsEnabled = Environment.GetEnvironmentVariable("SMALLMIND_TEST_DOWNLOADS") == "1";
            _testDir = Path.Combine(Path.GetTempPath(), $"smallmind_test_{Guid.NewGuid():N}");
            
            if (_testsEnabled && !Directory.Exists(_testDir))
            {
                Directory.CreateDirectory(_testDir);
            }
        }

        public void Dispose()
        {
            if (_testsEnabled && Directory.Exists(_testDir))
            {
                try
                {
                    Directory.Delete(_testDir, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Fact]
        public async Task ModelDownload_TinyLlama_Succeeds()
        {
            if (!_testsEnabled)
            {
                // Skip test if env var not set
                return;
            }

            // Arrange
            var downloader = new SmallMind.ConsoleApp.Commands.ModelDownloadCommand();
            string outputPath = Path.Combine(_testDir, "tinyllama.gguf");
            
            string[] args = new[]
            {
                "TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF",
                "tinyllama-1.1b-chat-v1.0.Q4_0.gguf",
                outputPath
            };

            // Act
            int exitCode = await downloader.ExecuteAsync(args);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            
            var fileInfo = new FileInfo(outputPath);
            Assert.True(fileInfo.Length > 1000000); // At least 1MB
        }

        [Fact]
        public async Task ImportGguf_TinyLlama_Succeeds()
        {
            if (!_testsEnabled)
            {
                return;
            }

            // This test assumes the download test has run or model exists
            string ggufPath = Path.Combine(_testDir, "tinyllama.gguf");
            
            // Skip if GGUF not available
            if (!File.Exists(ggufPath))
            {
                return;
            }

            // Arrange
            var importer = new SmallMind.ConsoleApp.Commands.ImportGgufCommand();
            string smqPath = Path.Combine(_testDir, "tinyllama.smq");
            
            string[] args = new[] { ggufPath, smqPath };

            // Act
            int exitCode = await importer.ExecuteAsync(args);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(smqPath));
            Assert.True(File.Exists($"{smqPath}.manifest.json"));
            
            var fileInfo = new FileInfo(smqPath);
            Assert.True(fileInfo.Length > 1000000); // At least 1MB
        }

        [Fact]
        public void ChatTemplate_DetectsLlama2()
        {
            // This test doesn't require downloads
            var template = SmallMind.ConsoleApp.Commands.ChatTemplates.DetectTemplate(
                "TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/tinyllama-1.1b-chat-v1.0.Q8_0.gguf"
            );

            Assert.Equal(SmallMind.ConsoleApp.Commands.ChatTemplates.TemplateType.Llama2, template);
        }

        [Fact]
        public void ChatTemplate_DetectsMistral()
        {
            var template = SmallMind.ConsoleApp.Commands.ChatTemplates.DetectTemplate(
                "mistral-7b-instruct-v0.2.Q4_0.gguf"
            );

            Assert.Equal(SmallMind.ConsoleApp.Commands.ChatTemplates.TemplateType.Mistral, template);
        }

        [Fact]
        public void ChatTemplate_DetectsPhi()
        {
            var template = SmallMind.ConsoleApp.Commands.ChatTemplates.DetectTemplate(
                "phi-2-q8_0.gguf"
            );

            Assert.Equal(SmallMind.ConsoleApp.Commands.ChatTemplates.TemplateType.Phi, template);
        }

        [Fact]
        public void ChatTemplate_FormatLlama2_Correct()
        {
            string prompt = "What is AI?";
            string formatted = SmallMind.ConsoleApp.Commands.ChatTemplates.Format(
                prompt,
                SmallMind.ConsoleApp.Commands.ChatTemplates.TemplateType.Llama2,
                isSystemMessage: false
            );

            Assert.Contains("[INST]", formatted);
            Assert.Contains(prompt, formatted);
            Assert.Contains("[/INST]", formatted);
        }

        [Fact]
        public void ChatTemplate_FormatMistral_Correct()
        {
            string prompt = "Hello";
            string formatted = SmallMind.ConsoleApp.Commands.ChatTemplates.Format(
                prompt,
                SmallMind.ConsoleApp.Commands.ChatTemplates.TemplateType.Mistral,
                isSystemMessage: false
            );

            Assert.Contains("[INST]", formatted);
            Assert.Contains(prompt, formatted);
            Assert.Contains("[/INST]", formatted);
        }

        [Fact]
        public void ChatTemplate_FormatChatML_Correct()
        {
            string prompt = "Test";
            string formatted = SmallMind.ConsoleApp.Commands.ChatTemplates.Format(
                prompt,
                SmallMind.ConsoleApp.Commands.ChatTemplates.TemplateType.ChatML,
                isSystemMessage: false
            );

            Assert.Contains("<|im_start|>user", formatted);
            Assert.Contains(prompt, formatted);
            Assert.Contains("<|im_end|>", formatted);
            Assert.Contains("<|im_start|>assistant", formatted);
        }

        [Fact]
        public void ChatTemplate_FormatPhi_Correct()
        {
            string prompt = "Question";
            string formatted = SmallMind.ConsoleApp.Commands.ChatTemplates.Format(
                prompt,
                SmallMind.ConsoleApp.Commands.ChatTemplates.TemplateType.Phi,
                isSystemMessage: false
            );

            Assert.Contains("User:", formatted);
            Assert.Contains(prompt, formatted);
            Assert.Contains("Assistant:", formatted);
        }

        [Fact]
        public async Task GenerateCommand_WithValidModel_Succeeds()
        {
            if (!_testsEnabled)
            {
                return;
            }

            // This test requires an SMQ model to exist
            string smqPath = Path.Combine(_testDir, "tinyllama.smq");
            
            if (!File.Exists(smqPath))
            {
                // Skip if model not available
                return;
            }

            // Arrange
            var generator = new SmallMind.ConsoleApp.Commands.GenerateCommand();
            string[] args = new[]
            {
                smqPath,
                "What is 2+2?",
                "--max-tokens", "50",
                "--temperature", "0.7",
                "--chat-template", "llama2"
            };

            // Act
            int exitCode = await generator.ExecuteAsync(args);

            // Assert
            Assert.Equal(0, exitCode);
            // Note: Actual output verification would require capturing stdout
        }
    }
}

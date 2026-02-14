namespace SmallMind.Tests.Chat
{
    /// <summary>
    /// Integration tests for chat template formatting.
    /// Tests that Engine and Console both produce consistent formatting via the canonical helper.
    /// </summary>
    public class ChatTemplateIntegrationTests
    {
        [Fact]
        public void Engine_ChatTemplates_Format_ProducesExpectedOutput()
        {
            // Arrange
            var template = SmallMind.Engine.ChatTemplateType.ChatML;
            string message = "Hello";

            // Act
            string result = SmallMind.Engine.ChatTemplates.Format(message, template, false);

            // Assert
            Assert.Contains("<|im_start|>user", result);
            Assert.Contains("Hello", result);
            Assert.Contains("<|im_end|>", result);
        }

        [Fact]
        public void Engine_ChatTemplates_DetectTemplate_Llama3_Detected()
        {
            // Act
            var result = SmallMind.Engine.ChatTemplates.DetectTemplate("llama-3-8b");

            // Assert
            Assert.Equal(SmallMind.Engine.ChatTemplateType.Llama3, result);
        }

        [Fact]
        public void Engine_ChatTemplates_DetectTemplate_WithMetadata_UsesMetadata()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "chat_template", "mistral" }
            };

            // Act
            var result = SmallMind.Engine.ChatTemplates.DetectTemplate("unknown-model", metadata);

            // Assert
            Assert.Equal(SmallMind.Engine.ChatTemplateType.Mistral, result);
        }

        [Fact]
        public void Engine_ChatTemplates_Format_Llama2_ProducesExpectedOutput()
        {
            // Arrange
            var template = SmallMind.Engine.ChatTemplateType.Llama2;
            string message = "Hello";

            // Act
            string result = SmallMind.Engine.ChatTemplates.Format(message, template, false);

            // Assert
            Assert.Contains("[INST]", result);
            Assert.Contains("Hello", result);
            Assert.Contains("[/INST]", result);
        }

        [Fact]
        public void Engine_ChatTemplates_DetectTemplate_Phi_Detected()
        {
            // Act
            var result = SmallMind.Engine.ChatTemplates.DetectTemplate("phi-2");

            // Assert
            Assert.Equal(SmallMind.Engine.ChatTemplateType.Phi, result);
        }

        [Fact]
        public void Engine_ChatTemplates_Format_ChatML_ConsistentOutput()
        {
            // Arrange
            string message = "Test message";

            // Act
            string result = SmallMind.Engine.ChatTemplates.Format(
                message, SmallMind.Engine.ChatTemplateType.ChatML, false);

            // Assert - Should produce expected ChatML output
            Assert.Contains("<|im_start|>user", result);
            Assert.Contains("Test message", result);
            Assert.Contains("<|im_end|>", result);
        }

        [Fact]
        public void Engine_ChatTemplates_Format_Llama3_SystemMessage()
        {
            // Arrange
            string message = "Test message";

            // Act
            string result = SmallMind.Engine.ChatTemplates.Format(
                message, SmallMind.Engine.ChatTemplateType.Llama3, true);

            // Assert - System message should use system role
            Assert.Contains("system", result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Test message", result);
        }

        [Fact]
        public void GetTemplateDescription_ReturnsUsefulDescription()
        {
            // Act
            string description = SmallMind.Engine.ChatTemplates.GetTemplateDescription(
                SmallMind.Engine.ChatTemplateType.Mistral);

            // Assert
            Assert.NotNull(description);
            Assert.NotEmpty(description);
            Assert.Contains("Mistral", description);
        }
    }
}

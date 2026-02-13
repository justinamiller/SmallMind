using SmallMind.Core.Utilities;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// Chat template formats for different model architectures.
    /// Automatically applies the appropriate template based on model metadata.
    /// This is now a thin wrapper around the canonical ChatTemplateFormatter in SmallMind.Core.
    /// </summary>
    internal static class ChatTemplates
    {
        internal enum TemplateType
        {
            None,
            ChatML,
            Llama2,
            Llama3,
            Mistral,
            Phi
        }

        /// <summary>
        /// Format a message using the specified chat template.
        /// </summary>
        public static string Format(string message, TemplateType template, bool isSystemMessage = false)
        {
            // Delegate to canonical helper
            return ChatTemplateFormatter.Format(message, (ChatTemplateType)template, isSystemMessage);
        }

        /// <summary>
        /// Detect template type from model name or metadata.
        /// </summary>
        public static TemplateType DetectTemplate(string modelNameOrPath, Dictionary<string, object>? metadata = null)
        {
            // Delegate to canonical helper
            return (TemplateType)ChatTemplateFormatter.DetectTemplate(modelNameOrPath, metadata);
        }

        /// <summary>
        /// Get a description of the template format.
        /// </summary>
        public static string GetTemplateDescription(TemplateType template)
        {
            // Delegate to canonical helper
            return ChatTemplateFormatter.GetTemplateDescription((ChatTemplateType)template);
        }
    }
}

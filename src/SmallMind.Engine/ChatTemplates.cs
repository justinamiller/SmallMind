using System;
using System.Collections.Generic;
using SmallMind.Core.Utilities;

namespace SmallMind.Engine
{
    /// <summary>
    /// Chat template formats for different model architectures.
    /// Moved from SmallMind.Console.Commands to SmallMind.Engine for unified chat pipeline.
    /// Internal static class for template detection and formatting.
    /// This is now a thin wrapper around the canonical ChatTemplateFormatter in SmallMind.Core.
    /// </summary>
    internal static class ChatTemplates
    {
        /// <summary>
        /// Format a message using the specified chat template.
        /// </summary>
        internal static string Format(string message, ChatTemplateType template, bool isSystemMessage = false)
        {
            // Delegate to canonical helper
            return ChatTemplateFormatter.Format(message, (Core.Utilities.ChatTemplateType)template, isSystemMessage);
        }

        /// <summary>
        /// Detect template type from model name or path.
        /// </summary>
        internal static ChatTemplateType DetectTemplate(string modelNameOrPath, Dictionary<string, object>? metadata = null)
        {
            // Delegate to canonical helper
            return (ChatTemplateType)ChatTemplateFormatter.DetectTemplate(modelNameOrPath, metadata);
        }

        /// <summary>
        /// Get a description of the template format.
        /// </summary>
        internal static string GetTemplateDescription(ChatTemplateType template)
        {
            // Delegate to canonical helper
            return ChatTemplateFormatter.GetTemplateDescription((Core.Utilities.ChatTemplateType)template);
        }
    }
}

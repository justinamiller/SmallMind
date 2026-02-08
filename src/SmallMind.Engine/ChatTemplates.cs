using System;
using System.Collections.Generic;

namespace SmallMind.Engine
{
    /// <summary>
    /// Chat template formats for different model architectures.
    /// Moved from SmallMind.Console.Commands to SmallMind.Engine for unified chat pipeline.
    /// Internal static class for template detection and formatting.
    /// </summary>
    internal static class ChatTemplates
    {
        /// <summary>
        /// Format a message using the specified chat template.
        /// </summary>
        internal static string Format(string message, ChatTemplateType template, bool isSystemMessage = false)
        {
            return template switch
            {
                ChatTemplateType.ChatML => FormatChatML(message, isSystemMessage),
                ChatTemplateType.Llama2 => FormatLlama2(message, isSystemMessage),
                ChatTemplateType.Llama3 => FormatLlama3(message, isSystemMessage),
                ChatTemplateType.Mistral => FormatMistral(message, isSystemMessage),
                ChatTemplateType.Phi => FormatPhi(message, isSystemMessage),
                _ => message // No template
            };
        }

        /// <summary>
        /// Detect template type from model name or path.
        /// </summary>
        internal static ChatTemplateType DetectTemplate(string modelNameOrPath, Dictionary<string, object>? metadata = null)
        {
            string modelLower = modelNameOrPath.ToLowerInvariant();

            // Check metadata first if available
            if (metadata != null)
            {
                if (metadata.TryGetValue("chat_template", out var chatTemplate))
                {
                    string templateStr = chatTemplate.ToString()?.ToLowerInvariant() ?? "";
                    if (templateStr.Contains("chatml")) return ChatTemplateType.ChatML;
                    if (templateStr.Contains("llama-3") || templateStr.Contains("llama3")) return ChatTemplateType.Llama3;
                    if (templateStr.Contains("llama") || templateStr.Contains("llama-2")) return ChatTemplateType.Llama2;
                    if (templateStr.Contains("mistral")) return ChatTemplateType.Mistral;
                    if (templateStr.Contains("phi")) return ChatTemplateType.Phi;
                }

                if (metadata.TryGetValue("model_type", out var modelType))
                {
                    string typeStr = modelType.ToString()?.ToLowerInvariant() ?? "";
                    if (typeStr.Contains("llama")) return ChatTemplateType.Llama2;
                    if (typeStr.Contains("mistral")) return ChatTemplateType.Mistral;
                    if (typeStr.Contains("phi")) return ChatTemplateType.Phi;
                }
            }

            // Fallback to name-based detection
            if (modelLower.Contains("llama-3") || modelLower.Contains("llama3"))
                return ChatTemplateType.Llama3;
            if (modelLower.Contains("llama"))
                return ChatTemplateType.Llama2;
            if (modelLower.Contains("mistral"))
                return ChatTemplateType.Mistral;
            if (modelLower.Contains("phi"))
                return ChatTemplateType.Phi;
            if (modelLower.Contains("chatml") || modelLower.Contains("vicuna") || modelLower.Contains("openchat"))
                return ChatTemplateType.ChatML;

            // Default to none
            return ChatTemplateType.None;
        }

        /// <summary>
        /// ChatML format (used by Vicuna, OpenChat, etc.)
        /// </summary>
        private static string FormatChatML(string message, bool isSystem)
        {
            string role = isSystem ? "system" : "user";
            return $"<|im_start|>{role}\n{message}<|im_end|>\n<|im_start|>assistant\n";
        }

        /// <summary>
        /// Llama 2 chat format
        /// </summary>
        private static string FormatLlama2(string message, bool isSystem)
        {
            if (isSystem)
            {
                return $"<<SYS>>\n{message}\n<</SYS>>\n\n";
            }
            return $"[INST] {message} [/INST] ";
        }

        /// <summary>
        /// Llama 3 chat format (uses special tokens)
        /// </summary>
        private static string FormatLlama3(string message, bool isSystem)
        {
            string role = isSystem ? "system" : "user";
            return $"<|start_header_id|>{role}<|end_header_id|>\n\n{message}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n";
        }

        /// <summary>
        /// Mistral Instruct format
        /// </summary>
        private static string FormatMistral(string message, bool isSystem)
        {
            if (isSystem)
            {
                // Mistral doesn't have a special system format, prepend to user message
                return $"{message}\n\n";
            }
            return $"[INST] {message} [/INST]";
        }

        /// <summary>
        /// Phi chat format
        /// </summary>
        private static string FormatPhi(string message, bool isSystem)
        {
            if (isSystem)
            {
                return $"System: {message}\n";
            }
            return $"User: {message}\nAssistant:";
        }

        /// <summary>
        /// Get a description of the template format.
        /// </summary>
        internal static string GetTemplateDescription(ChatTemplateType template)
        {
            return template switch
            {
                ChatTemplateType.ChatML => "ChatML (<|im_start|>user...)",
                ChatTemplateType.Llama2 => "Llama 2 ([INST]...)",
                ChatTemplateType.Llama3 => "Llama 3 (<|start_header_id|>...)",
                ChatTemplateType.Mistral => "Mistral Instruct ([INST]...)",
                ChatTemplateType.Phi => "Phi (User:.../Assistant:...)",
                _ => "None (raw text)"
            };
        }
    }
}

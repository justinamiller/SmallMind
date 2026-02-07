using System;
using System.Collections.Generic;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// Chat template formats for different model architectures.
    /// Automatically applies the appropriate template based on model metadata.
    /// </summary>
    public static class ChatTemplates
    {
        public enum TemplateType
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
            return template switch
            {
                TemplateType.ChatML => FormatChatML(message, isSystemMessage),
                TemplateType.Llama2 => FormatLlama2(message, isSystemMessage),
                TemplateType.Llama3 => FormatLlama3(message, isSystemMessage),
                TemplateType.Mistral => FormatMistral(message, isSystemMessage),
                TemplateType.Phi => FormatPhi(message, isSystemMessage),
                _ => message // No template
            };
        }

        /// <summary>
        /// Detect template type from model name or metadata.
        /// </summary>
        public static TemplateType DetectTemplate(string modelNameOrPath, Dictionary<string, object>? metadata = null)
        {
            string modelLower = modelNameOrPath.ToLowerInvariant();

            // Check metadata first if available
            if (metadata != null)
            {
                if (metadata.TryGetValue("chat_template", out var chatTemplate))
                {
                    string templateStr = chatTemplate.ToString()?.ToLowerInvariant() ?? "";
                    if (templateStr.Contains("chatml")) return TemplateType.ChatML;
                    if (templateStr.Contains("llama-3") || templateStr.Contains("llama3")) return TemplateType.Llama3;
                    if (templateStr.Contains("llama") || templateStr.Contains("llama-2")) return TemplateType.Llama2;
                    if (templateStr.Contains("mistral")) return TemplateType.Mistral;
                    if (templateStr.Contains("phi")) return TemplateType.Phi;
                }

                if (metadata.TryGetValue("model_type", out var modelType))
                {
                    string typeStr = modelType.ToString()?.ToLowerInvariant() ?? "";
                    if (typeStr.Contains("llama")) return TemplateType.Llama2;
                    if (typeStr.Contains("mistral")) return TemplateType.Mistral;
                    if (typeStr.Contains("phi")) return TemplateType.Phi;
                }
            }

            // Fallback to name-based detection
            if (modelLower.Contains("llama-3") || modelLower.Contains("llama3"))
                return TemplateType.Llama3;
            if (modelLower.Contains("llama"))
                return TemplateType.Llama2;
            if (modelLower.Contains("mistral"))
                return TemplateType.Mistral;
            if (modelLower.Contains("phi"))
                return TemplateType.Phi;
            if (modelLower.Contains("chatml") || modelLower.Contains("vicuna") || modelLower.Contains("openchat"))
                return TemplateType.ChatML;

            // Default to none
            return TemplateType.None;
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
        public static string GetTemplateDescription(TemplateType template)
        {
            return template switch
            {
                TemplateType.ChatML => "ChatML (<|im_start|>user...)",
                TemplateType.Llama2 => "Llama 2 ([INST]...)",
                TemplateType.Llama3 => "Llama 3 (<|start_header_id|>...)",
                TemplateType.Mistral => "Mistral Instruct ([INST]...)",
                TemplateType.Phi => "Phi (User:.../Assistant:...)",
                _ => "None (raw text)"
            };
        }
    }
}

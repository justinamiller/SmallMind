using System;
using System.IO;
using System.Threading.Tasks;
using SmallMind;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// CLI command to generate text from a trained model.
    /// Supports both raw generation and chat-formatted prompts.
    /// </summary>
    internal sealed class GenerateCommand : ICommand
    {
        public string Name => "generate";
        public string Description => "Generate text from a trained model";

        public async Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 2)
            {
                ShowUsage();
                return 1;
            }

            string modelPath = args[0];
            string prompt = args[1];

            // Parse optional arguments
            int maxTokens = 128;
            double temperature = 0.7;
            double topP = 0.9;
            ChatTemplates.TemplateType chatTemplate = ChatTemplates.TemplateType.None;
            bool autoDetectTemplate = false;

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--max-tokens" && i + 1 < args.Length)
                {
                    maxTokens = int.Parse(args[++i]);
                }
                else if (args[i] == "--temperature" && i + 1 < args.Length)
                {
                    temperature = double.Parse(args[++i]);
                }
                else if (args[i] == "--top-p" && i + 1 < args.Length)
                {
                    topP = double.Parse(args[++i]);
                }
                else if (args[i] == "--chat-template" && i + 1 < args.Length)
                {
                    string templateName = args[++i].ToLowerInvariant();
                    chatTemplate = templateName switch
                    {
                        "chatml" => ChatTemplates.TemplateType.ChatML,
                        "llama2" => ChatTemplates.TemplateType.Llama2,
                        "llama3" => ChatTemplates.TemplateType.Llama3,
                        "mistral" => ChatTemplates.TemplateType.Mistral,
                        "phi" => ChatTemplates.TemplateType.Phi,
                        "none" => ChatTemplates.TemplateType.None,
                        "auto" => ChatTemplates.TemplateType.None, // Will be detected
                        _ => throw new ArgumentException($"Unknown chat template: {templateName}")
                    };
                    autoDetectTemplate = templateName == "auto";
                }
            }

            if (!File.Exists(modelPath))
            {
                System.Console.Error.WriteLine($"Error: Model file not found: {modelPath}");
                return 1;
            }

            try
            {
                System.Console.WriteLine($"Loading model: {modelPath}");
                System.Console.WriteLine($"Max tokens: {maxTokens}");
                System.Console.WriteLine($"Temperature: {temperature}");
                System.Console.WriteLine($"Top-P: {topP}");

                // Create engine options
                var options = new SmallMindOptions
                {
                    ModelPath = modelPath,
                    MaxContextTokens = 2048,
                    EnableKvCache = true,
                    AllowGgufImport = false, // SMQ only for now
                    RequestTimeoutMs = 60000
                };

                using var engine = SmallMindFactory.Create(options);
                var caps = engine.GetCapabilities();

                System.Console.WriteLine($"Model loaded successfully!");
                System.Console.WriteLine($"  Format: {caps.ModelFormat}");
                System.Console.WriteLine($"  Quantization: {caps.Quantization}");

                // Auto-detect template if requested
                if (autoDetectTemplate)
                {
                    chatTemplate = ChatTemplates.DetectTemplate(modelPath);
                    System.Console.WriteLine($"  Auto-detected template: {ChatTemplates.GetTemplateDescription(chatTemplate)}");
                }
                else if (chatTemplate != ChatTemplates.TemplateType.None)
                {
                    System.Console.WriteLine($"  Chat template: {ChatTemplates.GetTemplateDescription(chatTemplate)}");
                }

                // Apply chat template if specified
                string formattedPrompt = prompt;
                if (chatTemplate != ChatTemplates.TemplateType.None)
                {
                    formattedPrompt = ChatTemplates.Format(prompt, chatTemplate, isSystemMessage: false);
                }

                System.Console.WriteLine();
                System.Console.WriteLine("Generating...");
                System.Console.WriteLine("─".PadRight(60, '─'));

                // Create session and generate using streaming
                var sessionOptions = new TextGenerationOptions
                {
                    MaxOutputTokens = maxTokens,
                    Temperature = (float)temperature,
                    TopP = (float)topP,
                    TopK = 40
                };

                using var session = engine.CreateTextGenerationSession(sessionOptions);
                
                var request = new TextGenerationRequest
                {
                    Prompt = formattedPrompt.AsMemory()
                };

                await foreach (var token in session.GenerateStreaming(request))
                {
                    System.Console.Write(token.TokenText);
                }

                System.Console.WriteLine();
                System.Console.WriteLine("─".PadRight(60, '─'));
                System.Console.WriteLine("Generation complete!");

                return 0;
            }
            catch (SmallMindException ex)
            {
                System.Console.Error.WriteLine($"Error: {ex.Message}");
                System.Console.Error.WriteLine($"Code: {ex.Code}");
                return 1;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Error: {ex.Message}");
                System.Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        public void ShowUsage()
        {
            System.Console.WriteLine("Usage: smallmind generate <model-path> <prompt> [options]");
            System.Console.WriteLine();
            System.Console.WriteLine("Arguments:");
            System.Console.WriteLine("  <model-path>   Path to model file (.smq)");
            System.Console.WriteLine("  <prompt>       Text prompt for generation");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            System.Console.WriteLine("  --max-tokens <n>           Maximum tokens to generate (default: 128)");
            System.Console.WriteLine("  --temperature <t>          Sampling temperature (default: 0.7)");
            System.Console.WriteLine("  --top-p <p>                Nucleus sampling threshold (default: 0.9)");
            System.Console.WriteLine("  --chat-template <type>     Apply chat template (chatml, llama2, llama3, mistral, phi, auto, none)");
            System.Console.WriteLine();
            System.Console.WriteLine("Examples:");
            System.Console.WriteLine("  # Basic generation:");
            System.Console.WriteLine("  smallmind generate model.smq \"Once upon a time\"");
            System.Console.WriteLine();
            System.Console.WriteLine("  # With custom parameters:");
            System.Console.WriteLine("  smallmind generate model.smq \"Explain quantum computing\" --max-tokens 256 --temperature 0.8");
            System.Console.WriteLine();
            System.Console.WriteLine("  # Using chat template:");
            System.Console.WriteLine("  smallmind generate model.smq \"What is the capital of France?\" --chat-template llama2");
            System.Console.WriteLine();
            System.Console.WriteLine("  # Auto-detect template:");
            System.Console.WriteLine("  smallmind generate mistral-7b.smq \"Hello!\" --chat-template auto");
            System.Console.WriteLine();
            System.Console.WriteLine("Supported Models:");
            System.Console.WriteLine("  - SMQ format (converted from Q8_0 or Q4_0 GGUF)");
            System.Console.WriteLine("  - Llama 2/3 architectures");
            System.Console.WriteLine("  - Mistral architectures");
            System.Console.WriteLine("  - Phi models");
            System.Console.WriteLine();
            System.Console.WriteLine("Note: Model must be in SMQ format. Use 'import-gguf' to convert GGUF files first.");
        }
    }
}

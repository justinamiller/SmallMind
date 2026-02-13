using SmallMind.Core;
using SmallMind.Transformers;
using SmallMind.Tokenizers;
using SmallMind.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MinimalGenerate
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("SmallMind Minimal Generation Example");
            Console.WriteLine("====================================\n");

            string? checkpointPath = args.Length > 0 ? args[0] : null;
            
            if (string.IsNullOrEmpty(checkpointPath) || !File.Exists(checkpointPath))
            {
                Console.WriteLine("Usage: dotnet run <checkpoint-path> [prompt]");
                Console.WriteLine("\nNo checkpoint provided. Creating tiny demo model...\n");
                await RunWithDemoModel(args.Length > 1 ? args[1] : "Hello");
            }
            else
            {
                string prompt = args.Length > 1 ? args[1] : "Hello";
                await RunWithCheckpoint(checkpointPath, prompt);
            }
        }

        static async Task RunWithCheckpoint(string checkpointPath, string prompt)
        {
            Console.WriteLine($"Loading checkpoint: {checkpointPath}");
            
            ICheckpointStore store = new BinaryCheckpointStore();
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { cts.Cancel(); e.Cancel = true; };

            try
            {
                var checkpoint = await store.LoadAsync(checkpointPath, cts.Token);
                Console.WriteLine($"✓ Loaded (v{checkpoint.FormatVersion}, {checkpoint.Parameters.Count} tensors)\n");

                var model = CheckpointExtensions.FromCheckpoint(checkpoint);
                
                // Create tokenizer with simple alphabet
                var tokenizer = CreateSimpleTokenizer();
                var generator = new Sampling(model, tokenizer, checkpoint.Metadata.BlockSize);
                
                Console.WriteLine($"Prompt: \"{prompt}\"");
                var generated = generator.Generate(prompt, 50, 0.8, 40, 42, false);
                Console.WriteLine($"\nGenerated:\n{generated}");
            }
            catch (OperationCanceledException) { Console.WriteLine("\nCancelled."); }
            catch (Exception ex) { Console.WriteLine($"\nError: {ex.Message}"); }
        }

        static async Task RunWithDemoModel(string prompt)
        {
            Console.WriteLine("Creating demo model (tiny, untrained)...");
            var model = new TransformerModel(50, 32, 64, 2, 4, 0.1, 42);
            Console.WriteLine($"✓ Created ({model.Parameters.Count} params)\n");

            var checkpointPath = "demo-model.smnd";
            var store = new BinaryCheckpointStore();
            await store.SaveAsync(model.ToCheckpoint(), checkpointPath);
            Console.WriteLine($"✓ Saved to {checkpointPath}\n");

            var tokenizer = CreateSimpleTokenizer();
            var generator = new Sampling(model, tokenizer, model.BlockSize);
            Console.WriteLine($"Generating from \"{prompt}\" (random, untrained)...");
            var generated = generator.Generate(prompt, 30, 1.0, 0, 123, false);
            Console.WriteLine($"\nGenerated:\n{generated}");
        }

        static CharTokenizer CreateSimpleTokenizer()
        {
            // Create a tokenizer with common characters
            const string vocab = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?;:'\"-\n";
            return new CharTokenizer(vocab);
        }
    }
}

using SmallMind.Runtime.PretrainedModels;
using System;
using System.IO;
using System.Linq;

namespace PackLoaderExample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SmallMind Pretrained Data Packs Example");
            Console.WriteLine("========================================\n");

            // Example 1: Discover available packs
            DiscoverPacksExample();

            Console.WriteLine("\n" + new string('-', 80) + "\n");

            // Example 2: Load and inspect sentiment pack
            LoadSentimentPackExample();
        }

        static void DiscoverPacksExample()
        {
            Console.WriteLine("Example 1: Discovering Available Packs");
            Console.WriteLine("=======================================\n");

            var registryPath = Path.Combine("data", "pretrained", "registry.json");
            
            if (!File.Exists(registryPath))
            {
                Console.WriteLine($"Registry not found. Please run from SmallMind root.");
                return;
            }

            var registry = PretrainedRegistry.Load(registryPath);
            Console.WriteLine($"Found {registry.Packs.Count} pretrained packs:\n");
            Console.WriteLine(registry.ListPacks());
        }

        static void LoadSentimentPackExample()
        {
            Console.WriteLine("Example 2: Loading Sentiment Analysis Pack");
            Console.WriteLine("===========================================\n");

            var packPath = Path.Combine("data", "pretrained", "sentiment");
            if (!Directory.Exists(packPath)) return;

            var pack = PretrainedPack.Load(packPath);
            Console.WriteLine(pack.GetSummary());
            
            Console.WriteLine("\nSample Texts:");
            foreach (var sample in pack.Samples.Take(3))
            {
                Console.WriteLine($"  [{sample.Id}] {sample.Label}: \"{sample.Text}\"");
            }
        }
    }
}

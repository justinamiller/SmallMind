using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmallMind.Core;
using SmallMind.Text;
using SmallMind.Workflows;

namespace SmallMind.Samples.Workflows
{
    /// <summary>
    /// Simple console example demonstrating workflow-aware generation.
    /// Shows how to create and execute a workflow with structured outputs.
    /// </summary>
    public class WorkflowExample
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Workflow-Aware Generation Example ===\n");

            // Note: This example requires a trained model.
            // For demonstration purposes, we'll show the workflow setup.
            // In a real scenario, you would load a trained model first.

            Console.WriteLine("This example demonstrates workflow setup and structure.");
            Console.WriteLine("To run with an actual model, first train SmallMind on your data.\n");

            // Example 1: Simple Classification Workflow
            DemonstrateClassificationWorkflow();

            // Example 2: IT Ticket Triage Workflow
            DemonstrateItTicketTriageWorkflow();

            // Example 3: Policy Decision Workflow
            DemonstratePolicyDecisionWorkflow();

            Console.WriteLine("\nWorkflow examples completed!");
            Console.WriteLine("See docs/WORKFLOWS.md for detailed documentation.");
        }

        private static void DemonstrateClassificationWorkflow()
        {
            Console.WriteLine("--- Example 1: Simple Classification Workflow ---\n");

            var workflow = new WorkflowDefinition
            {
                Name = "Sentiment Classification",
                Version = "1.0",
                RunnerOptions = new WorkflowRunnerOptions
                {
                    Deterministic = true,
                    Seed = 42,
                    Temperature = 0.3,
                    StopOnFailure = true,
                    EmitDiagnostics = true
                },
                Budgets = new WorkflowBudgets
                {
                    MaxTotalTokens = 1000,
                    MaxStepTokens = 200
                },
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        StepId = "classify_sentiment",
                        Title = "Classify Sentiment",
                        Instruction = "Based on the text, classify the sentiment.",
                        InputSpec = new StepInputSpec
                        {
                            RequiredStateKeys = new List<string> { "text" },
                            MaxContextChars = 500
                        },
                        OutputSpec = new StepOutputSpec
                        {
                            Format = OutputFormat.EnumOnly,
                            AllowedValues = new List<string> { "positive", "negative", "neutral" },
                            MaxOutputChars = 50,
                            Strict = true
                        }
                    }
                }
            };

            var state = new WorkflowState();
            state.Set("text", "This product exceeded my expectations! Highly recommend.");

            Console.WriteLine($"Workflow: {workflow.Name}");
            Console.WriteLine($"Version: {workflow.Version}");
            Console.WriteLine($"Steps: {workflow.Steps.Count}");
            Console.WriteLine($"Input: {state.GetString("text")}");
            Console.WriteLine($"Expected Output Format: {workflow.Steps[0].OutputSpec.Format}");
            Console.WriteLine($"Allowed Values: {string.Join(", ", workflow.Steps[0].OutputSpec.AllowedValues ?? new List<string>())}");
            Console.WriteLine();
        }

        private static void DemonstrateItTicketTriageWorkflow()
        {
            Console.WriteLine("--- Example 2: IT Ticket Triage Workflow ---\n");

            var workflow = ItTicketTriageWorkflow.CreateWorkflow();
            var state = ItTicketTriageWorkflow.CreateSampleState();

            Console.WriteLine($"Workflow: {workflow.Name}");
            Console.WriteLine($"Version: {workflow.Version}");
            Console.WriteLine($"Steps: {workflow.Steps.Count}");
            Console.WriteLine();

            foreach (var step in workflow.Steps)
            {
                Console.WriteLine($"  Step {step.StepId}: {step.Title}");
                Console.WriteLine($"    Output Format: {step.OutputSpec.Format}");
                
                if (step.OutputSpec.Format == OutputFormat.EnumOnly && step.OutputSpec.AllowedValues != null)
                {
                    Console.WriteLine($"    Allowed Values: {string.Join(", ", step.OutputSpec.AllowedValues)}");
                }
                else if (step.OutputSpec.Format == OutputFormat.JsonOnly && step.OutputSpec.RequiredJsonFields != null)
                {
                    Console.WriteLine($"    Required Fields: {string.Join(", ", step.OutputSpec.RequiredJsonFields)}");
                }
            }

            Console.WriteLine($"\nInput (ticket description):");
            Console.WriteLine($"  {state.GetString("ticket_description")}");
            Console.WriteLine();
        }

        private static void DemonstratePolicyDecisionWorkflow()
        {
            Console.WriteLine("--- Example 3: Policy Decision Workflow ---\n");

            var workflow = PolicyDecisionWorkflow.CreateWorkflow();
            var state = PolicyDecisionWorkflow.CreateSampleState();

            Console.WriteLine($"Workflow: {workflow.Name}");
            Console.WriteLine($"Version: {workflow.Version}");
            Console.WriteLine($"Steps: {workflow.Steps.Count}");
            Console.WriteLine();

            foreach (var step in workflow.Steps)
            {
                Console.WriteLine($"  Step {step.StepId}: {step.Title}");
                Console.WriteLine($"    Output Format: {step.OutputSpec.Format}");
                
                if (step.OutputSpec.Format == OutputFormat.EnumOnly && step.OutputSpec.AllowedValues != null)
                {
                    Console.WriteLine($"    Allowed Values: {string.Join(", ", step.OutputSpec.AllowedValues)}");
                }
                else if (step.OutputSpec.Format == OutputFormat.JsonOnly && step.OutputSpec.RequiredJsonFields != null)
                {
                    Console.WriteLine($"    Required Fields: {string.Join(", ", step.OutputSpec.RequiredJsonFields)}");
                }
            }

            Console.WriteLine($"\nInput (scenario):");
            Console.WriteLine($"  {state.GetString("scenario")}");
            Console.WriteLine();
        }

        // Uncomment and adapt this method to run with an actual trained model
        /*
        private static async Task RunWorkflowWithModel()
        {
            // Load your trained model and tokenizer
            var trainingText = System.IO.File.ReadAllText("data.txt");
            var tokenizer = new Tokenizer(trainingText);
            var model = new TransformerModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 128,
                nEmbd: 64,
                nLayer: 2,
                nHead: 4,
                dropout: 0.1,
                seed: 42
            );

            // Load trained weights
            // model.LoadWeights("checkpoint.json");

            // Create workflow runner
            var runner = new WorkflowRunner(model, tokenizer, blockSize: 128);

            // Create and execute workflow
            var workflow = ItTicketTriageWorkflow.CreateWorkflow();
            var state = ItTicketTriageWorkflow.CreateSampleState();

            var request = new WorkflowRunRequest
            {
                Workflow = workflow,
                InitialState = state
            };

            var result = await runner.RunAsync(request);

            // Display results
            Console.WriteLine($"Workflow Status: {result.Status}");
            Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2}s");
            Console.WriteLine($"Total Tokens: {result.TotalInputTokens + result.TotalOutputTokens}");
            Console.WriteLine();

            foreach (var stepResult in result.Steps)
            {
                Console.WriteLine($"Step {stepResult.StepId}:");
                Console.WriteLine($"  Status: {stepResult.Status}");
                Console.WriteLine($"  Output: {stepResult.OutputText}");
                Console.WriteLine($"  Tokens: {stepResult.TokensIn} in, {stepResult.TokensOut} out");
                Console.WriteLine($"  Duration: {stepResult.Duration.TotalMilliseconds:F0}ms");
                if (stepResult.ValidationErrors.Count > 0)
                {
                    Console.WriteLine($"  Validation Errors: {string.Join(", ", stepResult.ValidationErrors)}");
                }
                Console.WriteLine();
            }
        }
        */
    }
}

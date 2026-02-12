using System;
using System.Collections.Generic;
using SmallMind.Workflows;

namespace SmallMind.Samples.Workflows
{
    /// <summary>
    /// Demonstrates an IT ticket triage workflow with multiple classification steps.
    /// Shows EnumOnly and JsonOnly output formats.
    /// </summary>
    public class ItTicketTriageWorkflow
    {
        public static WorkflowDefinition CreateWorkflow()
        {
            return new WorkflowDefinition
            {
                Name = "IT Ticket Triage",
                Version = "1.0",
                Budgets = new WorkflowBudgets
                {
                    MaxTotalTokens = 2000,
                    MaxStepTokens = 300
                },
                RunnerOptions = new WorkflowRunnerOptions
                {
                    Deterministic = true,
                    Seed = 42,
                    Temperature = 0.3, // Lower temperature for more deterministic output
                    TopK = 20,
                    StopOnFailure = true,
                    EmitDiagnostics = true
                },
                Steps = new List<WorkflowStep>
                {
                    // Step 1: Classify ticket type
                    new WorkflowStep
                    {
                        StepId = "classify",
                        Title = "Classify Ticket Type",
                        Instruction = "Based on the ticket description, classify this as an incident, request, or problem.",
                        InputSpec = new StepInputSpec
                        {
                            RequiredStateKeys = new List<string> { "ticket_description" },
                            MaxContextChars = 1000
                        },
                        OutputSpec = new StepOutputSpec
                        {
                            Format = OutputFormat.EnumOnly,
                            AllowedValues = new List<string> { "incident", "request", "problem" },
                            MaxOutputChars = 100,
                            Strict = true
                        }
                    },

                    // Step 2: Determine severity
                    new WorkflowStep
                    {
                        StepId = "severity",
                        Title = "Determine Severity Level",
                        Instruction = "Based on the ticket description and type, determine the severity level.",
                        InputSpec = new StepInputSpec
                        {
                            RequiredStateKeys = new List<string> { "ticket_description" },
                            IncludePriorStepOutputs = true,
                            MaxContextChars = 1000
                        },
                        OutputSpec = new StepOutputSpec
                        {
                            Format = OutputFormat.EnumOnly,
                            AllowedValues = new List<string> { "low", "medium", "high", "critical" },
                            MaxOutputChars = 100,
                            Strict = true
                        }
                    },

                    // Step 3: Assign to group
                    new WorkflowStep
                    {
                        StepId = "assignment",
                        Title = "Assign to Support Group",
                        Instruction = "Based on the ticket description and classification, determine which support group should handle this ticket.",
                        InputSpec = new StepInputSpec
                        {
                            RequiredStateKeys = new List<string> { "ticket_description" },
                            IncludePriorStepOutputs = true,
                            MaxContextChars = 1500
                        },
                        OutputSpec = new StepOutputSpec
                        {
                            Format = OutputFormat.EnumOnly,
                            AllowedValues = new List<string> 
                            { 
                                "network", 
                                "application", 
                                "database", 
                                "security", 
                                "infrastructure" 
                            },
                            MaxOutputChars = 100,
                            Strict = true
                        }
                    },

                    // Step 4: Determine next action
                    new WorkflowStep
                    {
                        StepId = "next_action",
                        Title = "Determine Next Action",
                        Instruction = "Based on all previous information, provide the recommended next action and rationale in JSON format.",
                        InputSpec = new StepInputSpec
                        {
                            RequiredStateKeys = new List<string> { "ticket_description" },
                            IncludePriorStepOutputs = true,
                            MaxContextChars = 2000
                        },
                        OutputSpec = new StepOutputSpec
                        {
                            Format = OutputFormat.JsonOnly,
                            JsonTemplate = @"{
  ""action"": ""escalate"",
  ""rationale"": ""High severity incident requiring immediate attention""
}",
                            RequiredJsonFields = new List<string> { "action", "rationale" },
                            MaxOutputChars = 500,
                            Strict = true
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Create sample initial state for testing the workflow.
        /// </summary>
        public static WorkflowState CreateSampleState()
        {
            var state = new WorkflowState();
            state.Set("ticket_description", 
                "User reports that the customer database is completely unavailable. " +
                "Multiple users across different locations cannot access critical customer data. " +
                "This is affecting sales operations and customer service.");
            return state;
        }
    }
}

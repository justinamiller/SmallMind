using System;
using System.Collections.Generic;
using SmallMind.Workflows;

namespace SmallMind.Samples.Workflows
{
    /// <summary>
    /// Demonstrates a policy decision workflow with clause extraction and compliance checking.
    /// Shows JsonOnly and EnumOnly output formats for structured decision-making.
    /// </summary>
    public class PolicyDecisionWorkflow
    {
        public static WorkflowDefinition CreateWorkflow()
        {
            return new WorkflowDefinition
            {
                Name = "Policy Decision Engine",
                Version = "1.0",
                Budgets = new WorkflowBudgets
                {
                    MaxTotalTokens = 3000,
                    MaxStepTokens = 500
                },
                RunnerOptions = new WorkflowRunnerOptions
                {
                    Deterministic = true,
                    Seed = 123,
                    Temperature = 0.2, // Very low temperature for consistency
                    TopK = 10,
                    StopOnFailure = true,
                    EmitDiagnostics = true
                },
                Steps = new List<WorkflowStep>
                {
                    // Step 1: Extract relevant policy clause
                    new WorkflowStep
                    {
                        StepId = "extract_clause",
                        Title = "Extract Relevant Policy Clause",
                        Instruction = "Identify and extract the most relevant policy clause that applies to this scenario. Return as JSON with the clause text and source reference.",
                        InputSpec = new StepInputSpec
                        {
                            RequiredStateKeys = new List<string> { "scenario", "policy_document" },
                            MaxContextChars = 2000
                        },
                        OutputSpec = new StepOutputSpec
                        {
                            Format = OutputFormat.JsonOnly,
                            JsonTemplate = @"{
  ""clause"": ""Employees must not share credentials with third parties"",
  ""source"": ""Section 4.2.1 - Access Control Policy""
}",
                            RequiredJsonFields = new List<string> { "clause", "source" },
                            MaxOutputChars = 800,
                            Strict = true
                        }
                    },

                    // Step 2: Determine compliance
                    new WorkflowStep
                    {
                        StepId = "decide_compliance",
                        Title = "Determine Compliance Status",
                        Instruction = "Based on the scenario and the extracted policy clause, determine if the scenario is compliant, noncompliant, or if compliance status is unknown.",
                        InputSpec = new StepInputSpec
                        {
                            RequiredStateKeys = new List<string> { "scenario" },
                            IncludePriorStepOutputs = true,
                            MaxContextChars = 2000
                        },
                        OutputSpec = new StepOutputSpec
                        {
                            Format = OutputFormat.EnumOnly,
                            AllowedValues = new List<string> { "compliant", "noncompliant", "unknown" },
                            MaxOutputChars = 100,
                            Strict = true
                        }
                    },

                    // Step 3: Produce decision record
                    new WorkflowStep
                    {
                        StepId = "decision_record",
                        Title = "Generate Decision Record",
                        Instruction = "Create a formal decision record with the compliance decision, detailed justification, and identified risks. Return as JSON.",
                        InputSpec = new StepInputSpec
                        {
                            RequiredStateKeys = new List<string> { "scenario" },
                            IncludePriorStepOutputs = true,
                            MaxContextChars = 3000
                        },
                        OutputSpec = new StepOutputSpec
                        {
                            Format = OutputFormat.JsonOnly,
                            JsonTemplate = @"{
  ""decision"": ""noncompliant"",
  ""justification"": ""The scenario describes credential sharing which violates Section 4.2.1"",
  ""risks"": [""Unauthorized access"", ""Data breach"", ""Audit failure""]
}",
                            RequiredJsonFields = new List<string> { "decision", "justification", "risks" },
                            MaxOutputChars = 1000,
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
            
            state.Set("scenario", 
                "A contractor working on a temporary project requested access to the production database. " +
                "The team lead provided their own credentials to the contractor to expedite the work, " +
                "with the understanding that access would be revoked after the project completion.");

            state.Set("policy_document",
                "Section 4.2.1 - Access Control Policy: All users must have individual accounts. " +
                "Credential sharing is strictly prohibited. Temporary access must be provisioned through " +
                "the IT department using the standard access request process. " +
                "Section 4.2.2 - Contractor Access: Contractors require manager approval and separate accounts.");

            return state;
        }
    }
}

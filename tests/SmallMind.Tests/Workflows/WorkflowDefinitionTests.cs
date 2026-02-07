using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SmallMind.Workflows;
using SmallMind.Domain;

namespace SmallMind.Tests.Workflows
{
    /// <summary>
    /// Unit tests for workflow definitions and configuration.
    /// Tests workflow structure, step configuration, and validation.
    /// </summary>
    public class WorkflowDefinitionTests
    {
        [Fact]
        public void WorkflowDefinition_DefaultValues_AreSet()
        {
            // Arrange & Act
            var workflow = new WorkflowDefinition();

            // Assert
            Assert.NotNull(workflow.Steps);
            Assert.Empty(workflow.Steps);
            Assert.NotNull(workflow.Budgets);
            Assert.NotNull(workflow.RunnerOptions);
            Assert.Equal("1.0", workflow.Version);
        }

        [Fact]
        public void WorkflowStep_DefaultValues_AreSet()
        {
            // Arrange & Act
            var step = new WorkflowStep();

            // Assert
            Assert.NotNull(step.InputSpec);
            Assert.NotNull(step.OutputSpec);
            Assert.NotNull(step.Retry);
            Assert.False(step.RequiresHumanApproval);
        }

        [Fact]
        public void StepOutputSpec_DefaultFormat_IsJsonOnly()
        {
            // Arrange & Act
            var spec = new StepOutputSpec();

            // Assert
            Assert.Equal(OutputFormat.JsonOnly, spec.Format);
            Assert.True(spec.Strict);
            Assert.Equal(2000, spec.MaxOutputChars);
        }

        [Fact]
        public void StepInputSpec_DefaultValues_AreSet()
        {
            // Arrange & Act
            var spec = new StepInputSpec();

            // Assert
            Assert.NotNull(spec.RequiredStateKeys);
            Assert.Empty(spec.RequiredStateKeys);
            Assert.NotNull(spec.OptionalStateKeys);
            Assert.Empty(spec.OptionalStateKeys);
            Assert.True(spec.IncludePriorStepOutputs);
            Assert.False(spec.AllowUserNotes);
            Assert.Equal(4000, spec.MaxContextChars);
        }

        [Fact]
        public void StepRetryPolicy_DefaultValues_AreSet()
        {
            // Arrange & Act
            var policy = new StepRetryPolicy();

            // Assert
            Assert.Equal(2, policy.MaxAttempts);
            Assert.True(policy.UseSameSeed);
        }

        [Fact]
        public void WorkflowBudgets_DefaultValues_AreSet()
        {
            // Arrange & Act
            var budgets = new WorkflowBudgets();

            // Assert
            Assert.Null(budgets.MaxTotalTokens);
            Assert.Equal(500, budgets.MaxStepTokens);
            Assert.Null(budgets.MaxDuration);
        }

        [Fact]
        public void WorkflowRunnerOptions_DefaultValues_AreSet()
        {
            // Arrange & Act
            var options = new WorkflowRunnerOptions();

            // Assert
            Assert.False(options.Deterministic);
            Assert.Null(options.Seed);
            Assert.Equal(0.7, options.Temperature);
            Assert.Equal(40, options.TopK);
            Assert.Equal(0.9, options.TopP);
            Assert.True(options.StopOnFailure);
            Assert.True(options.EmitDiagnostics);
        }

        [Fact]
        public void WorkflowDefinition_WithSteps_CanBeCreated()
        {
            // Arrange
            var step1 = new WorkflowStep
            {
                StepId = "step1",
                Title = "First Step",
                Instruction = "Do something"
            };

            var step2 = new WorkflowStep
            {
                StepId = "step2",
                Title = "Second Step",
                Instruction = "Do something else"
            };

            // Act
            var workflow = new WorkflowDefinition
            {
                Name = "Test Workflow",
                Version = "1.0",
                Steps = new List<WorkflowStep> { step1, step2 }
            };

            // Assert
            Assert.Equal("Test Workflow", workflow.Name);
            Assert.Equal(2, workflow.Steps.Count);
            Assert.Equal("step1", workflow.Steps[0].StepId);
            Assert.Equal("step2", workflow.Steps[1].StepId);
        }

        [Fact]
        public void WorkflowDefinition_WithCustomBudgets_CanBeCreated()
        {
            // Arrange & Act
            var workflow = new WorkflowDefinition
            {
                Name = "Test Workflow",
                Budgets = new WorkflowBudgets
                {
                    MaxTotalTokens = 5000,
                    MaxStepTokens = 1000,
                    MaxDuration = TimeSpan.FromMinutes(5)
                }
            };

            // Assert
            Assert.Equal(5000, workflow.Budgets.MaxTotalTokens);
            Assert.Equal(1000, workflow.Budgets.MaxStepTokens);
            Assert.Equal(TimeSpan.FromMinutes(5), workflow.Budgets.MaxDuration);
        }

        [Fact]
        public void WorkflowDefinition_WithDeterministicOptions_CanBeCreated()
        {
            // Arrange & Act
            var workflow = new WorkflowDefinition
            {
                Name = "Deterministic Workflow",
                RunnerOptions = new WorkflowRunnerOptions
                {
                    Deterministic = true,
                    Seed = 42,
                    Temperature = 0.1,
                    TopK = 5
                }
            };

            // Assert
            Assert.True(workflow.RunnerOptions.Deterministic);
            Assert.Equal(42, workflow.RunnerOptions.Seed);
            Assert.Equal(0.1, workflow.RunnerOptions.Temperature);
            Assert.Equal(5, workflow.RunnerOptions.TopK);
        }

        [Fact]
        public void WorkflowStep_WithEnumOutput_CanBeCreated()
        {
            // Arrange & Act
            var step = new WorkflowStep
            {
                StepId = "classify",
                Title = "Classify Input",
                Instruction = "Classify the input",
                OutputSpec = new StepOutputSpec
                {
                    Format = OutputFormat.EnumOnly,
                    AllowedValues = new List<string> { "yes", "no", "maybe" },
                    Strict = true
                }
            };

            // Assert
            Assert.Equal(OutputFormat.EnumOnly, step.OutputSpec.Format);
            Assert.NotNull(step.OutputSpec.AllowedValues);
            Assert.Equal(3, step.OutputSpec.AllowedValues.Count);
            Assert.Contains("yes", step.OutputSpec.AllowedValues);
        }

        [Fact]
        public void WorkflowStep_WithJsonOutput_CanBeCreated()
        {
            // Arrange & Act
            var step = new WorkflowStep
            {
                StepId = "extract",
                Title = "Extract Data",
                Instruction = "Extract structured data",
                OutputSpec = new StepOutputSpec
                {
                    Format = OutputFormat.JsonOnly,
                    RequiredJsonFields = new List<string> { "name", "value" },
                    JsonTemplate = @"{""name"": ""example"", ""value"": 123}",
                    Strict = true
                }
            };

            // Assert
            Assert.Equal(OutputFormat.JsonOnly, step.OutputSpec.Format);
            Assert.NotNull(step.OutputSpec.RequiredJsonFields);
            Assert.Equal(2, step.OutputSpec.RequiredJsonFields.Count);
            Assert.NotNull(step.OutputSpec.JsonTemplate);
        }

        [Fact]
        public void WorkflowStep_WithRegexOutput_CanBeCreated()
        {
            // Arrange & Act
            var step = new WorkflowStep
            {
                StepId = "format",
                Title = "Format Output",
                Instruction = "Format the output",
                OutputSpec = new StepOutputSpec
                {
                    Format = OutputFormat.RegexConstrained,
                    Regex = @"^[A-Z]{3}-\d{5}$",
                    Strict = true
                }
            };

            // Assert
            Assert.Equal(OutputFormat.RegexConstrained, step.OutputSpec.Format);
            Assert.NotNull(step.OutputSpec.Regex);
            Assert.Equal(@"^[A-Z]{3}-\d{5}$", step.OutputSpec.Regex);
        }

        [Fact]
        public void WorkflowRunRequest_DefaultValues_AreSet()
        {
            // Arrange & Act
            var request = new WorkflowRunRequest();

            // Assert
            Assert.NotNull(request.Workflow);
            Assert.NotNull(request.InitialState);
            Assert.Null(request.RunId);
            Assert.Null(request.UserInput);
        }

        [Fact]
        public void WorkflowRunResult_DefaultValues_AreSet()
        {
            // Arrange & Act
            var result = new WorkflowRunResult();

            // Assert
            Assert.NotNull(result.FinalState);
            Assert.NotNull(result.Steps);
            Assert.Empty(result.Steps);
            Assert.Equal(TimeSpan.Zero, result.Duration);
            Assert.Equal(0, result.TotalInputTokens);
            Assert.Equal(0, result.TotalOutputTokens);
        }

        [Fact]
        public void StepResult_DefaultValues_AreSet()
        {
            // Arrange & Act
            var result = new StepResult();

            // Assert
            Assert.NotNull(result.ValidationErrors);
            Assert.Empty(result.ValidationErrors);
            Assert.Equal(TimeSpan.Zero, result.Duration);
            Assert.Equal(0, result.TokensIn);
            Assert.Equal(0, result.TokensOut);
            Assert.Equal(1, result.Attempts);
        }
    }
}

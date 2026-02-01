using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmallMind.Core;
using SmallMind.Text;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Executes workflow definitions using the SmallMind language model.
    /// </summary>
    public class WorkflowRunner : IWorkflowRunner
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly int _blockSize;
        private readonly ILogger<WorkflowRunner>? _logger;
        private readonly OutputValidator _validator;

        public WorkflowRunner(
            TransformerModel model,
            ITokenizer tokenizer,
            int blockSize,
            ILogger<WorkflowRunner>? logger = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _blockSize = blockSize;
            _logger = logger;
            _validator = new OutputValidator();
        }

        /// <summary>
        /// Execute a workflow and return the final result.
        /// </summary>
        public async Task<WorkflowRunResult> RunAsync(
            WorkflowRunRequest request,
            CancellationToken cancellationToken = default)
        {
            var runId = request.RunId ?? Guid.NewGuid().ToString("N");
            var workflow = request.Workflow;
            var state = request.InitialState;

            // Initialize metadata
            state.Metadata["runId"] = runId;
            state.Metadata["workflowName"] = workflow.Name;
            state.Metadata["workflowVersion"] = workflow.Version;
            state.Metadata["startTime"] = DateTime.UtcNow;

            var result = new WorkflowRunResult
            {
                RunId = runId,
                FinalState = state
            };

            var stepResults = new List<StepResult>();
            var runStopwatch = Stopwatch.StartNew();

            try
            {
                _logger?.LogInformation("Starting workflow run {RunId} for workflow {WorkflowName} v{Version}",
                    runId, workflow.Name, workflow.Version);

                foreach (var step in workflow.Steps)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check if we should stop on previous failure
                    if (workflow.RunnerOptions.StopOnFailure &&
                        stepResults.Any(r => r.Status == StepStatus.Failed))
                    {
                        _logger?.LogWarning("Stopping workflow {RunId} due to previous step failure", runId);
                        break;
                    }

                    var stepResult = await ExecuteStepAsync(
                        step,
                        state,
                        workflow,
                        request.UserInput,
                        cancellationToken);

                    stepResults.Add(stepResult);

                    // Update totals
                    result.TotalInputTokens += stepResult.TokensIn;
                    result.TotalOutputTokens += stepResult.TokensOut;

                    // Check budget constraints
                    if (workflow.Budgets.MaxTotalTokens.HasValue &&
                        result.TotalOutputTokens > workflow.Budgets.MaxTotalTokens.Value)
                    {
                        result.Status = WorkflowRunStatus.RejectedPolicy;
                        result.FailureReason = "Exceeded maximum total tokens budget";
                        _logger?.LogWarning("Workflow {RunId} rejected: {Reason}", runId, result.FailureReason);
                        return result;
                    }

                    // Check duration
                    if (workflow.Budgets.MaxDuration.HasValue &&
                        runStopwatch.Elapsed > workflow.Budgets.MaxDuration.Value)
                    {
                        result.Status = WorkflowRunStatus.RejectedPolicy;
                        result.FailureReason = "Exceeded maximum workflow duration";
                        _logger?.LogWarning("Workflow {RunId} rejected: {Reason}", runId, result.FailureReason);
                        return result;
                    }
                }

                // Determine overall status
                if (stepResults.All(r => r.Status == StepStatus.Success))
                {
                    result.Status = WorkflowRunStatus.Success;
                }
                else if (stepResults.Any(r => r.Status == StepStatus.Failed))
                {
                    result.Status = WorkflowRunStatus.Failed;
                    var failedStep = stepResults.First(r => r.Status == StepStatus.Failed);
                    result.FailureReason = $"Step {failedStep.StepId} failed: {failedStep.FailureReason}";
                }
                else
                {
                    result.Status = WorkflowRunStatus.Success;
                }

                _logger?.LogInformation("Workflow {RunId} completed with status {Status}", runId, result.Status);
            }
            catch (OperationCanceledException)
            {
                result.Status = WorkflowRunStatus.Cancelled;
                result.FailureReason = "Workflow execution was cancelled";
                _logger?.LogWarning("Workflow {RunId} was cancelled", runId);
            }
            catch (Exception ex)
            {
                result.Status = WorkflowRunStatus.Failed;
                result.FailureReason = $"Unexpected error: {ex.Message}";
                _logger?.LogError(ex, "Workflow {RunId} failed with exception", runId);
            }
            finally
            {
                runStopwatch.Stop();
                result.Duration = runStopwatch.Elapsed;
                result.Steps = stepResults;
                state.Metadata["endTime"] = DateTime.UtcNow;
            }

            return result;
        }

        /// <summary>
        /// Execute a workflow with streaming events.
        /// </summary>
        public async IAsyncEnumerable<WorkflowRunEvent> RunStreamAsync(
            WorkflowRunRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var runId = request.RunId ?? Guid.NewGuid().ToString("N");
            var runStopwatch = Stopwatch.StartNew();

            yield return new WorkflowRunEvent
            {
                Type = WorkflowRunEventType.RunStarted,
                RunId = runId,
                ElapsedMilliseconds = 0,
                Message = $"Starting workflow {request.Workflow.Name}"
            };

            // For now, just execute and yield final result
            // Streaming token generation would require deeper integration with Sampling class
            var result = await RunAsync(request, cancellationToken);

            foreach (var step in result.Steps)
            {
                yield return new WorkflowRunEvent
                {
                    Type = WorkflowRunEventType.StepStarted,
                    RunId = runId,
                    StepId = step.StepId,
                    ElapsedMilliseconds = runStopwatch.ElapsedMilliseconds
                };

                yield return new WorkflowRunEvent
                {
                    Type = WorkflowRunEventType.StepCompleted,
                    RunId = runId,
                    StepId = step.StepId,
                    ElapsedMilliseconds = runStopwatch.ElapsedMilliseconds,
                    Message = $"Status: {step.Status}"
                };
            }

            yield return new WorkflowRunEvent
            {
                Type = WorkflowRunEventType.RunCompleted,
                RunId = runId,
                ElapsedMilliseconds = runStopwatch.ElapsedMilliseconds,
                Message = $"Final status: {result.Status}"
            };
        }

        private async Task<StepResult> ExecuteStepAsync(
            WorkflowStep step,
            WorkflowState state,
            WorkflowDefinition workflow,
            string? userInput,
            CancellationToken cancellationToken)
        {
            var stepStopwatch = Stopwatch.StartNew();
            var result = new StepResult
            {
                StepId = step.StepId,
                Status = StepStatus.Failed
            };

            try
            {
                _logger?.LogInformation("Executing step {StepId}: {Title}", step.StepId, step.Title);

                // Check if human approval is required (just flag, no UI)
                if (step.RequiresHumanApproval)
                {
                    result.Status = StepStatus.HumanApprovalRequired;
                    result.FailureReason = "Human approval required (not implemented)";
                    return result;
                }

                // Validate required state keys
                if (!ValidateRequiredState(step, state, out var missingKeys))
                {
                    result.Status = StepStatus.Failed;
                    result.FailureReason = $"Missing required state keys: {string.Join(", ", missingKeys)}";
                    result.ValidationErrors.AddRange(missingKeys.Select(k => $"Missing: {k}"));
                    return result;
                }

                // Build prompt
                var prompt = BuildStepPrompt(step, state, workflow, userInput);
                result.TokensIn = _tokenizer.Encode(prompt).Count;

                // Get budgets
                var maxTokens = step.Budgets?.MaxOutputTokens
                    ?? workflow.Budgets.MaxStepTokens;

                var temperature = workflow.RunnerOptions.Temperature;
                var topK = workflow.RunnerOptions.TopK;
                var seed = workflow.RunnerOptions.Deterministic
                    ? workflow.RunnerOptions.Seed ?? 42
                    : (int?)null;

                // Execute with retry
                var maxAttempts = step.Retry.MaxAttempts;
                string? output = null;
                List<string>? validationErrors = null;
                int attempt = 0;

                for (attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    // Check cancellation
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check step duration budget
                    if (step.Budgets?.MaxStepDuration.HasValue == true &&
                        stepStopwatch.Elapsed > step.Budgets.MaxStepDuration.Value)
                    {
                        result.Status = StepStatus.Failed;
                        result.FailureReason = "Step exceeded duration budget";
                        break;
                    }

                    // Generate output
                    var attemptPrompt = attempt == 1 ? prompt :
                        prompt + "\n\n" + _validator.GenerateRepairPrompt(step.OutputSpec, validationErrors!);

                    output = await Task.Run(() =>
                    {
                        var sampling = new Sampling(_model, _tokenizer, _blockSize);
                        return sampling.Generate(
                            attemptPrompt,
                            maxTokens,
                            temperature,
                            topK,
                            seed: step.Retry.UseSameSeed && seed.HasValue ? seed.Value + attempt - 1 : null,
                            showPerf: false,
                            isPerfJsonMode: false);
                    }, cancellationToken);

                    // Extract just the generated part (remove prompt)
                    if (output.StartsWith(attemptPrompt))
                    {
                        output = output.Substring(attemptPrompt.Length);
                    }

                    output = output.Trim();
                    result.TokensOut = _tokenizer.Encode(output).Count;

                    // Validate output
                    var (isValid, errors, repairedOutput) = _validator.Validate(output, step.OutputSpec);

                    if (isValid)
                    {
                        result.Status = StepStatus.Success;
                        result.OutputText = repairedOutput ?? output;
                        
                        if (repairedOutput != null)
                        {
                            _logger?.LogInformation("Step {StepId} output was repaired", step.StepId);
                        }
                        
                        break;
                    }

                    validationErrors = errors;
                    result.ValidationErrors = errors;

                    if (attempt < maxAttempts)
                    {
                        _logger?.LogWarning("Step {StepId} attempt {Attempt} failed validation: {Errors}",
                            step.StepId, attempt, string.Join("; ", errors));
                    }
                }

                result.Attempts = attempt;

                // If still not valid and strict mode, fail
                if (result.Status != StepStatus.Success)
                {
                    if (step.OutputSpec.Strict)
                    {
                        result.Status = StepStatus.Failed;
                        result.FailureReason = $"Output validation failed after {attempt} attempts";
                        result.OutputText = output ?? string.Empty;
                    }
                    else
                    {
                        // Non-strict: accept with warnings
                        result.Status = StepStatus.Success;
                        result.OutputText = output ?? string.Empty;
                        _logger?.LogWarning("Step {StepId} accepted with validation warnings (non-strict mode)",
                            step.StepId);
                    }
                }

                // Parse JSON if applicable
                if (result.Status == StepStatus.Success &&
                    step.OutputSpec.Format == OutputFormat.JsonOnly)
                {
                    try
                    {
                        result.OutputObject = JsonSerializer.Deserialize<object>(result.OutputText);
                    }
                    catch (JsonException)
                    {
                        // Already validated, but parsing to object failed - keep as text
                        _logger?.LogWarning("Step {StepId} produced valid JSON but failed to deserialize to object",
                            step.StepId);
                    }
                }

                // Store step output in state
                if (result.Status == StepStatus.Success)
                {
                    state.SetStepOutput(step.StepId, result.OutputText);
                    
                    // Also store as state variable with step name
                    state.Set($"step_{step.StepId}", result.OutputText);
                    if (result.OutputObject != null)
                    {
                        state.Set($"step_{step.StepId}_object", result.OutputObject);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw to be handled at workflow level
            }
            catch (Exception ex)
            {
                result.Status = StepStatus.Failed;
                result.FailureReason = $"Exception: {ex.Message}";
                _logger?.LogError(ex, "Step {StepId} failed with exception", step.StepId);
            }
            finally
            {
                stepStopwatch.Stop();
                result.Duration = stepStopwatch.Elapsed;
            }

            _logger?.LogInformation("Step {StepId} completed with status {Status} in {Duration}ms",
                step.StepId, result.Status, result.Duration.TotalMilliseconds);

            return result;
        }

        private bool ValidateRequiredState(WorkflowStep step, WorkflowState state, out List<string> missingKeys)
        {
            missingKeys = new List<string>();

            foreach (var key in step.InputSpec.RequiredStateKeys)
            {
                if (!state.ContainsKey(key))
                {
                    missingKeys.Add(key);
                }
            }

            return missingKeys.Count == 0;
        }

        private string BuildStepPrompt(
            WorkflowStep step,
            WorkflowState state,
            WorkflowDefinition workflow,
            string? userInput)
        {
            var sb = new StringBuilder();

            // System header
            sb.AppendLine($"=== WORKFLOW: {workflow.Name} v{workflow.Version} ===");
            sb.AppendLine($"STEP: {step.Title}");
            sb.AppendLine();

            // Output format rules
            sb.AppendLine("OUTPUT RULES:");
            switch (step.OutputSpec.Format)
            {
                case OutputFormat.JsonOnly:
                    sb.AppendLine("- Return ONLY valid JSON");
                    sb.AppendLine("- Do NOT include any extra text, explanations, or commentary");
                    if (step.OutputSpec.RequiredJsonFields != null && step.OutputSpec.RequiredJsonFields.Count > 0)
                    {
                        sb.AppendLine($"- Required fields: {string.Join(", ", step.OutputSpec.RequiredJsonFields)}");
                    }
                    if (!string.IsNullOrEmpty(step.OutputSpec.JsonTemplate))
                    {
                        sb.AppendLine($"- Example format:\n{step.OutputSpec.JsonTemplate}");
                    }
                    break;

                case OutputFormat.EnumOnly:
                    sb.AppendLine($"- Return ONLY one of: {string.Join(", ", step.OutputSpec.AllowedValues ?? new List<string>())}");
                    sb.AppendLine("- Do NOT include any extra text");
                    break;

                case OutputFormat.RegexConstrained:
                    sb.AppendLine($"- Output must match pattern: {step.OutputSpec.Regex}");
                    break;

                case OutputFormat.PlainText:
                    sb.AppendLine("- Provide a clear, concise response");
                    break;
            }

            sb.AppendLine($"- Maximum output length: {step.OutputSpec.MaxOutputChars} characters");
            sb.AppendLine();

            // Step instruction
            sb.AppendLine("INSTRUCTION:");
            sb.AppendLine(step.Instruction);
            sb.AppendLine();

            // Context from state
            var contextChars = 0;
            var maxContext = step.InputSpec.MaxContextChars;

            // Include required and optional state keys
            var allKeys = step.InputSpec.RequiredStateKeys
                .Concat(step.InputSpec.OptionalStateKeys)
                .Distinct()
                .ToList();

            if (allKeys.Count > 0)
            {
                sb.AppendLine("CONTEXT:");
                foreach (var key in allKeys)
                {
                    if (state.ContainsKey(key))
                    {
                        var value = state.GetString(key) ?? "";
                        var entry = $"{key}: {value}\n";
                        
                        if (contextChars + entry.Length > maxContext)
                        {
                            break; // Don't exceed context budget
                        }

                        sb.Append(entry);
                        contextChars += entry.Length;
                    }
                }
                sb.AppendLine();
            }

            // Include prior step outputs if enabled
            if (step.InputSpec.IncludePriorStepOutputs)
            {
                var priorOutputs = state.GetAllStepOutputs();
                if (priorOutputs.Count > 0)
                {
                    sb.AppendLine("PREVIOUS STEP OUTPUTS:");
                    foreach (var kvp in priorOutputs)
                    {
                        var entry = $"{kvp.Key}: {kvp.Value}\n";
                        
                        if (contextChars + entry.Length > maxContext)
                        {
                            break;
                        }

                        sb.Append(entry);
                        contextChars += entry.Length;
                    }
                    sb.AppendLine();
                }
            }

            // User input if allowed
            if (step.InputSpec.AllowUserNotes && !string.IsNullOrEmpty(userInput))
            {
                sb.AppendLine("USER INPUT:");
                sb.AppendLine(userInput);
                sb.AppendLine();
            }

            sb.AppendLine("YOUR RESPONSE:");

            return sb.ToString();
        }
    }
}

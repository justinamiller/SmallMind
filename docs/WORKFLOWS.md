# SmallMind Workflows

Workflow-Aware Generation enables SmallMind to execute multi-step, deterministic, schema-safe AI workflows, producing machine-consumable outputs instead of free-form chat responses.

## Overview

The `SmallMind.Workflows` namespace provides a first-class workflow engine that:

- **Executes defined sequences of steps** with stateful context
- **Produces structured outputs** (JSON, enums, regex-constrained)
- **Enforces step-specific constraints** (token budgets, validators, output formats)
- **Supports deterministic execution** with seed control
- **Validates and repairs outputs** automatically
- **Provides observability** through logging and metrics
- **Maintains clean architecture** with no external dependencies

## Key Concepts

### Workflow Definition

A `WorkflowDefinition` describes the complete workflow:

```csharp
var workflow = new WorkflowDefinition
{
    Name = "IT Ticket Triage",
    Version = "1.0",
    Steps = new List<WorkflowStep> { /* ... */ },
    Budgets = new WorkflowBudgets { MaxTotalTokens = 2000 },
    RunnerOptions = new WorkflowRunnerOptions 
    { 
        Deterministic = true,
        Seed = 42
    }
};
```

### Workflow Steps

Each `WorkflowStep` defines:
- **Input requirements** (required/optional state keys)
- **Output format** (JSON, Enum, Regex, or PlainText)
- **Validation rules** (required fields, allowed values, patterns)
- **Budgets and retry policies**

### Workflow State

`WorkflowState` is a key-value store that:
- Holds context data across steps
- Stores step outputs automatically
- Maintains workflow metadata (run ID, timestamps)

### Output Formats

Four output formats are supported:

1. **JsonOnly** (recommended): Structured JSON with field validation
2. **EnumOnly**: Single value from allowed list
3. **RegexConstrained**: Pattern-matched text
4. **PlainText**: Free-form text (discouraged)

## Quick Start

### 1. Define a Workflow

```csharp
using SmallMind.Workflows;

var workflow = new WorkflowDefinition
{
    Name = "Classification Workflow",
    Version = "1.0",
    RunnerOptions = new WorkflowRunnerOptions
    {
        Deterministic = true,
        Seed = 42,
        Temperature = 0.3
    },
    Steps = new List<WorkflowStep>
    {
        new WorkflowStep
        {
            StepId = "classify",
            Title = "Classify Input",
            Instruction = "Classify the input text into one of the categories.",
            InputSpec = new StepInputSpec
            {
                RequiredStateKeys = new List<string> { "input_text" }
            },
            OutputSpec = new StepOutputSpec
            {
                Format = OutputFormat.EnumOnly,
                AllowedValues = new List<string> { "positive", "negative", "neutral" },
                Strict = true
            }
        }
    }
};
```

### 2. Create Initial State

```csharp
var state = new WorkflowState();
state.Set("input_text", "This product is amazing!");
```

### 3. Execute Workflow

```csharp
var runner = new WorkflowRunner(model, tokenizer, blockSize);
var request = new WorkflowRunRequest
{
    Workflow = workflow,
    InitialState = state
};

var result = await runner.RunAsync(request);

if (result.Status == WorkflowRunStatus.Success)
{
    var classification = result.FinalState.GetStepOutput("classify");
    Console.WriteLine($"Classification: {classification}");
}
```

## Examples

### IT Ticket Triage Workflow

See `samples/Workflows/ItTicketTriageWorkflow.cs` for a complete example that:
- Classifies ticket type (incident/request/problem)
- Determines severity (low/medium/high/critical)
- Assigns to support group
- Recommends next action with JSON output

```csharp
var workflow = ItTicketTriageWorkflow.CreateWorkflow();
var state = ItTicketTriageWorkflow.CreateSampleState();
// Run workflow...
```

### Policy Decision Workflow

See `samples/Workflows/PolicyDecisionWorkflow.cs` for an example that:
- Extracts relevant policy clause (JSON)
- Determines compliance status (enum)
- Generates decision record with justification (JSON)

## Output Validation and Repair

The workflow engine automatically validates outputs and attempts repairs:

### JSON Validation

```csharp
new StepOutputSpec
{
    Format = OutputFormat.JsonOnly,
    RequiredJsonFields = new List<string> { "name", "value" },
    JsonTemplate = @"{""name"": ""example"", ""value"": 123}",
    Strict = true
}
```

- Validates JSON syntax
- Checks for required fields
- Attempts to extract JSON from chatty output
- Retries with repair prompt if invalid

### Enum Validation

```csharp
new StepOutputSpec
{
    Format = OutputFormat.EnumOnly,
    AllowedValues = new List<string> { "yes", "no", "maybe" },
    Strict = true
}
```

- Trims whitespace
- Case-sensitive matching (by default)
- Optional case-insensitive repair in non-strict mode

### Regex Validation

```csharp
new StepOutputSpec
{
    Format = OutputFormat.RegexConstrained,
    Regex = @"^[A-Z]{3}-\d{5}$",
    Strict = true
}
```

- Validates against pattern
- Extracts matching portion if embedded in text

## Deterministic Execution

For reproducible results:

```csharp
var workflow = new WorkflowDefinition
{
    RunnerOptions = new WorkflowRunnerOptions
    {
        Deterministic = true,
        Seed = 42,
        Temperature = 0.0  // Very low for maximum consistency
    }
};
```

With the same seed and temperature:
- Same inputs → same outputs
- Retries use incremented seed (by default)

## Budget Enforcement

Control resource usage at workflow and step levels:

```csharp
var workflow = new WorkflowDefinition
{
    Budgets = new WorkflowBudgets
    {
        MaxTotalTokens = 5000,
        MaxStepTokens = 1000,
        MaxDuration = TimeSpan.FromMinutes(5)
    }
};

var step = new WorkflowStep
{
    Budgets = new StepBudgets
    {
        MaxOutputTokens = 200,
        MaxStepDuration = TimeSpan.FromSeconds(30)
    }
};
```

Workflow terminates with `RejectedPolicy` status if budgets are exceeded.

## Retry Policies

Configure retry behavior per step:

```csharp
new WorkflowStep
{
    Retry = new StepRetryPolicy
    {
        MaxAttempts = 3,
        UseSameSeed = true  // For deterministic retries
    }
}
```

## Streaming Support

Use `RunStreamAsync` for real-time events:

```csharp
await foreach (var evt in runner.RunStreamAsync(request, cancellationToken))
{
    switch (evt.Type)
    {
        case WorkflowRunEventType.RunStarted:
            Console.WriteLine("Workflow started");
            break;
        case WorkflowRunEventType.StepStarted:
            Console.WriteLine($"Step {evt.StepId} started");
            break;
        // ...
    }
}
```

## Advanced Features

### Context Management

Control what context is available to each step:

```csharp
new StepInputSpec
{
    RequiredStateKeys = new List<string> { "user_id", "request" },
    OptionalStateKeys = new List<string> { "preferences" },
    MaxContextChars = 2000,
    IncludePriorStepOutputs = true,
    AllowUserNotes = false
}
```

### Human Approval Gates

Flag steps that require human approval (flag only, no UI implementation):

```csharp
new WorkflowStep
{
    RequiresHumanApproval = true
}
```

Step returns `HumanApprovalRequired` status.

### Stop on Failure

Control whether workflow continues after step failures:

```csharp
new WorkflowRunnerOptions
{
    StopOnFailure = true  // Stop immediately on first failure
}
```

## Architecture

### No External Dependencies

Workflows use only:
- Built-in SmallMind `TransformerModel` and `Tokenizer`
- Standard .NET libraries (`System.Text.Json`, `System.Text.RegularExpressions`)
- Optional `Microsoft.Extensions.Logging` for diagnostics

### Integration Points

- **`WorkflowRunner`**: Main execution engine
- **`OutputValidator`**: Validates and repairs outputs
- **`Sampling`**: Existing text generation (reused)
- **`TransformerModel`**: Core LLM (reused)

### Performance Considerations

- Reuses existing `Sampling` class (optimized for CPU)
- Minimal allocations in validation logic
- ArrayPool for temporary buffers where applicable
- Efficient state management with dictionaries

## Testing

Comprehensive test coverage includes:

- **OutputValidatorTests**: 30+ tests for validation/repair
- **WorkflowStateTests**: State management tests
- **WorkflowDefinitionTests**: Configuration tests

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~Workflows"
```

## Best Practices

1. **Use JSON output** for structured data extraction
2. **Use Enum output** for classification tasks
3. **Keep steps focused** - one decision per step
4. **Set appropriate budgets** to prevent runaway generation
5. **Enable deterministic mode** for reproducible workflows
6. **Use lower temperature** (0.0-0.3) for consistency
7. **Validate required state keys** before execution
8. **Monitor step durations** and adjust budgets

## Limitations

- No GPU acceleration (CPU-only like base SmallMind)
- Character-level tokenization (small vocabulary)
- No beam search or advanced decoding strategies
- Simple retry logic (fixed attempts)
- Streaming events are simplified (not per-token yet)

## Future Enhancements

Potential improvements:
- Per-token streaming in RunStreamAsync
- Conditional branching between steps
- Parallel step execution
- Custom validators beyond JSON/Enum/Regex
- Workflow templates and composition
- Persistent workflow state storage

## API Reference

### Core Classes

- **`WorkflowDefinition`**: Workflow configuration
- **`WorkflowStep`**: Individual step configuration
- **`WorkflowState`**: Stateful context container
- **`WorkflowRunner`**: Execution engine
- **`IWorkflowRunner`**: Runner interface
- **`OutputValidator`**: Validation and repair logic

### Result Classes

- **`WorkflowRunResult`**: Complete workflow result
- **`StepResult`**: Individual step result
- **`WorkflowRunEvent`**: Streaming event

### Configuration Classes

- **`StepInputSpec`**: Input requirements
- **`StepOutputSpec`**: Output format and validation
- **`WorkflowBudgets`**: Resource limits
- **`StepBudgets`**: Per-step resource limits
- **`WorkflowRunnerOptions`**: Execution options
- **`StepRetryPolicy`**: Retry configuration

### Enums

- **`OutputFormat`**: JsonOnly, EnumOnly, RegexConstrained, PlainText
- **`WorkflowRunStatus`**: Success, Failed, Cancelled, RejectedPolicy
- **`StepStatus`**: Success, Failed, Skipped, HumanApprovalRequired

## License

Same as SmallMind (MIT)

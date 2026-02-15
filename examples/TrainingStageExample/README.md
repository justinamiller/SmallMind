# Training Stage Verification Example

This document demonstrates the training stage verification and progression functionality added to SmallMind.

## Overview

The training stage verification feature allows you to:
- Track the current training stage (Initialized, Pretraining, FineTuning, Validation, Completed)
- Monitor progress within each stage
- Verify completion and determine the next stage
- Save and load checkpoints with stage information

## Training Stages

SmallMind supports the following training stages:

1. **Initialized** - Model is created but not trained
2. **Pretraining** - Learning basic language patterns
3. **FineTuning** - Adapting to specific tasks
4. **Validation** - Evaluating model performance
5. **Completed** - Training finished successfully

## API Reference

### Properties

- `StageInfo` - Get information about the current training stage

### Methods

#### `SetTrainingStage(TrainingStage stage, int totalSteps = 0)`
Set the current training stage and optionally specify total steps planned.

```csharp
training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 100);
```

#### `UpdateStageProgress(int stepsCompleted)`
Update the number of steps completed in the current stage.

```csharp
training.UpdateStageProgress(50);
```

#### `VerifyCurrentStage()`
Get a formatted report of the current training stage status.

```csharp
string report = training.VerifyCurrentStage();
Console.WriteLine(report);
```

Output example:
```
=== Training Stage Information ===
Current Stage: Pretraining
Steps Completed: 50
Total Steps Planned: 100
Progress: 50.00%
Stage Started: 2026-02-15 02:30:00 UTC
Time in Stage: 5.23 minutes
Stage Status: IN PROGRESS
Remaining Steps: 50
```

#### `HasNextStage()`
Check if there is a next training stage to proceed to.

```csharp
if (training.HasNextStage())
{
    // Can advance to next stage
}
```

#### `GetNextStage()`
Get the next training stage, or null if training is complete.

```csharp
TrainingStage? nextStage = training.GetNextStage();
```

#### `AdvanceToNextStage(int nextStageTotalSteps = 0)`
Advance to the next training stage if current stage is complete.

```csharp
if (training.AdvanceToNextStage(nextStageTotalSteps: 50))
{
    Console.WriteLine($"Advanced to: {training.StageInfo.CurrentStage}");
}
```

#### `SaveCheckpointWithStage(string path)`
Save checkpoint with training stage information included in metadata.

```csharp
training.SaveCheckpointWithStage("checkpoint.smnd");
```

#### `LoadCheckpointWithStage(string path)`
Load checkpoint and restore training stage information from metadata.

```csharp
training.LoadCheckpointWithStage("checkpoint.smnd");
```

## Usage Examples

### Example 1: Basic Stage Tracking

```csharp
// Initialize training
var training = new Training(model, tokenizer, trainingText, 
                           blockSize: 32, batchSize: 4, seed: 42);

// Set pretraining stage
training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 1000);

// During training loop
for (int step = 0; step < 1000; step++)
{
    // ... perform training step ...
    
    training.UpdateStageProgress(step + 1);
    
    if ((step + 1) % 100 == 0)
    {
        Console.WriteLine(training.VerifyCurrentStage());
    }
}

// Check if stage is complete and advance
if (training.StageInfo.IsStageComplete())
{
    training.AdvanceToNextStage(nextStageTotalSteps: 500);
}
```

### Example 2: Multi-Stage Training Pipeline

```csharp
// Stage 1: Pretraining
training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 1000);
// ... run 1000 pretraining steps ...
training.UpdateStageProgress(1000);

if (training.AdvanceToNextStage(nextStageTotalSteps: 500))
{
    // Stage 2: Fine-tuning
    Console.WriteLine($"Starting {training.StageInfo.CurrentStage}");
    // ... run 500 fine-tuning steps ...
    training.UpdateStageProgress(500);
}

if (training.AdvanceToNextStage(nextStageTotalSteps: 100))
{
    // Stage 3: Validation
    Console.WriteLine($"Starting {training.StageInfo.CurrentStage}");
    // ... run validation ...
    training.UpdateStageProgress(100);
}

training.AdvanceToNextStage(); // Move to Completed
Console.WriteLine("Training complete!");
```

### Example 3: Checkpoint with Stage Info

```csharp
// Save checkpoint with stage information
training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 1000);
training.UpdateStageProgress(500);
training.SaveCheckpointWithStage("checkpoint_step500.smnd");

// Later, resume training
var training2 = new Training(model, tokenizer, trainingText, 
                            blockSize: 32, batchSize: 4, seed: 42);
training2.LoadCheckpointWithStage("checkpoint_step500.smnd");

// Verify loaded stage
Console.WriteLine(training2.VerifyCurrentStage());
// Shows: Current Stage: Pretraining, Steps Completed: 500, Total: 1000
```

### Example 4: Progress Monitoring

```csharp
training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 1000);

// During training
for (int step = 0; step < 1000; step++)
{
    // ... training step ...
    training.UpdateStageProgress(step + 1);
    
    // Monitor progress
    double progress = training.StageInfo.GetStageProgressPercentage();
    if (progress >= 25.0 && progress < 26.0)
    {
        Console.WriteLine("25% complete!");
    }
}
```

## Integration with Existing Training

The stage tracking integrates seamlessly with existing training methods:

```csharp
// Set stage before training
training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 1000);

// Use existing TrainEnhanced method
training.TrainEnhanced(
    steps: 1000,
    learningRate: 0.001,
    logEvery: 100,
    saveEvery: 500,
    checkpointDir: "./checkpoints",
    showPerf: true
);

// Update progress after training
training.UpdateStageProgress(1000);

// Verify and advance
Console.WriteLine(training.VerifyCurrentStage());
if (training.HasNextStage())
{
    training.AdvanceToNextStage();
}
```

## Stage Information Properties

The `TrainingStageInfo` class provides:

- `CurrentStage` - Current training stage
- `StepsCompleted` - Steps completed in current stage
- `TotalStepsPlanned` - Total steps planned for stage
- `StageStartedAt` - Timestamp when stage started
- `Metadata` - Dictionary for custom stage metadata

Helper methods:
- `IsStageComplete()` - Check if stage is complete
- `GetNextStage()` - Get next stage in progression
- `GetStageProgressPercentage()` - Get progress as percentage (0-100)

## Benefits

1. **Visibility** - Know exactly where you are in the training process
2. **Reproducibility** - Save and restore training state with stage information
3. **Automation** - Automate multi-stage training pipelines
4. **Monitoring** - Track progress and time spent in each stage
5. **Debugging** - Quickly identify which stage has issues

## Notes

- The Training class is marked as `[Obsolete]` and experimental - use at your own risk
- Stage progression is customizable through the `GetNextStage()` logic
- Checkpoints with stage info are backward compatible (extra metadata is optional)
- Stage tracking adds minimal overhead to training

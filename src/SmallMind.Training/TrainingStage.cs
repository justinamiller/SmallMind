namespace SmallMind.Training
{
    /// <summary>
    /// Represents different stages of model training/bootstrap process.
    /// </summary>
    public enum TrainingStage
    {
        /// <summary>
        /// Initial stage - model is created but not trained
        /// </summary>
        Initialized = 0,

        /// <summary>
        /// Pre-training stage - learning basic language patterns
        /// </summary>
        Pretraining = 1,

        /// <summary>
        /// Fine-tuning stage - adapting to specific tasks
        /// </summary>
        FineTuning = 2,

        /// <summary>
        /// Validation stage - evaluating model performance
        /// </summary>
        Validation = 3,

        /// <summary>
        /// Completed stage - training finished successfully
        /// </summary>
        Completed = 4
    }

    /// <summary>
    /// Metadata about the current training stage.
    /// </summary>
    public sealed class TrainingStageInfo
    {
        /// <summary>
        /// Current stage of training
        /// </summary>
        public TrainingStage CurrentStage { get; set; }

        /// <summary>
        /// Total number of steps completed in current stage
        /// </summary>
        public int StepsCompleted { get; set; }

        /// <summary>
        /// Total number of steps planned for current stage (0 if unknown)
        /// </summary>
        public int TotalStepsPlanned { get; set; }

        /// <summary>
        /// Timestamp when this stage was started
        /// </summary>
        public DateTime StageStartedAt { get; set; }

        /// <summary>
        /// Additional stage-specific metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Check if the current stage is complete
        /// </summary>
        public bool IsStageComplete()
        {
            if (CurrentStage == TrainingStage.Completed)
                return true;

            if (TotalStepsPlanned > 0 && StepsCompleted >= TotalStepsPlanned)
                return true;

            return false;
        }

        /// <summary>
        /// Get the next training stage, or null if already completed
        /// </summary>
        public TrainingStage? GetNextStage()
        {
            if (CurrentStage == TrainingStage.Completed)
                return null;

            // Default progression: Initialized -> Pretraining -> FineTuning -> Validation -> Completed
            return CurrentStage switch
            {
                TrainingStage.Initialized => TrainingStage.Pretraining,
                TrainingStage.Pretraining => TrainingStage.FineTuning,
                TrainingStage.FineTuning => TrainingStage.Validation,
                TrainingStage.Validation => TrainingStage.Completed,
                _ => null
            };
        }

        /// <summary>
        /// Calculate progress percentage for current stage (0-100)
        /// </summary>
        public double GetStageProgressPercentage()
        {
            if (TotalStepsPlanned <= 0)
                return 0.0;

            return Math.Min(100.0, (StepsCompleted * 100.0) / TotalStepsPlanned);
        }
    }
}

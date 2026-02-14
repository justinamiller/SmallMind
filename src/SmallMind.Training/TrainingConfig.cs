using SmallMind.Core.Core;

namespace SmallMind.Training
{
    /// <summary>
    /// Training configuration options for Phase 2 optimizations
    /// </summary>
    internal class TrainingConfig
    {
        public bool UseMixedPrecision { get; set; } = false;
        public bool UseGradientCheckpointing { get; set; } = false;
        public CheckpointStrategy CheckpointStrategy { get; set; } = CheckpointStrategy.SqrtLayers;
        public bool EnableDiagnostics { get; set; } = false;
        public bool CheckGradientHealth { get; set; } = false;
        public int DiagnosticInterval { get; set; } = 100;
        public bool TrackModelMetrics { get; set; } = true;
        public bool ComputeTokenAccuracy { get; set; } = false;
    }
}

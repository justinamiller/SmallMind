using System;
using System.Diagnostics.Metrics;

namespace SmallMind.Telemetry
{
    /// <summary>
    /// Provides OpenTelemetry-compatible metrics for SmallMind operations.
    /// </summary>
    public sealed class SmallMindMetrics : IDisposable
    {
        private readonly Meter _meter;
        private bool _disposed;

        // Training metrics
        private readonly Counter<long> _trainingStepsCounter;
        private readonly Histogram<double> _trainingStepDuration;
        private readonly Histogram<double> _trainingLoss;
        private readonly Histogram<long> _tokensProcessed;
        private readonly Histogram<double> _tokensPerSecond;
        private readonly Counter<long> _trainingSessions;
        
        // Inference metrics
        private readonly Counter<long> _tokensGenerated;
        private readonly Histogram<double> _generationDuration;
        private readonly Histogram<double> _generationTokensPerSecond;
        private readonly Counter<long> _inferenceSessions;
        
        // Resource metrics
        private readonly Histogram<long> _tensorAllocations;
        private readonly Histogram<long> _tensorPoolRents;
        private readonly Histogram<long> _tensorPoolReturns;
        private readonly UpDownCounter<long> _activeSessions;
        
        /// <summary>
        /// Gets the singleton instance of SmallMindMetrics.
        /// </summary>
        public static SmallMindMetrics Instance { get; } = new SmallMindMetrics();
        
        private SmallMindMetrics()
        {
            _meter = new Meter("SmallMind", "1.0.0");
            
            // Training metrics
            _trainingStepsCounter = _meter.CreateCounter<long>(
                "smallmind.training.steps",
                unit: "{step}",
                description: "Total number of training steps completed");
                
            _trainingStepDuration = _meter.CreateHistogram<double>(
                "smallmind.training.step.duration",
                unit: "ms",
                description: "Duration of a single training step");
                
            _trainingLoss = _meter.CreateHistogram<double>(
                "smallmind.training.loss",
                description: "Training loss value");
                
            _tokensProcessed = _meter.CreateHistogram<long>(
                "smallmind.training.tokens.processed",
                unit: "{token}",
                description: "Number of tokens processed in a training step");
                
            _tokensPerSecond = _meter.CreateHistogram<double>(
                "smallmind.training.tokens_per_second",
                unit: "{token}/s",
                description: "Training tokens processed per second");
                
            _trainingSessions = _meter.CreateCounter<long>(
                "smallmind.training.sessions",
                unit: "{session}",
                description: "Total number of training sessions started");
            
            // Inference metrics
            _tokensGenerated = _meter.CreateCounter<long>(
                "smallmind.inference.tokens.generated",
                unit: "{token}",
                description: "Total number of tokens generated");
                
            _generationDuration = _meter.CreateHistogram<double>(
                "smallmind.inference.duration",
                unit: "ms",
                description: "Duration of text generation");
                
            _generationTokensPerSecond = _meter.CreateHistogram<double>(
                "smallmind.inference.tokens_per_second",
                unit: "{token}/s",
                description: "Inference tokens generated per second");
                
            _inferenceSessions = _meter.CreateCounter<long>(
                "smallmind.inference.sessions",
                unit: "{session}",
                description: "Total number of inference sessions started");
            
            // Resource metrics
            _tensorAllocations = _meter.CreateHistogram<long>(
                "smallmind.tensor.allocations",
                unit: "{allocation}",
                description: "Tensor memory allocations");
                
            _tensorPoolRents = _meter.CreateHistogram<long>(
                "smallmind.tensor_pool.rents",
                unit: "{rent}",
                description: "Tensor pool rent operations");
                
            _tensorPoolReturns = _meter.CreateHistogram<long>(
                "smallmind.tensor_pool.returns",
                unit: "{return}",
                description: "Tensor pool return operations");
                
            _activeSessions = _meter.CreateUpDownCounter<long>(
                "smallmind.sessions.active",
                unit: "{session}",
                description: "Number of active training/inference sessions");
        }
        
        /// <summary>
        /// Records a completed training step with its metrics.
        /// </summary>
        public void RecordTrainingStep(double durationMs, float loss, long tokensProcessed, double tokensPerSecond)
        {
            _trainingStepsCounter.Add(1);
            _trainingStepDuration.Record(durationMs);
            _trainingLoss.Record(loss);
            _tokensProcessed.Record(tokensProcessed);
            _tokensPerSecond.Record(tokensPerSecond);
        }
        
        /// <summary>
        /// Records the start of a training session.
        /// </summary>
        public void RecordTrainingSessionStart()
        {
            _trainingSessions.Add(1);
            _activeSessions.Add(1);
        }
        
        /// <summary>
        /// Records the end of a training session.
        /// </summary>
        public void RecordTrainingSessionEnd()
        {
            _activeSessions.Add(-1);
        }
        
        /// <summary>
        /// Records tokens generated during inference.
        /// </summary>
        public void RecordTokensGenerated(long count, double durationMs)
        {
            _tokensGenerated.Add(count);
            _generationDuration.Record(durationMs);
            if (durationMs > 0)
            {
                _generationTokensPerSecond.Record(count / (durationMs / 1000.0));
            }
        }
        
        /// <summary>
        /// Records the start of an inference session.
        /// </summary>
        public void RecordInferenceSessionStart()
        {
            _inferenceSessions.Add(1);
            _activeSessions.Add(1);
        }
        
        /// <summary>
        /// Records the end of an inference session.
        /// </summary>
        public void RecordInferenceSessionEnd()
        {
            _activeSessions.Add(-1);
        }
        
        /// <summary>
        /// Records a tensor memory allocation.
        /// </summary>
        public void RecordTensorAllocation(long size)
        {
            _tensorAllocations.Record(size);
        }
        
        /// <summary>
        /// Records a tensor pool rent operation.
        /// </summary>
        public void RecordTensorPoolRent(long size)
        {
            _tensorPoolRents.Record(size);
        }
        
        /// <summary>
        /// Records a tensor pool return operation.
        /// </summary>
        public void RecordTensorPoolReturn(long size)
        {
            _tensorPoolReturns.Record(size);
        }
        
        /// <summary>
        /// Disposes the metrics meter.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _meter?.Dispose();
            _disposed = true;
        }
    }
}

using System;
using System.Collections.Generic;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Gradient checkpointing strategy selection
    /// </summary>
    public enum CheckpointStrategy
    {
        None,           // Store all: fastest, highest memory
        EveryLayer,     // Checkpoint all: slowest, lowest memory  
        SqrtLayers,     // Checkpoint √N layers: balanced
        Custom          // User-defined intervals
    }
    
    /// <summary>
    /// Gradient checkpointing utilities for memory-efficient training.
    /// Trades compute for memory by recomputing activations during backward pass.
    /// </summary>
    public static class GradientCheckpointing
    {
        /// <summary>
        /// Calculate optimal checkpoint interval based on available memory
        /// </summary>
        public static int GetOptimalCheckpointInterval(
            int numLayers, 
            long availableMemoryBytes, 
            long perLayerBytes,
            CheckpointStrategy strategy = CheckpointStrategy.SqrtLayers)
        {
            if (strategy == CheckpointStrategy.None)
            {
                return numLayers + 1;  // Never checkpoint
            }
            
            if (strategy == CheckpointStrategy.EveryLayer)
            {
                return 1;  // Checkpoint every layer
            }
            
            // Check if we have enough memory for no checkpointing
            long totalRequired = numLayers * perLayerBytes;
            if (totalRequired <= availableMemoryBytes && strategy != CheckpointStrategy.EveryLayer)
            {
                return numLayers + 1;  // Never checkpoint
            }
            
            if (strategy == CheckpointStrategy.SqrtLayers)
            {
                // Sqrt strategy: checkpoint every √N layers
                // Memory: O(√N), Recompute: O(√N) per layer
                int sqrtInterval = (int)Math.Ceiling(Math.Sqrt(numLayers));
                
                // Check if sqrt strategy fits
                long sqrtMemory = sqrtInterval * perLayerBytes;
                if (sqrtMemory <= availableMemoryBytes)
                {
                    return sqrtInterval;
                }
            }
            
            // Fall back to checkpoint every layer
            return 1;
        }
        
        /// <summary>
        /// Estimate memory savings from checkpointing
        /// </summary>
        public static (long withoutCheckpointing, long withCheckpointing, double savingsPercent) 
            EstimateMemorySavings(int numLayers, long perLayerBytes, int checkpointInterval)
        {
            long without = numLayers * perLayerBytes;
            
            // With checkpointing: only store checkpoints + temporary recompute buffers
            int numCheckpoints = (numLayers + checkpointInterval - 1) / checkpointInterval;
            long with = numCheckpoints * perLayerBytes;
            
            double savings = without > 0 ? (1.0 - (double)with / without) * 100.0 : 0.0;
            
            return (without, with, savings);
        }
    }
    
    /// <summary>
    /// Checkpoint manager for transformer layers
    /// </summary>
    public class CheckpointManager
    {
        private readonly Dictionary<int, Tensor> _checkpoints;
        private readonly int _checkpointInterval;
        private readonly bool _enabled;
        
        public bool IsEnabled => _enabled;
        public int CheckpointInterval => _checkpointInterval;
        
        public CheckpointManager(int checkpointInterval = 2, bool enabled = true)
        {
            _checkpointInterval = checkpointInterval;
            _enabled = enabled;
            _checkpoints = new Dictionary<int, Tensor>();
        }
        
        /// <summary>
        /// Store a checkpoint for a layer
        /// </summary>
        public void SaveCheckpoint(int layerIndex, Tensor activation)
        {
            if (!_enabled) return;
            
            if (layerIndex % _checkpointInterval == 0)
            {
                _checkpoints[layerIndex] = activation.Clone();
            }
        }
        
        /// <summary>
        /// Get the nearest checkpoint for recomputation
        /// </summary>
        public Tensor? GetNearestCheckpoint(int layerIndex)
        {
            if (!_enabled) return null;
            
            int checkpointIdx = (layerIndex / _checkpointInterval) * _checkpointInterval;
            
            if (_checkpoints.TryGetValue(checkpointIdx, out var checkpoint))
            {
                return checkpoint;
            }
            
            return null;
        }
        
        /// <summary>
        /// Clear all checkpoints (after backward pass)
        /// </summary>
        public void Clear()
        {
            _checkpoints.Clear();
        }
        
        /// <summary>
        /// Get current memory usage from checkpoints
        /// </summary>
        public long GetMemoryUsageBytes()
        {
            long total = 0;
            foreach (var checkpoint in _checkpoints.Values)
            {
                total += checkpoint.Size * sizeof(float);
            }
            return total;
        }
    }
}

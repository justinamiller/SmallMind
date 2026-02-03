using SmallMind.Core.Core;
using System;
using System.Collections.Generic;

namespace SmallMind.Transformers
{
    /// <summary>
    /// Manages a workspace of reusable tensors for a forward pass.
    /// Allocates tensors on first use and reuses them for subsequent calls.
    /// This eliminates allocations for intermediate results during inference.
    /// </summary>
    public sealed class TensorWorkspace : IDisposable
    {
        private readonly Dictionary<string, Tensor> _tensors;
        private bool _disposed;
        
        public TensorWorkspace()
        {
            _tensors = new Dictionary<string, Tensor>();
        }
        
        /// <summary>
        /// Get or create a tensor with the specified key and shape.
        /// If a tensor with the same key exists and matches the shape, it is reused.
        /// Otherwise, a new tensor is allocated and stored.
        /// </summary>
        public Tensor GetOrCreate(string key, int[] shape, bool requiresGrad = false)
        {
            if (_tensors.TryGetValue(key, out var existing))
            {
                // Check if shape matches
                if (ShapeMatches(existing.Shape, shape))
                {
                    // Clear the data for reuse
                    Array.Clear(existing.Data, 0, existing.Size);
                    return existing;
                }
                
                // Shape doesn't match, remove old tensor
                _tensors.Remove(key);
            }
            
            // Create new tensor
            var tensor = new Tensor(shape, requiresGrad);
            _tensors[key] = tensor;
            return tensor;
        }
        
        /// <summary>
        /// Clear all tensors in the workspace.
        /// </summary>
        public void Clear()
        {
            _tensors.Clear();
        }
        
        private bool ShapeMatches(int[] shape1, int[] shape2)
        {
            if (shape1.Length != shape2.Length)
                return false;
            
            for (int i = 0; i < shape1.Length; i++)
            {
                if (shape1[i] != shape2[i])
                    return false;
            }
            
            return true;
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _tensors.Clear();
            _disposed = true;
        }
    }
}

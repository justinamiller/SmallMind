using System;
using System.Runtime.Intrinsics.X86;

namespace SmallMind.Health
{
    /// <summary>
    /// Represents the health status of SmallMind components.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// The component is healthy and operating normally.
        /// </summary>
        Healthy,
        
        /// <summary>
        /// The component is degraded but still operational.
        /// </summary>
        Degraded,
        
        /// <summary>
        /// The component is unhealthy and may not function correctly.
        /// </summary>
        Unhealthy
    }
    
    /// <summary>
    /// Result of a health check operation.
    /// </summary>
    public sealed class HealthCheckResult
    {
        /// <summary>
        /// Gets the health status.
        /// </summary>
        public HealthStatus Status { get; }
        
        /// <summary>
        /// Gets the description of the health status.
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Gets optional exception information if unhealthy.
        /// </summary>
        public Exception? Exception { get; }
        
        /// <summary>
        /// Gets additional data about the health check.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object> Data { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="HealthCheckResult"/> class.
        /// </summary>
        public HealthCheckResult(
            HealthStatus status, 
            string description, 
            Exception? exception = null,
            System.Collections.Generic.Dictionary<string, object>? data = null)
        {
            Status = status;
            Description = description;
            Exception = exception;
            Data = data ?? new System.Collections.Generic.Dictionary<string, object>();
        }
        
        /// <summary>
        /// Creates a healthy result.
        /// </summary>
        public static HealthCheckResult Healthy(string description) =>
            new HealthCheckResult(HealthStatus.Healthy, description);
            
        /// <summary>
        /// Creates a degraded result.
        /// </summary>
        public static HealthCheckResult Degraded(string description, System.Collections.Generic.Dictionary<string, object>? data = null) =>
            new HealthCheckResult(HealthStatus.Degraded, description, data: data);
            
        /// <summary>
        /// Creates an unhealthy result.
        /// </summary>
        public static HealthCheckResult Unhealthy(string description, Exception? exception = null) =>
            new HealthCheckResult(HealthStatus.Unhealthy, description, exception);
    }
    
    /// <summary>
    /// Provides health checks for SmallMind components.
    /// </summary>
    public sealed class SmallMindHealthCheck
    {
        /// <summary>
        /// Performs a comprehensive health check of SmallMind infrastructure.
        /// </summary>
        /// <returns>Health check result with detailed status information.</returns>
        public HealthCheckResult Check()
        {
            try
            {
                var data = new System.Collections.Generic.Dictionary<string, object>();
                
                // Check SIMD availability
                bool simdAvailable = CheckSimdSupport(data);
                
                // Check memory pressure
                var memoryStatus = CheckMemoryPressure(data);
                
                // Check tensor pool health
                var poolStatus = CheckTensorPoolHealth(data);
                
                // Determine overall status
                if (!simdAvailable || memoryStatus == HealthStatus.Unhealthy || poolStatus == HealthStatus.Unhealthy)
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, "SmallMind infrastructure is unhealthy", data: data);
                }
                
                if (memoryStatus == HealthStatus.Degraded || poolStatus == HealthStatus.Degraded)
                {
                    return new HealthCheckResult(HealthStatus.Degraded, "SmallMind infrastructure is degraded", data: data);
                }
                
                return new HealthCheckResult(HealthStatus.Healthy, "SmallMind infrastructure is healthy", data: data);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Health check failed with exception", ex);
            }
        }
        
        private bool CheckSimdSupport(System.Collections.Generic.Dictionary<string, object> data)
        {
            // Check for SIMD support
            bool avxSupported = Avx.IsSupported;
            bool avx2Supported = Avx2.IsSupported;
            bool fmaSupported = Fma.IsSupported;
            
            data["simd.avx"] = avxSupported;
            data["simd.avx2"] = avx2Supported;
            data["simd.fma"] = fmaSupported;
            data["simd.available"] = avxSupported || avx2Supported;
            
            return avxSupported || avx2Supported;
        }
        
        private HealthStatus CheckMemoryPressure(System.Collections.Generic.Dictionary<string, object> data)
        {
            var gcInfo = GC.GetGCMemoryInfo();
            long totalMemory = GC.GetTotalMemory(false);
            long heapSize = gcInfo.HeapSizeBytes;
            long fragmentedBytes = gcInfo.FragmentedBytes;
            
            data["memory.total_bytes"] = totalMemory;
            data["memory.heap_size_bytes"] = heapSize;
            data["memory.fragmented_bytes"] = fragmentedBytes;
            data["memory.gen0_collections"] = GC.CollectionCount(0);
            data["memory.gen1_collections"] = GC.CollectionCount(1);
            data["memory.gen2_collections"] = GC.CollectionCount(2);
            
            // Check if memory pressure is high (check higher thresholds first)
            if (totalMemory > 4L * 1024 * 1024 * 1024) // > 4 GB
            {
                return HealthStatus.Unhealthy;
            }
            
            if (totalMemory > 2L * 1024 * 1024 * 1024) // > 2 GB
            {
                return HealthStatus.Degraded;
            }
            
            return HealthStatus.Healthy;
        }
        
        private HealthStatus CheckTensorPoolHealth(System.Collections.Generic.Dictionary<string, object> data)
        {
            try
            {
                // Test tensor pool by renting and returning
                var testArray = Core.TensorPool.Shared.Rent(64);
                Core.TensorPool.Shared.Return(testArray);
                
                data["tensor_pool.functional"] = true;
                return HealthStatus.Healthy;
            }
            catch (Exception ex)
            {
                data["tensor_pool.functional"] = false;
                data["tensor_pool.error"] = ex.Message;
                return HealthStatus.Unhealthy;
            }
        }
    }
}

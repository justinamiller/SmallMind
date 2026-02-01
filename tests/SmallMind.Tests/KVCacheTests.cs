using Xunit;
using SmallMind.Core.Core;
using System;
using System.IO;

namespace SmallMind.Tests
{
    public class KVCacheTests : IDisposable
    {
        private readonly string _testDirectory;

        public KVCacheTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"kvcache_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void InMemoryCache_WriteAndRead_WorksCorrectly()
        {
            using var cache = new KVCache(capacity: 100);
            
            cache.Write(0, 1.5f);
            cache.Write(10, 2.5f);
            cache.Write(99, 3.5f);
            
            Assert.Equal(1.5f, cache.Read(0));
            Assert.Equal(2.5f, cache.Read(10));
            Assert.Equal(3.5f, cache.Read(99));
            Assert.False(cache.UsesMemoryMapping);
        }

        [Fact]
        public void MemoryMappedCache_WriteAndRead_WorksCorrectly()
        {
            string fileName = Path.Combine(_testDirectory, "test_cache.bin");
            
            using var cache = new KVCache(fileName, capacity: 100, useMemoryMapping: true);
            
            cache.Write(0, 1.5f);
            cache.Write(10, 2.5f);
            cache.Write(99, 3.5f);
            
            Assert.Equal(1.5f, cache.Read(0));
            Assert.Equal(2.5f, cache.Read(10));
            Assert.Equal(3.5f, cache.Read(99));
            Assert.True(cache.UsesMemoryMapping);
        }

        [Fact]
        public void WriteArray_InMemory_WorksCorrectly()
        {
            using var cache = new KVCache(capacity: 100);
            
            var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
            cache.WriteArray(10, data);
            
            var result = new float[5];
            cache.ReadArray(10, 5, result);
            
            Assert.Equal(data, result);
        }

        [Fact]
        public void WriteArray_MemoryMapped_WorksCorrectly()
        {
            string fileName = Path.Combine(_testDirectory, "array_cache.bin");
            
            using var cache = new KVCache(fileName, capacity: 100, useMemoryMapping: true);
            
            var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
            cache.WriteArray(10, data);
            
            var result = new float[5];
            cache.ReadArray(10, 5, result);
            
            Assert.Equal(data, result);
        }

        [Fact]
        public void Clear_SetsAllValuesToZero()
        {
            using var cache = new KVCache(capacity: 10);
            
            for (int i = 0; i < 10; i++)
            {
                cache.Write(i, i + 1.0f);
            }
            
            cache.Clear();
            
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(0.0f, cache.Read(i));
            }
        }

        [Fact]
        public void Write_OutOfBounds_ThrowsException()
        {
            using var cache = new KVCache(capacity: 10);
            
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => cache.Write(-1, 1.0f));
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => cache.Write(10, 1.0f));
        }

        [Fact]
        public void Read_OutOfBounds_ThrowsException()
        {
            using var cache = new KVCache(capacity: 10);
            
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => cache.Read(-1));
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => cache.Read(10));
        }

        [Fact]
        public void WriteArray_ExceedsCapacity_ThrowsException()
        {
            using var cache = new KVCache(capacity: 10);
            
            var data = new float[20];
            Assert.Throws<ArgumentOutOfRangeException>(() => cache.WriteArray(0, data));
        }

        [Fact]
        public void ReadArray_ExceedsCapacity_ThrowsException()
        {
            using var cache = new KVCache(capacity: 10);
            
            var result = new float[20];
            Assert.Throws<ArgumentOutOfRangeException>(() => cache.ReadArray(0, 20, result));
        }

        [Fact]
        public void Dispose_MemoryMapped_DeletesFile()
        {
            string fileName = Path.Combine(_testDirectory, "dispose_test.bin");
            
            var cache = new KVCache(fileName, capacity: 100, useMemoryMapping: true);
            cache.Write(0, 1.0f);
            cache.Dispose();
            
            // File should be deleted after disposal
            // Note: File deletion might be delayed on some systems
            System.Threading.Thread.Sleep(100);
            Assert.False(File.Exists(fileName));
        }

        [Fact]
        public void LargeCache_HandlesCorrectly()
        {
            using var cache = new KVCache(capacity: 1_000_000);
            
            cache.Write(0, 1.0f);
            cache.Write(999_999, 2.0f);
            
            Assert.Equal(1.0f, cache.Read(0));
            Assert.Equal(2.0f, cache.Read(999_999));
        }
    }

    public class MultiLayerKVCacheTests : IDisposable
    {
        private readonly string _testDirectory;

        public MultiLayerKVCacheTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"multilayer_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void Constructor_InMemory_CreatesAllCaches()
        {
            using var mlCache = new MultiLayerKVCache(
                numLayers: 4,
                maxSeqLen: 2048,
                numHeads: 8,
                headDim: 64,
                useMemoryMapping: false);
            
            // Should be able to access all layer caches
            for (int i = 0; i < 4; i++)
            {
                var keyCache = mlCache.GetKeyCache(i);
                var valueCache = mlCache.GetValueCache(i);
                
                Assert.NotNull(keyCache);
                Assert.NotNull(valueCache);
                Assert.False(keyCache.UsesMemoryMapping);
                Assert.False(valueCache.UsesMemoryMapping);
            }
        }

        [Fact]
        public void Constructor_MemoryMapped_CreatesAllCaches()
        {
            using var mlCache = new MultiLayerKVCache(
                numLayers: 4,
                maxSeqLen: 2048,
                numHeads: 8,
                headDim: 64,
                useMemoryMapping: true,
                cacheDirectory: _testDirectory);
            
            for (int i = 0; i < 4; i++)
            {
                var keyCache = mlCache.GetKeyCache(i);
                var valueCache = mlCache.GetValueCache(i);
                
                Assert.True(keyCache.UsesMemoryMapping);
                Assert.True(valueCache.UsesMemoryMapping);
            }
        }

        [Fact]
        public void GetCache_InvalidLayerIndex_ThrowsException()
        {
            using var mlCache = new MultiLayerKVCache(
                numLayers: 4,
                maxSeqLen: 1024,
                numHeads: 8,
                headDim: 64,
                useMemoryMapping: false);
            
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => mlCache.GetKeyCache(-1));
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => mlCache.GetKeyCache(4));
        }

        [Fact]
        public void WriteAndRead_AcrossLayers_WorksCorrectly()
        {
            using var mlCache = new MultiLayerKVCache(
                numLayers: 3,
                maxSeqLen: 100,
                numHeads: 2,
                headDim: 4,
                useMemoryMapping: false);
            
            // Write to different layers
            mlCache.GetKeyCache(0).Write(0, 1.0f);
            mlCache.GetKeyCache(1).Write(0, 2.0f);
            mlCache.GetKeyCache(2).Write(0, 3.0f);
            
            mlCache.GetValueCache(0).Write(0, 10.0f);
            mlCache.GetValueCache(1).Write(0, 20.0f);
            mlCache.GetValueCache(2).Write(0, 30.0f);
            
            // Verify values are independent per layer
            Assert.Equal(1.0f, mlCache.GetKeyCache(0).Read(0));
            Assert.Equal(2.0f, mlCache.GetKeyCache(1).Read(0));
            Assert.Equal(3.0f, mlCache.GetKeyCache(2).Read(0));
            
            Assert.Equal(10.0f, mlCache.GetValueCache(0).Read(0));
            Assert.Equal(20.0f, mlCache.GetValueCache(1).Read(0));
            Assert.Equal(30.0f, mlCache.GetValueCache(2).Read(0));
        }

        [Fact]
        public void ClearAll_ClearsAllLayerCaches()
        {
            using var mlCache = new MultiLayerKVCache(
                numLayers: 2,
                maxSeqLen: 10,
                numHeads: 1,
                headDim: 4,
                useMemoryMapping: false);
            
            // Write to all caches
            for (int layer = 0; layer < 2; layer++)
            {
                mlCache.GetKeyCache(layer).Write(0, layer + 1.0f);
                mlCache.GetValueCache(layer).Write(0, layer + 10.0f);
            }
            
            mlCache.ClearAll();
            
            // Verify all cleared
            for (int layer = 0; layer < 2; layer++)
            {
                Assert.Equal(0.0f, mlCache.GetKeyCache(layer).Read(0));
                Assert.Equal(0.0f, mlCache.GetValueCache(layer).Read(0));
            }
        }

        [Fact]
        public void Constructor_MemoryMappingWithoutDirectory_ThrowsException()
        {
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => 
                new MultiLayerKVCache(
                    numLayers: 2,
                    maxSeqLen: 100,
                    numHeads: 4,
                    headDim: 16,
                    useMemoryMapping: true,
                    cacheDirectory: null));
        }
    }
}

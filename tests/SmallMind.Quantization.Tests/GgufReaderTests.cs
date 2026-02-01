using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Quantization.Tests
{
    public class GgufReaderTests
    {
        /// <summary>
        /// Create a minimal valid GGUF header with version 3.
        /// </summary>
        private byte[] CreateMinimalGgufHeader()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8);

            // Magic: "GGUF" (4 bytes)
            bw.Write(Encoding.ASCII.GetBytes("GGUF"));

            // Version: 3 (uint32, little-endian)
            bw.Write((uint)3);

            // Tensor count: 0 (uint64)
            bw.Write((ulong)0);

            // Metadata KV count: 0 (uint64)
            bw.Write((ulong)0);

            return ms.ToArray();
        }

        /// <summary>
        /// Create a GGUF file with metadata KV pairs.
        /// </summary>
        private byte[] CreateGgufWithMetadata(Dictionary<string, (GgufValueType type, object value)> kvPairs)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8);

            // Magic
            bw.Write(Encoding.ASCII.GetBytes("GGUF"));

            // Version
            bw.Write((uint)3);

            // Tensor count
            bw.Write((ulong)0);

            // Metadata KV count
            bw.Write((ulong)kvPairs.Count);

            // Write each KV pair
            foreach (var kvp in kvPairs)
            {
                // Key (GGUF string: uint64 length + UTF8 bytes)
                WriteGgufString(bw, kvp.Key);

                // Type
                bw.Write((uint)kvp.Value.type);

                // Value
                WriteGgufValue(bw, kvp.Value.type, kvp.Value.value);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Create a GGUF file with tensor info.
        /// </summary>
        private byte[] CreateGgufWithTensors(List<(string name, GgufTensorType type, ulong[] dims)> tensors)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8);

            // Magic
            bw.Write(Encoding.ASCII.GetBytes("GGUF"));

            // Version
            bw.Write((uint)3);

            // Tensor count
            bw.Write((ulong)tensors.Count);

            // Metadata KV count
            bw.Write((ulong)0);

            // Write tensor infos
            foreach (var tensor in tensors)
            {
                // Name
                WriteGgufString(bw, tensor.name);

                // Dimensions count
                bw.Write((uint)tensor.dims.Length);

                // Dimensions
                foreach (var dim in tensor.dims)
                {
                    bw.Write(dim);
                }

                // Type
                bw.Write((uint)tensor.type);

                // Offset (placeholder)
                bw.Write((ulong)0);
            }

            return ms.ToArray();
        }

        private void WriteGgufString(BinaryWriter bw, string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            bw.Write((ulong)bytes.Length);
            bw.Write(bytes);
        }

        private void WriteGgufValue(BinaryWriter bw, GgufValueType type, object value)
        {
            switch (type)
            {
                case GgufValueType.UInt8:
                    bw.Write((byte)value);
                    break;
                case GgufValueType.Int8:
                    bw.Write((sbyte)value);
                    break;
                case GgufValueType.UInt16:
                    bw.Write((ushort)value);
                    break;
                case GgufValueType.Int16:
                    bw.Write((short)value);
                    break;
                case GgufValueType.UInt32:
                    bw.Write((uint)value);
                    break;
                case GgufValueType.Int32:
                    bw.Write((int)value);
                    break;
                case GgufValueType.Float32:
                    bw.Write((float)value);
                    break;
                case GgufValueType.Bool:
                    bw.Write((byte)((bool)value ? 1 : 0));
                    break;
                case GgufValueType.String:
                    WriteGgufString(bw, (string)value);
                    break;
                case GgufValueType.UInt64:
                    bw.Write((ulong)value);
                    break;
                case GgufValueType.Int64:
                    bw.Write((long)value);
                    break;
                case GgufValueType.Float64:
                    bw.Write((double)value);
                    break;
                default:
                    throw new NotSupportedException($"Type {type} not supported in test helper");
            }
        }

        [Fact]
        public void ReadModelInfo_ValidMinimalHeader_Success()
        {
            // Arrange
            byte[] data = CreateMinimalGgufHeader();

            // Act
            using var ms = new MemoryStream(data);
            using var reader = new GgufReader(ms);
            var modelInfo = reader.ReadModelInfo();

            // Assert
            Assert.NotNull(modelInfo);
            Assert.Equal((uint)3, modelInfo.Version);
            Assert.Empty(modelInfo.Metadata);
            Assert.Empty(modelInfo.Tensors);
        }

        [Fact]
        public void ReadModelInfo_InvalidMagic_ThrowsException()
        {
            // Arrange
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.Write(Encoding.ASCII.GetBytes("BADF")); // Wrong magic
            bw.Write((uint)3);
            ms.Seek(0, SeekOrigin.Begin);

            // Act & Assert
            using var reader = new GgufReader(ms);
            var ex = Assert.Throws<InvalidDataException>(() => reader.ReadModelInfo());
            Assert.Contains("Invalid GGUF magic header", ex.Message);
        }

        [Fact]
        public void ReadModelInfo_UnsupportedVersion_ThrowsException()
        {
            // Arrange
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.Write(Encoding.ASCII.GetBytes("GGUF"));
            bw.Write((uint)999); // Unsupported version
            ms.Seek(0, SeekOrigin.Begin);

            // Act & Assert
            using var reader = new GgufReader(ms);
            var ex = Assert.Throws<NotSupportedException>(() => reader.ReadModelInfo());
            Assert.Contains("Unsupported GGUF version", ex.Message);
        }

        [Fact]
        public void ReadModelInfo_WithStringMetadata_ParsesCorrectly()
        {
            // Arrange
            var kvPairs = new Dictionary<string, (GgufValueType, object)>
            {
                ["general.name"] = (GgufValueType.String, "test-model"),
                ["general.architecture"] = (GgufValueType.String, "llama")
            };
            byte[] data = CreateGgufWithMetadata(kvPairs);

            // Act
            using var ms = new MemoryStream(data);
            using var reader = new GgufReader(ms);
            var modelInfo = reader.ReadModelInfo();

            // Assert
            Assert.Equal(2, modelInfo.Metadata.Count);
            Assert.Equal("test-model", modelInfo.Metadata["general.name"]);
            Assert.Equal("llama", modelInfo.Metadata["general.architecture"]);
        }

        [Fact]
        public void ReadModelInfo_WithNumericMetadata_ParsesCorrectly()
        {
            // Arrange
            var kvPairs = new Dictionary<string, (GgufValueType, object)>
            {
                ["llama.context_length"] = (GgufValueType.UInt32, (uint)2048),
                ["llama.embedding_length"] = (GgufValueType.UInt32, (uint)4096),
                ["test.float"] = (GgufValueType.Float32, 3.14f),
                ["test.bool"] = (GgufValueType.Bool, true)
            };
            byte[] data = CreateGgufWithMetadata(kvPairs);

            // Act
            using var ms = new MemoryStream(data);
            using var reader = new GgufReader(ms);
            var modelInfo = reader.ReadModelInfo();

            // Assert
            Assert.Equal(4, modelInfo.Metadata.Count);
            Assert.Equal((uint)2048, modelInfo.Metadata["llama.context_length"]);
            Assert.Equal((uint)4096, modelInfo.Metadata["llama.embedding_length"]);
            Assert.Equal(3.14f, (float)modelInfo.Metadata["test.float"], 2);
            Assert.True((bool)modelInfo.Metadata["test.bool"]);
        }

        [Fact]
        public void ReadModelInfo_WithTensorInfos_ParsesCorrectly()
        {
            // Arrange
            var tensors = new List<(string, GgufTensorType, ulong[])>
            {
                ("weight.0", GgufTensorType.Q8_0, new ulong[] { 256, 512 }),
                ("weight.1", GgufTensorType.Q4_0, new ulong[] { 512, 1024 })
            };
            byte[] data = CreateGgufWithTensors(tensors);

            // Act
            using var ms = new MemoryStream(data);
            using var reader = new GgufReader(ms);
            var modelInfo = reader.ReadModelInfo();

            // Assert
            Assert.Equal(2, modelInfo.Tensors.Count);
            
            Assert.Equal("weight.0", modelInfo.Tensors[0].Name);
            Assert.Equal(GgufTensorType.Q8_0, modelInfo.Tensors[0].Type);
            Assert.Equal(2, modelInfo.Tensors[0].Dimensions.Length);
            Assert.Equal((ulong)256, modelInfo.Tensors[0].Dimensions[0]);
            Assert.Equal((ulong)512, modelInfo.Tensors[0].Dimensions[1]);

            Assert.Equal("weight.1", modelInfo.Tensors[1].Name);
            Assert.Equal(GgufTensorType.Q4_0, modelInfo.Tensors[1].Type);
            Assert.Equal(2, modelInfo.Tensors[1].Dimensions.Length);
            Assert.Equal((ulong)512, modelInfo.Tensors[1].Dimensions[0]);
            Assert.Equal((ulong)1024, modelInfo.Tensors[1].Dimensions[1]);
        }

        [Fact]
        public void ReadModelInfo_StringWithZeroLength_ReturnsEmptyString()
        {
            // Arrange
            var kvPairs = new Dictionary<string, (GgufValueType, object)>
            {
                ["empty.string"] = (GgufValueType.String, "")
            };
            byte[] data = CreateGgufWithMetadata(kvPairs);

            // Act
            using var ms = new MemoryStream(data);
            using var reader = new GgufReader(ms);
            var modelInfo = reader.ReadModelInfo();

            // Assert
            Assert.Single(modelInfo.Metadata);
            Assert.Equal("", modelInfo.Metadata["empty.string"]);
        }

        [Fact]
        public void ReadModelInfo_Version2_Success()
        {
            // Arrange
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);

            bw.Write(Encoding.ASCII.GetBytes("GGUF"));
            bw.Write((uint)2); // Version 2
            bw.Write((ulong)0); // Tensor count
            bw.Write((ulong)0); // Metadata count
            ms.Seek(0, SeekOrigin.Begin);

            // Act
            using var reader = new GgufReader(ms);
            var modelInfo = reader.ReadModelInfo();

            // Assert
            Assert.Equal((uint)2, modelInfo.Version);
            Assert.Equal((uint)32, modelInfo.Alignment); // Default alignment
        }

        [Fact]
        public void ReadModelInfo_WithAlignmentMetadata_UsesCustomAlignment()
        {
            // Arrange
            var kvPairs = new Dictionary<string, (GgufValueType, object)>
            {
                ["general.alignment"] = (GgufValueType.UInt32, (uint)64)
            };
            byte[] data = CreateGgufWithMetadata(kvPairs);

            // Act
            using var ms = new MemoryStream(data);
            using var reader = new GgufReader(ms);
            var modelInfo = reader.ReadModelInfo();

            // Assert
            Assert.Equal((uint)64, modelInfo.Alignment);
        }

        [Fact]
        public void ReadTensorData_ValidOffsetAndSize_ReturnsData()
        {
            // Arrange
            byte[] testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            using var ms = new MemoryStream();
            ms.Write(new byte[100], 0, 100); // Padding
            ms.Write(testData, 0, testData.Length);
            ms.Seek(0, SeekOrigin.Begin);

            // Act
            using var reader = new GgufReader(ms);
            byte[] result = reader.ReadTensorData(100, (ulong)testData.Length);

            // Assert
            Assert.Equal(testData.Length, result.Length);
            for (int i = 0; i < testData.Length; i++)
            {
                Assert.Equal(testData[i], result[i]);
            }
        }
    }
}

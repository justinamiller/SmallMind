using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using SmallMind.Quantization.IO.Gguf;
using SmallMind.Runtime.Gguf;

namespace SmallMind.Quantization.Tests
{
    /// <summary>
    /// Negative tests for GGUF reader.
    /// Validates that the parser rejects corrupted, truncated, and malformed data
    /// with specific exception types and actionable error messages.
    /// </summary>
    public class GgufReaderNegativeTests
    {
        #region Helper Methods

        private static void WriteGgufString(BinaryWriter bw, string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            bw.Write((ulong)bytes.Length);
            bw.Write(bytes);
        }

        private static byte[] CreateMinimalGguf(uint version = 3, ulong tensorCount = 0, ulong metadataCount = 0)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8);
            bw.Write(Encoding.ASCII.GetBytes("GGUF"));
            bw.Write(version);
            bw.Write(tensorCount);
            bw.Write(metadataCount);
            return ms.ToArray();
        }

        #endregion

        #region Magic Header

        [Fact]
        public void ReadModelInfo_EmptyStream_ThrowsException()
        {
            using var ms = new MemoryStream(Array.Empty<byte>());
            using var reader = new GgufReader(ms);
            Assert.ThrowsAny<Exception>(() => reader.ReadModelInfo());
        }

        [Theory]
        [InlineData("BADF")]
        [InlineData("GGML")]
        [InlineData("\x00\x00\x00\x00")]
        public void ReadModelInfo_WrongMagic_ThrowsInvalidDataException(string magic)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.Write(Encoding.ASCII.GetBytes(magic));
            bw.Write((uint)3);
            bw.Write((ulong)0);
            bw.Write((ulong)0);
            ms.Seek(0, SeekOrigin.Begin);

            using var reader = new GgufReader(ms);
            var ex = Assert.Throws<InvalidDataException>(() => reader.ReadModelInfo());
            Assert.Contains("magic", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ReadModelInfo_TruncatedMagic_ThrowsException()
        {
            // Only 2 bytes when 4 are expected
            using var ms = new MemoryStream(new byte[] { (byte)'G', (byte)'G' });
            using var reader = new GgufReader(ms);
            Assert.ThrowsAny<Exception>(() => reader.ReadModelInfo());
        }

        #endregion

        #region Version

        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(4u)]
        [InlineData(999u)]
        public void ReadModelInfo_UnsupportedVersion_ThrowsNotSupportedException(uint version)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.Write(Encoding.ASCII.GetBytes("GGUF"));
            bw.Write(version);
            ms.Seek(0, SeekOrigin.Begin);

            using var reader = new GgufReader(ms);
            var ex = Assert.Throws<NotSupportedException>(() => reader.ReadModelInfo());
            Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Truncated Data

        [Fact]
        public void ReadModelInfo_TruncatedAfterVersion_ThrowsException()
        {
            // Has magic + version, but missing tensor/metadata counts
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.Write(Encoding.ASCII.GetBytes("GGUF"));
            bw.Write((uint)3);
            // Missing: tensorCount and metadataCount
            ms.Seek(0, SeekOrigin.Begin);

            using var reader = new GgufReader(ms);
            Assert.ThrowsAny<Exception>(() => reader.ReadModelInfo());
        }

        [Fact]
        public void ReadModelInfo_ClaimsMetadataButTruncated_ThrowsException()
        {
            // Header claims 1 metadata KV pair but stream ends
            byte[] data = CreateMinimalGguf(version: 3, tensorCount: 0, metadataCount: 1);
            using var ms = new MemoryStream(data);
            using var reader = new GgufReader(ms);
            Assert.ThrowsAny<Exception>(() => reader.ReadModelInfo());
        }

        [Fact]
        public void ReadModelInfo_ClaimsTensorsButTruncated_ThrowsException()
        {
            // Header claims 1 tensor but stream ends
            byte[] data = CreateMinimalGguf(version: 3, tensorCount: 1, metadataCount: 0);
            using var ms = new MemoryStream(data);
            using var reader = new GgufReader(ms);
            Assert.ThrowsAny<Exception>(() => reader.ReadModelInfo());
        }

        #endregion

        #region Unsupported Tensor Types

        [Fact]
        public void ReadModelInfo_UnsupportedTensorType_CanBeDetected()
        {
            // Create a GGUF with a tensor of type Q5_0 which may not be supported for size calculation
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);

            bw.Write(Encoding.ASCII.GetBytes("GGUF"));
            bw.Write((uint)3);
            bw.Write((ulong)1); // 1 tensor
            bw.Write((ulong)0); // 0 metadata

            // Tensor info
            WriteGgufString(bw, "test.weight");
            bw.Write((uint)2); // 2 dimensions
            bw.Write((ulong)64);
            bw.Write((ulong)64);
            bw.Write((uint)GgufTensorType.Q5_0); // Q5_0 — not in CalculateTensorSize
            bw.Write((ulong)0); // offset

            ms.Seek(0, SeekOrigin.Begin);

            using var reader = new GgufReader(ms);
            var ex = Assert.Throws<NotSupportedException>(() => reader.ReadModelInfo());
            Assert.Contains("tensor type", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Constructor Validation

        [Fact]
        public void Constructor_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GgufReader(null!));
        }

        [Fact]
        public void Constructor_NonSeekableStream_ThrowsArgumentException()
        {
            using var pipe = new NonSeekableStream();
            Assert.Throws<ArgumentException>(() => new GgufReader(pipe));
        }

        #endregion

        #region Compatibility Report

        [Fact]
        public void GgufCompatibilityReport_AllSupported_IsFullyCompatible()
        {
            var info = new GgufModelInfo();
            info.Metadata["general.architecture"] = "llama";
            info.Version = 3;
            info.Tensors.Add(new GgufTensorInfo { Name = "w1", Type = GgufTensorType.F32, Dimensions = new ulong[] { 64 } });
            info.Tensors.Add(new GgufTensorInfo { Name = "w2", Type = GgufTensorType.Q8_0, Dimensions = new ulong[] { 128 } });

            var report = GgufCompatibilityReport.FromModelInfo(info, type =>
                type == GgufTensorType.F32 || type == GgufTensorType.Q8_0);

            Assert.True(report.IsFullyCompatible);
            Assert.Equal(2, report.SupportedTensors);
            Assert.Equal(0, report.UnsupportedTensors);
            Assert.Equal("llama", report.Architecture);
        }

        [Fact]
        public void GgufCompatibilityReport_HasUnsupported_IsNotFullyCompatible()
        {
            var info = new GgufModelInfo();
            info.Version = 3;
            info.Tensors.Add(new GgufTensorInfo { Name = "w1", Type = GgufTensorType.F32, Dimensions = new ulong[] { 64 } });
            info.Tensors.Add(new GgufTensorInfo { Name = "w2", Type = GgufTensorType.IQ2_XXS, Dimensions = new ulong[] { 128 } });

            var report = GgufCompatibilityReport.FromModelInfo(info, type => type == GgufTensorType.F32);

            Assert.False(report.IsFullyCompatible);
            Assert.Equal(1, report.SupportedTensors);
            Assert.Equal(1, report.UnsupportedTensors);
            Assert.Contains("IQ2_XXS", report.UnsupportedTensorsByType.Keys);
        }

        [Fact]
        public void GgufCompatibilityReport_ThrowIfIncompatible_ThrowsForUnsupported()
        {
            var info = new GgufModelInfo();
            info.Version = 3;
            info.Tensors.Add(new GgufTensorInfo { Name = "bad", Type = GgufTensorType.IQ1_S, Dimensions = new ulong[] { 32 } });

            var report = GgufCompatibilityReport.FromModelInfo(info, _ => false);

            var ex = Assert.Throws<NotSupportedException>(() => report.ThrowIfIncompatible());
            Assert.Contains("unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GgufCompatibilityReport_GetSummary_ContainsRelevantInfo()
        {
            var info = new GgufModelInfo();
            info.Metadata["general.architecture"] = "llama";
            info.Version = 3;
            info.Tensors.Add(new GgufTensorInfo { Name = "w1", Type = GgufTensorType.F32, Dimensions = new ulong[] { 64 } });

            var report = GgufCompatibilityReport.FromModelInfo(info, _ => true);
            var summary = report.GetSummary();

            Assert.Contains("llama", summary);
            Assert.Contains("FULLY COMPATIBLE", summary);
        }

        #endregion

        /// <summary>
        /// A stream that is readable but not seekable, for testing constructor validation.
        /// </summary>
        private sealed class NonSeekableStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 0;
            public override long Position { get => 0; set { } }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) { }
            public override void Write(byte[] buffer, int offset, int count) { }
        }
    }
}

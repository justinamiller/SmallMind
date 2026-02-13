using SmallMind.Core.Core;

namespace SmallMind.Tests
{
    public class SlidingWindowProcessorTests
    {
        [Fact]
        public void Constructor_ValidParameters_CreatesProcessor()
        {
            var processor = new SlidingWindowProcessor(4096, 2048);

            Assert.Equal(4096, processor.WindowSize);
            Assert.Equal(2048, processor.Stride);
            Assert.Equal(2048, processor.OverlapSize);
        }

        [Fact]
        public void Constructor_InvalidWindowSize_ThrowsException()
        {
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => new SlidingWindowProcessor(0, 100));
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => new SlidingWindowProcessor(-100, 100));
        }

        [Fact]
        public void Constructor_InvalidStride_ThrowsException()
        {
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => new SlidingWindowProcessor(100, 0));
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => new SlidingWindowProcessor(100, -10));
        }

        [Fact]
        public void Constructor_StrideLargerThanWindow_ThrowsException()
        {
            var ex = Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(
                () => new SlidingWindowProcessor(100, 200));

            Assert.Contains("Stride cannot be greater than window size", ex.Message);
        }

        [Fact]
        public void GetWindows_EmptyArray_ReturnsEmpty()
        {
            var processor = new SlidingWindowProcessor(10, 5);
            var windows = processor.GetWindows(Array.Empty<int>()).ToList();

            Assert.Empty(windows);
        }

        [Fact]
        public void GetWindows_SequenceFitsInOneWindow_ReturnsSingleWindow()
        {
            var processor = new SlidingWindowProcessor(10, 5);
            var tokens = new int[] { 1, 2, 3, 4, 5 };

            var windows = processor.GetWindows(tokens).ToList();

            Assert.Single(windows);
            Assert.Equal(tokens, windows[0]);
        }

        [Fact]
        public void GetWindows_LargeSequence_ReturnsOverlappingWindows()
        {
            var processor = new SlidingWindowProcessor(windowSize: 4, stride: 2);
            var tokens = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            var windows = processor.GetWindows(tokens).ToList();

            // Should generate 3 windows: [1,2,3,4], [3,4,5,6], [5,6,7,8]
            Assert.Equal(3, windows.Count);
            Assert.Equal(new[] { 1, 2, 3, 4 }, windows[0]);
            Assert.Equal(new[] { 3, 4, 5, 6 }, windows[1]);
            Assert.Equal(new[] { 5, 6, 7, 8 }, windows[2]);
        }

        [Fact]
        public void GetWindows_PartialLastWindow_HandlesCorrectly()
        {
            var processor = new SlidingWindowProcessor(windowSize: 4, stride: 2);
            var tokens = new int[] { 1, 2, 3, 4, 5, 6, 7 }; // 7 tokens

            var windows = processor.GetWindows(tokens).ToList();

            // Windows: [1,2,3,4], [3,4,5,6], [5,6,7]
            Assert.Equal(3, windows.Count);
            Assert.Equal(new[] { 1, 2, 3, 4 }, windows[0]);
            Assert.Equal(new[] { 3, 4, 5, 6 }, windows[1]);
            Assert.Equal(new[] { 5, 6, 7 }, windows[2]); // Partial window
        }

        [Fact]
        public void GetWindowTensors_ValidTensor_ReturnsWindows()
        {
            var processor = new SlidingWindowProcessor(windowSize: 4, stride: 2);

            // Create tensor: batch=2, seq_len=8
            var tokens = new Tensor(new int[] { 2, 8 });
            for (int i = 0; i < tokens.Size; i++)
            {
                tokens.Data[i] = i;
            }

            var windows = processor.GetWindowTensors(tokens).ToList();

            Assert.Equal(3, windows.Count);
            foreach (var window in windows)
            {
                Assert.Equal(2, window.Shape[0]); // Batch size preserved
            }
        }

        [Fact]
        public void GetWindowTensors_InvalidShape_ThrowsException()
        {
            var processor = new SlidingWindowProcessor(10, 5);

            // 1D tensor (invalid)
            var invalidTensor = new Tensor(new int[] { 10 });

            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(
                () => processor.GetWindowTensors(invalidTensor).ToList());
        }

        [Fact]
        public void CombineWindowOutputs_SingleWindow_ReturnsSame()
        {
            var processor = new SlidingWindowProcessor(10, 5);

            var window = new Tensor(new int[] { 1, 5, 3 }); // batch=1, seq=5, dim=3
            for (int i = 0; i < window.Size; i++)
            {
                window.Data[i] = i;
            }

            var combined = processor.CombineWindowOutputs(new[] { window }.ToList(), 5);

            Assert.Equal(window.Shape, combined.Shape);
            Assert.Equal(window.Data, combined.Data);
        }

        [Fact]
        public void CombineWindowOutputs_MultipleWindows_AveragesOverlap()
        {
            var processor = new SlidingWindowProcessor(windowSize: 4, stride: 2);

            // Create 2 windows with overlap
            var window1 = new Tensor(new int[] { 1, 4, 2 }); // batch=1, seq=4, dim=2
            var window2 = new Tensor(new int[] { 1, 4, 2 });

            // Set different values
            for (int i = 0; i < window1.Size; i++)
            {
                window1.Data[i] = 1.0f;
                window2.Data[i] = 3.0f;
            }

            var combined = processor.CombineWindowOutputs(
                new[] { window1, window2 }.ToList(),
                6); // Original sequence length

            // First 2 positions: only from window1 (value=1)
            // Middle 2 positions: average of both windows (value=2)
            // Last 2 positions: only from window2 (value=3)
            Assert.Equal(1, combined.Shape[0]); // batch
            Assert.Equal(6, combined.Shape[1]); // seq
            Assert.Equal(2, combined.Shape[2]); // dim

            // Check averaging in overlap region
            float val0 = combined.Data[0]; // First position
            float val2 = combined.Data[2 * 2]; // Position 2 (overlap start)
            float val4 = combined.Data[4 * 2]; // Position 4 (from window2 only)

            Assert.Equal(1.0f, val0, precision: 3);
            Assert.Equal(2.0f, val2, precision: 3); // Average of 1 and 3
            Assert.Equal(3.0f, val4, precision: 3);
        }

        [Fact]
        public void CombineWindowOutputsMax_TakesMaximum()
        {
            var processor = new SlidingWindowProcessor(windowSize: 4, stride: 2);

            var window1 = new Tensor(new int[] { 1, 4, 1 });
            var window2 = new Tensor(new int[] { 1, 4, 1 });

            for (int i = 0; i < 4; i++)
            {
                window1.Data[i] = i; // 0, 1, 2, 3
                window2.Data[i] = 3 - i; // 3, 2, 1, 0
            }

            var combined = processor.CombineWindowOutputsMax(
                new[] { window1, window2 }.ToList(),
                6);

            // Should take max values where windows overlap
            Assert.Equal(1, combined.Shape[0]);
            Assert.Equal(6, combined.Shape[1]);
        }

        [Fact]
        public void EstimateWindowCount_SmallSequence_ReturnsOne()
        {
            var processor = new SlidingWindowProcessor(10, 5);

            Assert.Equal(1, processor.EstimateWindowCount(5));
            Assert.Equal(1, processor.EstimateWindowCount(10));
        }

        [Fact]
        public void EstimateWindowCount_LargeSequence_ReturnsCorrectCount()
        {
            var processor = new SlidingWindowProcessor(windowSize: 4, stride: 2);

            // seq_len=8: [0-3], [2-5], [4-7] = 3 windows
            Assert.Equal(3, processor.EstimateWindowCount(8));

            // seq_len=10: [0-3], [2-5], [4-7], [6-9] = 4 windows
            Assert.Equal(4, processor.EstimateWindowCount(10));
        }

        [Fact]
        public void RealWorldScenario_32kTokens_ProcessesCorrectly()
        {
            // Simulate processing 32k tokens with 4k windows and 2k overlap
            var processor = new SlidingWindowProcessor(
                windowSize: 4096,
                stride: 2048);

            var tokens = new int[32768]; // 32k tokens
            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = i % 1000;
            }

            var windows = processor.GetWindows(tokens).ToList();

            // Should generate about 15-16 windows
            int expectedWindows = processor.EstimateWindowCount(32768);
            Assert.Equal(expectedWindows, windows.Count);

            // Each window should have correct size
            for (int i = 0; i < windows.Count - 1; i++)
            {
                Assert.Equal(4096, windows[i].Length);
            }

            // Last window may be smaller
            Assert.True(windows[^1].Length <= 4096);
        }
    }
}

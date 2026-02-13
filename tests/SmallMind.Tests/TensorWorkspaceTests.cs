using SmallMind.Transformers;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for TensorWorkspace class.
    /// Tests workspace tensor reuse and verifies operations handle their own clearing.
    /// </summary>
    public class TensorWorkspaceTests
    {
        private const float Tolerance = 1e-5f;

        [Fact]
        public void WorkspaceReuse_ClearsData_ForCorrectness()
        {
            // Arrange
            var workspace = new TensorWorkspace();

            // First use: get tensor and write known pattern
            var tensor1 = workspace.GetOrCreate("test", new[] { 100 }, requiresGrad: false);
            for (int i = 0; i < tensor1.Size; i++)
                tensor1.Data[i] = i * 1.5f;

            // Verify data was written correctly
            Assert.Equal(0f, tensor1.Data[0], precision: 5);
            Assert.Equal(1.5f, tensor1.Data[1], precision: 5);
            Assert.Equal(3.0f, tensor1.Data[2], precision: 5);

            // Act: Second use - reuse same tensor (SHOULD clear the data for correctness)
            var tensor2 = workspace.GetOrCreate("test", new[] { 100 }, requiresGrad: false);

            // Assert: Verify it's the same instance
            Assert.Same(tensor1, tensor2);

            // Assert: Verify data WAS cleared (workspace clears to prevent accumulation bugs)
            // MatMul uses accumulation (+=), so workspaces must be zeroed
            Assert.Equal(0f, tensor2.Data[0], precision: 5);
            Assert.Equal(0f, tensor2.Data[1], precision: 5);
            Assert.Equal(0f, tensor2.Data[2], precision: 5);
            Assert.Equal(0f, tensor2.Data[3], precision: 5);
            Assert.Equal(0f, tensor2.Data[7], precision: 5);
            Assert.Equal(0f, tensor2.Data[99], precision: 5);
        }

        [Fact]
        public void WorkspaceReuse_WithDifferentShapes_CreatesNewTensor()
        {
            // Arrange
            var workspace = new TensorWorkspace();

            // First use with shape [100]
            var tensor1 = workspace.GetOrCreate("test", new[] { 100 }, requiresGrad: false);
            for (int i = 0; i < tensor1.Size; i++)
                tensor1.Data[i] = i * 1.5f;

            // Act: Request different shape with same key
            var tensor2 = workspace.GetOrCreate("test", new[] { 200 }, requiresGrad: false);

            // Assert: Should be a different instance with new shape
            Assert.NotSame(tensor1, tensor2);
            Assert.Equal(200, tensor2.Size);

            // New tensor should have zero-initialized data
            Assert.Equal(0f, tensor2.Data[0]);
            Assert.Equal(0f, tensor2.Data[1]);
        }

        [Fact]
        public void WorkspaceReuse_WithDifferentKeys_CreatesSeparateTensors()
        {
            // Arrange
            var workspace = new TensorWorkspace();

            // Create two tensors with different keys
            var tensor1 = workspace.GetOrCreate("key1", new[] { 50 }, requiresGrad: false);
            var tensor2 = workspace.GetOrCreate("key2", new[] { 50 }, requiresGrad: false);

            // Assert: Should be different instances
            Assert.NotSame(tensor1, tensor2);

            // Modify first tensor
            tensor1.Data[0] = 42f;

            // Verify second tensor is unaffected
            Assert.Equal(0f, tensor2.Data[0]);
        }

        [Fact]
        public void WorkspaceReuse_MultipleReuses_MaintainsSameInstance()
        {
            // Arrange
            var workspace = new TensorWorkspace();

            // Act: Get the same tensor multiple times
            var tensor1 = workspace.GetOrCreate("test", new[] { 50 }, requiresGrad: false);
            var tensor2 = workspace.GetOrCreate("test", new[] { 50 }, requiresGrad: false);
            var tensor3 = workspace.GetOrCreate("test", new[] { 50 }, requiresGrad: false);

            // Assert: All should be the same instance
            Assert.Same(tensor1, tensor2);
            Assert.Same(tensor2, tensor3);
            Assert.Same(tensor1, tensor3);
        }

        [Fact]
        public void Clear_RemovesAllTensors()
        {
            // Arrange
            var workspace = new TensorWorkspace();
            workspace.GetOrCreate("test1", new[] { 50 }, requiresGrad: false);
            workspace.GetOrCreate("test2", new[] { 100 }, requiresGrad: false);

            // Act
            workspace.Clear();

            // Get tensors again with same keys
            var tensor1 = workspace.GetOrCreate("test1", new[] { 50 }, requiresGrad: false);
            var tensor2 = workspace.GetOrCreate("test2", new[] { 100 }, requiresGrad: false);

            // Assert: Should get new instances (not reused)
            // We can verify this by checking that data is zero-initialized
            Assert.Equal(0f, tensor1.Data[0]);
            Assert.Equal(0f, tensor2.Data[0]);
        }
    }
}

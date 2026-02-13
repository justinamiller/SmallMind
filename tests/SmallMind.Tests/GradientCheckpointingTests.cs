using SmallMind.Core.Core;

namespace SmallMind.Tests
{
    public class GradientCheckpointingTests
    {
        [Fact]
        public void GetOptimalCheckpointInterval_NoStrategy_ReturnsNoCheckpointing()
        {
            // Arrange
            int numLayers = 10;
            long availableMemory = 1000000;
            long perLayerBytes = 1000;

            // Act
            int interval = GradientCheckpointing.GetOptimalCheckpointInterval(
                numLayers, availableMemory, perLayerBytes, CheckpointStrategy.None);

            // Assert
            Assert.True(interval > numLayers); // Never checkpoint
        }

        [Fact]
        public void GetOptimalCheckpointInterval_EveryLayer_Returns1()
        {
            // Arrange
            int numLayers = 10;
            long availableMemory = 1000000;
            long perLayerBytes = 1000;

            // Act
            int interval = GradientCheckpointing.GetOptimalCheckpointInterval(
                numLayers, availableMemory, perLayerBytes, CheckpointStrategy.EveryLayer);

            // Assert
            Assert.Equal(1, interval);
        }

        [Fact]
        public void GetOptimalCheckpointInterval_SqrtStrategy_ReturnsSquareRoot()
        {
            // Arrange
            int numLayers = 16;
            long availableMemory = 5000; // Limited memory to force checkpointing
            long perLayerBytes = 1000;

            // Act
            int interval = GradientCheckpointing.GetOptimalCheckpointInterval(
                numLayers, availableMemory, perLayerBytes, CheckpointStrategy.SqrtLayers);

            // Assert
            Assert.Equal(4, interval); // sqrt(16) = 4
        }

        [Fact]
        public void GetOptimalCheckpointInterval_InsufficientMemory_ReturnsEveryLayer()
        {
            // Arrange
            int numLayers = 100;
            long availableMemory = 500; // Very limited
            long perLayerBytes = 1000;

            // Act
            int interval = GradientCheckpointing.GetOptimalCheckpointInterval(
                numLayers, availableMemory, perLayerBytes, CheckpointStrategy.SqrtLayers);

            // Assert
            Assert.Equal(1, interval); // Fall back to checkpoint every layer
        }

        [Fact]
        public void EstimateMemorySavings_CalculatesCorrectly()
        {
            // Arrange
            int numLayers = 10;
            long perLayerBytes = 1000;
            int checkpointInterval = 2;

            // Act
            var (without, with, savings) = GradientCheckpointing.EstimateMemorySavings(
                numLayers, perLayerBytes, checkpointInterval);

            // Assert
            Assert.Equal(10000, without); // 10 layers * 1000 bytes
            Assert.Equal(5000, with);     // 5 checkpoints * 1000 bytes
            Assert.Equal(50.0, savings);  // 50% savings
        }

        [Fact]
        public void CheckpointManager_SavesCheckpointsAtInterval()
        {
            // Arrange
            var manager = new CheckpointManager(checkpointInterval: 2, enabled: true);
            var tensor0 = new Tensor(new float[] { 1.0f, 2.0f }, new int[] { 2 });
            var tensor2 = new Tensor(new float[] { 3.0f, 4.0f }, new int[] { 2 });
            var tensor3 = new Tensor(new float[] { 5.0f, 6.0f }, new int[] { 2 });

            // Act
            manager.SaveCheckpoint(0, tensor0); // Should save (0 % 2 == 0)
            manager.SaveCheckpoint(1, tensor3); // Should not save
            manager.SaveCheckpoint(2, tensor2); // Should save (2 % 2 == 0)

            // Assert
            var checkpoint0 = manager.GetNearestCheckpoint(0);
            var checkpoint1 = manager.GetNearestCheckpoint(1);
            var checkpoint2 = manager.GetNearestCheckpoint(2);

            Assert.NotNull(checkpoint0);
            Assert.NotNull(checkpoint1); // Should get checkpoint 0
            Assert.NotNull(checkpoint2);

            Assert.Equal(1.0f, checkpoint0!.Data[0]);
            Assert.Equal(3.0f, checkpoint2!.Data[0]);
        }

        [Fact]
        public void CheckpointManager_GetNearestCheckpoint_ReturnsNearestLowerCheckpoint()
        {
            // Arrange
            var manager = new CheckpointManager(checkpointInterval: 3, enabled: true);
            var tensor0 = new Tensor(new float[] { 1.0f }, new int[] { 1 });
            var tensor3 = new Tensor(new float[] { 2.0f }, new int[] { 1 });

            manager.SaveCheckpoint(0, tensor0);
            manager.SaveCheckpoint(3, tensor3);

            // Act
            var checkpoint1 = manager.GetNearestCheckpoint(1); // Should get 0
            var checkpoint2 = manager.GetNearestCheckpoint(2); // Should get 0
            var checkpoint4 = manager.GetNearestCheckpoint(4); // Should get 3

            // Assert
            Assert.NotNull(checkpoint1);
            Assert.NotNull(checkpoint2);
            Assert.NotNull(checkpoint4);

            Assert.Equal(1.0f, checkpoint1!.Data[0]);
            Assert.Equal(1.0f, checkpoint2!.Data[0]);
            Assert.Equal(2.0f, checkpoint4!.Data[0]);
        }

        [Fact]
        public void CheckpointManager_Clear_RemovesAllCheckpoints()
        {
            // Arrange
            var manager = new CheckpointManager(checkpointInterval: 1, enabled: true);
            var tensor = new Tensor(new float[] { 1.0f }, new int[] { 1 });

            manager.SaveCheckpoint(0, tensor);
            manager.SaveCheckpoint(1, tensor);

            // Act
            manager.Clear();

            // Assert
            var checkpoint = manager.GetNearestCheckpoint(0);
            Assert.Null(checkpoint);
        }

        [Fact]
        public void CheckpointManager_GetMemoryUsage_CalculatesCorrectly()
        {
            // Arrange
            var manager = new CheckpointManager(checkpointInterval: 2, enabled: true);
            var tensor1 = new Tensor(new float[] { 1.0f, 2.0f, 3.0f }, new int[] { 3 });
            var tensor2 = new Tensor(new float[] { 4.0f, 5.0f }, new int[] { 2 });

            // Act
            manager.SaveCheckpoint(0, tensor1); // 3 floats * 4 bytes = 12 bytes
            manager.SaveCheckpoint(2, tensor2); // 2 floats * 4 bytes = 8 bytes

            // Assert
            long memoryUsage = manager.GetMemoryUsageBytes();
            Assert.Equal(20, memoryUsage); // 12 + 8 = 20 bytes
        }

        [Fact]
        public void CheckpointManager_WhenDisabled_DoesNotSaveCheckpoints()
        {
            // Arrange
            var manager = new CheckpointManager(checkpointInterval: 1, enabled: false);
            var tensor = new Tensor(new float[] { 1.0f }, new int[] { 1 });

            // Act
            manager.SaveCheckpoint(0, tensor);

            // Assert
            var checkpoint = manager.GetNearestCheckpoint(0);
            Assert.Null(checkpoint);
            Assert.False(manager.IsEnabled);
        }
    }
}

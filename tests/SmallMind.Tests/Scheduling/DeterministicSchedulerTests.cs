using SmallMind.Runtime.Scheduling;

namespace SmallMind.Tests.Scheduling
{
    public class DeterministicSchedulerTests
    {
        [Fact]
        public void Constructor_CreatesEmptyScheduler()
        {
            // Arrange & Act
            var scheduler = new DeterministicScheduler();

            // Assert
            Assert.NotNull(scheduler);
            Assert.Equal(0, scheduler.TotalSchedules);
        }

        [Fact]
        public void Schedule_WithFIFOPolicy_CreatesSequentialOrder()
        {
            // Arrange
            var scheduler = new DeterministicScheduler();
            var promptTokens = new[] { 1, 2, 3 };
            int maxNewTokens = 10;

            // Act
            var result = scheduler.Schedule(
                promptTokens,
                maxNewTokens,
                SchedulingPolicy.FIFO);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(SchedulingPolicy.FIFO, result.Policy);
            Assert.Equal(promptTokens.Length + maxNewTokens, result.TotalTokens);
            Assert.Equal(maxNewTokens, result.GenerationOrder.Count);

            // FIFO should be sequential: 0, 1, 2, 3, ...
            for (int i = 0; i < maxNewTokens; i++)
            {
                Assert.Equal(i, result.GenerationOrder[i]);
            }
        }

        [Fact]
        public void Schedule_WithSeed_IsDeterministic()
        {
            // Arrange
            var scheduler = new DeterministicScheduler();
            var promptTokens = new[] { 1, 2, 3, 4, 5 };
            int maxNewTokens = 10;
            uint seed = 42;

            // Act - create two schedules with same seed
            var result1 = scheduler.Schedule(
                promptTokens,
                maxNewTokens,
                SchedulingPolicy.Priority,
                seed);

            var result2 = scheduler.Schedule(
                promptTokens,
                maxNewTokens,
                SchedulingPolicy.Priority,
                seed);

            // Assert - both should have same generation order
            Assert.Equal(result1.GenerationOrder.Count, result2.GenerationOrder.Count);
            for (int i = 0; i < result1.GenerationOrder.Count; i++)
            {
                Assert.Equal(result1.GenerationOrder[i], result2.GenerationOrder[i]);
            }
        }

        [Fact]
        public void Schedule_AllocatesResources()
        {
            // Arrange
            var scheduler = new DeterministicScheduler();
            var promptTokens = new[] { 1, 2, 3 };
            int maxNewTokens = 10;

            // Act
            var result = scheduler.Schedule(
                promptTokens,
                maxNewTokens,
                SchedulingPolicy.FIFO);

            // Assert
            Assert.NotNull(result.ResourceAllocation);
            Assert.True(result.ResourceAllocation.ContainsKey("memory_bytes"));
            Assert.True(result.ResourceAllocation.ContainsKey("compute_units"));
            Assert.Equal(promptTokens.Length, result.ResourceAllocation["prompt_tokens"]);
            Assert.Equal(maxNewTokens, result.ResourceAllocation["max_new_tokens"]);
        }

        [Fact]
        public void GetSchedule_RetrievesScheduleById()
        {
            // Arrange
            var scheduler = new DeterministicScheduler();
            var promptTokens = new[] { 1, 2, 3 };
            var created = scheduler.Schedule(promptTokens, 5, SchedulingPolicy.FIFO);

            // Act
            var retrieved = scheduler.GetSchedule(created.ScheduleId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(created.ScheduleId, retrieved.ScheduleId);
        }
    }
}

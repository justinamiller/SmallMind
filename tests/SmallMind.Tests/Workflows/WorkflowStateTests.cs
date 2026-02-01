using System;
using Xunit;
using SmallMind.Workflows;

namespace SmallMind.Tests.Workflows
{
    /// <summary>
    /// Unit tests for WorkflowState.
    /// Tests state management, metadata, and step output tracking.
    /// </summary>
    public class WorkflowStateTests
    {
        [Fact]
        public void Set_And_GetString_ReturnsCorrectValue()
        {
            // Arrange
            var state = new WorkflowState();

            // Act
            state.Set("key1", "value1");
            var result = state.GetString("key1");

            // Assert
            Assert.Equal("value1", result);
        }

        [Fact]
        public void GetString_NonExistentKey_ReturnsNull()
        {
            // Arrange
            var state = new WorkflowState();

            // Act
            var result = state.GetString("nonexistent");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Set_And_GetInt_ReturnsCorrectValue()
        {
            // Arrange
            var state = new WorkflowState();

            // Act
            state.Set("count", 42);
            var result = state.GetInt("count");

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public void GetInt_NonExistentKey_ReturnsNull()
        {
            // Arrange
            var state = new WorkflowState();

            // Act
            var result = state.GetInt("nonexistent");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetInt_StringValue_ParsesCorrectly()
        {
            // Arrange
            var state = new WorkflowState();

            // Act
            state.Set("count", "42");
            var result = state.GetInt("count");

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public void TryGet_ExistingKey_ReturnsTrue()
        {
            // Arrange
            var state = new WorkflowState();
            state.Set("key1", "value1");

            // Act
            var success = state.TryGet<string>("key1", out var value);

            // Assert
            Assert.True(success);
            Assert.Equal("value1", value);
        }

        [Fact]
        public void TryGet_NonExistentKey_ReturnsFalse()
        {
            // Arrange
            var state = new WorkflowState();

            // Act
            var success = state.TryGet<string>("nonexistent", out var value);

            // Assert
            Assert.False(success);
            Assert.Null(value);
        }

        [Fact]
        public void ContainsKey_ExistingKey_ReturnsTrue()
        {
            // Arrange
            var state = new WorkflowState();
            state.Set("key1", "value1");

            // Act
            var result = state.ContainsKey("key1");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsKey_NonExistentKey_ReturnsFalse()
        {
            // Arrange
            var state = new WorkflowState();

            // Act
            var result = state.ContainsKey("nonexistent");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void SetStepOutput_And_GetStepOutput_ReturnsCorrectValue()
        {
            // Arrange
            var state = new WorkflowState();

            // Act
            state.SetStepOutput("step1", "output1");
            var result = state.GetStepOutput("step1");

            // Assert
            Assert.Equal("output1", result);
        }

        [Fact]
        public void GetStepOutput_NonExistentStep_ReturnsNull()
        {
            // Arrange
            var state = new WorkflowState();

            // Act
            var result = state.GetStepOutput("nonexistent");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetAllStepOutputs_ReturnsAllOutputs()
        {
            // Arrange
            var state = new WorkflowState();
            state.SetStepOutput("step1", "output1");
            state.SetStepOutput("step2", "output2");

            // Act
            var outputs = state.GetAllStepOutputs();

            // Assert
            Assert.Equal(2, outputs.Count);
            Assert.Equal("output1", outputs["step1"]);
            Assert.Equal("output2", outputs["step2"]);
        }

        [Fact]
        public void Metadata_CanSetAndGet()
        {
            // Arrange
            var state = new WorkflowState();

            // Act
            state.Metadata["runId"] = "test-run-123";
            state.Metadata["timestamp"] = DateTime.UtcNow;

            // Assert
            Assert.Equal("test-run-123", state.Metadata["runId"]);
            Assert.NotNull(state.Metadata["timestamp"]);
        }

        [Fact]
        public void GetKeys_ReturnsAllStateKeys()
        {
            // Arrange
            var state = new WorkflowState();
            state.Set("key1", "value1");
            state.Set("key2", "value2");
            state.Set("key3", "value3");

            // Act
            var keys = state.GetKeys();

            // Assert
            Assert.Contains("key1", keys);
            Assert.Contains("key2", keys);
            Assert.Contains("key3", keys);
        }

        [Fact]
        public void GetState_ReturnsReadOnlyDictionary()
        {
            // Arrange
            var state = new WorkflowState();
            state.Set("key1", "value1");
            state.Set("key2", 42);

            // Act
            var stateDict = state.GetState();

            // Assert
            Assert.Equal(2, stateDict.Count);
            Assert.Equal("value1", stateDict["key1"]);
            Assert.Equal(42, stateDict["key2"]);
        }
    }
}

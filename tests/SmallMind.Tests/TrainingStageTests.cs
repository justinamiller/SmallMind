using SmallMind.Core;
using SmallMind.Tokenizers;
using SmallMind.Training;
using SmallMind.Transformers;
using System;
using System.IO;
using Xunit;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for training stage verification and progression functionality.
    /// </summary>
    public class TrainingStageTests
    {
        private const string TestVocab = "abcdefghijklmnopqrstuvwxyz ";
        private const string TestText = "hello world this is a test";

        private Training.Training CreateTestTraining()
        {
            var tokenizer = new CharTokenizer(TestVocab);
            var model = new TransformerModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 16,
                nEmbd: 32,
                nLayer: 2,
                nHead: 2,
                dropout: 0.0,
                seed: 42
            );

            return new Training.Training(
                model: model,
                tokenizer: tokenizer,
                trainingText: TestText,
                blockSize: 16,
                batchSize: 2,
                seed: 42
            );
        }

        [Fact]
        public void TrainingStage_DefaultInitialization_IsInitialized()
        {
            // Arrange & Act
            var training = CreateTestTraining();

            // Assert
            Assert.NotNull(training.StageInfo);
            Assert.Equal(TrainingStage.Initialized, training.StageInfo.CurrentStage);
            Assert.Equal(0, training.StageInfo.StepsCompleted);
            Assert.Equal(0, training.StageInfo.TotalStepsPlanned);
        }

        [Fact]
        public void TrainingStage_SetStage_UpdatesCurrentStage()
        {
            // Arrange
            var training = CreateTestTraining();

            // Act
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 100);

            // Assert
            Assert.Equal(TrainingStage.Pretraining, training.StageInfo.CurrentStage);
            Assert.Equal(0, training.StageInfo.StepsCompleted);
            Assert.Equal(100, training.StageInfo.TotalStepsPlanned);
        }

        [Fact]
        public void TrainingStage_UpdateProgress_UpdatesStepsCompleted()
        {
            // Arrange
            var training = CreateTestTraining();
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 100);

            // Act
            training.UpdateStageProgress(50);

            // Assert
            Assert.Equal(50, training.StageInfo.StepsCompleted);
            Assert.Equal(50.0, training.StageInfo.GetStageProgressPercentage());
        }

        [Fact]
        public void TrainingStage_IsStageComplete_ReturnsTrueWhenComplete()
        {
            // Arrange
            var training = CreateTestTraining();
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 100);

            // Act
            training.UpdateStageProgress(100);

            // Assert
            Assert.True(training.StageInfo.IsStageComplete());
        }

        [Fact]
        public void TrainingStage_IsStageComplete_ReturnsFalseWhenIncomplete()
        {
            // Arrange
            var training = CreateTestTraining();
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 100);

            // Act
            training.UpdateStageProgress(50);

            // Assert
            Assert.False(training.StageInfo.IsStageComplete());
        }

        [Fact]
        public void TrainingStage_GetNextStage_ReturnsCorrectProgression()
        {
            // Arrange & Act & Assert
            var training = CreateTestTraining();

            // Initialized -> Pretraining
            Assert.Equal(TrainingStage.Pretraining, training.StageInfo.GetNextStage());

            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 10);
            Assert.Equal(TrainingStage.FineTuning, training.StageInfo.GetNextStage());

            training.SetTrainingStage(TrainingStage.FineTuning, totalSteps: 10);
            Assert.Equal(TrainingStage.Validation, training.StageInfo.GetNextStage());

            training.SetTrainingStage(TrainingStage.Validation, totalSteps: 10);
            Assert.Equal(TrainingStage.Completed, training.StageInfo.GetNextStage());

            training.SetTrainingStage(TrainingStage.Completed, totalSteps: 0);
            Assert.Null(training.StageInfo.GetNextStage());
        }

        [Fact]
        public void TrainingStage_HasNextStage_ReturnsTrueWhenNotCompleted()
        {
            // Arrange
            var training = CreateTestTraining();

            // Act & Assert
            Assert.True(training.HasNextStage());

            training.SetTrainingStage(TrainingStage.Completed, totalSteps: 0);
            Assert.False(training.HasNextStage());
        }

        [Fact]
        public void TrainingStage_AdvanceToNextStage_AdvancesWhenComplete()
        {
            // Arrange
            var training = CreateTestTraining();
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 10);
            training.UpdateStageProgress(10);

            // Act
            bool advanced = training.AdvanceToNextStage(nextStageTotalSteps: 20);

            // Assert
            Assert.True(advanced);
            Assert.Equal(TrainingStage.FineTuning, training.StageInfo.CurrentStage);
            Assert.Equal(0, training.StageInfo.StepsCompleted);
            Assert.Equal(20, training.StageInfo.TotalStepsPlanned);
        }

        [Fact]
        public void TrainingStage_AdvanceToNextStage_FailsWhenIncomplete()
        {
            // Arrange
            var training = CreateTestTraining();
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 10);
            training.UpdateStageProgress(5); // Not complete

            // Act
            bool advanced = training.AdvanceToNextStage();

            // Assert
            Assert.False(advanced);
            Assert.Equal(TrainingStage.Pretraining, training.StageInfo.CurrentStage);
        }

        [Fact]
        public void TrainingStage_AdvanceToNextStage_FailsWhenAlreadyCompleted()
        {
            // Arrange
            var training = CreateTestTraining();
            training.SetTrainingStage(TrainingStage.Completed, totalSteps: 0);

            // Act
            bool advanced = training.AdvanceToNextStage();

            // Assert
            Assert.False(advanced);
            Assert.Equal(TrainingStage.Completed, training.StageInfo.CurrentStage);
        }

        [Fact]
        public void TrainingStage_VerifyCurrentStage_ReturnsFormattedReport()
        {
            // Arrange
            var training = CreateTestTraining();
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 100);
            training.UpdateStageProgress(50);

            // Act
            string report = training.VerifyCurrentStage();

            // Assert
            Assert.Contains("Training Stage Information", report);
            Assert.Contains("Current Stage: Pretraining", report);
            Assert.Contains("Steps Completed: 50", report);
            Assert.Contains("Total Steps Planned: 100", report);
            Assert.Contains("Progress: 50.00%", report);
            Assert.Contains("Stage Status: IN PROGRESS", report);
            Assert.Contains("Remaining Steps: 50", report);
        }

        [Fact]
        public void TrainingStage_VerifyCurrentStage_ShowsNextStageWhenComplete()
        {
            // Arrange
            var training = CreateTestTraining();
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 10);
            training.UpdateStageProgress(10);

            // Act
            string report = training.VerifyCurrentStage();

            // Assert
            Assert.Contains("Stage Status: COMPLETE", report);
            Assert.Contains("Next Stage: FineTuning", report);
            Assert.Contains("Ready to proceed to next stage", report);
        }

        [Fact]
        public void TrainingStage_SaveAndLoadCheckpoint_PreservesStageInfo()
        {
            // Arrange
            var training1 = CreateTestTraining();
            training1.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 100);
            training1.UpdateStageProgress(50);

            string tempPath = Path.Combine(Path.GetTempPath(), $"test_checkpoint_{Guid.NewGuid()}.smnd");

            try
            {
                // Act - Save
                training1.SaveCheckpointWithStage(tempPath);

                // Create new training instance and load
                var training2 = CreateTestTraining();
                training2.LoadCheckpointWithStage(tempPath);

                // Assert
                Assert.Equal(TrainingStage.Pretraining, training2.StageInfo.CurrentStage);
                Assert.Equal(50, training2.StageInfo.StepsCompleted);
                Assert.Equal(100, training2.StageInfo.TotalStepsPlanned);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public void TrainingStage_GetStageProgressPercentage_ReturnsCorrectValue()
        {
            // Arrange
            var training = CreateTestTraining();
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 100);

            // Act & Assert
            training.UpdateStageProgress(0);
            Assert.Equal(0.0, training.StageInfo.GetStageProgressPercentage());

            training.UpdateStageProgress(25);
            Assert.Equal(25.0, training.StageInfo.GetStageProgressPercentage());

            training.UpdateStageProgress(50);
            Assert.Equal(50.0, training.StageInfo.GetStageProgressPercentage());

            training.UpdateStageProgress(100);
            Assert.Equal(100.0, training.StageInfo.GetStageProgressPercentage());

            // Over 100% should cap at 100%
            training.UpdateStageProgress(150);
            Assert.Equal(100.0, training.StageInfo.GetStageProgressPercentage());
        }

        [Fact]
        public void TrainingStage_GetStageProgressPercentage_ReturnsZeroWhenNoStepsPlanned()
        {
            // Arrange
            var training = CreateTestTraining();
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 0);
            training.UpdateStageProgress(50);

            // Act & Assert
            Assert.Equal(0.0, training.StageInfo.GetStageProgressPercentage());
        }

        [Fact]
        public void TrainingStageInfo_StageStartedAt_IsSetCorrectly()
        {
            // Arrange
            var beforeTime = DateTime.UtcNow;
            var training = CreateTestTraining();

            // Act
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 10);
            var afterTime = DateTime.UtcNow;

            // Assert
            Assert.True(training.StageInfo.StageStartedAt >= beforeTime);
            Assert.True(training.StageInfo.StageStartedAt <= afterTime);
        }
    }
}

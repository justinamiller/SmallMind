namespace SmallMind.Tests;

public class LanguageModelTests
{
    [Fact]
    public void Constructor_InitializesModel()
    {
        var model = new LanguageModel(
            embeddingDim: 16,
            hiddenDim: 32,
            vocabSize: 50,
            learningRate: 0.01f
        );
        
        Assert.NotNull(model);
    }

    [Fact]
    public void Train_WithValidData_ReturnsLoss()
    {
        var model = new LanguageModel(
            embeddingDim: 8,
            hiddenDim: 16,
            vocabSize: 50,
            learningRate: 0.01f
        );
        
        string[] trainingData = { "the cat sat", "the dog ran" };
        float loss = model.Train(trainingData);
        
        Assert.True(loss > 0, "Loss should be positive");
        Assert.True(float.IsFinite(loss), "Loss should be a finite number");
    }

    [Fact]
    public void Train_MultipleEpochs_LossDecreases()
    {
        var model = new LanguageModel(
            embeddingDim: 16,
            hiddenDim: 32,
            vocabSize: 50,
            learningRate: 0.01f
        );
        
        string[] trainingData = { 
            "the cat sat on the mat",
            "the dog sat on the log",
            "cats and dogs are pets"
        };
        
        float firstLoss = model.Train(trainingData);
        
        // Train for several more epochs
        float lastLoss = 0;
        for (int i = 0; i < 20; i++)
        {
            lastLoss = model.Train(trainingData);
        }
        
        Assert.True(lastLoss < firstLoss, "Loss should decrease with training");
    }

    [Fact]
    public void Predict_ReturnsText()
    {
        var model = new LanguageModel(
            embeddingDim: 8,
            hiddenDim: 16,
            vocabSize: 50,
            learningRate: 0.01f
        );
        
        string[] trainingData = { "the cat sat" };
        model.Train(trainingData);
        
        string prediction = model.Predict("the", maxTokens: 2);
        Assert.NotNull(prediction);
        Assert.NotEmpty(prediction);
    }

    [Fact]
    public void Train_WithEmptyData_HandlesGracefully()
    {
        var model = new LanguageModel(
            embeddingDim: 8,
            hiddenDim: 16,
            vocabSize: 50,
            learningRate: 0.01f
        );
        
        string[] emptyData = Array.Empty<string>();
        float loss = model.Train(emptyData);
        
        Assert.Equal(0, loss);
    }

    [Fact]
    public void Train_ParallelProcessing_ProducesConsistentResults()
    {
        var model = new LanguageModel(
            embeddingDim: 16,
            hiddenDim: 32,
            vocabSize: 50,
            learningRate: 0.01f
        );
        
        string[] trainingData = Enumerable.Range(0, 100)
            .Select(i => $"sentence number {i}")
            .ToArray();
        
        float loss = model.Train(trainingData);
        
        Assert.True(float.IsFinite(loss), "Parallel training should produce finite loss");
        Assert.True(loss >= 0, "Loss should be non-negative");
    }

    [Fact]
    public void Train_PerformanceBenchmark_CompletesQuickly()
    {
        var model = new LanguageModel(
            embeddingDim: 32,
            hiddenDim: 64,
            vocabSize: 100,
            learningRate: 0.01f
        );
        
        string[] trainingData = {
            "The cat sat on the mat",
            "The dog sat on the log",
            "The bird flew in the sky"
        };
        
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        for (int i = 0; i < 10; i++)
        {
            model.Train(trainingData);
        }
        
        sw.Stop();
        
        // Should complete 10 epochs in under 1 second on modern hardware
        Assert.True(sw.ElapsedMilliseconds < 1000, 
            $"Training should be fast, took {sw.ElapsedMilliseconds}ms");
    }
}

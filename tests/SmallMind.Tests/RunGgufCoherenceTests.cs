using SmallMind.ConsoleApp.Commands;

namespace SmallMind.Tests;

/// <summary>
/// Tests for RunGgufCommand coherence validation, including loop/repetition detectors,
/// lexical diversity threshold, and max repeated token streak.
/// </summary>
public class RunGgufCoherenceTests
{
    private readonly RunGgufCommand _command = new RunGgufCommand();

    // ── Character-level repetition ───────────────────────────────────────────

    [Fact]
    public void ValidateCoherence_NormalEnglish_ReturnsTrue()
    {
        // Typical English sentence with varied vocabulary
        string prompt = "The capital of France is";
        string output = prompt + " Paris, a city known for the Eiffel Tower and world-class cuisine.";
        Assert.True(_command.ValidateCoherence(output, prompt));
    }

    [Fact]
    public void ValidateCoherence_EmptyGenerated_ReturnsFalse()
    {
        string prompt = "Hello";
        Assert.False(_command.ValidateCoherence(prompt, prompt));
    }

    [Fact]
    public void ValidateCoherence_OutputEqualsPrompt_ReturnsFalse()
    {
        // When the model produces no new tokens the decoded output equals the prompt exactly.
        // ValidateCoherence must detect this and return false (no generation).
        string prompt = "What is the capital of France?";
        Assert.False(_command.ValidateCoherence(prompt, prompt));
    }

    [Fact]
    public void ValidateCoherence_OutputShorterThanPrompt_ReturnsFalse()
    {
        // output shorter than prompt → no generation, must fail
        string prompt = "What is the capital of France?";
        string output = "What is the capital";  // truncated
        Assert.False(_command.ValidateCoherence(output, prompt));
    }

    [Fact]
    public void ValidateCoherence_TooShort_ReturnsFalse()
    {
        string prompt = "Hi";
        string output = prompt + " ok";
        Assert.False(_command.ValidateCoherence(output, prompt));
    }

    [Fact]
    public void ValidateCoherence_ExcessiveCharRepetition_ReturnsFalse()
    {
        string prompt = "test";
        string output = prompt + " aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        Assert.False(_command.ValidateCoherence(output, prompt));
    }

    // ── Word-level streak detection ──────────────────────────────────────────

    [Fact]
    public void ValidateCoherence_RepeatedWordStreak_ReturnsFalse()
    {
        string prompt = "Say something";
        // "the" repeated 5 times consecutively
        string output = prompt + " the the the the the cat sat on the mat";
        Assert.False(_command.ValidateCoherence(output, prompt));
    }

    [Fact]
    public void ValidateCoherence_WordStreakBelowThreshold_ReturnsTrue()
    {
        // "the" appears twice in a row, which is below the threshold of 4
        string prompt = "A sample";
        string output = prompt + " the the quick brown fox jumps over the lazy dog";
        Assert.True(_command.ValidateCoherence(output, prompt));
    }

    // ── N-gram loop detection ─────────────────────────────────────────────────

    [Fact]
    public void ValidateCoherence_RepeatedBigram_ReturnsFalse()
    {
        string prompt = "Repeat";
        // "hello world" repeated many times
        string output = prompt + " hello world hello world hello world hello world hello world hello world hello world";
        Assert.False(_command.ValidateCoherence(output, prompt));
    }

    [Fact]
    public void ValidateCoherence_SlightBigramRepetition_ReturnsTrue()
    {
        // "the cat" appears twice, which is fine
        string prompt = "A sentence";
        string output = prompt + " the cat sat on the mat the cat sat by the dog";
        Assert.True(_command.ValidateCoherence(output, prompt));
    }

    // ── Lexical diversity ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateCoherence_LowLexicalDiversity_ReturnsFalse()
    {
        string prompt = "Describe";
        // 15 words but only 2 unique (the, cat) - diversity ~13%
        string output = prompt + " the cat the cat the cat the cat the cat the cat the cat the";
        Assert.False(_command.ValidateCoherence(output, prompt));
    }

    [Fact]
    public void ValidateCoherence_HighLexicalDiversity_ReturnsTrue()
    {
        string prompt = "Tell me";
        string output = prompt + " about the quick brown fox that jumped over an extremely lazy dog near the river";
        Assert.True(_command.ValidateCoherence(output, prompt));
    }

    // ── CountMaxNgramRepetitions helper ──────────────────────────────────────

    [Fact]
    public void CountMaxNgramRepetitions_NoRepetition_ReturnsOne()
    {
        var words = new[] { "the", "quick", "brown", "fox" };
        int result = RunGgufCommand.CountMaxNgramRepetitions(words, 2);
        Assert.Equal(1, result);
    }

    [Fact]
    public void CountMaxNgramRepetitions_TwoConsecutiveBigrams_ReturnsTwo()
    {
        // "a b a b" - bigram "a b" repeated twice
        var words = new[] { "a", "b", "a", "b" };
        int result = RunGgufCommand.CountMaxNgramRepetitions(words, 2);
        Assert.Equal(2, result);
    }

    [Fact]
    public void CountMaxNgramRepetitions_ThreeConsecutiveBigrams_ReturnsThree()
    {
        // "x y x y x y" - bigram "x y" repeated 3 times
        var words = new[] { "x", "y", "x", "y", "x", "y" };
        int result = RunGgufCommand.CountMaxNgramRepetitions(words, 2);
        Assert.Equal(3, result);
    }

    [Fact]
    public void CountMaxNgramRepetitions_Trigram_DetectsRepetition()
    {
        // "a b c a b c a b c"
        var words = new[] { "a", "b", "c", "a", "b", "c", "a", "b", "c" };
        int result = RunGgufCommand.CountMaxNgramRepetitions(words, 3);
        Assert.Equal(3, result);
    }
}

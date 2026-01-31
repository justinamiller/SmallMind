using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SmallMind;

/// <summary>
/// High-performance language model using C# core libraries optimizations:
/// - System.Numerics.Vectors for SIMD operations
/// - Parallel.For for multi-threaded processing
/// - Span<T> and Memory<T> for zero-allocation operations
/// - ArrayPool for memory reuse
/// - Aggressive inlining for hot paths
/// </summary>
public class LanguageModel
{
    private readonly int _embeddingDim;
    private readonly int _hiddenDim;
    private readonly int _vocabSize;
    private readonly float _learningRate;
    
    private readonly Tokenizer _tokenizer;
    private float[] _embeddings = Array.Empty<float>();
    private float[] _hiddenWeights = Array.Empty<float>();
    private float[] _outputWeights = Array.Empty<float>();
    private float[] _hiddenBias = Array.Empty<float>();
    private float[] _outputBias = Array.Empty<float>();
    
    private readonly Random _random;
    private readonly ArrayPool<float> _arrayPool;

    public LanguageModel(int embeddingDim, int hiddenDim, int vocabSize, float learningRate)
    {
        _embeddingDim = embeddingDim;
        _hiddenDim = hiddenDim;
        _vocabSize = vocabSize;
        _learningRate = learningRate;
        
        _tokenizer = new Tokenizer();
        _random = new Random(42);
        _arrayPool = ArrayPool<float>.Shared;
        
        InitializeWeights();
    }

    private void InitializeWeights()
    {
        // Xavier/Glorot initialization for better convergence
        _embeddings = new float[_vocabSize * _embeddingDim];
        _hiddenWeights = new float[_embeddingDim * _hiddenDim];
        _outputWeights = new float[_hiddenDim * _vocabSize];
        _hiddenBias = new float[_hiddenDim];
        _outputBias = new float[_vocabSize];
        
        float embeddingScale = MathF.Sqrt(2.0f / _embeddingDim);
        float hiddenScale = MathF.Sqrt(2.0f / (_embeddingDim + _hiddenDim));
        float outputScale = MathF.Sqrt(2.0f / (_hiddenDim + _vocabSize));
        
        InitializeArray(_embeddings, embeddingScale);
        InitializeArray(_hiddenWeights, hiddenScale);
        InitializeArray(_outputWeights, outputScale);
    }

    private void InitializeArray(float[] array, float scale)
    {
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = ((float)_random.NextDouble() - 0.5f) * 2.0f * scale;
        }
    }

    public float Train(string[] sentences)
    {
        float totalLoss = 0;
        int count = 0;
        
        // Parallel processing for batch training
        var lockObject = new object();
        
        Parallel.ForEach(sentences, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, sentence =>
        {
            var tokens = _tokenizer.Tokenize(sentence);
            if (tokens.Length < 2) return;
            
            float sentenceLoss = 0;
            
            // Use array pool for temporary allocations
            var hiddenActivations = _arrayPool.Rent(_hiddenDim);
            var outputActivations = _arrayPool.Rent(_vocabSize);
            var hiddenGradients = _arrayPool.Rent(_hiddenDim);
            var embeddingGradients = _arrayPool.Rent(_embeddingDim);
            
            try
            {
                // Process each token pair (current -> next)
                for (int i = 0; i < tokens.Length - 1; i++)
                {
                    int currentToken = tokens[i];
                    int nextToken = tokens[i + 1];
                    
                    // Forward pass with SIMD optimization
                    ForwardPass(currentToken, hiddenActivations, outputActivations);
                    
                    // Calculate loss
                    float loss = CrossEntropyLoss(outputActivations, nextToken);
                    sentenceLoss += loss;
                    
                    // Backward pass
                    BackwardPass(currentToken, nextToken, hiddenActivations, outputActivations, 
                               hiddenGradients, embeddingGradients);
                }
                
                lock (lockObject)
                {
                    totalLoss += sentenceLoss;
                    count += tokens.Length - 1;
                }
            }
            finally
            {
                // Return arrays to pool
                _arrayPool.Return(hiddenActivations);
                _arrayPool.Return(outputActivations);
                _arrayPool.Return(hiddenGradients);
                _arrayPool.Return(embeddingGradients);
            }
        });
        
        return count > 0 ? totalLoss / count : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ForwardPass(int tokenId, float[] hidden, float[] output)
    {
        // Get embedding with bounds check
        int embeddingOffset = (tokenId % _vocabSize) * _embeddingDim;
        
        Array.Clear(hidden, 0, _hiddenDim);
        Array.Clear(output, 0, _vocabSize);
        
        // Embedding to hidden layer with SIMD vectorization
        MatMulAddVectorized(
            _embeddings.AsSpan(embeddingOffset, _embeddingDim),
            _hiddenWeights,
            hidden,
            _embeddingDim,
            _hiddenDim
        );
        
        // Add bias and apply ReLU activation
        for (int i = 0; i < _hiddenDim; i++)
        {
            hidden[i] = MathF.Max(0, hidden[i] + _hiddenBias[i]);
        }
        
        // Hidden to output layer with SIMD vectorization
        MatMulAddVectorized(
            hidden.AsSpan(0, _hiddenDim),
            _outputWeights,
            output,
            _hiddenDim,
            _vocabSize
        );
        
        // Add bias
        for (int i = 0; i < _vocabSize; i++)
        {
            output[i] += _outputBias[i];
        }
        
        // Softmax
        Softmax(output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MatMulAddVectorized(ReadOnlySpan<float> input, float[] weights, float[] output, 
                                     int inputSize, int outputSize)
    {
        // Use SIMD vectorization for matrix multiplication
        int vectorSize = Vector<float>.Count;
        int i = 0;
        
        for (int outIdx = 0; outIdx < outputSize; outIdx++)
        {
            int weightOffset = outIdx * inputSize;
            var sum = Vector<float>.Zero;
            
            // Vectorized loop
            for (i = 0; i <= inputSize - vectorSize; i += vectorSize)
            {
                var inputVec = new Vector<float>(input.Slice(i, vectorSize));
                var weightVec = new Vector<float>(weights, weightOffset + i);
                sum += inputVec * weightVec;
            }
            
            // Sum vector elements
            float result = Vector.Dot(sum, Vector<float>.One);
            
            // Handle remaining elements
            for (; i < inputSize; i++)
            {
                result += input[i] * weights[weightOffset + i];
            }
            
            output[outIdx] += result;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Softmax(float[] values)
    {
        float max = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > max) max = values[i];
        }
        
        float sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = MathF.Exp(values[i] - max);
            sum += values[i];
        }
        
        float invSum = 1.0f / sum;
        for (int i = 0; i < values.Length; i++)
        {
            values[i] *= invSum;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CrossEntropyLoss(float[] predictions, int targetToken)
    {
        int token = targetToken % _vocabSize;
        return -MathF.Log(MathF.Max(predictions[token], 1e-7f));
    }

    private void BackwardPass(int currentToken, int nextToken, float[] hidden, float[] output,
                             float[] hiddenGradients, float[] embeddingGradients)
    {
        int targetToken = nextToken % _vocabSize;
        int embeddingOffset = (currentToken % _vocabSize) * _embeddingDim;
        
        Array.Clear(hiddenGradients, 0, _hiddenDim);
        Array.Clear(embeddingGradients, 0, _embeddingDim);
        
        // Output gradient (softmax + cross-entropy derivative)
        var outputGradient = _arrayPool.Rent(_vocabSize);
        try
        {
            Array.Copy(output, outputGradient, _vocabSize);
            outputGradient[targetToken] -= 1.0f;
            
            // Update output weights and bias
            UpdateWeights(hidden, outputGradient, _outputWeights, _outputBias, _hiddenDim, _vocabSize);
            
            // Backprop to hidden layer
            for (int i = 0; i < _hiddenDim; i++)
            {
                float grad = 0;
                for (int j = 0; j < _vocabSize; j++)
                {
                    grad += outputGradient[j] * _outputWeights[j * _hiddenDim + i];
                }
                // ReLU gradient
                hiddenGradients[i] = hidden[i] > 0 ? grad : 0;
            }
            
            // Update hidden weights and bias
            var embedding = _embeddings.AsSpan(embeddingOffset, _embeddingDim);
            UpdateWeightsFromSpan(embedding, hiddenGradients, _hiddenWeights, _hiddenBias, 
                                 _embeddingDim, _hiddenDim);
            
            // Backprop to embeddings
            for (int i = 0; i < _embeddingDim; i++)
            {
                float grad = 0;
                for (int j = 0; j < _hiddenDim; j++)
                {
                    grad += hiddenGradients[j] * _hiddenWeights[j * _embeddingDim + i];
                }
                embeddingGradients[i] = grad;
            }
            
            // Update embeddings
            for (int i = 0; i < _embeddingDim; i++)
            {
                _embeddings[embeddingOffset + i] -= _learningRate * embeddingGradients[i];
            }
        }
        finally
        {
            _arrayPool.Return(outputGradient);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateWeights(float[] input, float[] gradient, float[] weights, float[] bias,
                              int inputSize, int outputSize)
    {
        for (int i = 0; i < outputSize; i++)
        {
            float grad = gradient[i];
            int weightOffset = i * inputSize;
            
            for (int j = 0; j < inputSize; j++)
            {
                weights[weightOffset + j] -= _learningRate * grad * input[j];
            }
            bias[i] -= _learningRate * grad;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateWeightsFromSpan(ReadOnlySpan<float> input, float[] gradient, 
                                       float[] weights, float[] bias, int inputSize, int outputSize)
    {
        for (int i = 0; i < outputSize; i++)
        {
            float grad = gradient[i];
            int weightOffset = i * inputSize;
            
            for (int j = 0; j < inputSize; j++)
            {
                weights[weightOffset + j] -= _learningRate * grad * input[j];
            }
            bias[i] -= _learningRate * grad;
        }
    }

    public string Predict(string input, int maxTokens = 5)
    {
        var tokens = _tokenizer.Tokenize(input);
        if (tokens.Length == 0) return "";
        
        var result = new List<int>(tokens);
        var hidden = new float[_hiddenDim];
        var output = new float[_vocabSize];
        
        for (int i = 0; i < maxTokens; i++)
        {
            int lastToken = result[^1];
            ForwardPass(lastToken, hidden, output);
            
            // Get the token with highest probability
            int predictedToken = 0;
            float maxProb = output[0];
            for (int j = 1; j < _vocabSize; j++)
            {
                if (output[j] > maxProb)
                {
                    maxProb = output[j];
                    predictedToken = j;
                }
            }
            
            result.Add(predictedToken);
        }
        
        return _tokenizer.Detokenize(result.ToArray());
    }
}

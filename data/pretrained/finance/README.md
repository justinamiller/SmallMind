# Finance Sentiment & RAG Pack

## Overview

**Pack ID**: `sm.pretrained.finance.v1`  
**Domain**: Finance  
**Task Types**: Sentiment Analysis + RAG (Retrieval-Augmented Generation)  
**Status**: Stable  
**License**: MIT

This pack provides production-ready finance domain data for validating SmallMind's sentiment analysis and RAG capabilities on financial news, market reports, and economic content.

## Intended Use

- **Classification**: Finance-specific sentiment analysis
- **RAG**: Citation-backed question answering on financial topics
- **Evaluation**: Benchmark model accuracy and RAG quality
- **Regression Testing**: Validate consistent behavior across versions
- **Performance Benchmarking**: Measure inference throughput and retrieval latency

## Not Intended For

This is **not** your proprietary financial data or investment advice. It provides:
- A standardized format for finance sentiment and RAG workflows
- A validation harness for testing pipelines
- Baseline performance metrics
- Educational financial content (not investment recommendations)

For production finance applications, collect domain-specific labeled data and authoritative financial documents relevant to your use case.

## Data Statistics

### Sentiment Analysis
- **Total Samples**: 30
- **Label Distribution**:
  - Positive: 11 samples (37%)
  - Negative: 10 samples (33%)
  - Neutral: 9 samples (30%)
- **Average Text Length**: ~80 characters
- **Domain**: Market news, earnings reports, economic indicators

### RAG Documents
- **Total Documents**: 5
- **Topics**: Market volatility, stock valuation, interest rates, portfolio diversification, risk management
- **Total Words**: ~2,850
- **Format**: Markdown
- **License**: MIT (redistributable educational content)

## Pack Structure

```
finance/
├── manifest.json          # Pack metadata and configuration
├── README.md             # This file
├── task/
│   └── inputs.jsonl      # Sentiment analysis inputs with labels
├── eval/
│   ├── expected.jsonl    # Ground truth for sentiment evaluation
│   └── scoring.md        # Evaluation methodology and thresholds
└── rag/
    ├── documents/        # Curated finance documents (markdown)
    │   ├── market-volatility.md
    │   ├── stock-valuation.md
    │   ├── interest-rates.md
    │   ├── portfolio-diversification.md
    │   └── risk-management.md
    └── index/
        └── metadata.json # Document index and metadata
```

## Data Format

### Sentiment Inputs (`task/inputs.jsonl`)

Each line is a JSON object:
```json
{
  "id": "finance_sentiment_001",
  "task": "finance_sentiment",
  "text": "Stock prices surged after strong earnings report exceeded expectations.",
  "label": "positive"
}
```

**Finance-Specific Labels**:
- `positive`: Bullish sentiment, market gains, positive indicators
- `negative`: Bearish sentiment, market losses, negative indicators
- `neutral`: Balanced reporting without directional bias

### RAG Documents

Documents are stored as Markdown files in `rag/documents/`:
- **market-volatility.md**: VIX, volatility types, risk management
- **stock-valuation.md**: P/E ratios, DCF analysis, valuation methods
- **interest-rates.md**: Fed policy, bonds, yield curve
- **portfolio-diversification.md**: Asset allocation, Modern Portfolio Theory
- **risk-management.md**: Risk types, VaR, hedging strategies

### RAG Index (`rag/index/metadata.json`)

Structured metadata for document retrieval:
```json
{
  "documents": [
    {
      "id": "doc_001",
      "path": "market-volatility.md",
      "title": "Understanding Market Volatility",
      "topics": ["volatility", "VIX", "risk management"],
      "key_concepts": ["Volatility Index (VIX)", "Hedging Strategies"]
    }
  ]
}
```

## Usage Examples

### CLI Usage

```bash
# Run sentiment analysis (deterministic mode)
smallmind pack run finance --deterministic --out artifacts/finance-sentiment.md

# Run RAG Q&A with citations
smallmind pack rag finance --query "What is the VIX?" --out artifacts/finance-rag.md

# Score sentiment results
smallmind pack score finance --run artifacts/finance-sentiment.run.json
```

### API Usage: Sentiment Analysis (C#)

```csharp
using SmallMind.Runtime.PretrainedModels;

// Load the pack
var packPath = "data/pretrained/finance";
var pack = PretrainedPack.Load(packPath);

Console.WriteLine($"Pack: {pack.Manifest.Id}");
Console.WriteLine($"Domain: {pack.Manifest.Domain}");

// Run sentiment analysis
foreach (var sample in pack.Samples)
{
    var sentiment = model.AnalyzeSentiment(sample.Text);
    var scores = model.AnalyzeSentimentWithScores(sample.Text);
    
    Console.WriteLine($"\n[{sample.Id}]");
    Console.WriteLine($"Text: {sample.Text}");
    Console.WriteLine($"True: {sample.Label}, Predicted: {sentiment}");
    Console.WriteLine($"Confidence: {scores[sentiment]:F3}");
}

// Compute accuracy
var scorer = new PackScorer();
var results = scorer.Score(pack, predictions);
Console.WriteLine($"\nOverall Accuracy: {results.Accuracy:F2}%");
```

### API Usage: RAG with Citations (C#)

```csharp
using SmallMind.Rag;

// Load RAG documents
var ragPath = "data/pretrained/finance/rag";
var documentLoader = new DocumentLoader();
var documents = await documentLoader.LoadFromDirectoryAsync(
    Path.Combine(ragPath, "documents")
);

Console.WriteLine($"Loaded {documents.Count} finance documents");

// Initialize RAG engine
var ragEngine = new RagEngine(model, documents);

// Query with citation
var query = "What is the VIX and what does it measure?";
var result = await ragEngine.QueryAsync(query, new RagOptions
{
    TopK = 3,
    IncludeCitations = true,
    MinRelevanceScore = 0.5
});

Console.WriteLine($"Query: {query}");
Console.WriteLine($"Answer: {result.Answer}");
Console.WriteLine("\nCitations:");
foreach (var citation in result.Citations)
{
    Console.WriteLine($"  [{citation.DocumentId}] {citation.DocumentTitle}");
    Console.WriteLine($"  Relevance: {citation.Score:F3}");
    Console.WriteLine($"  Excerpt: {citation.Text}");
}
```

### Loading RAG Index Metadata

```csharp
using System.Text.Json;

var indexPath = "data/pretrained/finance/rag/index/metadata.json";
var json = await File.ReadAllTextAsync(indexPath);
var index = JsonSerializer.Deserialize<RagIndexMetadata>(json);

foreach (var doc in index.Documents)
{
    Console.WriteLine($"{doc.Title} ({doc.Id})");
    Console.WriteLine($"  Topics: {string.Join(", ", doc.Topics)}");
    Console.WriteLine($"  Key Concepts: {string.Join(", ", doc.KeyConcepts)}");
}
```

## Evaluation Metrics

See `eval/scoring.md` for detailed evaluation methodology.

### Sentiment Analysis Metrics
- **Accuracy**: Percentage of correct sentiment predictions
- **Per-Sentiment Precision/Recall**: Performance on positive/negative/neutral
- **Confusion Matrix**: Common misclassification patterns

**Acceptance Thresholds**:
- Untrained baseline: ~33% (random chance)
- Minimum viable: ≥65% (finance text has clearer signals)
- Production ready: ≥80%
- High quality: ≥90%

### RAG Evaluation Metrics
- **Retrieval Precision**: Relevant documents retrieved / total retrieved
- **Retrieval Recall**: Relevant documents retrieved / total relevant
- **Citation Accuracy**: Answers supported by retrieved documents
- **Response Quality**: Factual correctness against source documents

## Deterministic Execution

To ensure reproducible results:
- Set `deterministic: true` in manifest
- Use temperature = 0.0
- Fix random seeds for retrieval ranking
- Disable sampling randomness

**Critical for Finance**: Determinism is often required for:
- Audit trails
- Compliance documentation
- Regulatory reporting
- Reproducible research

## Finance Domain Considerations

Finance sentiment differs from general sentiment:

1. **Magnitude Matters**: "Up 50%" vs "Up 0.5%" have different implications
2. **Context Sensitivity**: "Inflation rises" is negative; "Employment rises" is positive
3. **Terminology**: Finance-specific terms (rally, crash, surge, plummet)
4. **Relative Performance**: "Outperforms" is positive even if absolute value is negative
5. **Time Sensitivity**: Past vs. future tense affects interpretation

## RAG Query Examples

Example queries suited for this pack's documents:

- "What is the VIX and how is it interpreted?"
- "Explain the inverse relationship between bond prices and interest rates"
- "What are the main types of financial risk?"
- "How does diversification reduce portfolio risk?"
- "What is a P/E ratio and how is it used for stock valuation?"
- "Describe the components of a normal yield curve"
- "What is Modern Portfolio Theory?"

## Swapping In Your Data

To use your own finance data:

### For Sentiment Analysis
1. **Convert to JSONL**: Transform your labeled data
2. **Update Manifest**: Modify metadata and statistics
3. **Add Evaluation Data**: Include ground truth
4. **Adjust Thresholds**: Update scoring criteria for your domain

### For RAG
1. **Add Documents**: Replace with your authoritative finance documents
2. **Update Index**: Modify `rag/index/metadata.json` with your document metadata
3. **Verify Licensing**: Ensure you have rights to redistribute documents
4. **Update Topics**: Adjust topic taxonomy for your domain

**Important**: For proprietary financial data:
- Do NOT include confidential information
- Verify compliance with data licensing agreements
- Consider regulatory requirements (GDPR, CCPA, etc.)
- Maintain data lineage and audit trails

## Source and Licensing

### Sentiment Data
- **Origin**: Synthetic data created for demonstration
- **License**: MIT
- **Redistributable**: Yes
- **Attribution**: None required

### RAG Documents
- **Origin**: Educational content created for demonstration
- **License**: MIT
- **Redistributable**: Yes
- **Content**: General finance knowledge, not investment advice
- **Disclaimer**: For educational purposes only, not financial guidance

## Compliance and Disclaimers

**This pack does not provide**:
- Investment advice or recommendations
- Real-time market data
- Proprietary financial analysis
- Regulated financial services

**For production finance applications**:
- Consult with legal and compliance teams
- Verify data licensing and usage rights
- Implement appropriate disclaimers
- Consider regulatory requirements (SEC, FINRA, etc.)
- Maintain audit trails for model decisions

## Related Packs

- **`sentiment`**: General-purpose sentiment analysis
- **`classification`**: Multi-class topic classification

## Support

This is an open-source pack with no paid support. For issues or contributions, see the main SmallMind repository.

**Not a substitute for**: Financial advisors, licensed investment professionals, or regulatory counsel.

# Production-Grade Pretrained Data Packs

## Overview

This directory contains **production-ready, redistributable pretrained data packs** for validating and deploying SmallMind in real systems. These packs provide structured, standardized datasets for:

- **Classification & Extraction**: Sentiment analysis, topic categorization
- **RAG (Retrieval-Augmented Generation)**: Citation-backed question answering
- **Evaluation & Benchmarking**: Model accuracy, performance metrics
- **Regression Testing**: Deterministic validation across model versions

## What These Packs Are

✅ **Redistributable starter assets** for pipeline validation  
✅ **Standardized formats** for your own domain data  
✅ **Evaluation harnesses** with scoring methodology  
✅ **Production workflow examples** (CLI + API)  
✅ **Legally safe** (MIT licensed, synthetic/educational content)

## What These Packs Are NOT

❌ **Not proprietary training data** - bring your own labeled data  
❌ **Not production models** - models require training on real data  
❌ **Not domain-specific solutions** - generic starter content  
❌ **Not "examples" or "demos"** - real, production-grade infrastructure

## Available Packs

### 📦 ITIL v4 Mastery Pack (`itil_v4_mastery/`) — **Flagship Real-World Use Case**
- **ID**: `sm.pretrained.itil_v4_mastery.v1`
- **Type**: Knowledge Pack
- **Tasks**: RAG-based Q&A + Structured Consulting
- **Domain**: IT Service Management (ITIL v4)
- **Content**: 19 original documents, 45 queries
- **Use Cases**: Citation-backed knowledge retrieval, structured consulting guidance, ITSM reference, evaluation benchmarking

**Key Features**:
- ✨ **Production-quality knowledge base** for ITIL v4 best practices
- ✨ **Citation requirements** - all answers must reference corpus documents
- ✨ **Structured JSON output** - consulting recommendations with workflows, KPIs, risks
- ✨ **45 real-world queries** spanning foundational, scenario-based, operational, and governance questions
- ✨ **Comprehensive evaluation harness** with automated scoring
- ✨ **Original content** (MIT licensed, no copyrighted ITIL text)

This pack demonstrates SmallMind's **full RAG capabilities** on realistic, non-toy content designed for actual IT professional use. It's the most comprehensive pack showcasing RAG, citations, structured output, and deterministic execution.

[➡️ See itil_v4_mastery/README.md](itil_v4_mastery/README.md)

### 📦 Sentiment Analysis (`sentiment/`)
- **ID**: `sm.pretrained.sentiment.v1`
- **Task**: 3-class sentiment (positive, negative, neutral)
- **Domain**: General product reviews and feedback
- **Samples**: 30 labeled texts
- **Use Cases**: Classification, evaluation, benchmarking

[➡️ See sentiment/README.md](sentiment/README.md)

### 📦 Topic Classification (`classification/`)
- **ID**: `sm.pretrained.classification.v1`
- **Task**: 4-class topic categorization
- **Categories**: Technology, Sports, Politics, Entertainment
- **Samples**: 30 labeled texts
- **Use Cases**: Classification, evaluation, benchmarking

[➡️ See classification/README.md](classification/README.md)

### 📦 Finance Sentiment & RAG (`finance/`)
- **ID**: `sm.pretrained.finance.v1`
- **Tasks**: Finance sentiment + RAG-backed Q&A
- **Domain**: Financial markets, economic analysis
- **Samples**: 30 labeled texts + 5 curated documents
- **Use Cases**: Classification, RAG, evaluation, citations

[➡️ See finance/README.md](finance/README.md)

## Pack Structure

Each pack follows a consistent structure:

```
{pack-name}/
├── manifest.json          # Pack metadata (ID, domain, tasks, settings)
├── README.md             # Pack-specific documentation
├── task/
│   ├── inputs.jsonl      # Task inputs with ground truth
│   └── categories.json   # (Classification only) Category definitions
├── eval/
│   ├── labels.jsonl      # Ground truth for evaluation
│   └── scoring.md        # Evaluation methodology and thresholds
└── rag/                  # (RAG-enabled packs only)
    ├── documents/        # Curated documents (markdown/txt)
    └── index/
        └── metadata.json # Document index and retrieval metadata
```

## Data Format: JSONL

All task data uses **JSON Lines** (`.jsonl`) format for stable IDs and extensibility:

```jsonl
{"id":"sentiment_001","task":"sentiment","text":"Great product!","label":"positive"}
{"id":"sentiment_002","task":"sentiment","text":"Disappointing.","label":"negative"}
```

**Legacy pipe-delimited format** (`label|text`) is still supported by DatasetLoader for backward compatibility.

## Discovery: Registry

The `registry.json` file lists all available packs:

```json
{
  "packs": [
    {"id": "sm.pretrained.itil_v4_mastery.v1", "path": "itil_v4_mastery"},
    {"id": "sm.pretrained.sentiment.v1", "path": "sentiment"},
    {"id": "sm.pretrained.classification.v1", "path": "classification"},
    {"id": "sm.pretrained.finance.v1", "path": "finance"}
  ]
}
```

Use this for programmatic pack discovery.

## Usage Patterns

### 1. CLI Workflows (Production-Style)

```bash
# Discover available packs
smallmind pack list

# Run pack inference (deterministic)
smallmind pack run sentiment --deterministic --out results/sentiment.md

# Score against ground truth
smallmind pack score sentiment --run results/sentiment.run.json

# RAG query with citations
smallmind pack rag finance --query "What is market volatility?" \
  --out results/finance-rag.md
```

### 2. API Workflows (C#)

```csharp
using SmallMind.Runtime.PretrainedModels;

// Discover packs
var registry = PretrainedRegistry.Load("data/pretrained/registry.json");
foreach (var pack in registry.Packs)
{
    Console.WriteLine($"{pack.Id}: {pack.Name}");
}

// Load a specific pack
var pack = PretrainedPack.Load("data/pretrained/sentiment");
Console.WriteLine($"Loaded {pack.Samples.Count} samples");

// Run inference
foreach (var sample in pack.Samples)
{
    var prediction = model.AnalyzeSentiment(sample.Text);
    Console.WriteLine($"[{sample.Id}] {prediction}");
}

// Evaluate
var scorer = new PackScorer();
var results = scorer.Score(pack, predictions);
Console.WriteLine($"Accuracy: {results.Accuracy:F2}%");
```

### 3. Loading for Training

```csharp
using SmallMind.Runtime.PretrainedModels;

// Load pack data
var samples = DatasetLoader.LoadFromJsonl(
    "data/pretrained/sentiment/task/inputs.jsonl"
);

// Split for training/validation
var (train, val) = DatasetLoader.SplitDataset(
    samples, 
    trainRatio: 0.8, 
    seed: 42
);

// Train your model
// ... (use your training pipeline)
```

## Real Use Cases

### ✅ Classification
Train or fine-tune models for sentiment analysis, topic categorization, intent detection.

### ✅ RAG (Retrieval-Augmented Generation)
Build citation-backed Q&A systems with document retrieval and source attribution.

### ✅ Evaluation & Regression Testing
- Benchmark model accuracy against ground truth
- Validate consistent behavior across model versions
- Track performance regressions

### ✅ Performance Benchmarking
- Measure inference latency (mean, p95, p99)
- Profile memory usage
- Test throughput under load

### ✅ Pipeline Validation
Verify end-to-end workflows: data loading → inference → scoring → reporting

## Deterministic Execution

All packs support **deterministic mode** for reproducible results:

```json
{
  "recommended_settings": {
    "deterministic": true
  }
}
```

When enabled:
- **Same input → same output** (across runs and machines)
- **Temperature = 0.0** (no sampling randomness)
- **Fixed random seeds** (reproducible shuffling/splitting)

Critical for:
- Regression testing
- Compliance and audit trails
- Reproducible research
- Fair benchmarking

## Swapping In Your Own Data

To use your domain-specific data with this infrastructure:

1. **Convert to JSONL**: Transform your labeled data to match pack format
2. **Update Manifest**: Modify `manifest.json` with your metadata
3. **Add Evaluation Data**: Include ground truth in `eval/`
4. **Update Scoring**: Adjust thresholds in `eval/scoring.md` for your domain
5. **Maintain Structure**: Keep folder hierarchy for tool compatibility

This ensures your custom data works seamlessly with SmallMind's pack infrastructure.

## Licensing and Redistribution

- **License**: MIT
- **Origin**: Synthetic/educational content
- **Redistributable**: Yes (no attribution required)
- **Safe for**: Commercial use, modification, distribution

**Important**: Finance pack documents are educational only, not investment advice.

## What These Packs Replace

Previously, this directory contained:
- Pipe-delimited sample files (`label|text`)
- No structured metadata
- No evaluation harnesses
- Described as "examples" and "demonstrations"

Now, these are **production-grade starter assets** with:
- Structured JSONL format with stable IDs
- Manifest-based metadata
- Evaluation methodology and scoring
- Real workflow examples (not demos)

## Extending the Pack Ecosystem

To add a new pack:

1. Create pack directory: `data/pretrained/{domain}/`
2. Add required files: `manifest.json`, `README.md`, `task/`, `eval/`
3. Convert data to JSONL format
4. Write evaluation scoring methodology
5. (Optional) Add RAG documents if applicable
6. Register in `registry.json`
7. Document usage patterns in pack README

See existing packs as templates.

## Support

These packs are open-source with no paid support. For issues or contributions:
- Open an issue in the main SmallMind repository
- Follow contribution guidelines in `CONTRIBUTING.md`

## Next Steps

1. **Explore a pack**: Start with `sentiment/` for simplicity
2. **Read pack README**: Each pack has detailed documentation
3. **Run examples**: Try CLI and API workflows
4. **Swap in your data**: Adapt packs to your domain
5. **Build workflows**: Integrate packs into your pipelines

---

**Remember**: These packs are **redistributable starter assets for validating and deploying SmallMind in real systems**, not toy datasets or demos.

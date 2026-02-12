# Finance Sentiment Analysis Evaluation Scoring

## Correctness Measurement

### Primary Metric: Exact Match Accuracy
- **Definition**: Percentage of predictions that exactly match the ground truth sentiment label
- **Formula**: `accuracy = (correct_predictions / total_predictions) * 100`
- **Range**: 0-100%

### Labels
- `positive`: Bullish sentiment, market gains, positive economic indicators
- `negative`: Bearish sentiment, market losses, negative economic indicators
- `neutral`: Balanced, objective financial reporting without clear directional bias

## Domain-Specific Considerations

Finance sentiment differs from general sentiment:
- **Magnitude Matters**: "Stock rises 50%" vs "Stock rises 0.5%" have different sentiment intensities
- **Context Sensitivity**: "Inflation rises" is negative; "Employment rises" is positive
- **Terminology**: Finance-specific terms (rally, crash, surge, plummet) carry strong sentiment
- **Relative Performance**: "Outperforms expectations" is positive even if absolute numbers are negative

## Acceptance Thresholds

### Untrained Model Baseline
- **Expected Accuracy**: ~33% (random chance for 3 classes)
- **Threshold**: Should not exceed 40% without training

### Trained Model Targets
- **Minimum Viable**: ≥65% accuracy (higher than general sentiment due to clearer signal)
- **Production Ready**: ≥80% accuracy
- **High Quality**: ≥90% accuracy

Note: Financial text typically has clearer sentiment signals than general reviews.

## Determinism Expectations

### Deterministic Mode
When `deterministic: true` is set:
- Same input must produce identical output across runs
- Temperature must be 0.0
- Sampling must use fixed seed
- Results must be reproducible across machines
- Critical for compliance and audit trails in financial applications

### Non-Deterministic Mode
- Results may vary between runs
- Must maintain statistical consistency (accuracy within ±3% across runs)

## Known Edge Cases

1. **Contrarian Indicators**: Negative event with positive market reaction
   - Example: "Layoffs announced, stock surges on cost-cutting optimism"
   - Label should reflect the stated sentiment, not implied market reaction

2. **Sentiment vs. Magnitude**: Strong words describing small changes
   - Example: "Stock skyrockets 0.01%"
   - Consider both the language and the actual numbers

3. **Future vs. Present**: Predictions vs. current state
   - Example: "Market strong today, but analysts predict downturn"
   - Label should reflect the primary temporal focus

4. **Multi-Asset Statements**: Mixed sentiment across different securities
   - Example: "Stocks fall while bonds rally"
   - Classify based on the primary asset mentioned first

5. **Technical Jargon**: Finance-specific terms may be ambiguous
   - Example: "Correction" is negative; "Recovery" is positive
   - Requires domain knowledge

## Evaluation Process

1. Load ground truth labels from `eval/expected.jsonl`
2. Run model inference on each input text (field: `text`)
3. Compare predicted sentiment against ground truth (field: `label`)
4. Calculate accuracy: `correct / total`
5. Generate per-sentiment precision, recall, F1 scores
6. Create confusion matrix to identify systematic errors

## Quality Checks

- **Per-Sentiment Accuracy**: Each sentiment should have ≥60% accuracy minimum
- **Positive/Negative Balance**: Ensure model isn't biased toward bullish or bearish
- **Neutral Precision**: Verify "neutral" isn't used as a catch-all for uncertainty
- **Confidence Calibration**: High-confidence predictions should correlate with accuracy
- **Performance**: Track inference latency and memory usage

## RAG Evaluation (If Enabled)

When using RAG mode with financial documents:
- **Citation Accuracy**: Verify sentiment claims are supported by retrieved documents
- **Source Relevance**: Retrieved documents should be topically relevant
- **Attribution**: Model should cite specific passages supporting the sentiment classification

## Scoring Implementation

Use exact string matching (case-insensitive):
```
predicted_label.ToLower() == ground_truth_label.ToLower()
```

No partial credit. No fuzzy matching.

## Regulatory & Compliance Notes

Financial sentiment analysis may have regulatory implications:
- **Determinism Required**: Many use cases require reproducible results for audits
- **Explainability**: Consider tracking which tokens/phrases drove the sentiment prediction
- **Bias Detection**: Monitor for systematic bias that could affect trading decisions
- **Data Lineage**: Maintain clear records of data sources and model versions

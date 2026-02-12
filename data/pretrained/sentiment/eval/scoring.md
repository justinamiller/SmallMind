# Sentiment Analysis Evaluation Scoring

## Correctness Measurement

### Primary Metric: Exact Match Accuracy
- **Definition**: Percentage of predictions that exactly match the ground truth label
- **Formula**: `accuracy = (correct_predictions / total_predictions) * 100`
- **Range**: 0-100%

### Labels
- `positive`: Expressions of satisfaction, approval, or positive emotion
- `negative`: Expressions of dissatisfaction, disapproval, or negative emotion
- `neutral`: Balanced, objective statements without strong sentiment

## Acceptance Thresholds

### Untrained Model Baseline
- **Expected Accuracy**: ~33% (random chance for 3 classes)
- **Threshold**: Should not exceed 40% without training (indicates overfitting to test set)

### Trained Model Targets
- **Minimum Viable**: ≥60% accuracy
- **Production Ready**: ≥75% accuracy
- **High Quality**: ≥85% accuracy

## Determinism Expectations

### Deterministic Mode
When `deterministic: true` is set:
- Same input must produce identical output across runs
- Temperature must be 0.0
- Sampling must use fixed seed
- Results must be reproducible across machines

### Non-Deterministic Mode
- Results may vary between runs
- Must still maintain statistical consistency (accuracy within ±3% across multiple runs)

## Known Edge Cases

1. **Mixed Sentiment**: Texts with both positive and negative elements
   - Example: "Great product but terrible customer service"
   - Expected: Model may struggle; consider most dominant sentiment

2. **Sarcasm/Irony**: Not represented in this dataset
   - Current dataset is literal sentiment only
   - Sarcastic inputs will likely be misclassified

3. **Short Texts**: Very brief statements (< 5 words)
   - May lack sufficient context
   - Higher variance in predictions expected

4. **Domain Specificity**: This is general product review sentiment
   - May not transfer well to other domains without fine-tuning
   - See `finance` pack for domain-specific sentiment

## Evaluation Process

1. Load ground truth labels from `eval/labels.jsonl`
2. Run model inference on each input text (field: `text`)
3. Compare predicted label against ground truth (field: `label`)
4. Calculate accuracy: `correct / total`
5. Report per-class accuracy and confusion matrix for detailed analysis

## Quality Checks

- **Label Distribution**: Verify predictions aren't biased toward one class
- **Confidence Scores**: If available, check calibration (high confidence should correlate with accuracy)
- **Inference Time**: Track mean/p95/p99 latency per sample
- **Memory Usage**: Monitor peak memory during batch evaluation

## Scoring Implementation

Use exact string matching (case-insensitive):
```
predicted_label.ToLower() == ground_truth_label.ToLower()
```

No partial credit. No fuzzy matching.

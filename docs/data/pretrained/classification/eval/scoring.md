# Text Classification Evaluation Scoring

## Correctness Measurement

### Primary Metric: Exact Match Accuracy
- **Definition**: Percentage of predictions that exactly match the ground truth category
- **Formula**: `accuracy = (correct_predictions / total_predictions) * 100`
- **Range**: 0-100%

### Categories
- `Technology`: Technology news, software, hardware, AI, cybersecurity
- `Sports`: Sports events, championships, athletes, teams
- `Politics`: Government, elections, legislation, policy
- `Entertainment`: Movies, music, celebrities, streaming

## Acceptance Thresholds

### Untrained Model Baseline
- **Expected Accuracy**: ~25% (random chance for 4 classes)
- **Threshold**: Should not exceed 35% without training

### Trained Model Targets
- **Minimum Viable**: ≥55% accuracy
- **Production Ready**: ≥70% accuracy
- **High Quality**: ≥85% accuracy

## Determinism Expectations

### Deterministic Mode
When `deterministic: true` is set:
- Identical input → identical output across all runs
- Temperature = 0.0
- Fixed random seed for any sampling
- Reproducible on any machine with same model

### Non-Deterministic Mode
- Results may vary between runs
- Must maintain statistical consistency (±3% accuracy variance)

## Known Edge Cases

1. **Multi-Topic Articles**: Content spanning multiple categories
   - Example: "Tech company sponsors sports team for championship"
   - Expected: Classify based on primary topic (dominant theme)

2. **Emerging Topics**: Content about new domains not well-represented
   - May default to closest category
   - Model uncertainty may be high

3. **Ambiguous Language**: Generic statements applicable to multiple categories
   - Example: "Record-breaking achievement announced"
   - Context is critical; may require longer input

4. **Category Overlap**: Technology in entertainment, politics in sports, etc.
   - Expected: Some legitimate disagreement on borderline cases
   - Aim for >90% inter-annotator agreement on clear examples

## Evaluation Process

1. Load ground truth labels from `eval/labels.jsonl`
2. Run model inference on each input text (field: `text`)
3. Compare predicted category against ground truth (field: `label`)
4. Calculate overall accuracy: `correct / total`
5. Generate per-category precision, recall, F1 scores
6. Create confusion matrix to identify systematic errors

## Quality Checks

- **Per-Category Accuracy**: Each category should have ≥50% accuracy minimum
- **Balanced Predictions**: Verify model doesn't over-predict majority class
- **Confusion Patterns**: Identify if certain category pairs are frequently confused
- **Confidence Distribution**: High-confidence predictions should have higher accuracy
- **Performance Metrics**: Track inference latency and memory usage

## Scoring Implementation

Use exact string matching (case-insensitive):
```
predicted_category.ToLower() == ground_truth_category.ToLower()
```

No partial credit. No fuzzy matching. No synonym expansion.

## Category Validation

Before evaluation, ensure all predictions map to one of the four valid categories:
- Technology
- Sports
- Politics
- Entertainment

Invalid predictions (out-of-vocabulary) count as incorrect.

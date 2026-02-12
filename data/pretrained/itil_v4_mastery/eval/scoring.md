# ITIL v4 Mastery Pack - Scoring Methodology

## Overview

This document defines the scoring rules and evaluation methodology for the ITIL v4 Mastery Pack queries. Scoring emphasizes citation accuracy, content correctness, and practical applicability.

## Task Types

### 1. Document Q&A (docqa)

**Purpose**: Answer factual questions using the RAG corpus

**Scoring Dimensions**:
- **Citation Presence** (30 points): Answer includes required citations
- **Citation Relevance** (20 points): Citations actually support the answer
- **Correctness** (30 points): Answer is factually accurate per corpus
- **Completeness** (20 points): Answer addresses all parts of question

**Citation Requirements**:
- Minimum citations as specified in query `expected_citations_min`
- Citations must reference actual document IDs from corpus
- Citation text should be relevant to question topic

**Scoring Rubric**:

| Score | Citation | Relevance | Correctness | Completeness |
|-------|----------|-----------|-------------|--------------|
| **Excellent (90-100)** | All required citations present | All citations highly relevant | 100% accurate | Comprehensive |
| **Good (75-89)** | Required citations present | Most citations relevant | Minor inaccuracies | Addresses main points |
| **Acceptable (60-74)** | Some citations missing | Some relevant citations | Some inaccuracies | Partial answer |
| **Poor (<60)** | Few/no citations | Irrelevant citations | Significant errors | Incomplete |

**Automated Checks**:
1. Count citations in response
2. Verify cited document IDs exist in corpus
3. Check for keyword matches between citation and question topic

**Example**:

*Question*: "What is the difference between incident and problem management?"

*Good Answer*:
"Incident management focuses on restoring service quickly [citing: 060_incident_management.md], while problem management identifies root causes to prevent recurrence [citing: 070_problem_management.md]. Incidents trigger immediate action; problems require analytical investigation."

*Poor Answer*:
"They're different. One fixes things, the other prevents them." (No citations, vague)

### 2. Structured Consultation (structured_consult)

**Purpose**: Provide actionable consulting guidance with structured output

**Scoring Dimensions**:
- **JSON Schema Compliance** (25 points): Response matches required JSON structure
- **Citation Quality** (25 points): Recommendations cite relevant corpus documents
- **Practical Applicability** (25 points): Advice is specific and actionable
- **Completeness** (25 points): All required JSON fields populated meaningfully

**Required JSON Schema**:
```json
{
  "summary": "Executive summary of consultation (2-3 sentences)",
  "recommended_practices": ["Practice 1", "Practice 2", ...],
  "workflow": [
    {
      "step": "Step name",
      "owner": "Role responsible",
      "inputs": ["Input 1", ...],
      "outputs": ["Output 1", ...]
    }
  ],
  "kpis": [
    {
      "name": "KPI name",
      "definition": "What it measures",
      "target": "Target value",
      "cadence": "How often measured"
    }
  ],
  "risks_and_pitfalls": ["Risk 1", "Risk 2", ...],
  "next_actions_30_days": ["Action 1", "Action 2", ...],
  "citations": [
    {
      "doc_id": "060_incident_management.md",
      "why": "Supports incident response workflow design"
    }
  ]
}
```

**Scoring Rubric**:

| Score | Schema | Citations | Practicality | Completeness |
|-------|--------|-----------|--------------|--------------|
| **Excellent (90-100)** | Perfect JSON | 3+ relevant citations | Immediately actionable | All fields meaningful |
| **Good (75-89)** | Valid JSON, minor gaps | 2 relevant citations | Mostly actionable | Most fields populated |
| **Acceptable (60-74)** | Valid JSON | 1 citation or weak citations | Somewhat generic | Some fields missing |
| **Poor (<60)** | Invalid JSON or missing | No citations | Too abstract | Many fields missing/empty |

**Automated Checks**:
1. Parse response as JSON (validate structure)
2. Check all required fields present
3. Verify `citations` array has ≥ `expected_citations_min` entries
4. Verify cited `doc_id` values exist in corpus
5. Count `recommended_practices`, `kpis`, `risks_and_pitfalls`, `next_actions_30_days` (should have ≥2 each)

**Example**:

*Question*: "We have high change failure rate. Propose an improvement plan."

*Good Answer*:
```json
{
  "summary": "Implement risk-based change categorization with automated testing for standard changes and enhanced CAB review for high-risk changes to reduce failure rate from 15% to <5%.",
  "recommended_practices": ["Change Enablement", "Release Management", "Service Validation and Testing"],
  "workflow": [
    {
      "step": "Risk Assessment",
      "owner": "Change Manager",
      "inputs": ["RFC details", "Historical failure data"],
      "outputs": ["Risk score", "Required approvals"]
    }
  ],
  "kpis": [
    {
      "name": "Change Success Rate",
      "definition": "% changes completed without failure or backout",
      "target": ">95%",
      "cadence": "Weekly"
    }
  ],
  "risks_and_pitfalls": ["Resistance to additional approval steps", "Testing may slow deployment"],
  "next_actions_30_days": ["Audit last 50 changes to identify failure patterns", "Define standard change criteria"],
  "citations": [
    {
      "doc_id": "080_change_enablement.md",
      "why": "Provides change type definitions and risk assessment framework"
    },
    {
      "doc_id": "170_metrics_kpis_okrs.md",
      "why": "Defines change success rate KPI and targets"
    }
  ]
}
```

### 3. Classification (classification)

**Purpose**: Categorize scenarios into appropriate ITIL practices or components

**Scoring Dimensions**:
- **Accuracy** (50 points): Classification is correct
- **Justification** (30 points): Reasoning cites relevant concepts
- **Citation** (20 points): References corpus document

**Automated Checks**:
1. Compare predicted category to expected category
2. Check for citation presence
3. Keyword match between justification and expected category

## Cross-Cutting Requirements

### Citation Validity

**All citations must**:
- Reference an existing document ID from the corpus (documents in `rag/documents/`)
- Be relevant to the question topic (checked via keyword overlap)
- Include reasoning for why the citation supports the answer (for `structured_consult`)

**Valid Document IDs**:
- 001_foundations.md
- 010_service_value_system.md
- 020_guiding_principles.md
- 030_four_dimensions.md
- 040_service_value_chain.md
- 050_practices_overview.md
- 060_incident_management.md
- 070_problem_management.md
- 080_change_enablement.md
- 090_service_request_management.md
- 100_service_catalog_management.md
- 110_service_level_management.md
- 120_monitoring_and_event_management.md
- 130_release_management.md
- 140_configuration_management.md
- 150_knowledge_management.md
- 160_continual_improvement.md
- 170_metrics_kpis_okrs.md
- 180_common_pitfalls_anti_patterns.md
- 190_mappings_itil_to_ops.md

### Keyword-Based Correctness Checks

For automated evaluation, responses are checked for presence of expected keywords based on question topic:

**Incident Management Questions**: Must mention "restore service", "MTTR", "prioritization", "escalation"

**Problem Management Questions**: Must mention "root cause", "known error", "prevent", "recurrence"

**Change Enablement Questions**: Must mention "RFC", "authorization", "risk assessment", "CAB"

**Guiding Principles Questions**: Must mention at least 3 of the 7 principles by name

**Service Value Chain Questions**: Must mention at least 4 of the 6 activities

## Acceptance Thresholds

### Per-Query Thresholds

- **Minimum Passing Score**: 60/100
- **Good Score**: 75/100
- **Excellent Score**: 90/100

### Pack-Level Thresholds

- **Overall Accuracy**: ≥70% of queries score ≥60
- **High-Quality Responses**: ≥40% of queries score ≥75
- **Citation Compliance**: ≥90% of responses include required minimum citations

### Deterministic Execution

**For reproducibility**:
- Temperature = 0.0
- Fixed random seeds
- Top-K = 3 (consistent retrieval)
- No sampling randomness

**Validation**: Running the same query twice should produce identical results.

## Reporting Format

### Per-Query Report

```
Query ID: itil_q001
Task: docqa
Question: "What is the difference between incident management and problem management?"

Score: 85/100
- Citation Presence: 30/30 ✓
- Citation Relevance: 18/20
- Correctness: 25/30
- Completeness: 12/20

Citations Found: 2 (required: 2)
- 060_incident_management.md ✓
- 070_problem_management.md ✓

Issues:
- Missing discussion of handoff workflow between practices
```

### Pack-Level Report

```
ITIL v4 Mastery Pack Evaluation Report

Total Queries: 45
Passing Queries: 38 (84%)
Average Score: 77/100

By Task Type:
- docqa: 30 queries, 78 avg score
- structured_consult: 12 queries, 73 avg score
- classification: 3 queries, 82 avg score

Citation Compliance: 42/45 (93%)
JSON Schema Compliance: 11/12 (92%)

Top Performing Topics:
1. Guiding Principles: 88 avg score
2. Service Value System: 82 avg score

Areas for Improvement:
1. Complex integration questions: 68 avg score
2. Multi-practice workflows: 71 avg score
```

## Continuous Improvement

Evaluation results should feed back into:
1. **Corpus Enhancement**: Add content for low-scoring topics
2. **Query Refinement**: Clarify ambiguous questions
3. **Retrieval Tuning**: Adjust top-K, chunking, relevance thresholds
4. **Expected Answer Updates**: Refine `expected.jsonl` based on learnings

## Summary

Scoring emphasizes practical applicability, citation-backed answers, and compliance with output schemas. The methodology supports both automated checks (citations, JSON structure, keywords) and human judgment (relevance, applicability). Results drive continuous improvement of corpus, queries, and retrieval parameters.

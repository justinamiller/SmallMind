# ITIL v4 Mastery Pack

## Overview

**Pack ID**: `sm.pretrained.itil_v4_mastery.v1`  
**Type**: Knowledge Pack  
**Domain**: ITIL v4 / IT Service Management  
**Status**: Stable  
**License**: MIT

This pack provides a comprehensive, production-ready knowledge base for ITIL v4 (Information Technology Infrastructure Library version 4) concepts, practices, and implementation guidance. It demonstrates SmallMind's full RAG capabilities with citation-backed answers, deterministic execution, structured output, and comprehensive evaluation.

## Intended Use

- **RAG (Retrieval-Augmented Generation)**: Query ITIL v4 knowledge with document citations
- **Consulting Guidance**: Get structured, actionable ITSM recommendations
- **Learning and Reference**: Understand ITIL v4 concepts and best practices
- **Evaluation and Benchmarking**: Test RAG quality, citation accuracy, structured output
- **Deterministic Validation**: Reproducible results for compliance and testing

## Key Features

✅ **19 Original ITIL v4 Documents**: Foundations, Service Value System, practices, metrics, anti-patterns  
✅ **45 Real-World Queries**: Foundational, scenario-based, operational, governance questions  
✅ **Citation Requirements**: All answers must reference corpus documents  
✅ **Structured Output**: JSON-formatted consulting guidance with workflows, KPIs, risks  
✅ **Evaluation Harness**: Automated scoring with keyword checks, citation validation, schema compliance  
✅ **Deterministic Mode**: Temperature=0, reproducible results  
✅ **No Copyrighted Content**: All documents are original work, MIT licensed

## Not Official ITIL Content

**Important**: This pack contains educational content authored specifically for SmallMind. It is NOT:
- Official ITIL publications or training material
- Endorsed by AXELOS (ITIL trademark owner)
- A substitute for ITIL certification programs
- Copyrighted ITIL text

For authoritative ITIL information, consult official AXELOS publications. See [PROVENANCE.md](./PROVENANCE.md) for details.

## Pack Structure

```
itil_v4_mastery/
├── manifest.json              # Pack metadata and settings
├── README.md                  # This file
├── PROVENANCE.md             # Content licensing and origin
├── rag/
│   ├── documents/            # 19 ITIL v4 knowledge documents (markdown)
│   │   ├── 001_foundations.md
│   │   ├── 010_service_value_system.md
│   │   ├── 020_guiding_principles.md
│   │   ├── 030_four_dimensions.md
│   │   ├── 040_service_value_chain.md
│   │   ├── 050_practices_overview.md
│   │   ├── 060_incident_management.md
│   │   ├── 070_problem_management.md
│   │   ├── 080_change_enablement.md
│   │   ├── 090_service_request_management.md
│   │   ├── 100_service_catalog_management.md
│   │   ├── 110_service_level_management.md
│   │   ├── 120_monitoring_and_event_management.md
│   │   ├── 130_release_management.md
│   │   ├── 140_configuration_management.md
│   │   ├── 150_knowledge_management.md
│   │   ├── 160_continual_improvement.md
│   │   ├── 170_metrics_kpis_okrs.md
│   │   ├── 180_common_pitfalls_anti_patterns.md
│   │   └── 190_mappings_itil_to_ops.md
│   └── index/                # (Runtime-built or config files for indexing)
├── task/
│   └── queries.jsonl         # 45 queries across difficulty levels
├── eval/
│   ├── expected.jsonl        # Expected keywords, practices, quality scores
│   └── scoring.md            # Evaluation methodology and rubrics
└── scenarios/
    ├── docqa.json            # Document Q&A scenario configuration
    └── structured_consult.json  # Structured consulting scenario configuration
```

## Document Topics

### Foundational Concepts (001-050)
- ITIL v4 Foundations and evolution from v3
- Service Value System (SVS)
- Seven Guiding Principles
- Four Dimensions of Service Management
- Service Value Chain (six activities)
- 34 Practices Overview

### Core Practices (060-160)
- **Incident Management**: Restore service quickly, MTTR, major incidents
- **Problem Management**: Root cause analysis, known errors, prevention
- **Change Enablement**: RFC, CAB, standard/normal/emergency changes
- **Service Request Management**: Fulfillment, self-service, catalog integration
- **Service Catalog Management**: Service offerings, customer-facing catalog
- **Service Level Management**: SLAs, OLAs, UCs, targets
- **Monitoring and Event Management**: Proactive detection, alerting
- **Release Management**: Packaging changes, deployment coordination
- **Configuration Management**: CMDB, CI relationships, dependencies
- **Knowledge Management**: Knowledge base, article lifecycle, reuse
- **Continual Improvement**: Improvement model, opportunity identification

### Operational Guidance (170-190)
- **Metrics, KPIs, OKRs**: ITSM measurement, leading/lagging indicators
- **Common Pitfalls and Anti-Patterns**: Checkbox compliance, process theater, tool-first
- **Mappings to Operations**: DevOps, SRE, cloud, traditional IT scenarios

## Query Coverage

**45 Queries** across multiple dimensions:

### By Difficulty
- **Foundational** (15): "What is the Service Value System?" "Explain the seven guiding principles"
- **Scenario-Based** (12): "High change failure rate—propose improvement plan"
- **Operational** (10): "How to prioritize problems?" "What KPIs for incident management?"
- **Governance** (8): "Design SLA structure" "How does governance fit in SVS?"

### By Task Type
- **docqa** (33): Answer questions with citations
- **structured_consult** (12): Provide JSON-formatted consulting guidance

### By Topic
- Guiding Principles (7)
- Service Value System/Chain (5)
- Incident Management (6)
- Problem Management (4)
- Change Enablement (5)
- Other Practices (10)
- Metrics/Improvement (5)
- Integration (DevOps, SRE, etc.) (3)

## Data Format

### Task Queries (`task/queries.jsonl`)

Each line is a JSON object:
```json
{
  "id": "itil_q001",
  "task": "docqa",
  "question": "What is the difference between incident and problem management?",
  "expected_citations_min": 2,
  "tags": ["incident", "problem", "practices"]
}
```

**Fields**:
- `id`: Unique query identifier
- `task`: `docqa` | `structured_consult` | `classification`
- `question`: The question text
- `expected_citations_min`: Minimum citations required in answer
- `tags`: Topic tags for categorization

### Expected Results (`eval/expected.jsonl`)

Each line defines evaluation criteria:
```json
{
  "id": "itil_q001",
  "expected_keywords": ["restore", "service", "quickly", "root cause", "prevent"],
  "expected_practices": ["incident_management", "problem_management"],
  "min_quality_score": 70
}
```

For `structured_consult` tasks:
```json
{
  "id": "itil_q018",
  "required_json_fields": ["summary", "recommended_practices", "workflow", "kpis", "citations"],
  "expected_practices": ["change_enablement"],
  "min_quality_score": 70
}
```

### Scenario Configuration (`scenarios/*.json`)

See [scenarios/docqa.json](./scenarios/docqa.json) and [scenarios/structured_consult.json](./scenarios/structured_consult.json) for full configuration including:
- Retrieval settings (top_k, min_relevance_score)
- Generation settings (temperature, max_tokens, deterministic)
- Citation requirements
- Output format (markdown or JSON schema)

## Usage Examples

### CLI Usage

```bash
# Run document Q&A scenario (deterministic)
smallmind pack run itil_v4_mastery --scenario docqa --deterministic --out artifacts/itil_docqa.md

# Run structured consulting scenario
smallmind pack run itil_v4_mastery --scenario structured_consult --deterministic --out artifacts/itil_consult.md

# Score results
smallmind pack score itil_v4_mastery --run artifacts/itil_docqa.run.json --out artifacts/itil_score.md
```

### API Usage (C#)

```csharp
using SmallMind.Runtime.PretrainedModels;
using SmallMind.Rag;

// Load the pack
var packPath = "data/pretrained/itil_v4_mastery";
var pack = PretrainedPack.Load(packPath);

Console.WriteLine($"Pack: {pack.Manifest.Id}");
Console.WriteLine($"Documents: {pack.Manifest.Rag.DocumentCount}");
Console.WriteLine($"Queries: {pack.Manifest.Tasks.TotalQueries}");

// Load RAG documents
var documentLoader = new DocumentLoader();
var documents = await documentLoader.LoadFromDirectoryAsync(
    Path.Combine(packPath, "rag/documents")
);

// Initialize RAG pipeline
var ragPipeline = new RagPipelineBuilder()
    .WithDocuments(documents)
    .WithChunking(maxChunkSize: 512, overlap: 64)
    .WithRetrieval(topK: 3, minRelevanceScore: 0.5)
    .WithCitations(required: true)
    .Build();

// Query with citation
var query = "What is the difference between incident and problem management?";
var result = await ragPipeline.QueryAsync(query);

Console.WriteLine($"Query: {query}");
Console.WriteLine($"Answer: {result.Answer}");
Console.WriteLine($"\nCitations:");
foreach (var citation in result.Citations)
{
    Console.WriteLine($"  [{citation.DocumentId}] - {citation.Snippet}");
}
```

### Querying Specific Topics

```csharp
// Query about guiding principles
var principlesQuery = "What are the seven guiding principles and how do they work together?";
var principlesResult = await ragPipeline.QueryAsync(principlesQuery);

// Query about change management
var changeQuery = "What are the three types of changes and when is each appropriate?";
var changeResult = await ragPipeline.QueryAsync(changeQuery);

// Structured consulting query
var consultQuery = "We have a 15% change failure rate. Propose an improvement plan.";
var consultResult = await ragPipeline.QueryAsync(consultQuery, new RagOptions
{
    OutputFormat = OutputFormat.Json,
    JsonSchema = structuredConsultSchema,
    RequiredCitations = 2
});

// Parse structured JSON response
var consultGuidance = JsonSerializer.Deserialize<ConsultingGuidance>(consultResult.Answer);
Console.WriteLine($"Summary: {consultGuidance.Summary}");
Console.WriteLine($"Recommended Practices: {string.Join(", ", consultGuidance.RecommendedPractices)}");
```

## Evaluation Metrics

See [eval/scoring.md](./eval/scoring.md) for detailed methodology.

### Document Q&A (docqa)
- **Citation Presence** (30%): Required citations included
- **Citation Relevance** (20%): Citations support answer
- **Correctness** (30%): Factually accurate per corpus
- **Completeness** (20%): Addresses all parts of question

**Passing Score**: ≥60/100  
**Good Score**: ≥75/100  
**Excellent Score**: ≥90/100

### Structured Consultation (structured_consult)
- **JSON Schema Compliance** (25%): Valid structure
- **Citation Quality** (25%): Recommendations cite documents
- **Practical Applicability** (25%): Specific and actionable
- **Completeness** (25%): All fields meaningful

**Required JSON Fields**:
- `summary`: Executive summary
- `recommended_practices`: ITIL practices to implement
- `workflow`: Step-by-step process (step, owner, inputs, outputs)
- `kpis`: Metrics (name, definition, target, cadence)
- `risks_and_pitfalls`: What to watch for
- `next_actions_30_days`: Immediate next steps
- `citations`: Document references with reasoning

### Pack-Level Targets
- **Overall Accuracy**: ≥70% queries score ≥60
- **High-Quality Responses**: ≥40% queries score ≥75
- **Citation Compliance**: ≥90% include required citations
- **JSON Schema Compliance**: 100% for structured_consult tasks

## Deterministic Execution

To ensure reproducible results:

**Recommended Settings** (from manifest.json):
```json
{
  "context_tokens": 8192,
  "chunk_size": 512,
  "chunk_overlap": 64,
  "top_k": 3,
  "min_relevance_score": 0.5,
  "deterministic": true,
  "temperature": 0.0
}
```

**Validation**: Running the same query twice should produce identical output (same citations, same answer text, same JSON structure).

## Extending the Pack

### Adding New Documents

1. Create new `.md` file in `rag/documents/` (e.g., `200_your_topic.md`)
2. Follow existing document structure:
   - Purpose statement
   - Key concepts with definitions
   - Do's and Don'ts
   - Practical guidance
   - Cross-links to related documents
   - Common Q&A section
3. Update `manifest.json` document count
4. Add cross-references from existing documents
5. Test retrieval with related queries

### Adding New Queries

1. Add line to `task/queries.jsonl`:
   ```json
   {"id":"itil_q046","task":"docqa","question":"Your question?","expected_citations_min":1,"tags":["topic"]}
   ```
2. Add expected results to `eval/expected.jsonl`:
   ```json
   {"id":"itil_q046","expected_keywords":["keyword1","keyword2"],"min_quality_score":70}
   ```
3. Update `manifest.json` total_queries count
4. Test query and validate citations

### Customizing for Your Organization

Replace documents with your internal ITSM guidance:
1. Keep the same file naming convention (001-190)
2. Maintain markdown structure for consistent chunking
3. Update manifest metadata (domain, statistics)
4. Adjust retrieval settings based on document lengths
5. Create organization-specific queries
6. Update evaluation criteria to match your standards

**Important**: If using proprietary content, update license info and don't redistribute publicly.

## Limitations

This pack provides:
✅ Educational ITIL v4 summaries and practical guidance  
✅ Citation-backed answers from curated corpus  
✅ Structured consulting recommendations

This pack does NOT provide:
❌ Official ITIL certification preparation  
❌ Authoritative ITIL publications (use AXELOS sources)  
❌ Organization-specific implementation guidance  
❌ Legal or regulatory compliance advice

## Performance Characteristics

**Tested On**:
- .NET 10 Runtime
- CPU-only execution (no GPU required)
- Typical query response time: 1-3 seconds (including retrieval + generation)

**Corpus Statistics**:
- 19 documents
- ~50,000 words total
- Average document length: ~2,600 words
- Chunk size: 512 characters (with 64-character overlap)
- Estimated chunks: ~400 (varies by chunking strategy)

**Retrieval Performance**:
- Top-K=3: Returns 3 most relevant chunks
- Min relevance score: 0.5 (adjustable)
- Typical retrieval time: <100ms
- Hybrid retrieval (BM25 + semantic) recommended

## Source and Licensing

### Content
- **Origin**: Original educational content authored for SmallMind
- **License**: MIT License
- **Redistributable**: Yes
- **Attribution**: None required (but appreciated)

### ITIL Trademark
ITIL® is a registered trademark of AXELOS Limited. This pack is an independent educational resource, not endorsed by or affiliated with AXELOS.

### Contributions
Contributions welcome! See [CONTRIBUTING.md](../../CONTRIBUTING.md) in the main repository.

## Related Packs

- **`finance`**: Finance sentiment + RAG pack (demonstrates similar RAG capabilities)
- **`classification`**: Topic classification pack
- **`sentiment`**: General sentiment analysis pack

## Support and Issues

This is an open-source pack. For issues, improvements, or questions:
- Open an issue in the SmallMind GitHub repository
- Review existing discussions and documentation
- Contribute enhancements via pull requests

**Not a substitute for**: Official ITIL training, certification programs, or professional ITSM consulting.

## Changelog

### v1.0.0 (2026-02-02)
- Initial release
- 19 ITIL v4 documents covering foundations, practices, metrics
- 45 queries across difficulty levels and task types
- Document Q&A and structured consulting scenarios
- Comprehensive evaluation harness with scoring methodology
- Deterministic execution support
- MIT licensed original content

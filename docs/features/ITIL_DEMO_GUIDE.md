# ITIL v4 Mastery Pack - Demo Guide

## Quick Start

The ITIL v4 Mastery Pack includes a complete end-to-end demonstration showcasing RAG (Retrieval-Augmented Generation) capabilities with citation-backed answers and structured consulting output.

### Running the Demo

#### Option 1: Quick-Start Script (Recommended)

**Linux/Mac:**
```bash
./run-itil-demo.sh
```

**Windows:**
```cmd
run-itil-demo.bat
```

#### Option 2: Manual Execution

```bash
cd examples/ItilPackDemo
dotnet run
```

## What the Demo Shows

The demo is an interactive console application that demonstrates:

### 1. Pack Loading ✓
- Loads the ITIL v4 Mastery Pack from `data/pretrained/itil_v4_mastery/`
- Displays pack metadata (ID, domain, document count, license)
- Lists available ITIL documents (20 markdown files)

### 2. RAG Index Building ✓
- Loads ITIL v4 documents (foundations, practices, operational guidance)
- Chunks documents into 512-character segments with 64-character overlap
- Builds searchable index using BM25 sparse retrieval
- Shows chunking statistics for each document

### 3. Document Q&A with Citations ✓
Runs sample queries demonstrating citation-backed retrieval:

**Query 1:** "What is the difference between incident management and problem management?"
- Retrieves relevant chunks from incident and problem management documents
- Shows relevance scores and document previews
- Demonstrates citation tracking

**Query 2:** "What are the seven guiding principles in ITIL v4?"
- Retrieves from guiding principles document
- Lists the seven principles with context

**Query 3:** "Explain the Service Value System (SVS) and its key components."
- Retrieves from SVS foundational document
- Shows comprehensive context retrieval

### 4. Structured Consulting (JSON Output) ✓
Demonstrates structured output for consulting scenarios:

**Scenario:** "We have a high change failure rate (15%) and unplanned outages. Propose an ITIL v4-aligned improvement plan."

**Output includes:**
- `summary`: Executive summary of recommendations
- `recommended_practices`: ITIL practices to implement
- `workflow`: Step-by-step processes with owners and I/O
- `kpis`: Key performance indicators with targets and cadence
- `risks_and_pitfalls`: Identified risks
- `next_actions_30_days`: Immediate action items
- `citations`: References to ITIL corpus documents

**JSON Schema Validation:**
- Validates all required fields present
- Checks citation count (minimum 2)
- Verifies workflow structure
- Confirms KPI completeness

## Demo Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  Step 1: Load Pack                                           │
│  ├─ Read manifest.json                                       │
│  ├─ Enumerate RAG documents                                  │
│  └─ Display pack metadata                                    │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│  Step 2: Build RAG Index                                     │
│  ├─ Load markdown documents                                  │
│  ├─ Chunk text (512 chars, 64 overlap)                       │
│  ├─ Build BM25 inverted index                                │
│  └─ Store document records                                   │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│  Step 3: Document Q&A                                        │
│  ├─ Run BM25 search for queries                              │
│  ├─ Retrieve top-K chunks (K=3)                              │
│  ├─ Display citations with scores                            │
│  └─ Show document previews                                   │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│  Step 4: Structured Consulting                               │
│  ├─ Process consulting query                                 │
│  ├─ Generate structured JSON response                        │
│  ├─ Validate schema compliance                               │
│  └─ Display formatted output                                 │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│  Step 5: Summary                                             │
│  ├─ Recap capabilities demonstrated                          │
│  ├─ Provide next steps                                       │
│  └─ Link to documentation                                    │
└──────────────────────────────────────────────────────────────┘
```

## Sample Output

### Pack Loading
```
╔═══ Step 1: Loading ITIL v4 Mastery Pack ═══════════════════════════════════
╚════════════════════════════════════════════════════════════════════════════

✓ Pack Loaded: sm.pretrained.itil_v4_mastery.v1
  Domain: itil_v4
  Type: knowledge-pack
  Documents: 20
  Intended Use: rag, citations, evaluation, bench
  License: MIT

📚 Available Documents:
  • 001_foundations.md
  • 010_service_value_system.md
  • 020_guiding_principles.md
  • 030_four_dimensions.md
  • 040_service_value_chain.md
  ... and 15 more documents
```

### RAG Index Building
```
╔═══ Step 2: Building RAG Index from Documents ═════════════════════════════
╚════════════════════════════════════════════════════════════════════════════

📁 Loading documents from: documents
  Found 20 markdown documents

🔧 Initializing RAG components...

📄 Processing documents:
  ✓ 001_foundations: 45 chunks
  ✓ 010_service_value_system: 52 chunks
  ✓ 020_guiding_principles: 58 chunks
  ✓ 030_four_dimensions: 48 chunks
  ✓ 040_service_value_chain: 51 chunks

✓ Total chunks created: 254
✓ RAG index ready for queries
```

### Document Q&A
```
╔═══ Step 3: Document Q&A with Citations ═══════════════════════════════════
╚════════════════════════════════════════════════════════════════════════════

[Q1] Question:
  What is the difference between incident management and problem management?

📑 Retrieved Citations:

  📄 Document: Incident Management Practice
     Relevance Score: 2.456
     Preview: Minimize negative impact of incidents by restoring normal service operation as quickly as possible...

  📄 Document: Problem Management Practice
     Relevance Score: 2.134
     Preview: Reduce the likelihood and impact of incidents by identifying actual and potential causes...

  💡 Answer: [In a real RAG system, this would be generated using the retrieved context]
     The answer would synthesize information from the cited documents above.
```

### Structured Consulting JSON
```json
{
  "summary": "Implement risk-based change categorization with enhanced testing...",
  "recommended_practices": [
    "Change Enablement",
    "Release Management",
    "Service Validation and Testing",
    "Problem Management"
  ],
  "workflow": [
    {
      "step": "Risk Assessment",
      "owner": "Change Manager",
      "inputs": ["RFC details", "Historical failure data", "Service dependencies"],
      "outputs": ["Risk score", "Required approval level", "Testing requirements"]
    }
  ],
  "kpis": [
    {
      "name": "Change Success Rate",
      "definition": "Percentage of changes completed without failure or rollback",
      "target": ">95%",
      "cadence": "Weekly"
    }
  ],
  "risks_and_pitfalls": [
    "Resistance to additional approval steps slowing deployment velocity"
  ],
  "next_actions_30_days": [
    "Audit last 50 changes to identify common failure patterns"
  ],
  "citations": [
    {
      "doc_id": "080_change_enablement.md",
      "why": "Provides change type definitions, risk assessment framework, and CAB structure"
    }
  ]
}
```

## Technical Details

### Technologies Used
- **SmallMind.Runtime**: Pack loading and management
- **SmallMind.Rag**: Document chunking, indexing, retrieval
- **SmallMind.Tokenizers**: Text tokenization for chunking
- **BM25**: Sparse retrieval algorithm for document search
- **System.Text.Json**: JSON serialization for structured output

### RAG Pipeline Components
1. **Document Loader**: Reads markdown files from pack
2. **Chunker**: Splits documents into retrievable segments
3. **Inverted Index**: BM25-based search index
4. **Retriever**: Sparse retrieval with relevance scoring
5. **Citation Tracker**: Maps chunks back to source documents

### Performance Characteristics
- **Index Build Time**: ~1-2 seconds for 5 documents (~200 chunks)
- **Query Time**: <100ms per query (BM25 retrieval)
- **Memory Usage**: ~10-20MB for indexed documents
- **Document Processing**: ~5-10 chunks per document (varies by size)

## Extending the Demo

### Add More Queries
Edit `Program.cs` and add to the `queries` array in `Step3_DocumentQA()`:

```csharp
var queries = new[]
{
    new { Id = "Q4", Question = "Your ITIL question here?" }
};
```

### Increase Document Coverage
Change the document limit in `Step2_BuildRagIndex()`:

```csharp
foreach (var docFile in documentFiles.Take(20)) // Process all documents
```

### Adjust Retrieval Parameters
Modify top-K in `Step3_DocumentQA()`:

```csharp
var results = bm25Retriever.Search(query.Question, invertedIndex, topK: 5); // Retrieve 5 chunks
```

### Add Your Own Queries
Load queries from the pack's `queries.jsonl`:

```csharp
var queriesPath = Path.Combine(packPath, "task", "queries.jsonl");
var packQueries = DatasetLoader.LoadFromJsonl(queriesPath);
```

## Next Steps

After running the demo:

1. **Explore Pack Documentation**
   - Read `data/pretrained/itil_v4_mastery/README.md`
   - Review scoring methodology in `eval/scoring.md`

2. **Try All 45 Queries**
   - Load queries from `task/queries.jsonl`
   - Run document Q&A and structured consulting queries

3. **Implement Full RAG Pipeline**
   - Add LLM text generation for answers
   - Implement answer grounding with citations
   - Add evaluation scoring

4. **Customize for Your Needs**
   - Replace ITIL documents with your content
   - Modify JSON schema for your use case
   - Adjust retrieval and chunking parameters

5. **Integrate with Applications**
   - Build web API for RAG queries
   - Create interactive chat interface
   - Deploy as knowledge base service

## Troubleshooting

### "ITIL v4 pack not found"
**Solution**: Run the demo from the SmallMind root directory, or use the quick-start scripts.

### Build Errors
**Solution**: Restore NuGet packages first:
```bash
dotnet restore
```

### Slow Performance
**Solution**: The demo processes only 5 documents by default. This is intentional for quick demonstration. To process all 20 documents, modify the `Take(5)` limit in `Step2_BuildRagIndex()`.

### No Output or Empty Results
**Solution**: Ensure the ITIL pack documents exist in `data/pretrained/itil_v4_mastery/rag/documents/`.

## Related Resources

- **Pack README**: [data/pretrained/itil_v4_mastery/README.md](../data/pretrained/itil_v4_mastery/README.md)
- **Scoring Guide**: [data/pretrained/itil_v4_mastery/eval/scoring.md](../data/pretrained/itil_v4_mastery/eval/scoring.md)
- **Queries**: [data/pretrained/itil_v4_mastery/task/queries.jsonl](../data/pretrained/itil_v4_mastery/task/queries.jsonl)
- **Pack Manifest**: [data/pretrained/itil_v4_mastery/manifest.json](../data/pretrained/itil_v4_mastery/manifest.json)

## Support

For issues or questions:
- Review this demo guide
- Check the main README
- Open an issue in the SmallMind repository

---

**Note**: This demo uses BM25 sparse retrieval and simulated structured output for demonstration purposes. In a production RAG system, you would integrate an LLM for answer generation and implement full citation grounding.

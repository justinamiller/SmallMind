# RAG System Documentation

## Overview

The RAG (Retrieval-Augmented Generation) system in SmallMind combines document retrieval with text generation.

## Key Features

- **BM25 Retrieval**: Sparse retrieval using the BM25 algorithm for lexical matching
- **Dense Retrieval**: Optional vector-based retrieval using feature hashing embeddings
- **Hybrid Search**: Combines both sparse and dense retrieval for better results
- **Citation Support**: All retrieved chunks include source citations
- **Security**: Built-in authorization and access control
- **Telemetry**: Comprehensive logging and metrics

## Components

1. Document Ingestion: Scans directories and processes files
2. Chunking: Splits documents into manageable pieces with overlap
3. Indexing: Builds inverted index for fast BM25 scoring
4. Retrieval: Finds relevant chunks for queries
5. Prompt Composition: Assembles context with citations
6. Generation: Optional LLM integration for answers

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SmallMind.Rag.Ingestion;

/// <summary>
/// Handles ingestion of documents from the filesystem into the RAG system.
/// Scans directories, computes content hashes, and creates document records.
/// </summary>
internal sealed class DocumentIngestor
{
    /// <summary>
    /// Ingests all documents from a directory matching the specified file patterns.
    /// </summary>
    /// <param name="path">The directory path to scan for documents.</param>
    /// <param name="includePatterns">Semicolon-separated file patterns (e.g., "*.txt;*.md;*.json;*.log").</param>
    /// <returns>A list of document records for successfully ingested files.</returns>
    /// <exception cref="ArgumentNullException">Thrown when path is null.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory does not exist.</exception>
    public List<DocumentRecord> IngestDirectory(string path, string includePatterns = "*.txt;*.md;*.json;*.log")
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        
        var documents = new List<DocumentRecord>();
        string[] patterns = ParsePatterns(includePatterns);
        
        // Collect all matching files
        var filePaths = new List<string>();
        for (int i = 0; i < patterns.Length; i++)
        {
            try
            {
                string[] matchingFiles = Directory.GetFiles(path, patterns[i], SearchOption.AllDirectories);
                for (int j = 0; j < matchingFiles.Length; j++)
                {
                    filePaths.Add(matchingFiles[j]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to enumerate files with pattern '{patterns[i]}': {ex.Message}");
            }
        }
        
        // Ingest each file
        for (int i = 0; i < filePaths.Count; i++)
        {
            try
            {
                DocumentRecord doc = IngestFile(filePaths[i]);
                documents.Add(doc);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to ingest file '{filePaths[i]}': {ex.Message}");
            }
        }
        
        return documents;
    }

    /// <summary>
    /// Ingests a single document file and creates a document record.
    /// </summary>
    /// <param name="filePath">The full path to the document file.</param>
    /// <returns>A document record for the ingested file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public DocumentRecord IngestFile(string filePath)
    {
        if (filePath == null)
            throw new ArgumentNullException(nameof(filePath));
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");
        
        // Read file content
        byte[] contentBytes = File.ReadAllBytes(filePath);
        
        // Compute SHA256 hash
        string contentHash = ComputeSha256Hash(contentBytes);
        
        // Get file metadata
        FileInfo fileInfo = new FileInfo(filePath);
        string fileName = Path.GetFileName(filePath);
        DateTime lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        long sizeBytes = fileInfo.Length;
        
        // Create stable document ID from path and hash
        string docId = ComputeDocumentId(filePath, contentHash);
        
        return new DocumentRecord(
            docId: docId,
            title: fileName,
            sourceUri: Path.GetFullPath(filePath),
            lastWriteTimeUtc: lastWriteTimeUtc,
            contentHash: contentHash,
            sizeBytes: sizeBytes,
            version: 1
        );
    }

    /// <summary>
    /// Parses semicolon-separated file patterns into an array.
    /// </summary>
    private static string[] ParsePatterns(string includePatterns)
    {
        if (string.IsNullOrWhiteSpace(includePatterns))
            return new[] { "*.*" };
        
        string[] patterns = includePatterns.Split(';');
        var trimmedPatterns = new List<string>(patterns.Length);
        
        for (int i = 0; i < patterns.Length; i++)
        {
            string trimmed = patterns[i].Trim();
            if (trimmed.Length > 0)
            {
                trimmedPatterns.Add(trimmed);
            }
        }
        
        return trimmedPatterns.Count > 0 ? trimmedPatterns.ToArray() : new[] { "*.*" };
    }

    /// <summary>
    /// Computes the SHA256 hash of file content and returns it as a hex string.
    /// </summary>
    private static string ComputeSha256Hash(byte[] contentBytes)
    {
        Span<byte> hashBytes = stackalloc byte[32]; // SHA256 produces 32 bytes
        SHA256.HashData(contentBytes, hashBytes);
        
        // Convert to hex string efficiently using .NET 5+ API
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Computes a stable document identifier from the file path and content hash.
    /// </summary>
    private static string ComputeDocumentId(string filePath, string contentHash)
    {
        // Normalize path to use forward slashes for cross-platform consistency
        string normalizedPath = filePath.Replace('\\', '/');
        
        // Combine path and hash for uniqueness
        string combined = $"{normalizedPath}|{contentHash}";
        
        // Hash the combined string
        Span<byte> hashBytes = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(combined), hashBytes);
        
        return Convert.ToHexString(hashBytes);
    }
}

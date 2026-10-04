using System.Text.Json;
using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using EnterpriseKnowledgeAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAssistant.Infrastructure.Persistence;

public class PostgresVectorStore : IVectorStore
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PostgresVectorStore> _logger;

    public PostgresVectorStore(AppDbContext dbContext, ILogger<PostgresVectorStore> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task StoreChunksAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        // Chunks are already added to DbContext in the command, ensuring single transaction
        _logger.LogInformation("Indexed {Count} document chunks in persistent store.", chunks.Count);
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> HybridSearchAsync(
        Guid tenantId,
        IReadOnlyList<string> userRoles,
        float[] queryEmbedding,
        string queryText,
        int topK = 25,
        CancellationToken cancellationToken = default)
    {
        // 1. Pre-filter by Tenant and User ACL Roles (Row-Level Security)
        var rolesSet = new HashSet<string>(userRoles, StringComparer.OrdinalIgnoreCase) { "General" };

        var tenantChunks = await _dbContext.DocumentChunks
            .Include(c => c.Document)
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        // In-memory ACL role pre-filter
        var allowedChunks = tenantChunks
            .Where(c => c.AclRoles.Any(r => rolesSet.Contains(r)))
            .ToList();

        if (allowedChunks.Count == 0)
        {
            _logger.LogWarning("No accessible document chunks found for Tenant: {TenantId} and Roles: {Roles}",
                tenantId, string.Join(", ", userRoles));
            return Array.Empty<RetrievedChunk>();
        }

        // 2. Dense Vector Search (Cosine Similarity)
        var denseScored = allowedChunks
            .Select(c => new
            {
                Chunk = c,
                Score = ComputeCosineSimilarity(queryEmbedding, c.Embedding)
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select((x, index) => new
            {
                x.Chunk,
                DenseScore = x.Score,
                DenseRank = index + 1
            })
            .ToDictionary(x => x.Chunk.Id);

        // 3. Sparse Keyword Search (BM25 / Keyword Density)
        var queryTokens = TokenizeQuery(queryText);
        var sparseScored = allowedChunks
            .Select(c => new
            {
                Chunk = c,
                Score = ComputeBm25Score(queryTokens, c.Content)
            })
            .Where(x => x.Score > 0 || allowedChunks.Count <= topK)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select((x, index) => new
            {
                x.Chunk,
                SparseScore = x.Score,
                SparseRank = index + 1
            })
            .ToDictionary(x => x.Chunk.Id);

        // 4. Reciprocal Rank Fusion (RRF) with constant k = 60
        const double rrfK = 60.0;
        var allChunkIds = denseScored.Keys.Union(sparseScored.Keys).Distinct();

        var fusedResults = new List<RetrievedChunk>();

        foreach (var chunkId in allChunkIds)
        {
            var chunk = allowedChunks.First(c => c.Id == chunkId);

            int denseRank = denseScored.TryGetValue(chunkId, out var d) ? d.DenseRank : 1000;
            double denseScore = d?.DenseScore ?? 0.0;

            int sparseRank = sparseScored.TryGetValue(chunkId, out var s) ? s.SparseRank : 1000;
            double sparseScore = s?.SparseScore ?? 0.0;

            double rrfScore = (1.0 / (rrfK + denseRank)) + (1.0 / (rrfK + sparseRank));

            // Extract bounding box from metadata
            BoundingBoxDto? boundingBox = null;
            try
            {
                if (!string.IsNullOrEmpty(chunk.MetadataJson))
                {
                    using var doc = JsonDocument.Parse(chunk.MetadataJson);
                    if (doc.RootElement.TryGetProperty("boundingBox", out var boxProp) && boxProp.ValueKind == JsonValueKind.Object)
                    {
                        boundingBox = JsonSerializer.Deserialize<BoundingBoxDto>(boxProp.GetRawText());
                    }
                }
            }
            catch
            {
                // Fallback if metadata is not a bounding box
            }

            fusedResults.Add(new RetrievedChunk(
                ChunkId: chunk.Id,
                DocumentId: chunk.DocumentId,
                DocumentName: chunk.Document?.Filename ?? "Unknown Document",
                PageNumber: chunk.PageNumber,
                ChunkIndex: chunk.ChunkIndex,
                Content: chunk.Content,
                ContentHash: chunk.ContentHash,
                DenseScore: Math.Round(denseScore, 4),
                DenseRank: denseRank,
                SparseScore: Math.Round(sparseScore, 4),
                SparseRank: sparseRank,
                RrfScore: Math.Round(rrfScore, 6),
                BoundingBox: boundingBox,
                AclRoles: chunk.AclRoles
            ));
        }

        var topCandidates = fusedResults
            .OrderByDescending(r => r.RrfScore)
            .Take(topK)
            .ToList();

        _logger.LogInformation("Hybrid search retrieved {Count} candidates via RRF.", topCandidates.Count);
        return topCandidates;
    }

    public static double ComputeCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0.0;

        double dot = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA <= 0.0 || normB <= 0.0) return 0.0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static List<string> TokenizeQuery(string query)
    {
        return query
            .ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ';', ':', '?', '!', '\t', '\n', '\r', '-', '(', ')', '"' }, 
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .Distinct()
            .ToList();
    }

    private static double ComputeBm25Score(List<string> queryTokens, string content)
    {
        if (queryTokens.Count == 0 || string.IsNullOrWhiteSpace(content)) return 0.0;

        var lowerContent = content.ToLowerInvariant();
        double score = 0.0;

        foreach (var token in queryTokens)
        {
            int count = 0;
            int pos = 0;
            while ((pos = lowerContent.IndexOf(token, pos, StringComparison.Ordinal)) != -1)
            {
                count++;
                pos += token.Length;
            }

            if (count > 0)
            {
                // Term frequency saturation
                score += (count * 2.2) / (count + 1.2);
            }
        }

        return score;
    }
}

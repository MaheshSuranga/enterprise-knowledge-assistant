namespace EnterpriseKnowledgeAssistant.Application.DTOs;

public record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    int PageNumber,
    int ChunkIndex,
    string Content,
    string ContentHash,
    double DenseScore,
    int DenseRank,
    double SparseScore,
    int SparseRank,
    double RrfScore,
    BoundingBoxDto? BoundingBox,
    IReadOnlyList<string> AclRoles
);

public record RerankedChunk(
    RetrievedChunk Chunk,
    double RerankScore,
    int RerankRank
);

public record LatencyProfileDto(
    long TotalLatencyMs,
    long EmbeddingLatencyMs,
    long RetrievalLatencyMs,
    long RerankLatencyMs,
    long LlmLatencyMs
);

public record RagInspectionDetails(
    string Query,
    LatencyProfileDto Latencies,
    IReadOnlyList<RetrievedChunk> TopRetrievedCandidates,
    IReadOnlyList<RerankedChunk> TopRerankedChunks,
    int TotalCandidatesEvaluated
);

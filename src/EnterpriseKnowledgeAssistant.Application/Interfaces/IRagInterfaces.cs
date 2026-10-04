using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAssistant.Application.Interfaces;

public interface IPdfParser
{
    Task<ParsedDocument> ParsePdfAsync(Stream pdfStream, string filename, CancellationToken cancellationToken = default);
}

public interface IDocumentChunker
{
    IReadOnlyList<ChunkResult> ChunkDocument(ParsedDocument parsedDocument, int targetChunkSize = 512, int chunkOverlap = 64);
}

public interface IEmbeddingGenerator
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
    int EmbeddingDimension { get; }
}

public interface IVectorStore
{
    Task StoreChunksAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetrievedChunk>> HybridSearchAsync(
        Guid tenantId,
        IReadOnlyList<string> userRoles,
        float[] queryEmbedding,
        string queryText,
        int topK = 25,
        CancellationToken cancellationToken = default);
}

public interface IReranker
{
    Task<IReadOnlyList<RerankedChunk>> RerankAsync(
        string query,
        IReadOnlyList<RetrievedChunk> candidates,
        int topN = 5,
        CancellationToken cancellationToken = default);
}

public interface ISemanticKernelService
{
    Task<GroundedAnswerResult> GenerateGroundedAnswerAsync(
        string question,
        IReadOnlyList<RerankedChunk> contextChunks,
        IReadOnlyList<ChatMessageDto> conversationHistory,
        CancellationToken cancellationToken = default);
}

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentChunk> DocumentChunks { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

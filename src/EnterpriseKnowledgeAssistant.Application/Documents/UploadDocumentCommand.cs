using System.Security.Cryptography;
using EnterpriseKnowledgeAssistant.Application.Common;
using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using EnterpriseKnowledgeAssistant.Domain.Entities;
using EnterpriseKnowledgeAssistant.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnterpriseKnowledgeAssistant.Application.Documents;

public record UploadDocumentCommand(
    Guid TenantId,
    string Filename,
    string ContentType,
    long FileSizeBytes,
    Stream FileStream,
    List<string> AclRoles
) : IRequest<Result<DocumentUploadResult>>;

public record DocumentUploadResult(
    Guid DocumentId,
    string Filename,
    int PageCount,
    int ChunkCount,
    string Checksum
);

public class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, Result<DocumentUploadResult>>
{
    private readonly IAppDbContext _dbContext;
    private readonly IPdfParser _pdfParser;
    private readonly IDocumentChunker _chunker;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IVectorStore _vectorStore;

    public UploadDocumentCommandHandler(
        IAppDbContext dbContext,
        IPdfParser pdfParser,
        IDocumentChunker chunker,
        IEmbeddingGenerator embeddingGenerator,
        IVectorStore vectorStore)
    {
        _dbContext = dbContext;
        _pdfParser = pdfParser;
        _chunker = chunker;
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = vectorStore;
    }

    public async Task<Result<DocumentUploadResult>> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify tenant exists
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Result<DocumentUploadResult>.Failure($"Tenant {request.TenantId} not found.");
        }

        // 2. Read stream into memory to calculate deterministic SHA-256 file checksum
        using var memoryStream = new MemoryStream();
        await request.FileStream.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();
        var fileChecksum = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();

        // 3. Create document record
        var document = new Document
        {
            TenantId = request.TenantId,
            Filename = request.Filename,
            FilePath = $"uploads/{request.TenantId}/{request.Filename}",
            FileSizeBytes = request.FileSizeBytes > 0 ? request.FileSizeBytes : fileBytes.Length,
            ContentType = request.ContentType,
            Checksum = fileChecksum,
            Status = DocumentStatus.Processing
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // 4. Parse PDF layout and extract pages & blocks
            using var parseStream = new MemoryStream(fileBytes);
            var parsedDoc = await _pdfParser.ParsePdfAsync(parseStream, request.Filename, cancellationToken);
            document.PageCount = parsedDoc.PageCount;

            // 5. Chunk layout text with header preservation and SHA-256 content hashes
            var chunks = _chunker.ChunkDocument(parsedDoc);

            if (chunks.Count == 0)
            {
                document.Status = DocumentStatus.Indexed;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Result<DocumentUploadResult>.Success(new DocumentUploadResult(
                    document.Id, document.Filename, document.PageCount, 0, fileChecksum));
            }

            // 6. Generate embeddings in batches
            var chunkTexts = chunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingGenerator.GenerateEmbeddingsBatchAsync(chunkTexts, cancellationToken);

            // 7. Assemble DocumentChunk entities
            var chunkEntities = new List<DocumentChunk>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var entity = new DocumentChunk
                {
                    DocumentId = document.Id,
                    TenantId = request.TenantId,
                    ChunkIndex = chunk.ChunkIndex,
                    PageNumber = chunk.PageNumber,
                    Content = chunk.Content,
                    ContentHash = chunk.ContentHash,
                    AclRoles = request.AclRoles.Count > 0 ? request.AclRoles : new List<string> { "General" },
                    Embedding = embeddings[i],
                    MetadataJson = JsonSerializer.Serialize(new
                    {
                        section = chunk.SectionHeader,
                        boundingBox = chunk.PrimaryBoundingBox
                    })
                };
                chunkEntities.Add(entity);
            }

            _dbContext.DocumentChunks.AddRange(chunkEntities);
            document.Status = DocumentStatus.Indexed;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 8. Store in vector store
            await _vectorStore.StoreChunksAsync(chunkEntities, cancellationToken);

            return Result<DocumentUploadResult>.Success(new DocumentUploadResult(
                document.Id,
                document.Filename,
                document.PageCount,
                chunkEntities.Count,
                fileChecksum
            ));
        }
        catch (Exception ex)
        {
            document.Status = DocumentStatus.Failed;
            document.ErrorMessage = ex.Message;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<DocumentUploadResult>.Failure($"Failed to process document: {ex.Message}");
        }
    }
}

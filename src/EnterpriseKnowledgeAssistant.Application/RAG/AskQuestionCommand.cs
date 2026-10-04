using System.Diagnostics;
using System.Text.Json;
using EnterpriseKnowledgeAssistant.Application.Common;
using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using EnterpriseKnowledgeAssistant.Domain.Entities;
using EnterpriseKnowledgeAssistant.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAssistant.Application.RAG;

public record AskQuestionCommand(
    Guid? ConversationId,
    string Question,
    int TopKCandidates = 25,
    int TopNReranked = 5
) : IRequest<Result<AskQuestionResponse>>;

public class AskQuestionCommandHandler : IRequestHandler<AskQuestionCommand, Result<AskQuestionResponse>>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IVectorStore _vectorStore;
    private readonly IReranker _reranker;
    private readonly ISemanticKernelService _semanticKernelService;

    public AskQuestionCommandHandler(
        IAppDbContext dbContext,
        ICurrentUserService currentUserService,
        IEmbeddingGenerator embeddingGenerator,
        IVectorStore vectorStore,
        IReranker reranker,
        ISemanticKernelService semanticKernelService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = vectorStore;
        _reranker = reranker;
        _semanticKernelService = semanticKernelService;
    }

    public async Task<Result<AskQuestionResponse>> Handle(AskQuestionCommand request, CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var tenantId = _currentUserService.TenantId;
        var userRoles = _currentUserService.Roles;

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Result<AskQuestionResponse>.Failure("Question cannot be empty.");
        }

        // 1. Resolve or create conversation
        Conversation? conversation;
        if (request.ConversationId.HasValue)
        {
            conversation = await _dbContext.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value && c.TenantId == tenantId, cancellationToken);

            if (conversation == null)
            {
                return Result<AskQuestionResponse>.Failure("Conversation not found.");
            }
        }
        else
        {
            conversation = new Conversation
            {
                TenantId = tenantId,
                UserId = _currentUserService.UserId,
                Title = request.Question.Length > 50 ? request.Question[..50] + "..." : request.Question
            };
            _dbContext.Conversations.Add(conversation);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. Compute Query Embedding
        var embSw = Stopwatch.StartNew();
        var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(request.Question, cancellationToken);
        embSw.Stop();

        // 3. Hybrid Search (Dense <=> + Sparse tsvector fused via RRF)
        var retSw = Stopwatch.StartNew();
        var candidates = await _vectorStore.HybridSearchAsync(
            tenantId,
            userRoles,
            queryEmbedding,
            request.Question,
            request.TopKCandidates,
            cancellationToken
        );
        retSw.Stop();

        // 4. Cross-Encoder Reranking
        var rerankSw = Stopwatch.StartNew();
        var reranked = await _reranker.RerankAsync(
            request.Question,
            candidates,
            request.TopNReranked,
            cancellationToken
        );
        rerankSw.Stop();

        // 5. Build recent conversation history
        var history = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .TakeLast(6)
            .Select(m => new ChatMessageDto(
                m.Id,
                m.ConversationId,
                m.Role,
                m.Content,
                string.IsNullOrEmpty(m.CitationsJson) 
                    ? new List<CitationDto>() 
                    : JsonSerializer.Deserialize<List<CitationDto>>(m.CitationsJson) ?? new List<CitationDto>(),
                m.IsGrounded,
                m.RefusalReason,
                m.LatencyMs,
                m.CreatedAt
            ))
            .ToList();

        // 6. Invoke Microsoft Semantic Kernel with Structured JSON Schema Grounding
        var llmSw = Stopwatch.StartNew();
        var groundedResult = await _semanticKernelService.GenerateGroundedAnswerAsync(
            request.Question,
            reranked,
            history,
            cancellationToken
        );
        llmSw.Stop();

        totalStopwatch.Stop();

        // 7. Save User Message
        var userMsg = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = request.Question,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.ChatMessages.Add(userMsg);

        // 8. Save Assistant Message with verified citations
        var assistantMsg = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = groundedResult.Answer,
            CitationsJson = JsonSerializer.Serialize(groundedResult.Citations),
            IsGrounded = groundedResult.IsGrounded,
            GroundednessScore = groundedResult.ConfidenceScore,
            RefusalReason = groundedResult.RefusalReason,
            LatencyMs = totalStopwatch.ElapsedMilliseconds,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.ChatMessages.Add(assistantMsg);

        // 9. Record Enterprise Audit Log & Telemetry
        var audit = new AuditLog
        {
            TenantId = tenantId,
            UserId = _currentUserService.UserId,
            UserRole = userRoles.FirstOrDefault() ?? "General",
            QueryText = request.Question,
            RetrievedChunkIdsJson = JsonSerializer.Serialize(reranked.Select(r => r.Chunk.ChunkId)),
            TotalLatencyMs = totalStopwatch.ElapsedMilliseconds,
            EmbeddingLatencyMs = embSw.ElapsedMilliseconds,
            RetrievalLatencyMs = retSw.ElapsedMilliseconds,
            RerankLatencyMs = rerankSw.ElapsedMilliseconds,
            LlmLatencyMs = llmSw.ElapsedMilliseconds,
            DenseScoreAvg = candidates.Count > 0 ? candidates.Average(c => c.DenseScore) : 0,
            SparseScoreAvg = candidates.Count > 0 ? candidates.Average(c => c.SparseScore) : 0,
            RerankScoreAvg = reranked.Count > 0 ? reranked.Average(r => r.RerankScore) : 0,
            IsGrounded = groundedResult.IsGrounded,
            RefusalReason = groundedResult.RefusalReason?.ToString()
        };
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 10. Assemble Inspection Details for Frontend RAG Inspector
        var inspection = new RagInspectionDetails(
            Query: request.Question,
            Latencies: new LatencyProfileDto(
                TotalLatencyMs: totalStopwatch.ElapsedMilliseconds,
                EmbeddingLatencyMs: embSw.ElapsedMilliseconds,
                RetrievalLatencyMs: retSw.ElapsedMilliseconds,
                RerankLatencyMs: rerankSw.ElapsedMilliseconds,
                LlmLatencyMs: llmSw.ElapsedMilliseconds
            ),
            TopRetrievedCandidates: candidates,
            TopRerankedChunks: reranked,
            TotalCandidatesEvaluated: candidates.Count
        );

        return Result<AskQuestionResponse>.Success(new AskQuestionResponse(
            MessageId: assistantMsg.Id,
            ConversationId: conversation.Id,
            Answer: groundedResult.Answer,
            Citations: groundedResult.Citations,
            IsGrounded: groundedResult.IsGrounded,
            ConfidenceScore: groundedResult.ConfidenceScore,
            RefusalReason: groundedResult.RefusalReason,
            Inspection: inspection
        ));
    }
}

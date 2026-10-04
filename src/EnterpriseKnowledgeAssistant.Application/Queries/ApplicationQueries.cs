using System.Text.Json;
using EnterpriseKnowledgeAssistant.Application.Common;
using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using EnterpriseKnowledgeAssistant.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAssistant.Application.Queries;

// 1. GetTenants
public record GetTenantsQuery : IRequest<Result<List<TenantDto>>>;
public record TenantDto(Guid Id, string Name, string Description, int DocumentCount);

public class GetTenantsQueryHandler : IRequestHandler<GetTenantsQuery, Result<List<TenantDto>>>
{
    private readonly IAppDbContext _dbContext;

    public GetTenantsQueryHandler(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<TenantDto>>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await _dbContext.Tenants
            .Include(t => t.Documents)
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto(t.Id, t.Name, t.Description, t.Documents.Count))
            .ToListAsync(cancellationToken);

        return Result<List<TenantDto>>.Success(tenants);
    }
}

// 2. GetConversations
public record GetConversationsQuery : IRequest<Result<List<ConversationSummaryDto>>>;
public record ConversationSummaryDto(Guid Id, string Title, int MessageCount, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public class GetConversationsQueryHandler : IRequestHandler<GetConversationsQuery, Result<List<ConversationSummaryDto>>>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetConversationsQueryHandler(IAppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<ConversationSummaryDto>>> Handle(GetConversationsQuery request, CancellationToken cancellationToken)
    {
        var conversations = await _dbContext.Conversations
            .Where(c => c.TenantId == _currentUserService.TenantId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ConversationSummaryDto(
                c.Id,
                c.Title,
                c.Messages.Count,
                c.CreatedAt,
                c.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<List<ConversationSummaryDto>>.Success(conversations);
    }
}

// 3. GetConversationMessages
public record GetConversationMessagesQuery(Guid ConversationId) : IRequest<Result<List<ChatMessageDto>>>;

public class GetConversationMessagesQueryHandler : IRequestHandler<GetConversationMessagesQuery, Result<List<ChatMessageDto>>>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetConversationMessagesQueryHandler(IAppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<ChatMessageDto>>> Handle(GetConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _dbContext.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.TenantId == _currentUserService.TenantId, cancellationToken);

        if (conversation == null)
        {
            return Result<List<ChatMessageDto>>.Failure("Conversation not found.");
        }

        var messages = conversation.Messages
            .OrderBy(m => m.CreatedAt)
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

        return Result<List<ChatMessageDto>>.Success(messages);
    }
}

// 4. GetDocumentChunks (for PDF viewer chunk breakdown)
public record GetDocumentChunksQuery(Guid DocumentId) : IRequest<Result<List<DocumentChunkDto>>>;
public record DocumentChunkDto(
    Guid Id,
    int ChunkIndex,
    int PageNumber,
    string Content,
    string ContentHash,
    List<string> AclRoles,
    string MetadataJson
);

public class GetDocumentChunksQueryHandler : IRequestHandler<GetDocumentChunksQuery, Result<List<DocumentChunkDto>>>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetDocumentChunksQueryHandler(IAppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<DocumentChunkDto>>> Handle(GetDocumentChunksQuery request, CancellationToken cancellationToken)
    {
        var doc = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId && d.TenantId == _currentUserService.TenantId, cancellationToken);

        if (doc == null)
        {
            return Result<List<DocumentChunkDto>>.Failure("Document not found.");
        }

        var chunks = await _dbContext.DocumentChunks
            .Where(c => c.DocumentId == request.DocumentId)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new DocumentChunkDto(
                c.Id,
                c.ChunkIndex,
                c.PageNumber,
                c.Content,
                c.ContentHash,
                c.AclRoles,
                c.MetadataJson
            ))
            .ToListAsync(cancellationToken);

        return Result<List<DocumentChunkDto>>.Success(chunks);
    }
}

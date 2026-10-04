using EnterpriseKnowledgeAssistant.Domain.Enums;

namespace EnterpriseKnowledgeAssistant.Application.DTOs;

public record CitationDto(
    int CitationNumber,
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    int PageNumber,
    string ExactQuote,
    BoundingBoxDto? BoundingBox
);

public record GroundedAnswerResult(
    string Answer,
    IReadOnlyList<CitationDto> Citations,
    bool IsGrounded,
    double ConfidenceScore,
    RefusalReason? RefusalReason
);

public record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    string Role,
    string Content,
    IReadOnlyList<CitationDto> Citations,
    bool IsGrounded,
    RefusalReason? RefusalReason,
    long LatencyMs,
    DateTimeOffset CreatedAt
);

public record AskQuestionResponse(
    Guid MessageId,
    Guid ConversationId,
    string Answer,
    IReadOnlyList<CitationDto> Citations,
    bool IsGrounded,
    double ConfidenceScore,
    RefusalReason? RefusalReason,
    RagInspectionDetails Inspection
);

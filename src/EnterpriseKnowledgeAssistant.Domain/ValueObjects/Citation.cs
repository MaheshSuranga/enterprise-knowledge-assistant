namespace EnterpriseKnowledgeAssistant.Domain.ValueObjects;

public record BoundingBox(
    int PageNumber,
    double Left,
    double Top,
    double Width,
    double Height
);

public record Citation(
    int CitationNumber,
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    int PageNumber,
    string ExactQuote,
    BoundingBox? BoundingBox = null
);

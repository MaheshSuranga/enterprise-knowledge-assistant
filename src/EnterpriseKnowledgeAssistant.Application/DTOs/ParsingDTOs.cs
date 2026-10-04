namespace EnterpriseKnowledgeAssistant.Application.DTOs;

public record BoundingBoxDto(
    int PageNumber,
    double Left,
    double Top,
    double Width,
    double Height
);

public record ParsedTextBlock(
    string Text,
    BoundingBoxDto BoundingBox,
    string? FontName = null,
    double? FontSize = null,
    bool IsHeader = false
);

public record ParsedPage(
    int PageNumber,
    double Width,
    double Height,
    IReadOnlyList<ParsedTextBlock> TextBlocks
);

public record ParsedDocument(
    string Filename,
    int PageCount,
    IReadOnlyList<ParsedPage> Pages
);

public record ChunkResult(
    int ChunkIndex,
    int PageNumber,
    string Content,
    string ContentHash, // Deterministic SHA-256
    BoundingBoxDto? PrimaryBoundingBox,
    string SectionHeader
);

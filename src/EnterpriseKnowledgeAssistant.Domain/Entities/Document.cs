using EnterpriseKnowledgeAssistant.Domain.Common;
using EnterpriseKnowledgeAssistant.Domain.Enums;

namespace EnterpriseKnowledgeAssistant.Domain.Entities;

public class Document : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Filename { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    public int Version { get; set; } = 1;
    public string Checksum { get; set; } = string.Empty; // SHA-256 of physical file
    public int PageCount { get; set; } = 0;

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public string? ErrorMessage { get; set; }

    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}

public class DocumentChunk : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int ChunkIndex { get; set; }
    public int PageNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty; // SHA-256 of chunk content

    public List<string> AclRoles { get; set; } = new() { "General" };

    // Dense embedding vector (1536 dimensions for text-embedding-3-small)
    public float[] Embedding { get; set; } = Array.Empty<float>();

    // JSON metadata containing bounding boxes, section titles, headers
    public string MetadataJson { get; set; } = "{}";
}

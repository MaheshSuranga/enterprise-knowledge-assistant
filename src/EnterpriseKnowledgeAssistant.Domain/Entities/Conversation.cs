using EnterpriseKnowledgeAssistant.Domain.Common;
using EnterpriseKnowledgeAssistant.Domain.Enums;

namespace EnterpriseKnowledgeAssistant.Domain.Entities;

public class Conversation : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string Title { get; set; } = "New Conversation";
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    public string Role { get; set; } = "user"; // "user", "assistant", "system"
    public string Content { get; set; } = string.Empty;

    // Serialized List<Citation>
    public string CitationsJson { get; set; } = "[]";

    public bool IsGrounded { get; set; } = true;
    public double? GroundednessScore { get; set; }
    public RefusalReason? RefusalReason { get; set; }

    public long LatencyMs { get; set; }
    public string MetadataJson { get; set; } = "{}";
}

public class AuditLog : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string UserRole { get; set; } = "General";

    public string QueryText { get; set; } = string.Empty;
    public string RetrievedChunkIdsJson { get; set; } = "[]";

    public long TotalLatencyMs { get; set; }
    public long EmbeddingLatencyMs { get; set; }
    public long RetrievalLatencyMs { get; set; }
    public long RerankLatencyMs { get; set; }
    public long LlmLatencyMs { get; set; }

    public double DenseScoreAvg { get; set; }
    public double SparseScoreAvg { get; set; }
    public double RerankScoreAvg { get; set; }

    public bool IsGrounded { get; set; }
    public string? RefusalReason { get; set; }
}

namespace EnterpriseKnowledgeAssistant.Domain.Enums;

public enum DocumentStatus
{
    Pending,
    Processing,
    Indexed,
    Failed
}

public enum UserRole
{
    General,
    Engineering,
    HR,
    Finance,
    Executive,
    Admin
}

public enum RefusalReason
{
    InsufficientContext,
    OffTopic,
    UnauthorizedAccess
}

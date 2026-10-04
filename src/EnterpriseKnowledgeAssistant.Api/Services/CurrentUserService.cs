using EnterpriseKnowledgeAssistant.Application.Common;

namespace EnterpriseKnowledgeAssistant.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    public Guid TenantId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111"); // Default Acme Aerospace
    public Guid? UserId { get; set; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public IReadOnlyList<string> Roles { get; set; } = new List<string> { "Engineering", "General" };

    public void SetContext(Guid tenantId, Guid? userId, IReadOnlyList<string> roles)
    {
        TenantId = tenantId;
        UserId = userId;
        Roles = roles;
    }
}

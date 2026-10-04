using EnterpriseKnowledgeAssistant.Api.Services;

namespace EnterpriseKnowledgeAssistant.Api.Middleware;

public class MultiTenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MultiTenantMiddleware> _logger;

    public MultiTenantMiddleware(RequestDelegate next, ILogger<MultiTenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, CurrentUserService currentUserService)
    {
        // 1. Resolve Tenant Id from Header (or query param)
        var defaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid tenantId = defaultTenantId;

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) &&
            Guid.TryParse(tenantHeader, out var parsedTenantId))
        {
            tenantId = parsedTenantId;
        }

        // 2. Resolve User Roles from Header
        var roles = new List<string> { "General" };
        if (context.Request.Headers.TryGetValue("X-User-Roles", out var rolesHeader) && !string.IsNullOrWhiteSpace(rolesHeader))
        {
            roles = rolesHeader.ToString()
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .ToList();
        }
        else
        {
            roles.Add("Engineering");
        }

        // 3. Resolve User Id from Header
        Guid? userId = null;
        if (context.Request.Headers.TryGetValue("X-User-Id", out var userHeader) &&
            Guid.TryParse(userHeader, out var parsedUserId))
        {
            userId = parsedUserId;
        }

        currentUserService.SetContext(tenantId, userId, roles);

        await _next(context);
    }
}

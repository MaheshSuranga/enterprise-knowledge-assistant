using System.Threading.RateLimiting;
using EnterpriseKnowledgeAssistant.Api.Middleware;
using EnterpriseKnowledgeAssistant.Api.Services;
using EnterpriseKnowledgeAssistant.Application;
using EnterpriseKnowledgeAssistant.Application.Common;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using EnterpriseKnowledgeAssistant.Domain.Entities;
using EnterpriseKnowledgeAssistant.Domain.Enums;
using EnterpriseKnowledgeAssistant.Infrastructure;
using EnterpriseKnowledgeAssistant.Infrastructure.AI;
using EnterpriseKnowledgeAssistant.Infrastructure.Persistence;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Setup Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Add Layer Services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 3. User & Multi-tenant Scoped Service
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());

// 4. Rate Limiting (Token Bucket rate limiter for RAG API protection)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("rag-policy", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

// 5. CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 6. Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Enterprise Knowledge Assistant API", Version = "v1" });
});

var app = builder.Build();

// 7. Seed & Initialize Database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedSampleDocuments(db);
}

// 8. Configure HTTP Middleware Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Enterprise Knowledge Assistant API v1");
    c.RoutePrefix = "swagger";
});

app.UseMiddleware<MultiTenantMiddleware>();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers().RequireRateLimiting("rag-policy");

Log.Information("Starting Enterprise Knowledge Assistant Web API on .NET 9...");
app.Run();

static void SeedSampleDocuments(AppDbContext db)
{
    var tenantAcmeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    if (!db.Documents.Any(d => d.TenantId == tenantAcmeId))
    {
        var docId = Guid.NewGuid();
        var doc = new Document
        {
            Id = docId,
            TenantId = tenantAcmeId,
            Filename = "Acme_Propulsion_System_Specifications_v4.2.pdf",
            FilePath = $"uploads/{tenantAcmeId}/Acme_Propulsion_System_Specifications_v4.2.pdf",
            FileSizeBytes = 204800,
            ContentType = "application/pdf",
            Version = 4,
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            PageCount = 3,
            Status = DocumentStatus.Indexed,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Documents.Add(doc);

        var chunk1 = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = docId,
            TenantId = tenantAcmeId,
            ChunkIndex = 0,
            PageNumber = 1,
            Content = "## 1. Engine Specifications & Thermal Dissipation Limits\nThe Acme Ion-Drive Mark IV thruster operates at a peak chamber temperature of 2,450 Kelvin under 85% throttle. The magnetic containment field requires a continuous power feed of 48.6 kW ± 0.5 kW. Liquid Xenon propellant must maintain an inlet manifold pressure of 3.2 MPa at all times.",
            ContentHash = "a1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef0",
            AclRoles = new List<string> { "Engineering", "General" },
            Embedding = OpenAiEmbeddingGenerator.GenerateDeterministicFallbackEmbedding("Acme Ion-Drive Mark IV thruster temperature 2450 Kelvin power 48.6 kW xenon propellant", 1536),
            MetadataJson = "{\"section\":\"Engine Specifications\",\"boundingBox\":{\"pageNumber\":1,\"left\":0.1,\"top\":0.2,\"width\":0.8,\"height\":0.15}}"
        };

        var chunk2 = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = docId,
            TenantId = tenantAcmeId,
            ChunkIndex = 1,
            PageNumber = 2,
            Content = "## 2. Emergency Shutdown Protocols\nIn the event of magnetic containment degradation below 91.5% field density, the autonomous safety interlock will trigger a SCRAM within 45 milliseconds. Engineers must vent the secondary helium coolant loops manually using Valve HV-409 located in Deck 4 Compartment B.",
            ContentHash = "b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef01",
            AclRoles = new List<string> { "Engineering" },
            Embedding = OpenAiEmbeddingGenerator.GenerateDeterministicFallbackEmbedding("Emergency Shutdown SCRAM magnetic containment Valve HV-409 Compartment B", 1536),
            MetadataJson = "{\"section\":\"Emergency Shutdown Protocols\",\"boundingBox\":{\"pageNumber\":2,\"left\":0.1,\"top\":0.35,\"width\":0.8,\"height\":0.2}}"
        };

        var chunk3 = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = docId,
            TenantId = tenantAcmeId,
            ChunkIndex = 2,
            PageNumber = 3,
            Content = "## 3. Executive Compensation & Proprietary Patent Disclosures\nProject Chronos propulsion patents are solely assigned to Acme Aerospace Holdings Corp. Senior executive retention bonuses for Phase 4 deployment are capped at $250,000 per fiscal year subject to defense audit approval.",
            ContentHash = "c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef012",
            AclRoles = new List<string> { "Executive" }, // Restricted role for testing Row-Level Security!
            Embedding = OpenAiEmbeddingGenerator.GenerateDeterministicFallbackEmbedding("Executive Compensation Patent Disclosures Project Chronos bonuses $250,000", 1536),
            MetadataJson = "{\"section\":\"Executive Disclosures\",\"boundingBox\":{\"pageNumber\":3,\"left\":0.1,\"top\":0.15,\"width\":0.8,\"height\":0.25}}"
        };

        db.DocumentChunks.AddRange(chunk1, chunk2, chunk3);
        db.SaveChanges();
    }
}

public partial class Program { }

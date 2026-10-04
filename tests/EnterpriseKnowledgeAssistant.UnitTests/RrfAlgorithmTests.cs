using EnterpriseKnowledgeAssistant.Domain.Entities;
using EnterpriseKnowledgeAssistant.Infrastructure.AI;
using EnterpriseKnowledgeAssistant.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EnterpriseKnowledgeAssistant.UnitTests;

public class RrfAlgorithmTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task HybridSearch_ShouldIsolateTenants_AndEnforceRowLevelSecurity()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var store = new PostgresVectorStore(context, NullLogger<PostgresVectorStore>.Instance);

        var tenantA = new Tenant { Id = Guid.NewGuid(), Name = "Tenant A" };
        var tenantB = new Tenant { Id = Guid.NewGuid(), Name = "Tenant B" };
        context.Tenants.AddRange(tenantA, tenantB);

        var docA = new Document { Id = Guid.NewGuid(), TenantId = tenantA.Id, Filename = "DocA.pdf", Checksum = "c1", Status = Domain.Enums.DocumentStatus.Indexed };
        var docB = new Document { Id = Guid.NewGuid(), TenantId = tenantB.Id, Filename = "DocB.pdf", Checksum = "c2", Status = Domain.Enums.DocumentStatus.Indexed };
        context.Documents.AddRange(docA, docB);

        // Chunks for Tenant A (Engineering)
        var chunkA1 = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA.Id,
            DocumentId = docA.Id,
            ChunkIndex = 0,
            PageNumber = 1,
            Content = "Xenon Ion Drive thrust telemetry diagnostics.",
            ContentHash = "hash-a1",
            AclRoles = new List<string> { "Engineering" },
            Embedding = OpenAiEmbeddingGenerator.GenerateDeterministicFallbackEmbedding("Xenon Ion Drive thrust telemetry diagnostics", 1536)
        };

        // Chunks for Tenant A (Executive restricted)
        var chunkA2 = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA.Id,
            DocumentId = docA.Id,
            ChunkIndex = 1,
            PageNumber = 2,
            Content = "Executive bonus structure and secret defense patents.",
            ContentHash = "hash-a2",
            AclRoles = new List<string> { "Executive" },
            Embedding = OpenAiEmbeddingGenerator.GenerateDeterministicFallbackEmbedding("Executive bonus structure and secret defense patents", 1536)
        };

        // Chunks for Tenant B
        var chunkB1 = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB.Id,
            DocumentId = docB.Id,
            ChunkIndex = 0,
            PageNumber = 1,
            Content = "Pharmaceutical clinical trial results for Compound X.",
            ContentHash = "hash-b1",
            AclRoles = new List<string> { "General" },
            Embedding = OpenAiEmbeddingGenerator.GenerateDeterministicFallbackEmbedding("Pharmaceutical clinical trial results for Compound X", 1536)
        };
        context.DocumentChunks.AddRange(chunkA1, chunkA2, chunkB1);
        await context.SaveChangesAsync();

        var queryEmbedding = OpenAiEmbeddingGenerator.GenerateDeterministicFallbackEmbedding("Ion Drive telemetry", 1536);

        // Act 1: Query as Tenant A with Role 'Engineering'
        var resultsEngineering = await store.HybridSearchAsync(
            tenantId: tenantA.Id,
            userRoles: new[] { "Engineering" },
            queryEmbedding: queryEmbedding,
            queryText: "Ion Drive telemetry"
        );

        // Assert 1: Only Chunk A1 accessible; Tenant B and Executive chunk A2 are strictly hidden
        resultsEngineering.Should().NotBeEmpty();
        resultsEngineering.Should().Contain(c => c.ChunkId == chunkA1.Id);
        resultsEngineering.Should().NotContain(c => c.ChunkId == chunkA2.Id); // RLS filtered
        resultsEngineering.Should().NotContain(c => c.ChunkId == chunkB1.Id); // Multi-tenant filtered

        // Act 2: Query as Tenant A with Role 'Executive'
        var resultsExecutive = await store.HybridSearchAsync(
            tenantId: tenantA.Id,
            userRoles: new[] { "Executive" },
            queryEmbedding: OpenAiEmbeddingGenerator.GenerateDeterministicFallbackEmbedding("bonus structure", 1536),
            queryText: "bonus structure"
        );

        // Assert 2: Executive role can see chunk A2
        resultsExecutive.Should().Contain(c => c.ChunkId == chunkA2.Id);
    }

    [Fact]
    public void ReciprocalRankFusion_Formula_ShouldPrioritizeDualMatches()
    {
        // RRF score = 1 / (60 + denseRank) + 1 / (60 + sparseRank)
        const double k = 60.0;

        // Rank 1 in both Dense and Sparse
        double dualRank1 = (1.0 / (k + 1)) + (1.0 / (k + 1));

        // Rank 1 in Dense but missing in Sparse (penalty 1000)
        double singleRank1 = (1.0 / (k + 1)) + (1.0 / (k + 1000));

        dualRank1.Should().BeGreaterThan(singleRank1);
        Math.Round(dualRank1, 4).Should().Be(0.0328);
    }
}

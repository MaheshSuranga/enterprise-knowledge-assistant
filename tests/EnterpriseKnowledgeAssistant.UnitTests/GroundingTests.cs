using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Domain.Enums;
using EnterpriseKnowledgeAssistant.Infrastructure.AI;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EnterpriseKnowledgeAssistant.UnitTests;

public class GroundingTests
{
    [Fact]
    public async Task SemanticKernelService_ShouldRefuse_WhenContextIsEmpty()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"OpenAI:ApiKey", "offline"}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var service = new SemanticKernelService(config, NullLogger<SemanticKernelService>.Instance);

        // Act
        var result = await service.GenerateGroundedAnswerAsync(
            question: "What is the secret launch code?",
            contextChunks: Array.Empty<RerankedChunk>(),
            conversationHistory: Array.Empty<ChatMessageDto>()
        );

        // Assert
        result.IsGrounded.Should().BeFalse();
        result.RefusalReason.Should().Be(RefusalReason.InsufficientContext);
        result.Answer.Should().Contain("I cannot answer this question based on the provided corporate documentation.");
        result.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task SemanticKernelService_ShouldCiteExactSnippet_WhenGrounded()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"OpenAI:ApiKey", "offline"}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var service = new SemanticKernelService(config, NullLogger<SemanticKernelService>.Instance);

        var chunkId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var retrievedChunk = new RetrievedChunk(
            ChunkId: chunkId,
            DocumentId: docId,
            DocumentName: "Thermal_Limits.pdf",
            PageNumber: 4,
            ChunkIndex: 0,
            Content: "## Thermal Dissipation Limits\nThe cooling loop maintains hydraulic equilibrium at 350 Bar of pressure.",
            ContentHash: "hash123",
            DenseScore: 0.92,
            DenseRank: 1,
            SparseScore: 2.1,
            SparseRank: 1,
            RrfScore: 0.032,
            BoundingBox: new BoundingBoxDto(4, 0.1, 0.2, 0.8, 0.1),
            AclRoles: new[] { "Engineering" }
        );

        var rerankedChunk = new RerankedChunk(retrievedChunk, 0.95, 1);

        // Act
        var result = await service.GenerateGroundedAnswerAsync(
            question: "What pressure does the cooling loop maintain?",
            contextChunks: new[] { rerankedChunk },
            conversationHistory: Array.Empty<ChatMessageDto>()
        );

        // Assert
        result.IsGrounded.Should().BeTrue();
        result.RefusalReason.Should().BeNull();
        result.Answer.Should().Contain("[1]");
        result.Citations.Should().HaveCount(1);
        result.Citations[0].PageNumber.Should().Be(4);
        result.Citations[0].DocumentName.Should().Be("Thermal_Limits.pdf");
        result.Citations[0].ExactQuote.Should().Contain("350 Bar");
    }
}

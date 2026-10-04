using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Infrastructure.Chunking;
using FluentAssertions;
using Xunit;

namespace EnterpriseKnowledgeAssistant.UnitTests;

public class ChunkingTests
{
    [Fact]
    public void ComputeDeterministicHash_ShouldBeConsistent_ForIdenticalContent()
    {
        // Arrange
        var content = "## Section 1: Reactor Core\nThe core temperature is regulated at 450C.";

        // Act
        var hash1 = RecursiveMarkdownChunker.ComputeDeterministicHash(content);
        var hash2 = RecursiveMarkdownChunker.ComputeDeterministicHash(content);

        // Assert
        hash1.Should().NotBeNullOrEmpty();
        hash1.Length.Should().Be(64); // SHA-256 hex string
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ChunkDocument_ShouldRetainSectionHeaders_AndPreserveLineage()
    {
        // Arrange
        var chunker = new RecursiveMarkdownChunker();
        var blocks = new List<ParsedTextBlock>
        {
            new("System Overview", new BoundingBoxDto(1, 0.1, 0.1, 0.8, 0.05), "Helvetica-Bold", 16.0, true),
            new("The secondary thruster utilizes liquid xenon propellant.", new BoundingBoxDto(1, 0.1, 0.2, 0.8, 0.1), "Helvetica", 11.0, false)
        };

        var parsedDoc = new ParsedDocument("specs.pdf", 1, new[]
        {
            new ParsedPage(1, 600, 800, blocks)
        });

        // Act
        var chunks = chunker.ChunkDocument(parsedDoc, targetChunkSize: 50, chunkOverlap: 10);

        // Assert
        chunks.Should().NotBeEmpty();
        var firstChunk = chunks.First();
        firstChunk.PageNumber.Should().Be(1);
        firstChunk.Content.Should().Contain("## System Overview");
        firstChunk.Content.Should().Contain("liquid xenon propellant");
        firstChunk.ContentHash.Should().NotBeNullOrEmpty();
        firstChunk.PrimaryBoundingBox.Should().NotBeNull();
        firstChunk.PrimaryBoundingBox!.PageNumber.Should().Be(1);
    }
}

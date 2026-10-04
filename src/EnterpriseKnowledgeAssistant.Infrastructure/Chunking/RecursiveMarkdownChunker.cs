using System.Security.Cryptography;
using System.Text;
using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Application.Interfaces;

namespace EnterpriseKnowledgeAssistant.Infrastructure.Chunking;

public class RecursiveMarkdownChunker : IDocumentChunker
{
    public IReadOnlyList<ChunkResult> ChunkDocument(
        ParsedDocument parsedDocument,
        int targetChunkSize = 512,
        int chunkOverlap = 64)
    {
        var results = new List<ChunkResult>();
        int chunkIndex = 0;
        string currentSection = "General Overview";

        // Convert token estimates (1 token ~= 4 characters in English)
        int targetCharSize = targetChunkSize * 4;
        int overlapChars = chunkOverlap * 4;

        foreach (var page in parsedDocument.Pages)
        {
            var currentChunkText = new StringBuilder();
            BoundingBoxDto? firstBoxInChunk = null;
            BoundingBoxDto? lastBoxInChunk = null;

            foreach (var block in page.TextBlocks)
            {
                if (block.IsHeader)
                {
                    currentSection = block.Text;
                }

                // If adding this block exceeds target size and currentChunk has content, emit chunk
                if (currentChunkText.Length > 0 && (currentChunkText.Length + block.Text.Length) > targetCharSize)
                {
                    var rawContent = currentChunkText.ToString().Trim();
                    var contentWithHeader = $"## {currentSection}\n{rawContent}";
                    var hash = ComputeDeterministicHash(contentWithHeader);

                    var combinedBox = MergeBoxes(firstBoxInChunk, lastBoxInChunk);

                    results.Add(new ChunkResult(
                        ChunkIndex: chunkIndex++,
                        PageNumber: page.PageNumber,
                        Content: contentWithHeader,
                        ContentHash: hash,
                        PrimaryBoundingBox: combinedBox,
                        SectionHeader: currentSection
                    ));

                    // Carry over overlap text
                    var overlap = rawContent.Length > overlapChars 
                        ? rawContent[^overlapChars..] 
                        : rawContent;

                    currentChunkText.Clear();
                    currentChunkText.Append(overlap).Append(" ");
                    firstBoxInChunk = block.BoundingBox;
                }

                if (firstBoxInChunk == null)
                {
                    firstBoxInChunk = block.BoundingBox;
                }
                lastBoxInChunk = block.BoundingBox;

                currentChunkText.AppendLine(block.Text);
            }

            // Flush remaining text on page
            if (currentChunkText.Length > 0)
            {
                var rawContent = currentChunkText.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(rawContent))
                {
                    var contentWithHeader = $"## {currentSection}\n{rawContent}";
                    var hash = ComputeDeterministicHash(contentWithHeader);
                    var combinedBox = MergeBoxes(firstBoxInChunk, lastBoxInChunk);

                    results.Add(new ChunkResult(
                        ChunkIndex: chunkIndex++,
                        PageNumber: page.PageNumber,
                        Content: contentWithHeader,
                        ContentHash: hash,
                        PrimaryBoundingBox: combinedBox,
                        SectionHeader: currentSection
                    ));
                }
            }
        }

        return results;
    }

    public static string ComputeDeterministicHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static BoundingBoxDto? MergeBoxes(BoundingBoxDto? a, BoundingBoxDto? b)
    {
        if (a == null) return b;
        if (b == null) return a;
        if (a.PageNumber != b.PageNumber) return a;

        var minLeft = Math.Min(a.Left, b.Left);
        var minTop = Math.Min(a.Top, b.Top);
        var maxRight = Math.Max(a.Left + a.Width, b.Left + b.Width);
        var maxBottom = Math.Max(a.Top + a.Height, b.Top + b.Height);

        return new BoundingBoxDto(
            PageNumber: a.PageNumber,
            Left: Math.Round(minLeft, 4),
            Top: Math.Round(minTop, 4),
            Width: Math.Round(maxRight - minLeft, 4),
            Height: Math.Round(maxBottom - minTop, 4)
        );
    }
}

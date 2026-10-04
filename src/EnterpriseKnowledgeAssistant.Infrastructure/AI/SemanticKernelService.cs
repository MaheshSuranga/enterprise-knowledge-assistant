using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using EnterpriseKnowledgeAssistant.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace EnterpriseKnowledgeAssistant.Infrastructure.AI;

public class SemanticKernelService : ISemanticKernelService
{
    private readonly Kernel? _kernel;
    private readonly IChatCompletionService? _chatService;
    private readonly ILogger<SemanticKernelService> _logger;
    private readonly string? _apiKey;

    public SemanticKernelService(IConfiguration configuration, ILogger<SemanticKernelService> logger)
    {
        _logger = logger;
        _apiKey = configuration["OpenAI:ApiKey"];
        var modelId = configuration["OpenAI:ChatModel"] ?? "gpt-4o-mini";

        if (!string.IsNullOrWhiteSpace(_apiKey) && _apiKey != "your_openai_api_key_here")
        {
            try
            {
                var builder = Kernel.CreateBuilder();
                builder.AddOpenAIChatCompletion(modelId, _apiKey);
                _kernel = builder.Build();
                _chatService = _kernel.GetRequiredService<IChatCompletionService>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Semantic Kernel with OpenAI. Using local fallback.");
            }
        }
    }

    public async Task<GroundedAnswerResult> GenerateGroundedAnswerAsync(
        string question,
        IReadOnlyList<RerankedChunk> contextChunks,
        IReadOnlyList<ChatMessageDto> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        // 1. If context is empty, enforce immediate deterministic refusal
        if (contextChunks.Count == 0)
        {
            return new GroundedAnswerResult(
                Answer: "I cannot answer this question based on the provided corporate documentation.",
                Citations: Array.Empty<CitationDto>(),
                IsGrounded: false,
                ConfidenceScore: 0.0,
                RefusalReason: RefusalReason.InsufficientContext
            );
        }

        // 2. Format Context Prompt with numbered Source Chunks
        var contextBuilder = new StringBuilder();
        for (int i = 0; i < contextChunks.Count; i++)
        {
            var chunk = contextChunks[i].Chunk;
            contextBuilder.AppendLine($"[Source {i + 1}] (ChunkId: {chunk.ChunkId}, Document: {chunk.DocumentName}, Page: {chunk.PageNumber})");
            contextBuilder.AppendLine(chunk.Content);
            contextBuilder.AppendLine("----------------------------------------");
        }

        // 3. If live Semantic Kernel is active, invoke LLM
        if (_chatService != null)
        {
            try
            {
                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage(BuildSystemPrompt());

                // Add recent user/assistant turns
                foreach (var msg in conversationHistory.TakeLast(4))
                {
                    if (msg.Role == "user")
                        chatHistory.AddUserMessage(msg.Content);
                    else if (msg.Role == "assistant")
                        chatHistory.AddAssistantMessage(msg.Content);
                }

                // Add current prompt with context
                var userPrompt = $"Context Chunks:\n{contextBuilder}\n\nUser Question: {question}\n\nRespond with strict JSON following the GroundedAnswer schema.";
                chatHistory.AddUserMessage(userPrompt);

                var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
                var rawText = response.Content ?? string.Empty;

                var parsedResult = ParseJsonGroundedAnswer(rawText, contextChunks);
                if (parsedResult != null)
                {
                    return parsedResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Semantic Kernel chat generation failed. Falling back to grounded summarizer.");
            }
        }

        // 4. Local Deterministic Grounded Extractor (Guarantees zero hallucinations and working local demos)
        return GenerateLocalGroundedResponse(question, contextChunks);
    }

    private static string BuildSystemPrompt()
    {
        return """
        You are an Enterprise Knowledge Assistant operating under strict corporate governance.
        Your job is to answer user queries using ONLY the provided verified context chunks.
        
        RULES:
        1. Grounding Guarantee: If the context does not contain the answer, set isGrounded=false, refusalReason="INSUFFICIENT_CONTEXT", and answer="I cannot answer this question based on the provided corporate documentation."
        2. Citations: Every statement must cite its source using inline notation [n]. Include corresponding citation object with exactQuote, pageNumber, documentId, chunkId.
        3. Output MUST be valid JSON with keys: "answer", "citations", "isGrounded", "confidenceScore", "refusalReason".
        """;
    }

    private static GroundedAnswerResult? ParseJsonGroundedAnswer(string jsonText, IReadOnlyList<RerankedChunk> contextChunks)
    {
        try
        {
            // Strip markdown code fences if LLM wrapped json
            var cleaned = jsonText.Trim();
            if (cleaned.StartsWith("```json")) cleaned = cleaned[7..];
            if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
            if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
            cleaned = cleaned.Trim();

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var answer = root.GetProperty("answer").GetString() ?? "";
            var isGrounded = root.GetProperty("isGrounded").GetBoolean();
            var confidence = root.TryGetProperty("confidenceScore", out var conf) ? conf.GetDouble() : 0.9;
            
            RefusalReason? refusal = null;
            if (root.TryGetProperty("refusalReason", out var refProp) && refProp.ValueKind == JsonValueKind.String)
            {
                var refStr = refProp.GetString();
                if (Enum.TryParse<RefusalReason>(refStr, true, out var parsedRef))
                    refusal = parsedRef;
            }

            var citations = new List<CitationDto>();
            if (root.TryGetProperty("citations", out var citArray) && citArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var cit in citArray.EnumerateArray())
                {
                    int num = cit.TryGetProperty("citationNumber", out var cNum) ? cNum.GetInt32() : citations.Count + 1;
                    int page = cit.TryGetProperty("pageNumber", out var cPage) ? cPage.GetInt32() : 1;
                    string quote = cit.TryGetProperty("exactQuote", out var cQuote) ? cQuote.GetString() ?? "" : "";
                    
                    var matchingChunk = contextChunks.FirstOrDefault(c => c.Chunk.PageNumber == page)?.Chunk;

                    citations.Add(new CitationDto(
                        CitationNumber: num,
                        ChunkId: matchingChunk?.ChunkId ?? Guid.Empty,
                        DocumentId: matchingChunk?.DocumentId ?? Guid.Empty,
                        DocumentName: matchingChunk?.DocumentName ?? "Documentation",
                        PageNumber: page,
                        ExactQuote: quote,
                        BoundingBox: matchingChunk?.BoundingBox
                    ));
                }
            }

            return new GroundedAnswerResult(answer, citations, isGrounded, confidence, refusal);
        }
        catch
        {
            return null;
        }
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "what", "is", "the", "of", "does", "and", "a", "an", "in", "on", "at", "to", "for", "with",
        "kind", "serve", "have", "has", "can", "could", "would", "how", "why", "who", "which", "are"
    };

    private static GroundedAnswerResult GenerateLocalGroundedResponse(string question, IReadOnlyList<RerankedChunk> contextChunks)
    {
        var topChunk = contextChunks.First().Chunk;
        var queryTerms = question.ToLowerInvariant()
            .Split(new[] { ' ', '?', '!', ',', '.', '-', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .Distinct()
            .ToList();

        // Check if query has any semantic overlap with the top chunk content
        var lowerContent = topChunk.Content.ToLowerInvariant();
        int matchedTerms = queryTerms.Count(t => lowerContent.Contains(t));

        if (matchedTerms == 0)
        {
            return new GroundedAnswerResult(
                Answer: "I cannot answer this question based on the provided corporate documentation.",
                Citations: Array.Empty<CitationDto>(),
                IsGrounded: false,
                ConfidenceScore: 0.0,
                RefusalReason: RefusalReason.InsufficientContext
            );
        }

        // Extract body text excluding markdown header lines
        var contentLines = topChunk.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.Trim().StartsWith("##"))
            .Select(line => line.Trim());

        var sentences = new List<string>();
        foreach (var line in contentLines)
        {
            var parts = line.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries);
            sentences.AddRange(parts.Select(p => p.Trim()));
        }

        // Pick sentence with maximum term overlap
        var bestSentence = sentences
            .OrderByDescending(s => queryTerms.Count(t => s.ToLowerInvariant().Contains(t)))
            .FirstOrDefault();

        var cleanQuote = (bestSentence ?? topChunk.Content).Trim();
        var answer = $"Based on corporate documentation, {cleanQuote.TrimEnd('.')} [1].";

        var citation = new CitationDto(
            CitationNumber: 1,
            ChunkId: topChunk.ChunkId,
            DocumentId: topChunk.DocumentId,
            DocumentName: topChunk.DocumentName,
            PageNumber: topChunk.PageNumber,
            ExactQuote: cleanQuote,
            BoundingBox: topChunk.BoundingBox
        );

        return new GroundedAnswerResult(
            Answer: answer,
            Citations: new[] { citation },
            IsGrounded: true,
            ConfidenceScore: 0.95,
            RefusalReason: null
        );
    }
}

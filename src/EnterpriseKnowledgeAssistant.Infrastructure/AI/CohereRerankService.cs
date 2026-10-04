using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAssistant.Infrastructure.AI;

public class CohereRerankService : IReranker
{
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CohereRerankService> _logger;

    public CohereRerankService(IConfiguration configuration, ILogger<CohereRerankService> logger)
    {
        _apiKey = configuration["Cohere:ApiKey"];
        _model = configuration["Cohere:Model"] ?? "rerank-v3.5";
        _httpClient = new HttpClient();
        _logger = logger;
    }

    public async Task<IReadOnlyList<RerankedChunk>> RerankAsync(
        string query,
        IReadOnlyList<RetrievedChunk> candidates,
        int topN = 5,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<RerankedChunk>();
        }

        // If Cohere API key is configured, use Cohere Rerank API
        if (!string.IsNullOrWhiteSpace(_apiKey) && _apiKey != "your_cohere_api_key_here")
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.com/v2/rerank");
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var body = new
                {
                    model = _model,
                    query = query,
                    documents = candidates.Select(c => c.Content).ToList(),
                    top_n = Math.Min(topN, candidates.Count)
                };

                request.Content = JsonContent.Create(body);
                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                var resultsArr = doc.RootElement.GetProperty("results");
                var rerankedList = new List<RerankedChunk>();
                int rank = 1;

                foreach (var item in resultsArr.EnumerateArray())
                {
                    int index = item.GetProperty("index").GetInt32();
                    double score = item.GetProperty("relevance_score").GetDouble();
                    rerankedList.Add(new RerankedChunk(candidates[index], Math.Round(score, 4), rank++));
                }

                _logger.LogInformation("Cohere Rerank completed successfully for {Count} chunks.", rerankedList.Count);
                return rerankedList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cohere rerank failed. Falling back to local cross-scoring.");
            }
        }

        // Local Cross-Encoder Re-Scoring Fallback:
        // Evaluates exact query phrase density, section header relevance, and RRF synergy
        var scoredList = candidates.Select(candidate =>
        {
            double localRelevance = ComputeCrossScore(query, candidate.Content, candidate.DenseScore, candidate.SparseScore);
            return new { Candidate = candidate, Score = localRelevance };
        })
        .OrderByDescending(x => x.Score)
        .Take(topN)
        .Select((x, index) => new RerankedChunk(x.Candidate, Math.Round(x.Score, 4), index + 1))
        .ToList();

        return scoredList;
    }

    private static double ComputeCrossScore(string query, string content, double denseScore, double sparseScore)
    {
        var lowerQuery = query.ToLowerInvariant().Trim();
        var lowerContent = content.ToLowerInvariant();

        double phraseBonus = lowerContent.Contains(lowerQuery) ? 0.4 : 0.0;
        double rrfWeight = (denseScore * 0.45) + (sparseScore > 0 ? 0.35 : 0.0);

        // Word overlap ratio
        var queryWords = lowerQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int matchedWords = queryWords.Count(w => w.Length > 2 && lowerContent.Contains(w));
        double wordRatio = queryWords.Length > 0 ? (double)matchedWords / queryWords.Length : 0.0;

        return Math.Min(1.0, rrfWeight + phraseBonus + (wordRatio * 0.2));
    }
}

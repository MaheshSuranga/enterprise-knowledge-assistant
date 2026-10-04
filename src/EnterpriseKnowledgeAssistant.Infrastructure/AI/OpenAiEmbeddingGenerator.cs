using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAssistant.Infrastructure.AI;

public class OpenAiEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiEmbeddingGenerator> _logger;

    public int EmbeddingDimension => 1536;

    public OpenAiEmbeddingGenerator(IConfiguration configuration, ILogger<OpenAiEmbeddingGenerator> logger)
    {
        _apiKey = configuration["OpenAI:ApiKey"];
        _model = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
        _httpClient = new HttpClient();
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var batch = await GenerateEmbeddingsBatchAsync(new[] { text }, cancellationToken);
        return batch.FirstOrDefault() ?? GenerateDeterministicFallbackEmbedding(text, EmbeddingDimension);
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "your_openai_api_key_here")
        {
            _logger.LogWarning("OpenAI API key not configured. Using deterministic high-entropy vector embeddings for local execution.");
            return texts.Select(t => GenerateDeterministicFallbackEmbedding(t, EmbeddingDimension)).ToList();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");

            var body = new
            {
                input = texts,
                model = _model,
                dimensions = EmbeddingDimension
            };

            request.Content = JsonContent.Create(body);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var dataArr = doc.RootElement.GetProperty("data");
            var results = new List<float[]>();

            foreach (var item in dataArr.EnumerateArray())
            {
                var embArr = item.GetProperty("embedding");
                var vec = new float[EmbeddingDimension];
                int i = 0;
                foreach (var val in embArr.EnumerateArray())
                {
                    if (i < EmbeddingDimension)
                    {
                        vec[i++] = val.GetSingle();
                    }
                }
                results.Add(vec);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI embedding generation failed. Falling back to deterministic embeddings.");
            return texts.Select(t => GenerateDeterministicFallbackEmbedding(t, EmbeddingDimension)).ToList();
        }
    }

    /// <summary>
    /// Produces a deterministic, unit-normalized 1536-dimensional vector based on token frequencies and cryptographic hashing.
    /// This enables rich local testing, CI builds, and offline development without external API costs.
    /// </summary>
    public static float[] GenerateDeterministicFallbackEmbedding(string text, int dimensions)
    {
        var vector = new float[dimensions];
        var words = text.ToLowerInvariant().Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(word));
            for (int i = 0; i < hash.Length && (i * 48) < dimensions; i++)
            {
                int index = (hash[i] * 6 + i) % dimensions;
                vector[index] += 1.0f;
            }
        }

        // L2 normalize vector
        double sumSq = 0;
        for (int i = 0; i < dimensions; i++)
        {
            sumSq += vector[i] * vector[i];
        }

        if (sumSq > 0)
        {
            float norm = (float)Math.Sqrt(sumSq);
            for (int i = 0; i < dimensions; i++)
            {
                vector[i] /= norm;
            }
        }
        else
        {
            vector[0] = 1.0f;
        }

        return vector;
    }
}

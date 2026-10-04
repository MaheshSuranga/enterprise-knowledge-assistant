using EnterpriseKnowledgeAssistant.Application.Interfaces;
using EnterpriseKnowledgeAssistant.Infrastructure.AI;
using EnterpriseKnowledgeAssistant.Infrastructure.Chunking;
using EnterpriseKnowledgeAssistant.Infrastructure.Parsing;
using EnterpriseKnowledgeAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseKnowledgeAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var useInMemory = bool.TryParse(configuration["UseInMemoryDatabase"], out var inMem) && inMem;

        if (useInMemory || string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("localhost_placeholder", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("EnterpriseKnowledgeDb"));
        }
        else
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString, o => o.UseVector()));
        }

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IPdfParser, PdfPigDocumentParser>();
        services.AddScoped<IDocumentChunker, RecursiveMarkdownChunker>();
        services.AddScoped<IEmbeddingGenerator, OpenAiEmbeddingGenerator>();
        services.AddScoped<IVectorStore, PostgresVectorStore>();
        services.AddScoped<IReranker, CohereRerankService>();
        services.AddScoped<ISemanticKernelService, SemanticKernelService>();

        return services;
    }
}

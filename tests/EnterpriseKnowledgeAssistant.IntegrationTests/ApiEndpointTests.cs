using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Application.Queries;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EnterpriseKnowledgeAssistant.IntegrationTests;

public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        // Set default tenant header
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", "11111111-1111-1111-1111-111111111111");
        _client.DefaultRequestHeaders.Add("X-User-Roles", "Engineering,General");
    }

    [Fact]
    public async Task GetTenants_ShouldReturnSeededTenants()
    {
        // Act
        var response = await _client.GetAsync("/api/tenants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<List<TenantDto>>();
        tenants.Should().NotBeNull();
        tenants.Should().Contain(t => t.Name == "Acme Aerospace");
        tenants.Should().Contain(t => t.Name == "Globex Health");
    }

    [Fact]
    public async Task AskChat_ShouldReturnGroundedAnswer_WithCitations_AndInspectionDetails()
    {
        // Arrange
        var request = new
        {
            Question = "What is the peak chamber temperature of the Acme Ion-Drive?"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat/ask", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var answerResult = await response.Content.ReadFromJsonAsync<AskQuestionResponse>();

        answerResult.Should().NotBeNull();
        answerResult!.IsGrounded.Should().BeTrue();
        answerResult.Answer.Should().Contain("2,450 Kelvin");
        answerResult.Answer.Should().Contain("[1]");
        answerResult.Citations.Should().NotBeEmpty();
        answerResult.Citations[0].PageNumber.Should().Be(1);
        answerResult.Inspection.Should().NotBeNull();
        answerResult.Inspection.Latencies.TotalLatencyMs.Should().BeGreaterThanOrEqualTo(0);
        answerResult.Inspection.TopRetrievedCandidates.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AskChat_ShouldRefuse_WhenQueryCannotBeAnsweredFromContext()
    {
        // Arrange
        var request = new
        {
            Question = "What kind of ice cream does the chef serve on weekends?"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat/ask", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var answerResult = await response.Content.ReadFromJsonAsync<AskQuestionResponse>();

        answerResult.Should().NotBeNull();
        answerResult!.IsGrounded.Should().BeFalse();
        answerResult.RefusalReason.Should().NotBeNull();
        answerResult.Answer.Should().Contain("I cannot answer this question based on the provided corporate documentation.");
    }
}

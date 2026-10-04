using EnterpriseKnowledgeAssistant.Application.Queries;
using EnterpriseKnowledgeAssistant.Application.RAG;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseKnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;

    public ChatController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public record AskRequest(
        Guid? ConversationId,
        string Question,
        int? TopKCandidates = 25,
        int? TopNReranked = 5
    );

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { error = "Question cannot be empty." });
        }

        var command = new AskQuestionCommand(
            ConversationId: request.ConversationId,
            Question: request.Question,
            TopKCandidates: request.TopKCandidates ?? 25,
            TopNReranked: request.TopNReranked ?? 5
        );

        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var result = await _mediator.Send(new GetConversationsQuery());
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }
        return Ok(result.Value);
    }

    [HttpGet("conversations/{id}/messages")]
    public async Task<IActionResult> GetConversationMessages(Guid id)
    {
        var result = await _mediator.Send(new GetConversationMessagesQuery(id));
        if (!result.IsSuccess)
        {
            return NotFound(new { error = result.Error });
        }
        return Ok(result.Value);
    }
}

using EnterpriseKnowledgeAssistant.Application.Common;
using EnterpriseKnowledgeAssistant.Application.Documents;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using EnterpriseKnowledgeAssistant.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public DocumentsController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        IAppDbContext dbContext,
        IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetDocuments()
    {
        var tenantId = _currentUserService.TenantId;
        var docs = await _dbContext.Documents
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.Filename,
                d.FileSizeBytes,
                d.PageCount,
                d.Status,
                ChunkCount = d.Chunks.Count,
                d.CreatedAt
            })
            .ToListAsync();

        return Ok(docs);
    }

    public class DocumentUploadRequest
    {
        public IFormFile? File { get; set; }
        public string? AclRoles { get; set; }
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument([FromForm] DocumentUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded or file is empty." });
        }

        var tenantId = _currentUserService.TenantId;

        // Persist file locally in uploads folder
        var uploadsDir = Path.Combine(_environment.ContentRootPath, "uploads", tenantId.ToString());
        Directory.CreateDirectory(uploadsDir);
        var filePath = Path.Combine(uploadsDir, request.File.FileName);

        using (var fs = new FileStream(filePath, FileMode.Create))
        {
            await request.File.CopyToAsync(fs);
        }

        var rolesList = string.IsNullOrWhiteSpace(request.AclRoles)
            ? new List<string> { "General", "Engineering" }
            : request.AclRoles.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).ToList();

        using var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var command = new UploadDocumentCommand(
            TenantId: tenantId,
            Filename: request.File.FileName,
            ContentType: request.File.ContentType ?? "application/pdf",
            FileSizeBytes: request.File.Length,
            FileStream: readStream,
            AclRoles: rolesList
        );

        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpGet("{id}/chunks")]
    public async Task<IActionResult> GetDocumentChunks(Guid id)
    {
        var result = await _mediator.Send(new GetDocumentChunksQuery(id));
        if (!result.IsSuccess)
        {
            return NotFound(new { error = result.Error });
        }
        return Ok(result.Value);
    }

    [HttpGet("{id}/file")]
    public async Task<IActionResult> GetDocumentFile(Guid id)
    {
        var tenantId = _currentUserService.TenantId;
        var doc = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);
        if (doc == null)
        {
            return NotFound(new { error = "Document not found." });
        }

        var uploadsDir = Path.Combine(_environment.ContentRootPath, "uploads", tenantId.ToString());
        var filePath = Path.Combine(uploadsDir, doc.Filename);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { error = "Physical document file not found on disk." });
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(fileBytes, "application/pdf", doc.Filename);
    }
}

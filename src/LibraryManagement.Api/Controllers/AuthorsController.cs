using System.ComponentModel.DataAnnotations;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Api.Controllers;

[ApiController]
[Route("api/authors")]
public class AuthorsController(IAuthorService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AuthorResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    [ProducesResponseType<AuthorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthorResponse>> GetById(
        [Range(1, long.MaxValue, ErrorMessage = "Author ID must be positive.")] long id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<AuthorResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthorResponse>> Create(CreateAuthorRequest request, CancellationToken cancellationToken)
    {
        var author = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = author.Id }, author);
    }
}

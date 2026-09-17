using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Exceptions;
using LibraryManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Api.Controllers;

[ApiController]
[Route("api/books")]
public class BooksController(IBookService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BookResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var book = await service.GetByIdAsync(id, cancellationToken);
        return book is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Book not found.")
            : Ok(book);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookResponse>> Update(long id, BookRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var book = await service.UpdateAsync(id, request, cancellationToken);
            return book is null
                ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Book not found.")
                : Ok(book);
        }
        catch (DuplicateIsbnException exception)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
        }
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Book not found.");

    [HttpPost]
    [ProducesResponseType<BookResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookResponse>> Create(BookRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var book = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
        }
        catch (DuplicateIsbnException exception)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
        }
    }
}

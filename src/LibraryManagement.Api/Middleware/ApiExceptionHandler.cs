using LibraryManagement.Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace LibraryManagement.Api.Middleware;

public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            BookNotFoundException => (StatusCodes.Status404NotFound, "Book not found."),
            AuthorNotFoundException => (StatusCodes.Status404NotFound, "Author not found."),
            DuplicateIsbnException => (StatusCodes.Status409Conflict, "A book with this ISBN already exists."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            // .NET 10 suppresses framework diagnostics for handled exceptions.
            // Keep unexpected failures in server logs, never in the response body.
            logger.LogError(exception, "Unhandled API error. TraceId: {TraceId}",
                System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier);
        }

        await ApiProblemResponses.Create(httpContext, status, title).ExecuteAsync(httpContext);
        return true;
    }
}

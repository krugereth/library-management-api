using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Api.Middleware;

public static class ApiProblemResponses
{
    public static void Customize(ProblemDetails problem, HttpContext context)
    {
        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
    }

    public static IResult Create(HttpContext context, int status, string? title = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        // Populate before writing so these fields survive JSON fallback when
        // the client's Accept header is unsupported by IProblemDetailsService.
        Customize(problem, context);
        return Results.Problem(problem);
    }
}

using LibraryManagement.Api.Data;
using LibraryManagement.Api.Repositories;
using LibraryManagement.Api.Services;
using LibraryManagement.Api.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database configuration is missing. Set ConnectionStrings__DefaultConnection.");
}

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseNpgsql(connectionString, postgres => postgres.SetPostgresVersion(17, 0)));

builder.Services.AddControllers();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        ApiProblemResponses.Customize(context.ProblemDetails, context.HttpContext);
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(context =>
    ApiProblemResponses.Create(context.HttpContext, context.HttpContext.Response.StatusCode)
        .ExecuteAsync(context.HttpContext));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();

// Exposes the entry point to WebApplicationFactory in the test project.
public partial class Program { }

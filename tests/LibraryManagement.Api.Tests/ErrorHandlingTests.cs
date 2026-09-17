using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManagement.Api.Exceptions;
using LibraryManagement.Api.Models;
using LibraryManagement.Api.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LibraryManagement.Api.Tests;

// These tests exercise real controllers/services/middleware without a database.
public class ErrorHandlingTests
{
    private const string InternalDiagnostic = "sensitive-internal-diagnostic";

    private static WebApplicationFactory<Program> CreateFactory(string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=unused;Username=test");
            builder.UseSetting("Logging:LogLevel:Default", "None");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBookRepository>();
                services.AddScoped<IBookRepository, StubBookRepository>();
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

    private static JsonContent ValidRequest() => JsonContent.Create(new
    {
        title = "Book", isbn = "isbn-1", availableCopies = 1
    });

    [Theory]
    [InlineData("Development", "application/json")]
    [InlineData("Development", "text/html")]
    [InlineData("Development", "text/plain")]
    [InlineData("Production", "application/json")]
    [InlineData("Production", "text/html")]
    [InlineData("Production", "text/plain")]
    public async Task UnexpectedErrorsHaveSafeJsonResponse(string environment, string accept)
    {
        await using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        using var response = await client.GetAsync("/api/books?debug=private-query");
        using var body = await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "/api/books");
        Assert.Equal("An unexpected error occurred.", body.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("private-query", body.RootElement.GetRawText());
        Assert.False(body.RootElement.TryGetProperty("exception", out _));
        Assert.False(body.RootElement.TryGetProperty("stackTrace", out _));
    }

    [Theory]
    [InlineData("Development", "GET", "/api/books/999", 404, "Book not found.")]
    [InlineData("Development", "PUT", "/api/books/999", 404, "Book not found.")]
    [InlineData("Development", "DELETE", "/api/books/999", 404, "Book not found.")]
    [InlineData("Development", "POST", "/api/books", 409, "A book with this ISBN already exists.")]
    [InlineData("Production", "GET", "/api/books/999", 404, "Book not found.")]
    [InlineData("Production", "PUT", "/api/books/999", 404, "Book not found.")]
    [InlineData("Production", "DELETE", "/api/books/999", 404, "Book not found.")]
    [InlineData("Production", "POST", "/api/books", 409, "A book with this ISBN already exists.")]
    public async Task DomainErrorsUseCentralHandler(string environment, string method, string path, int status, string title)
    {
        await using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "PUT" or "POST") request.Content = ValidRequest();
        using var response = await client.SendAsync(request);
        using var body = await AssertProblemAsync(response, (HttpStatusCode)status, path);
        Assert.Equal(title, body.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/missing", 404)]
    [InlineData("PATCH", "/api/books/1", 405)]
    [InlineData("GET", "/api/books/not-a-number", 404)]
    [InlineData("GET", "/api/books/0", 400)]
    [InlineData("PUT", "/api/books/0", 400)]
    [InlineData("DELETE", "/api/books/0", 400)]
    [InlineData("GET", "/api/books/-1", 400)]
    [InlineData("PUT", "/api/books/-1", 400)]
    [InlineData("DELETE", "/api/books/-1", 400)]
    public async Task RoutingAndIdErrorsUseProblemDetails(string method, string path, int status)
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "PUT") request.Content = ValidRequest();
        using var response = await client.SendAsync(request);
        using var body = await AssertProblemAsync(response, (HttpStatusCode)status, path);
        if (status == 400)
        {
            var errors = body.RootElement.GetProperty("errors").GetProperty("id");
            Assert.Equal("Book ID must be positive.", errors[0].GetString());
        }
        if (status == 405) Assert.Contains("PUT", response.Content.Headers.Allow);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    [InlineData("text/html")]
    public async Task ValidationIncludesFieldMessagesAndTraceId(string accept)
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        using var response = await client.PostAsJsonAsync("/api/books", new
        {
            title = " ", isbn = "", availableCopies = -1, publicationYear = 0
        });
        using var body = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "/api/books");
        var errors = body.RootElement.GetProperty("errors");
        Assert.Equal("Title is required.", errors.GetProperty("Title")[0].GetString());
        Assert.Equal("ISBN is required.", errors.GetProperty("Isbn")[0].GetString());
        Assert.Equal("Available copies must be zero or greater.", errors.GetProperty("AvailableCopies")[0].GetString());
        Assert.Equal("Publication year must be between 1 and 9999.", errors.GetProperty("PublicationYear")[0].GetString());
    }

    [Fact]
    public async Task UnsupportedContentTypeUsesProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var content = new StringContent("not JSON");
        using var response = await client.PostAsync("/api/books", content);
        using var body = await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "/api/books");
    }

    private static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string path)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(InternalDiagnostic, json);
        Assert.DoesNotContain(nameof(InvalidOperationException), json);
        var document = JsonDocument.Parse(json);
        Assert.Equal((int)status, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(path, document.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        return document;
    }

    private sealed class StubBookRepository : IBookRepository
    {
        public Task<List<Book>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException(InternalDiagnostic);
        public Task<Book?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult<Book?>(null);
        public Task AddAsync(Book book, CancellationToken cancellationToken) =>
            throw new DuplicateIsbnException(new InvalidOperationException(InternalDiagnostic));
        public Task<bool> UpdateAsync(Book book, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}

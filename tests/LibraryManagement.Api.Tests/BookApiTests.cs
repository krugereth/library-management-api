using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LibraryManagement.Api.Tests;

// Each test gets a fresh schema in an explicitly named test database.
// Never run migrations or delete data in either application's development schema.
public class BookApiTests : IAsyncLifetime
{
    internal const string ConnectionVariable = "LibraryManagement__TestConnection";
    private readonly string schema = $"books_test_{Guid.NewGuid():N}";
    private string connectionString = null!;
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(ConnectionVariable));
        if (connection.Database != "library_management_cs_tests")
        {
            throw new InvalidOperationException(
                "Book API tests require database library_management_cs_tests.");
        }

        connection.SearchPath = schema;
        connection.Pooling = false;
        connectionString = connection.ConnectionString;
        await ExecuteAsync($"CREATE SCHEMA \"{schema}\"");

        try
        {
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
                builder.UseSetting("Logging:LogLevel:Default", "None");
            });
            client = factory.CreateClient();
            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            await context.Database.MigrateAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
        if (connectionString is not null)
        {
            await ExecuteAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static object ValidBook(string isbn = "9780132350884") => new
    {
        title = "Clean Code", isbn, publicationYear = 2008, availableCopies = 3
    };

    [PostgresFact]
    public async Task EmptyDatabaseReturnsEmptyList()
    {
        using var response = await client.GetAsync("/api/books");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<BookResponse>>())!);
    }

    [PostgresFact]
    public async Task CreateReturnsLocationAndPersistsAcrossRequests()
    {
        using var response = await client.PostAsJsonAsync("/api/books", ValidBook());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<BookResponse>())!;
        Assert.True(created.Id > 0);
        Assert.Equal("Clean Code", created.Title);
        Assert.Equal("9780132350884", created.Isbn);
        Assert.Equal(2008, created.PublicationYear);
        Assert.Equal(3, created.AvailableCopies);
        Assert.Equal($"/api/books/{created.Id}", response.Headers.Location!.AbsolutePath);

        var fetched = await client.GetFromJsonAsync<BookResponse>(response.Headers.Location);
        Assert.Equal(created, fetched);
        var books = await client.GetFromJsonAsync<List<BookResponse>>("/api/books");
        Assert.Equal(created, Assert.Single(books!));

        // Confirm that the application and migrations agree about the database model.
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(created.Title, (await context.Books.AsNoTracking().SingleAsync()).Title);
    }

    [PostgresFact]
    public async Task MissingBookReturnsProblemDetails()
    {
        using var response = await client.GetAsync("/api/books/999");
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [PostgresFact]
    public async Task CreateTrimsTextAndAllowsOmittedYearAndZeroCopies()
    {
        using var response = await client.PostAsJsonAsync("/api/books", new
        {
            title = "  A Book  ", isbn = "  isbn-1  ", availableCopies = 0
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = (await response.Content.ReadFromJsonAsync<BookResponse>())!;
        Assert.Equal("A Book", book.Title);
        Assert.Equal("isbn-1", book.Isbn);
        Assert.Null(book.PublicationYear);
        Assert.Equal(0, book.AvailableCopies);
    }

    [PostgresFact]
    public async Task DuplicateIsbnReturnsConflictWithoutCreatingAnotherBook()
    {
        using var first = await client.PostAsJsonAsync("/api/books", ValidBook());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await client.PostAsJsonAsync("/api/books", ValidBook(" 9780132350884 "));
        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict);
        Assert.Single((await client.GetFromJsonAsync<List<BookResponse>>("/api/books"))!);
    }

    [PostgresFact]
    public async Task ConcurrentDuplicateRequestsCreateOnlyOneBook()
    {
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/books", ValidBook()),
            client.PostAsJsonAsync("/api/books", ValidBook()));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await AssertProblemAsync(conflict, HttpStatusCode.Conflict);
            Assert.Single((await client.GetFromJsonAsync<List<BookResponse>>("/api/books"))!);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [PostgresTheory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{invalid json")]
    [InlineData("{\"title\":\" \",\"isbn\":\"isbn-1\",\"availableCopies\":1}")]
    [InlineData("{\"title\":\"Book\",\"isbn\":\" \",\"availableCopies\":1}")]
    [InlineData("{\"title\":\"Book\",\"isbn\":\"isbn-1\"}")]
    [InlineData("{\"title\":\"Book\",\"isbn\":\"isbn-1\",\"availableCopies\":null}")]
    [InlineData("{\"title\":\"Book\",\"isbn\":\"isbn-1\",\"availableCopies\":-1}")]
    [InlineData("{\"title\":\"Book\",\"isbn\":\"isbn-1\",\"availableCopies\":1,\"publicationYear\":0}")]
    [InlineData("{\"title\":\"Book\",\"isbn\":\"isbn-1\",\"availableCopies\":1,\"publicationYear\":10000}")]
    public async Task InvalidRequestsReturn400AndDoNotPersist(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/books", content);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty((await client.GetFromJsonAsync<List<BookResponse>>("/api/books"))!);
    }

    [PostgresTheory]
    [InlineData(256, 10)]
    [InlineData(10, 33)]
    public async Task OversizedTextReturns400(int titleLength, int isbnLength)
    {
        using var response = await client.PostAsJsonAsync("/api/books", new
        {
            title = new string('a', titleLength), isbn = new string('1', isbnLength), availableCopies = 1
        });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("title").GetString()));
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BookApiTests.ConnectionVariable)))
            Skip = "Set LibraryManagement__TestConnection to run PostgreSQL integration tests.";
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BookApiTests.ConnectionVariable)))
            Skip = "Set LibraryManagement__TestConnection to run PostgreSQL integration tests.";
    }
}

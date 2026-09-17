using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LibraryManagement.Api.Tests;

public class BookAuthorApiTests : PostgresApiTestBase
{
    [PostgresFact]
    public async Task MultipleAuthorsCanBeSharedAndAreReturnedInIdOrder()
    {
        var first = await CreateAuthorAsync("First");
        var second = await CreateAuthorAsync("Second");
        var one = await CreateBookAsync("one", second.Id, first.Id);
        var two = await CreateBookAsync("two", first.Id);
        Assert.Equal(new[] { first, second }, one.Authors);
        Assert.Equal(first, Assert.Single(two.Authors));
        Assert.Equivalent(one, await client.GetFromJsonAsync<BookResponse>($"/api/books/{one.Id}"));
        Assert.Equivalent(new[] { one, two }, await client.GetFromJsonAsync<List<BookResponse>>("/api/books"));
        Assert.Equal(2, (await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!.Count);
        await AssertLinkCountAsync(3);
    }

    [PostgresFact]
    public async Task PutReplacesLinksAndRepeatingItDoesNotDuplicateThem()
    {
        var first = await CreateAuthorAsync("First");
        var second = await CreateAuthorAsync("Second");
        var third = await CreateAuthorAsync("Third");
        var book = await CreateBookAsync("one", first.Id, second.Id);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await client.PutAsJsonAsync($"/api/books/{book.Id}", Payload("one", third.Id, second.Id));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = (await response.Content.ReadFromJsonAsync<BookResponse>())!;
            Assert.Equal(new[] { second, third }, updated.Authors);
        }
        var fetched = (await client.GetFromJsonAsync<BookResponse>($"/api/books/{book.Id}"))!;
        Assert.Equal(new[] { second, third }, fetched.Authors);
        Assert.Equal(new[] { first, second, third }, (await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!);
        await AssertLinkCountAsync(2);
    }

    [PostgresTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmptyOrOmittedIdsClearLinksWithoutDeletingAuthors(bool includeEmptyIds)
    {
        var author = await CreateAuthorAsync("First");
        var book = await CreateBookAsync("one", author.Id);
        object payload = includeEmptyIds ? Payload("one") : new { title = "Book", isbn = "one", availableCopies = 2 };
        using var response = await client.PutAsJsonAsync($"/api/books/{book.Id}", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<BookResponse>())!.Authors);
        Assert.Empty((await client.GetFromJsonAsync<BookResponse>($"/api/books/{book.Id}"))!.Authors);
        Assert.Equal(author, await client.GetFromJsonAsync<AuthorResponse>($"/api/authors/{author.Id}"));
        await AssertLinkCountAsync(0);
    }

    [PostgresTheory]
    [InlineData("null")]
    [InlineData("[0]")]
    [InlineData("[-1]")]
    [InlineData("[1,1]")]
    [InlineData("[null]")]
    [InlineData("[1.5]")]
    [InlineData("1")]
    public async Task InvalidIdsReturn400AndDoNotChangeBooks(string idsJson)
    {
        var author = await CreateAuthorAsync("First");
        var original = await CreateBookAsync("original", author.Id);
        var json = "{\"title\":\"Changed\",\"isbn\":\"new\",\"availableCopies\":0,\"authorIds\":" + idsJson + "}";
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put })
        {
            using var request = new HttpRequestMessage(method, method == HttpMethod.Post ? "/api/books" : $"/api/books/{original.Id}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var response = await client.SendAsync(request);
            await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        }
        Assert.Equivalent(original, Assert.Single((await client.GetFromJsonAsync<List<BookResponse>>("/api/books"))!));
        await AssertLinkCountAsync(1);
    }

    [PostgresFact]
    public async Task MissingAuthorRejectsWholeCreateAndUpdate()
    {
        var author = await CreateAuthorAsync("First");
        var original = await CreateBookAsync("original", author.Id);
        var newAuthor = await CreateAuthorAsync("Second");
        using var create = await client.PostAsJsonAsync("/api/books", Payload("new", newAuthor.Id, long.MaxValue));
        await AssertProblemAsync(create, HttpStatusCode.NotFound, "Author not found.");
        using var update = await client.PutAsJsonAsync($"/api/books/{original.Id}", Payload("changed", newAuthor.Id, long.MaxValue));
        await AssertProblemAsync(update, HttpStatusCode.NotFound, "Author not found.");
        Assert.Equivalent(original, Assert.Single((await client.GetFromJsonAsync<List<BookResponse>>("/api/books"))!));
        await AssertLinkCountAsync(1);
    }

    [PostgresFact]
    public async Task IsbnConflictRollsBackScalarAndRelationshipChanges()
    {
        var first = await CreateAuthorAsync("First");
        var second = await CreateAuthorAsync("Second");
        var original = await CreateBookAsync("original", first.Id);
        var other = await CreateBookAsync("taken", second.Id);
        using var update = await client.PutAsJsonAsync($"/api/books/{original.Id}", Payload("taken", second.Id));
        await AssertProblemAsync(update, HttpStatusCode.Conflict);
        using var create = await client.PostAsJsonAsync("/api/books", Payload("taken", first.Id));
        await AssertProblemAsync(create, HttpStatusCode.Conflict);
        Assert.Equivalent(new[] { original, other }, await client.GetFromJsonAsync<List<BookResponse>>("/api/books"));
        await AssertLinkCountAsync(2);
    }

    [PostgresFact]
    public async Task DeleteCascadesLinksAndPreservesSharedAuthorAndOtherBook()
    {
        var author = await CreateAuthorAsync("First");
        var first = await CreateBookAsync("one", author.Id);
        var second = await CreateBookAsync("two", author.Id);
        using var deletion = await client.DeleteAsync($"/api/books/{first.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        Assert.Equivalent(second, Assert.Single((await client.GetFromJsonAsync<List<BookResponse>>("/api/books"))!));
        Assert.Equal(author, Assert.Single((await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!));
        await AssertLinkCountAsync(1);
    }

    [PostgresFact]
    public async Task DatabaseEnforcesUniqueLinksForeignKeysAndAuthorDeleteRestriction()
    {
        var author = await CreateAuthorAsync("First");
        var book = await CreateBookAsync("one", author.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO book_authors (\"BookId\", \"AuthorId\") VALUES ({book.Id}, {author.Id})"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        var missing = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO book_authors (\"BookId\", \"AuthorId\") VALUES ({book.Id}, {long.MaxValue})"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, missing.SqlState);
        var deleted = await Assert.ThrowsAsync<PostgresException>(() => context.Authors.Where(a => a.Id == author.Id).ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, deleted.SqlState);
        await AssertLinkCountAsync(1);
    }

    [PostgresFact]
    public async Task MigrationPreservesExistingBooksAndAuthors()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var migrator = context.GetService<IMigrator>();
        // Only this test's private schema is migrated backwards to seed old-schema data.
        await migrator.MigrateAsync("AddAuthors");
        var book = new LibraryManagement.Api.Models.Book { Title = "Existing", Isbn = "existing", AvailableCopies = 2 };
        var author = new LibraryManagement.Api.Models.Author { FirstName = "Existing", LastName = "Author" };
        context.Books.Add(book);
        context.Authors.Add(author);
        await context.SaveChangesAsync();
        await migrator.MigrateAsync();
        var fetched = (await client.GetFromJsonAsync<BookResponse>($"/api/books/{book.Id}"))!;
        Assert.Equal("Existing", fetched.Title);
        Assert.Equal("existing", fetched.Isbn);
        Assert.Equal(2, fetched.AvailableCopies);
        Assert.Empty(fetched.Authors);
        Assert.Equal("Existing", (await client.GetFromJsonAsync<AuthorResponse>($"/api/authors/{author.Id}"))!.FirstName);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static object Payload(string isbn, params long[] authorIds) =>
        new { title = "Book", isbn, availableCopies = 2, authorIds };

    private async Task<AuthorResponse> CreateAuthorAsync(string name)
    {
        using var response = await client.PostAsJsonAsync("/api/authors", new { firstName = name, lastName = "Author" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthorResponse>())!;
    }

    private async Task<BookResponse> CreateBookAsync(string isbn, params long[] ids)
    {
        using var response = await client.PostAsJsonAsync("/api/books", Payload(isbn, ids));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookResponse>())!;
    }

    private async Task AssertLinkCountAsync(int expected)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        Assert.Equal(expected, await context.Set<Dictionary<string, object>>("book_authors").CountAsync());
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string? title = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, body.RootElement.GetProperty("status").GetInt32());
        if (title is not null) Assert.Equal(title, body.RootElement.GetProperty("title").GetString());
    }
}

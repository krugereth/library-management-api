using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Exceptions;
using LibraryManagement.Api.Models;
using LibraryManagement.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Api.Tests;

public class AuthorApiTests : PostgresApiTestBase
{
    [PostgresFact]
    public async Task EmptyDatabaseReturnsEmptyList()
    {
        using var response = await client.GetAsync("/api/authors");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<AuthorResponse>>())!);
    }

    [PostgresFact]
    public async Task CreateTrimsNamesAndPersistsAcrossRequests()
    {
        using var response = await client.PostAsJsonAsync("/api/authors", new
        {
            firstName = "  Ursula K.  ", lastName = "  Le Guin  "
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<AuthorResponse>())!;
        Assert.True(created.Id > 0);
        Assert.Equal("Ursula K.", created.FirstName);
        Assert.Equal("Le Guin", created.LastName);
        Assert.Equal($"/api/authors/{created.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal(created, await client.GetFromJsonAsync<AuthorResponse>(response.Headers.Location));
        Assert.Equal(created, Assert.Single((await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var persisted = await context.Authors.AsNoTracking().SingleAsync();
        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(created.FirstName, persisted.FirstName);
        Assert.Equal(created.LastName, persisted.LastName);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task AuthorsWithSameNamesHaveDistinctIdsAndListInIdOrder()
    {
        using var first = await client.PostAsJsonAsync("/api/authors", new { firstName = "Alex", lastName = "Smith" });
        using var second = await client.PostAsJsonAsync("/api/authors", new { firstName = "Alex", lastName = "Smith" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var one = (await first.Content.ReadFromJsonAsync<AuthorResponse>())!;
        var two = (await second.Content.ReadFromJsonAsync<AuthorResponse>())!;
        Assert.True(two.Id > one.Id);
        Assert.Equal(new[] { one, two }, (await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!);
    }

    [PostgresTheory]
    [InlineData("999", 404)]
    [InlineData("0", 400)]
    [InlineData("-1", 400)]
    [InlineData("not-a-number", 404)]
    public async Task MissingAndInvalidIdsReturnProblemDetails(string id, int status)
    {
        var path = $"/api/authors/{id}";
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Put) request.Content = JsonContent.Create(new { firstName = "First", lastName = "Last" });
            using var response = await client.SendAsync(request);
            using var problem = await AssertProblemAsync(response, (HttpStatusCode)status, path);
            if (id == "999") Assert.Equal("Author not found.", problem.RootElement.GetProperty("title").GetString());
            if (status == 400)
                Assert.Equal("Author ID must be positive.", problem.RootElement.GetProperty("errors").GetProperty("id")[0].GetString());
        }
        Assert.Empty((await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!);
    }

    [PostgresTheory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{invalid json")]
    [InlineData("{\"firstName\":\"Ursula\"}")]
    [InlineData("{\"lastName\":\"Le Guin\"}")]
    [InlineData("{\"firstName\":null,\"lastName\":\"Le Guin\"}")]
    [InlineData("{\"firstName\":\"Ursula\",\"lastName\":null}")]
    [InlineData("{\"firstName\":\" \",\"lastName\":\"Le Guin\"}")]
    [InlineData("{\"firstName\":\"Ursula\",\"lastName\":\" \",\"id\":1}")]
    [InlineData("{\"firstName\":\"Ursula\",\"lastName\":\" \"}")]
    [InlineData("{\"name\":\"Ursula K. Le Guin\"}")]
    [InlineData("{\"firstName\":\"Ursula\",\"lastName\":\"Le Guin\",\"id\":123}")]
    public async Task InvalidRequestsReturn400WithoutPersisting(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/authors", content);
        using var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "/api/authors");
        Assert.NotEmpty(problem.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Empty((await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!);

        var original = await CreateAuthorAsync("Original");
        using var updateContent = new StringContent(json, Encoding.UTF8, "application/json");
        using var update = await client.PutAsync($"/api/authors/{original.Id}", updateContent);
        using var updateProblem = await AssertProblemAsync(update, HttpStatusCode.BadRequest, $"/api/authors/{original.Id}");
        Assert.Equal(original, await client.GetFromJsonAsync<AuthorResponse>($"/api/authors/{original.Id}"));
    }

    [PostgresTheory]
    [InlineData(101, 1)]
    [InlineData(1, 101)]
    public async Task OversizedNamesReturn400(int firstLength, int lastLength)
    {
        using var response = await client.PostAsJsonAsync("/api/authors", new
        {
            firstName = new string('a', firstLength), lastName = new string('b', lastLength)
        });
        using var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "/api/authors");
        var field = firstLength > 100 ? "FirstName" : "LastName";
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        Assert.Empty((await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!);

        var original = await CreateAuthorAsync("Original");
        using var update = await client.PutAsJsonAsync($"/api/authors/{original.Id}", new
        {
            firstName = new string('a', firstLength), lastName = new string('b', lastLength)
        });
        using var updateProblem = await AssertProblemAsync(update, HttpStatusCode.BadRequest, $"/api/authors/{original.Id}");
        Assert.Equal(original, await client.GetFromJsonAsync<AuthorResponse>($"/api/authors/{original.Id}"));
    }

    [PostgresFact]
    public async Task MaximumLengthNamesAreAccepted()
    {
        var first = new string('a', 100);
        var last = new string('b', 100);
        using var response = await client.PostAsJsonAsync("/api/authors", new { firstName = first, lastName = last });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var author = (await response.Content.ReadFromJsonAsync<AuthorResponse>())!;
        Assert.Equal(first, author.FirstName);
        Assert.Equal(last, author.LastName);
        using var update = await client.PutAsJsonAsync($"/api/authors/{author.Id}", new { firstName = last, lastName = first });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(author with { FirstName = last, LastName = first }, await update.Content.ReadFromJsonAsync<AuthorResponse>());
    }

    [PostgresFact]
    public async Task UpdateTrimsNamesAndUpdatesSharedBookViewsWithoutChangingLinks()
    {
        var original = await CreateAuthorAsync("Original");
        var other = await CreateAuthorAsync("Other");
        var one = await CreateBookAsync("one", original.Id, other.Id);
        var two = await CreateBookAsync("two", original.Id);
        var expected = original with { FirstName = other.FirstName, LastName = other.LastName };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var update = await client.PutAsJsonAsync($"/api/authors/{original.Id}", new
            {
                firstName = " Other ", lastName = " Author "
            });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            Assert.Equal(expected, await update.Content.ReadFromJsonAsync<AuthorResponse>());
        }
        Assert.Equal(expected, await client.GetFromJsonAsync<AuthorResponse>($"/api/authors/{original.Id}"));
        Assert.Equal(new[] { expected, other }, (await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!);
        var expectedOne = one with { Authors = new[] { expected, other } };
        var expectedTwo = two with { Authors = new[] { expected } };
        Assert.Equivalent(expectedOne, await client.GetFromJsonAsync<BookResponse>($"/api/books/{one.Id}"), strict: true);
        Assert.Equivalent(new[] { expectedOne, expectedTwo }, await client.GetFromJsonAsync<List<BookResponse>>("/api/books"), strict: true);
    }

    [PostgresFact]
    public async Task DeleteUnlinkedAuthorReturns204Then404AndPreservesOthers()
    {
        var author = await CreateAuthorAsync("First");
        var other = await CreateAuthorAsync("Other");
        using var response = await client.DeleteAsync($"/api/authors/{author.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        using var lookup = await client.GetAsync($"/api/authors/{author.Id}");
        using var lookupProblem = await AssertProblemAsync(lookup, HttpStatusCode.NotFound, $"/api/authors/{author.Id}");
        using var repeated = await client.DeleteAsync($"/api/authors/{author.Id}");
        using var deleteProblem = await AssertProblemAsync(repeated, HttpStatusCode.NotFound, $"/api/authors/{author.Id}");
        Assert.Equal(other, Assert.Single((await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!));
    }

    [PostgresFact]
    public async Task LinkedAuthorCannotBeDeletedUntilAllBooksAreUnlinked()
    {
        var author = await CreateAuthorAsync("First");
        var one = await CreateBookAsync("one", author.Id);
        var two = await CreateBookAsync("two", author.Id);
        using var rejected = await client.DeleteAsync($"/api/authors/{author.Id}");
        using var problem = await AssertProblemAsync(rejected, HttpStatusCode.Conflict, $"/api/authors/{author.Id}");
        Assert.Equal("Author is linked to books. Remove those links before deleting the author.", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(author, await client.GetFromJsonAsync<AuthorResponse>($"/api/authors/{author.Id}"));
        Assert.Equivalent(new[] { one, two }, await client.GetFromJsonAsync<List<BookResponse>>("/api/books"), strict: true);

        using var firstUnlink = await client.PutAsJsonAsync($"/api/books/{one.Id}", new { title = one.Title, isbn = one.Isbn, availableCopies = 1, authorIds = Array.Empty<long>() });
        Assert.Equal(HttpStatusCode.OK, firstUnlink.StatusCode);
        using var stillLinked = await client.DeleteAsync($"/api/authors/{author.Id}");
        using var stillLinkedProblem = await AssertProblemAsync(stillLinked, HttpStatusCode.Conflict, $"/api/authors/{author.Id}");
        using var secondUnlink = await client.PutAsJsonAsync($"/api/books/{two.Id}", new { title = two.Title, isbn = two.Isbn, availableCopies = 1, authorIds = Array.Empty<long>() });
        Assert.Equal(HttpStatusCode.OK, secondUnlink.StatusCode);
        using var deleted = await client.DeleteAsync($"/api/authors/{author.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var books = (await client.GetFromJsonAsync<List<BookResponse>>("/api/books"))!;
        Assert.Equal(2, books.Count);
        Assert.All(books, book => Assert.Empty(book.Authors));
    }

    [PostgresFact]
    public async Task StaleAuthorUpdateDoesNotRecreateDeletedRecord()
    {
        var author = await CreateAuthorAsync("First");
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuthorRepository>();
        var stale = (await repository.GetByIdAsync(author.Id, CancellationToken.None))!;
        using var deleted = await client.DeleteAsync($"/api/authors/{author.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        stale.FirstName = "Changed";
        Assert.False(await repository.UpdateAsync(stale, CancellationToken.None));
        Assert.Empty((await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!);
    }

    [PostgresFact]
    public async Task AuthorDeletedAfterLookupRejectsBookSaveWithoutPartialData()
    {
        var author = await CreateAuthorAsync("First");
        await using var scope = factory.Services.CreateAsyncScope();
        var authors = scope.ServiceProvider.GetRequiredService<IAuthorRepository>();
        var staleAuthors = await authors.GetByIdsAsync(new[] { author.Id }, CancellationToken.None);
        using var deleted = await client.DeleteAsync($"/api/authors/{author.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var book = new Book { Title = "Unsaved", Isbn = "unsaved", AvailableCopies = 1, Authors = staleAuthors };
        var books = scope.ServiceProvider.GetRequiredService<IBookRepository>();
        await Assert.ThrowsAsync<AuthorNotFoundException>(() => books.AddAsync(book, CancellationToken.None));
        Assert.Empty((await client.GetFromJsonAsync<List<BookResponse>>("/api/books"))!);
        Assert.Empty((await client.GetFromJsonAsync<List<AuthorResponse>>("/api/authors"))!);
    }

    private async Task<AuthorResponse> CreateAuthorAsync(string firstName)
    {
        using var response = await client.PostAsJsonAsync("/api/authors", new { firstName, lastName = "Author" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthorResponse>())!;
    }

    private async Task<BookResponse> CreateBookAsync(string isbn, params long[] authorIds)
    {
        using var response = await client.PostAsJsonAsync("/api/books", new { title = "Book", isbn, availableCopies = 1, authorIds });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookResponse>())!;
    }

    private static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string path)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(path, document.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        return document;
    }
}

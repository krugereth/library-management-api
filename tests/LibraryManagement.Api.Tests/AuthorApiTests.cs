using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
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
        using var response = await client.GetAsync(path);
        using var problem = await AssertProblemAsync(response, (HttpStatusCode)status, path);
        if (id == "999") Assert.Equal("Author not found.", problem.RootElement.GetProperty("title").GetString());
        if (status == 400)
            Assert.Equal("Author ID must be positive.", problem.RootElement.GetProperty("errors").GetProperty("id")[0].GetString());
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

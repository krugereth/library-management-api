using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using LibraryManagement.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Api.Tests;

public class ApplicationStartupTests
{
    // Deliberately has no password: these tests never open a database connection.
    private const string TestConnection =
        "Host=localhost;Port=5433;Database=library_management_cs_tests;Username=test";

    private static WebApplicationFactory<Program> CreateFactory(
        string environment, string connectionString = TestConnection) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        });

    [Fact]
    public async Task DevelopmentPublishesOpenApiDocument()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.StartsWith("3.", document.RootElement.GetProperty("openapi").GetString());
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("paths").ValueKind);
    }

    [Fact]
    public async Task ProductionDoesNotExposeOpenApiDocument()
    {
        await using var factory = CreateFactory("Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingDatabaseConfigurationFailsWithHelpfulMessage(string connectionString)
    {
        using var factory = CreateFactory("Development", connectionString);

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Equal(
            "Database configuration is missing. Set ConnectionStrings__DefaultConnection.",
            error.Message);
    }

    [Fact]
    public void DatabaseContextUsesConfiguredPostgresConnectionAndRequestScope()
    {
        using var factory = CreateFactory("Development");
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();

        var context = firstScope.ServiceProvider.GetRequiredService<LibraryDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal(TestConnection, context.Database.GetConnectionString());
        Assert.Same(context, firstScope.ServiceProvider.GetRequiredService<LibraryDbContext>());
        Assert.NotSame(context, secondScope.ServiceProvider.GetRequiredService<LibraryDbContext>());
    }
}

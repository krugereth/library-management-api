using LibraryManagement.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LibraryManagement.Api.Tests;

// Each test gets a fresh schema in an explicitly named test database.
// Never run migrations or delete data in either application's development schema.
public abstract class PostgresApiTestBase : IAsyncLifetime
{
    internal const string ConnectionVariable = "LibraryManagement__TestConnection";
    private readonly string schema = $"api_test_{Guid.NewGuid():N}";
    private string connectionString = null!;
    protected WebApplicationFactory<Program> factory = null!;
    protected HttpClient client = null!;

    public async Task InitializeAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(ConnectionVariable));
        if (connection.Database != "library_management_cs_tests")
        {
            throw new InvalidOperationException(
                "API tests require database library_management_cs_tests.");
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

}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresApiTestBase.ConnectionVariable)))
            Skip = "Set LibraryManagement__TestConnection to run PostgreSQL integration tests.";
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresApiTestBase.ConnectionVariable)))
            Skip = "Set LibraryManagement__TestConnection to run PostgreSQL integration tests.";
    }
}

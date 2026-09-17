# Library Management REST API

A portfolio backend project being migrated from Java/Spring Boot to C# and ASP.NET Core, one milestone at a time. The target application will manage books, authors, members, and borrowing records.

## Current milestone: PostgreSQL with Entity Framework Core

The C# implementation currently includes:

- A .NET 10 solution with an ASP.NET Core Web API project and an xUnit test project.
- Controller routing and dependency injection setup.
- The folders for the planned layered architecture.
- OpenAPI JSON at `/openapi/v1.json` in Development.
- EF Core with Npgsql and a scoped `LibraryDbContext`.
- An initial baseline migration and repository-local `dotnet-ef` tooling.
- Startup tests for documentation, required configuration, and database context registration.

The C# database is configured, but there are no business endpoints or domain tables yet. The root URL and `/api/books` currently return `404`. Interactive Swagger UI will be added in a later milestone; this foundation exposes the OpenAPI document only.

## Preserved Java implementation

The existing Java code remains in `src/main/java` and `src/test/java`, with its Maven build files. It is also preserved in Git at commit `8bf6e80`, before the `csharp-migration` branch was created.

The Java implementation has:

- Book create, list, lookup, update, and delete endpoints.
- Author create, list, and lookup endpoints.
- One optional author per book, shared across multiple books.
- Request validation, DTOs, and structured errors.
- PostgreSQL persistence and controller/database relationship tests.

Author update/deletion, members, loans, and search are not implemented in Java. These existing Java features are not yet ported to C#.

See the [Java setup and API reference](docs/java/README.md) to run the original application. Commands in that guide are run from the repository root. Its database, `library_management`, is retained.

## Technology and architecture

Foundation: **C#, .NET 10, ASP.NET Core Web API, OpenAPI, xUnit**.

Persistence: **Entity Framework Core, Npgsql, PostgreSQL 17, EF Core migrations**.

The target request flow is:

```text
Controller → Service → Repository → EF Core → PostgreSQL
```

```text
LibraryManagement.sln
 global.json
 src/
 ├── LibraryManagement.Api/
 │   ├── Controllers/
 │   ├── Services/
 │   ├── Repositories/
 │   ├── Data/
 │   ├── Models/
 │   ├── DTOs/
 │   ├── Exceptions/
 │   ├── Middleware/
 │   ├── Program.cs
 │   └── appsettings.json
 ├── main/                           Preserved Java application
 └── test/                           Preserved Java tests
 tests/
 └── LibraryManagement.Api.Tests/
 docs/
 └── java/README.md
```

The C# architecture folders have `.gitkeep` files so Git preserves them while they are empty. Business classes will be introduced when their milestones need them.

## Run the C# API

Install the **.NET 10 SDK**, which includes the ASP.NET Core runtime. A runtime-only installation cannot build the project. Download the SDK for your operating system and CPU from [Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

From the repository root, verify the SDK:

```bash
dotnet --version
dotnet --info
```

`global.json` requires a stable .NET 10 SDK and allows newer installed 10.0 feature bands and patches. It does not silently select .NET 5 or .NET 11.

Restore packages and build:

```bash
dotnet restore LibraryManagement.sln
dotnet build LibraryManagement.sln --no-restore
```

Configure the database connection and apply migrations using the [database setup](#database-and-secrets) below. Then start the development HTTP profile:

```bash
dotnet run --project src/LibraryManagement.Api --launch-profile http
```

Open `http://localhost:5080/openapi/v1.json` in a browser or send a GET request from Postman. The document has no business paths yet. Press **Ctrl+C** to stop the application.

These `dotnet` commands work from macOS Terminal and Windows PowerShell. Java uses port 8080; the C# development profile uses 5080.

### Optional local HTTPS

To use the HTTPS profile, trust the local development certificate and run:

```bash
dotnet dev-certs https --trust
dotnet run --project src/LibraryManagement.Api --launch-profile https
```

The HTTPS URL is `https://localhost:7080/openapi/v1.json`. Development supports local HTTP; outside Development, the application enables HTTPS redirection and does not map the OpenAPI endpoint.

## Tests

Run the tests after building:

```bash
dotnet test LibraryManagement.sln --no-build --no-restore
```

The five xUnit test cases boot the application through `WebApplicationFactory` and verify:

- Development serves a valid OpenAPI JSON document.
- Production does not expose that document.
- Empty or whitespace-only database configuration stops startup with an actionable error.
- The context uses Npgsql, the configured connection, and a separate instance per dependency-injection scope.

These tests use an in-process ASP.NET Core test server and a test-only connection setting without a password. They never open a database connection and need no PostgreSQL server or credentials. Applying and listing the migrations below provides a separate live PostgreSQL check. They do not test business features, which are not implemented yet.

## From Spring Boot to ASP.NET Core

| Previous Java concept | C# equivalent |
| --- | --- |
| Spring Boot application startup | `Program.cs` builds and runs the ASP.NET Core host |
| Spring dependency injection | Services registered through `builder.Services` |
| `@RestController` | A controller using `[ApiController]` and `ControllerBase` |
| `@RequestMapping` / `@GetMapping` | `[Route]` / `[HttpGet]` |
| `application.properties` | `appsettings.json` plus environment variables |
| Maven / dependencies in `pom.xml` | `dotnet` CLI / NuGet references in `.csproj` |
| JUnit / MockMvc | xUnit / `WebApplicationFactory` for HTTP integration tests |
| Hibernate / JPA | EF Core with `LibraryDbContext` tracking database changes |

`AddControllers()` registers controller services; `MapControllers()` makes controller routes reachable. No controller actions are defined yet. The public partial `Program` declaration allows the test project to boot the application's entry point.

Microsoft references: [controller-based APIs](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api?view=aspnetcore-10.0), [integration testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0), and [SDK selection with global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).

## Database and secrets

Use PostgreSQL 17 at `localhost:5433` with database **`library_management_cs`**. The Java database `library_management` is separate and must not be used for C# migrations. Port 5432 may belong to a different PostgreSQL installation.

### This MacBook

The development database and its dedicated login role, both named `library_management_cs`, have been created. The role owns only the C# database and has neither superuser nor database-creation privileges. Its fresh password is stored as part of the connection string in macOS Keychain, outside the repository.

Load it into each new Terminal session without displaying it:

```bash
export ConnectionStrings__DefaultConnection="$(security find-generic-password -s library-management-api-cs-connection -a library_management_cs -w)"
```

The Keychain entry exists only on this Mac; cloning the repository does not copy credentials.

### Another development machine

Have a PostgreSQL administrator create the `library_management_cs` login and database, using a fresh password. For example, inside `psql` as an administrator:

```sql
CREATE ROLE library_management_cs LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE;
\password library_management_cs
CREATE DATABASE library_management_cs OWNER library_management_cs;
```

`\password` prompts for the new password. Supply the connection through your local secret store or the environment variable `ConnectionStrings__DefaultConnection`. Its format is:

```text
Host=localhost;Port=5433;Database=library_management_cs;Username=library_management_cs;Password=<your fresh local password>
```

This is a placeholder, not a usable credential. Do not save a real password in tracked files or type it into shell history. ASP.NET Core maps the double underscore in the environment variable to `ConnectionStrings:DefaultConnection`. Startup fails with a helpful message if the setting is missing or blank.

### Apply migrations

From the repository root, with the connection variable set:

```bash
dotnet tool restore
dotnet ef database update --project src/LibraryManagement.Api
dotnet ef migrations list --project src/LibraryManagement.Api
```

`InitialDatabase` is intentionally empty: it establishes a baseline and EF Core's `__EFMigrationsHistory` table. There are no books, authors, members, or loan tables yet. Re-running `database update` is safe when all migrations are already applied.

`LibraryDbContext` is EF Core's database session and change tracker, comparable to Hibernate's persistence context. Dependency injection creates one context per request scope. Npgsql translates EF operations for PostgreSQL. Migrations are versioned schema changes; unlike Hibernate automatic schema updates, they are explicitly generated, reviewed, and applied. The API does not automatically create or migrate the database at startup, and startup alone does not verify database connectivity.

When a future milestone adds or changes an entity, generate its migration, review it, and then apply it:

```bash
dotnet ef migrations add DescribeSchemaChange --project src/LibraryManagement.Api --output-dir Data/Migrations
dotnet ef database update --project src/LibraryManagement.Api
```

Never commit passwords or connection strings containing credentials. The previously exposed Java password is not reused for C#. Local secret files and build output are ignored by Git, but review staged changes before committing.

References: [Npgsql EF Core provider](https://www.npgsql.org/efcore/) and [EF Core CLI migrations](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

## Future features

- Port book creation and read endpoints, then update and deletion.
- Add request validation and centralized ProblemDetails error responses.
- Add author CRUD and many-to-many book/author relationships.
- Add member CRUD with unique email addresses.
- Implement borrowing and returns with copy availability tracking and transactions.
- Add book search/filtering and pagination where useful.
- Add interactive Swagger UI and endpoint documentation.
- Create a Postman collection near the end of the backend milestones.
- Expand xUnit service and database integration tests.
- Update the README as each milestone is completed.

# Library Management REST API

A portfolio backend project being migrated from Java/Spring Boot to C# and ASP.NET Core, one milestone at a time. The target application will manage books, authors, members, and borrowing records.

## Current milestone: ASP.NET Core foundation

The C# implementation currently includes:

- A .NET 10 solution with an ASP.NET Core Web API project and an xUnit test project.
- Controller routing and dependency injection setup.
- The folders for the planned layered architecture.
- OpenAPI JSON at `/openapi/v1.json` in Development.
- Startup tests for development documentation and production documentation exposure.

There are no C# business endpoints or database connections yet. The root URL and `/api/books` currently return `404`. Interactive Swagger UI will be added in a later milestone; this foundation exposes the OpenAPI document only.

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

Planned persistence: **Entity Framework Core, Npgsql, PostgreSQL 17, EF Core migrations**.

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

## Run the C# foundation

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

Start the development HTTP profile:

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

Run the foundation tests after building:

```bash
dotnet test LibraryManagement.sln --no-build --no-restore
```

The two xUnit tests boot the application through `WebApplicationFactory` and verify:

- Development serves a valid OpenAPI JSON document.
- Production does not expose that document.

These tests use an in-process ASP.NET Core test server. They need no PostgreSQL server or credentials. They do not test business features, which are not implemented yet.

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
| Hibernate / JPA | EF Core, introduced in the next persistence milestone |

`AddControllers()` registers controller services; `MapControllers()` makes controller routes reachable. No controller actions are defined yet. The public partial `Program` declaration allows the test project to boot the application's entry point.

Microsoft references: [controller-based APIs](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api?view=aspnetcore-10.0), [integration testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0), and [SDK selection with global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).

## Database and secrets plan

Database setup is the **next milestone**. The foundation neither reads a connection string nor connects to PostgreSQL.

The planned C# development database is `library_management_cs` on PostgreSQL 17 at `localhost:5433`. It will be separate from the Java database `library_management`. Port 5432 may belong to a different PostgreSQL installation.

The future connection will be supplied through `ConnectionStrings__DefaultConnection`. ASP.NET Core maps the double underscore to the `ConnectionStrings:DefaultConnection` configuration key.

Never commit passwords or connection strings containing credentials. Use fresh development credentials; the password previously exposed during development must be rotated rather than reused. Local secret files and build output are ignored by Git, but `.gitignore` is not a substitute for reviewing staged changes.

The EF Core milestone will introduce migrations. It will not reset or destructively modify the Java database.

## Future features

- Configure PostgreSQL, Npgsql, `LibraryDbContext`, and EF Core migrations.
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

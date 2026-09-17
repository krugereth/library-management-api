# Library Management REST API

A portfolio backend project being migrated from Java/Spring Boot to C# and ASP.NET Core, one milestone at a time. The target application will manage books, authors, members, and borrowing records.

## Current milestone: Book update and deletion

The C# implementation currently includes:

- A .NET 10 solution with an ASP.NET Core Web API project and an xUnit test project.
- Controller routing and dependency injection setup.
- The folders for the planned layered architecture.
- OpenAPI JSON at `/openapi/v1.json` in Development.
- EF Core with Npgsql and a scoped `LibraryDbContext`.
- Baseline and books-table migrations, with repository-local `dotnet-ef` tooling.
- Book CRUD through Controller → Service → Repository.
- Request validation and database-enforced unique ISBNs.
- Startup tests plus PostgreSQL integration tests for books.

The root URL still returns `404`; use `/api/books` or the OpenAPI URL below. Interactive Swagger UI will be added in a later milestone; this foundation exposes the OpenAPI document only.

## Preserved Java implementation

The existing Java code remains in `src/main/java` and `src/test/java`, with its Maven build files. It is also preserved in Git at commit `8bf6e80`, before the `csharp-migration` branch was created.

The Java implementation has:

- Book create, list, lookup, update, and delete endpoints.
- Author create, list, and lookup endpoints.
- One optional author per book, shared across multiple books.
- Request validation, DTOs, and structured errors.
- PostgreSQL persistence and controller/database relationship tests.

Author update/deletion, members, loans, and search are not implemented in Java. Book CRUD is now ported; the remaining Java features are tracked below.

See the [Java setup and API reference](docs/java/README.md) to run the original application. Commands in that guide are run from the repository root. Its database, `library_management`, is retained.

## Migration completion and Java cleanup

The C# migration is **not complete yet**. Track replacement of the existing Java functionality separately from new portfolio features:

- [x] .NET foundation, PostgreSQL configuration, and EF migrations.
- [x] Book create/list/lookup with persisted data and basic validation.
- [x] Book update and deletion.
- [ ] Centralized error handling and complete validation coverage.
- [ ] Author management and book/author relationships (the C# target is many-to-many).
- [ ] Verify all replacement endpoints, relationships, failure cases, and migrations with passing tests.
- [ ] Verify documented C# setup works without Java/Maven and document API contract differences.
- [ ] Mark migration complete, then remove obsolete Java/Maven files in a separate focused cleanup commit.

Keep the Java source and build files until those checks pass. Git commit `8bf6e80` preserves the original implementation. Removing Java source later does **not** mean deleting the original database; it remains retained. No Java database records have been copied into C#.

Members, loans, search, Swagger UI, and the Postman collection remain on the wider project roadmap. They are new work, not prerequisites for replacing the existing Java functionality.

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

Open `http://localhost:5080/openapi/v1.json` in a browser or send a GET request from Postman. The document describes all five book CRUD endpoints. Press **Ctrl+C** to stop the application.

These `dotnet` commands work from macOS Terminal and Windows PowerShell. Java uses port 8080; the C# development profile uses 5080.

### Optional local HTTPS

To use the HTTPS profile, trust the local development certificate and run:

```bash
dotnet dev-certs https --trust
dotnet run --project src/LibraryManagement.Api --launch-profile https
```

The HTTPS URL is `https://localhost:7080/openapi/v1.json`. Development supports local HTTP; outside Development, the application enables HTTPS redirection and does not map the OpenAPI endpoint.

## Book endpoints

| Method | URL | Result |
| --- | --- | --- |
| GET | `/api/books` | `200` with a list ordered by ID; `[]` when empty |
| GET | `/api/books/{id}` | `200` with a book, or `404` ProblemDetails |
| POST | `/api/books` | `201` with the saved book and a `Location` header |
| PUT | `/api/books/{id}` | `200` with the updated book; `404` if absent |
| DELETE | `/api/books/{id}` | `204` with no body; `404` if absent |

Example POST body for Postman (`Content-Type: application/json`):

```json
{
  "title": "Clean Code",
  "isbn": "9780132350884",
  "publicationYear": 2008,
  "availableCopies": 3
}
```

Send it to `http://localhost:5080/api/books`, then GET the returned `Location` URL or list all books. Repeating the POST with the same ISBN returns `409` ProblemDetails. To remove a sample book, send DELETE to its returned `Location` URL.

Title and ISBN must be nonblank, with maximum lengths of 255 and 32 respectively. Leading and trailing whitespace is trimmed before storage. ISBN uniqueness compares the trimmed string; ISBN format/checksum validation is not implemented yet. `publicationYear` is optional and, when present, must be 1–9999. `availableCopies` is required and must be a nonnegative integer. Invalid requests return `400` ValidationProblemDetails.

### Update and delete in Postman

After creating a book, use its returned ID:

1. Send **PUT** to `http://localhost:5080/api/books/{id}` with the same JSON fields as POST, changing the title, ISBN, year, or available copies. Expect `200` and the updated book with its original ID.
2. Send **GET** to the same URL to confirm the changes persisted.
3. Send **DELETE** to the same URL. Expect `204` with an empty body.
4. Repeat GET or DELETE for that ID. Expect `404` ProblemDetails.

PUT replaces the editable fields. Title, ISBN, and available copies are required; omitting `publicationYear` or setting it to `null` clears the year. PUT does not create missing books. Repeating a valid PUT leaves the same stored values. Both POST and PUT use the shared `BookRequest` validation rules.

Keeping a book's own ISBN is allowed. Changing it to another book's ISBN returns `409` and preserves the original data. Invalid PUT input returns `400` without modifying the book. Concurrent edits to the same book currently use last-write-wins; version-based conflict detection is not implemented. DELETE permanently removes the selected book; a repeated delete returns `404`, and its ISBN can be reused.

This milestone requires no new migration because the book schema has not changed.

The current book response includes `id`, `title`, `isbn`, `publicationYear`, and `availableCopies`. Author relationships and `totalCopies` will be introduced in later milestones. Expected book errors are handled locally for now; centralized exception handling is still planned.

## Tests

Run after building:

```bash
dotnet test LibraryManagement.sln --no-build --no-restore
```

Without `LibraryManagement__TestConnection`, the five database-independent startup cases run and the PostgreSQL book tests are explicitly **skipped**. Startup tests cover OpenAPI exposure, required configuration, and scoped Npgsql context registration.

### Full PostgreSQL test suite on this Mac

A separate database, `library_management_cs_tests`, has been created with the same dedicated C# owner. Load the development connection from Keychain, then point the test variable at the test database (macOS zsh/bash):

```bash
export ConnectionStrings__DefaultConnection="$(security find-generic-password -s library-management-api-cs-connection -a library_management_cs -w)"
export LibraryManagement__TestConnection="${ConnectionStrings__DefaultConnection/Database=library_management_cs;/Database=library_management_cs_tests;}"
dotnet test LibraryManagement.sln --no-build --no-restore
```

On another machine, have a PostgreSQL administrator create the test database once:

```sql
CREATE DATABASE library_management_cs_tests OWNER library_management_cs;
```

Set `LibraryManagement__TestConnection` securely to that database. The test runner refuses other database names. Each book test creates a randomly named schema, applies the real EF migrations, and drops only its own schema when finished. It never clears either development database. The test user needs schema-creation permission in the test database.

The full suite has 32 cases: five startup cases and 27 PostgreSQL cases. They cover CRUD persistence, missing books, validation on POST and PUT, trimmed input, optional years, zero copies, duplicate ISBNs, concurrent ISBN conflicts, repeated updates/deletes, and deletion between reading and saving a book. Failed updates are checked for unchanged persisted data. These tests use the actual Npgsql provider and PostgreSQL constraints.

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

`AddControllers()` registers controller services; `MapControllers()` makes controller routes reachable. `BooksController` now defines all five book CRUD actions. `[ApiController]` applies DTO validation automatically, similar to Spring request validation with `@Valid`. `CreatedAtAction` returns `201` and builds a link to the GET-by-ID action. The public partial `Program` declaration allows the test project to boot the application's entry point.

The update repository attaches the previously untracked book and calls `SaveChangesAsync`, similar to persisting an edited JPA entity. Deletion uses a single ID-filtered SQL operation through `ExecuteDeleteAsync`; the affected-row count distinguishes a deletion from a missing book. See [EF Core updates and deletes](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete).

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

`InitialDatabase` establishes the baseline and EF Core's `__EFMigrationsHistory` table. `AddBooks` creates the `books` table with an identity primary key and unique ISBN index. Authors, members, and loans are not modeled yet. Re-running `database update` is safe when all migrations are already applied.

`LibraryDbContext` is EF Core's database session and change tracker, comparable to Hibernate's persistence context. Dependency injection creates one context per request scope. Npgsql translates EF operations for PostgreSQL. Migrations are versioned schema changes; unlike Hibernate automatic schema updates, they are explicitly generated, reviewed, and applied. The API does not automatically create or migrate the database at startup, and startup alone does not verify database connectivity.

When a future milestone adds or changes an entity, generate its migration, review it, and then apply it:

```bash
dotnet ef migrations add DescribeSchemaChange --project src/LibraryManagement.Api --output-dir Data/Migrations
dotnet ef database update --project src/LibraryManagement.Api
```

Never commit passwords or connection strings containing credentials. The previously exposed Java password is not reused for C#. Local secret files and build output are ignored by Git, but review staged changes before committing.

References: [Npgsql EF Core provider](https://www.npgsql.org/efcore/) and [EF Core CLI migrations](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

## Future features

- Add request validation and centralized ProblemDetails error responses.
- Add author CRUD and many-to-many book/author relationships.
- Add member CRUD with unique email addresses.
- Implement borrowing and returns with copy availability tracking and transactions.
- Add book search/filtering and pagination where useful.
- Add interactive Swagger UI and endpoint documentation.
- Create a Postman collection near the end of the backend milestones.
- Expand xUnit service and database integration tests.
- Update the README as each milestone is completed.

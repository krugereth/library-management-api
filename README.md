# Library Management REST API

A portfolio backend project migrated from Java/Spring Boot to C# and ASP.NET Core, with new features developed one milestone at a time. The target application will manage books, authors, members, and borrowing records.

## Current milestone: Legacy Java cleanup complete

The C# implementation currently includes:

- A .NET 10 solution with an ASP.NET Core Web API project and an xUnit test project.
- Controller routing and dependency injection setup.
- The folders for the planned layered architecture.
- OpenAPI JSON at `/openapi/v1.json` in Development.
- EF Core with Npgsql and a scoped `LibraryDbContext`.
- Baseline, books, authors, and book-author join-table migrations, with repository-local `dotnet-ef` tooling.
- Book CRUD through Controller → Service → Repository.
- Author creation, listing, and lookup with PostgreSQL persistence.
- Multiple authors per book, with authors shared across books.
- Request validation and database-enforced unique ISBNs.
- Centralized ProblemDetails responses with request paths and trace IDs.
- Startup tests plus PostgreSQL integration tests for books.

The root URL still returns `404`; use `/api/books` or the OpenAPI URL below. Interactive Swagger UI will be added in a later milestone; this foundation exposes the OpenAPI document only.

## Preserved Java implementation

The Java sources, tests, Maven build files, and generated Maven output have been removed from the current checkout. The original implementation remains preserved in Git at commit `8bf6e80`, before the `csharp-migration` branch was created.

The preserved Java implementation had:

- Book create, list, lookup, update, and delete endpoints.
- Author create, list, and lookup endpoints.
- One optional author per book, shared across multiple books.
- Request validation, DTOs, and structured errors.
- PostgreSQL persistence and controller/database relationship tests.

Author update/deletion, members, loans, and search are not implemented in Java. The existing Java features now have C# replacements, with the intentional API differences documented in the [migration review](docs/migration-review.md).

To inspect or run the original application, follow the recovery steps in the [historical Java guide](docs/java/README.md). Its commands apply to a separate checkout of the Java baseline. The original database, `library_management`, is retained.

## Migration completion and Java cleanup

The C# migration is **complete for the existing Java features**. Java source/build cleanup is complete; the working application is now C#/.NET only. The final [migration review](docs/migration-review.md) records the feature comparison, API changes, and successful setup/tests without Java or Maven.

- [x] .NET foundation, PostgreSQL configuration, and EF migrations.
- [x] Book create/list/lookup with persisted data and basic validation.
- [x] Book update and deletion.
- [x] Centralized error handling and book request validation coverage.
- [x] Author creation, listing, and lookup.
- [x] Many-to-many book/author relationships.
- [x] Verify replacement endpoints, relationships, failure cases, and migrations with passing tests.
- [x] Verify documented C# setup works without Java/Maven and document API contract differences.
- [x] Mark migration complete.
- [x] Remove obsolete Java/Maven files in a separate focused cleanup milestone.

The checks passed on 2026-09-17: a clean .NET-only export built with zero warnings, all 103 tests passed, fresh/repeated migrations succeeded, and a live HTTP workflow passed. Java source/build files have now been removed from the working tree. Git commit `8bf6e80` preserves the original implementation. The original database remains retained. No Java database records have been copied into C#.

Author update/deletion, members, loans, search, Swagger UI, and the Postman collection remain on the wider project roadmap. They are new work, not prerequisites for replacing the existing Java functionality.

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
 └── LibraryManagement.Api/
     ├── Controllers/
     ├── Services/
     ├── Repositories/
     ├── Data/
     ├── Models/
     ├── DTOs/
     ├── Exceptions/
     ├── Middleware/
     ├── Program.cs
     └── appsettings.json
 tests/
 └── LibraryManagement.Api.Tests/
 docs/
 ├── migration-review.md
 └── java/README.md                  Historical reference and recovery instructions
```

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

Open `http://localhost:5080/openapi/v1.json` in a browser or send a GET request from Postman. The document describes book CRUD and author create/read endpoints. Press **Ctrl+C** to stop the application.

These `dotnet` commands work from macOS Terminal and Windows PowerShell. The C# development profile uses port 5080; the historical Java application used 8080.

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

Title and ISBN must be nonblank, with maximum lengths of 255 and 32 respectively. Leading and trailing whitespace is trimmed before storage. ISBN uniqueness compares the trimmed string; ISBN format/checksum validation is not implemented yet. `publicationYear` is optional and, when present, must be 1–9999. `availableCopies` is required and must be a nonnegative integer. Invalid requests return `400` ValidationProblemDetails. Unknown JSON fields are rejected: for example, `titel`, `authorId`, and `id` are not accepted in a book request. Use the URL ID for updates. IDs must be positive; zero or negative IDs return `400`. Non-numeric IDs do not match the route and return `404`.

### Update and delete in Postman

After creating a book, use its returned ID:

1. Send **PUT** to `http://localhost:5080/api/books/{id}` with the same JSON fields as POST, changing the title, ISBN, year, or available copies. Expect `200` and the updated book with its original ID.
2. Send **GET** to the same URL to confirm the changes persisted.
3. Send **DELETE** to the same URL. Expect `204` with an empty body.
4. Repeat GET or DELETE for that ID. Expect `404` ProblemDetails.

PUT replaces the editable fields. Title, ISBN, and available copies are required; omitting `publicationYear` or setting it to `null` clears the year. PUT does not create missing books. Repeating a valid PUT leaves the same stored values. Both POST and PUT use the shared `BookRequest` validation rules.

Keeping a book's own ISBN is allowed. Changing it to another book's ISBN returns `409` and preserves the original data. Invalid PUT input returns `400` without modifying the book. Concurrent edits to the same book currently use last-write-wins; version-based conflict detection is not implemented. DELETE permanently removes the selected book; a repeated delete returns `404`, and its ISBN can be reused.

This milestone requires no new migration because the book schema has not changed.

The current book response includes `id`, `title`, `isbn`, `publicationYear`, `availableCopies`, and an `authors` array. `totalCopies` will be introduced in a later milestone. Services raise domain exceptions for missing books and ISBN conflicts; the central handler maps them to HTTP responses.

## Author endpoints

| Method | URL | Result |
| --- | --- | --- |
| POST | `/api/authors` | `201` with the saved author and a `Location` header |
| GET | `/api/authors` | `200` with authors ordered by ID; `[]` when empty |
| GET | `/api/authors/{id}` | `200` with an author, or `404` ProblemDetails |

In Postman, send POST to `http://localhost:5080/api/authors` with `Content-Type: application/json`:

```json
{
  "firstName": "Ursula K.",
  "lastName": "Le Guin"
}
```

The response contains `id`, `firstName`, and `lastName`. GET the returned `Location` URL to retrieve that author, or GET `/api/authors` to list authors.

Both names are required, nonblank, and limited to 100 characters each. Leading and trailing whitespace is trimmed before storage. Names do not have to be unique: two authors can share the same name and receive different IDs. Invalid bodies and unknown JSON fields return `400`. IDs must be positive; zero or negative IDs return `400`, and an unknown positive ID returns `404` with `Author not found.`

**Java contract difference:** the C# API uses `firstName` and `lastName` instead of Java's single `name` field. A legacy `{ "name": "..." }` request is rejected. No existing Java author records are automatically converted or copied.

Author update/deletion is not implemented yet. Creating an author does not change a book: link it using `authorIds` on a book request. Manual author records remain in the development database until author deletion is added.

## Link books and authors

Create authors first, then send their IDs in POST `/api/books` or PUT `/api/books/{id}`:

```json
{
  "title": "A Collaborative Book",
  "isbn": "collaborative-book-1",
  "publicationYear": 2024,
  "availableCopies": 2,
  "authorIds": [1, 2]
}
```

Replace the sample IDs with IDs returned by your author requests. Book responses include author details, ordered by author ID:

```json
{
  "id": 1,
  "title": "A Collaborative Book",
  "isbn": "collaborative-book-1",
  "publicationYear": 2024,
  "availableCopies": 2,
  "authors": [
    { "id": 1, "firstName": "Alex", "lastName": "Smith" },
    { "id": 2, "firstName": "Sam", "lastName": "Jones" }
  ]
}
```

- A book can have zero, one, or multiple authors. An author can belong to multiple books.
- `authorIds` must contain distinct positive IDs. Explicit `null`, repeated IDs, and invalid values return `400`.
- An unknown author ID returns `404` with `Author not found.` The entire operation is rejected before saving.
- On POST, omitted `authorIds` or `[]` creates a book without authors.
- **On PUT, `authorIds` replaces all links. Omitting it or sending `[]` clears the links.** Include existing IDs to retain those authors.
- A duplicate ISBN returns `409`; neither scalar fields nor author links are partially saved.
- Deleting a book deletes its links while preserving authors and their links to other books.

EF Core uses `book_authors`, a join table containing `(BookId, AuthorId)` pairs. Its composite primary key prevents duplicate links, and foreign keys require both records to exist. A book deletion cascades to its links; deleting a linked author is restricted at the database level. No author-delete endpoint is exposed yet.

The book and its authors are tracked together during updates so EF can detect additions/removals and save them in the same transaction. Existing authors are reused without changing their names. This corresponds to a JPA many-to-many relationship, with explicit EF mapping in `LibraryDbContext`. See [EF Core many-to-many relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many).

**Java contract differences:** Java accepted one `authorId` and returned one nested `author`; C# accepts `authorIds` and returns an `authors` array. The legacy `authorId` field is rejected. A Java client retaining its author during PUT must now send `authorIds: [existingAuthorId]`. Existing C# books keep their scalar data and initially have no author links when this migration is applied. The Java database is not migrated or modified.

## Error responses

The API uses `application/problem+json` for validation, expected domain failures, unexpected failures, and routing errors. Responses include:

- `type`: a link describing the HTTP error category.
- `title`: a short explanation.
- `status`: the HTTP status code.
- `instance`: the request path, without query parameters.
- `traceId`: the request identifier to correlate with server diagnostics.
- `errors`: field messages on validation failures.

| Situation | Status |
| --- | --- |
| Missing/invalid book or author fields, unknown JSON fields, invalid numeric values, nonpositive ID | `400` |
| Missing book/author or unmatched route | `404` |
| Unsupported HTTP method | `405` |
| Duplicate ISBN | `409` |
| Unsupported request content type | `415` |
| Unexpected server failure | `500` |

For example, a missing book returns this shape (the trace ID varies):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Book not found.",
  "status": 404,
  "instance": "/api/books/999",
  "traceId": "<request trace ID>"
}
```

Unexpected failures return the title `An unexpected error occurred.` in both Development and Production, without internal exception messages or stack traces. The exception handler logs unexpected failures on the server with their trace ID. Known missing-book, missing-author, and duplicate-ISBN exceptions are handled centrally, so controllers no longer repeat error mapping.

`ApiExceptionHandler` implements ASP.NET Core's `IExceptionHandler`, comparable to Spring's `@RestControllerAdvice`. `UseExceptionHandler()` enables it in the request pipeline. `AddProblemDetails()` shares formatting with built-in MVC validation, while `UseStatusCodePages()` formats otherwise empty routing errors. Error responses remain JSON even when the client prefers HTML or plain text. See [ASP.NET Core error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0).

## Tests

Run after building:

```bash
dotnet test LibraryManagement.sln --no-build --no-restore
```

Without `LibraryManagement__TestConnection`, 32 database-independent startup/error-handling cases run and the PostgreSQL book/author tests are explicitly **skipped**. Startup tests cover OpenAPI exposure, required configuration, and scoped Npgsql context registration. Error tests replace only the repository, keeping real controllers, services, and middleware; they check safe responses in Development/Production, validation messages, content negotiation, and routing errors.

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

Set `LibraryManagement__TestConnection` securely to that database. The test runner refuses other database names. Each database test uses the shared `PostgresApiTestBase` to create a randomly named schema, apply the real EF migrations, and drop only its own schema when finished. It never clears either development database. The test user needs schema-creation permission in the test database.

The full suite has 103 cases: five startup cases, 27 database-independent error-handling cases, 32 PostgreSQL book cases, 23 PostgreSQL author cases, and 16 PostgreSQL relationship cases. They cover CRUD persistence, missing books, validation on POST and PUT, trimmed input, optional years, zero copies, duplicate ISBNs, concurrent ISBN conflicts, repeated updates/deletes, and deletion between reading and saving a book. Failed updates are checked for unchanged persisted data. The PostgreSQL tests use the actual Npgsql provider and database constraints; they also verify the shared ProblemDetails fields and rejection of unknown fields, null required values, fractional counts, and numeric overflow. Author tests cover persisted/trimmed names, duplicate names, ID ordering, missing/invalid IDs, malformed requests, the Java request shape, and name-length boundaries. Relationship tests cover shared authors, replacement/clearing, invalid/missing IDs, atomic failure, cascading link deletion, database constraints, and preservation of pre-migration book/author data.

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

The update repository uses a tracked book and its author collection, marks the book fields for update, and calls `SaveChangesAsync`, similar to persisting an edited JPA entity and its relationships. Deletion uses a single ID-filtered SQL operation through `ExecuteDeleteAsync`; the affected-row count distinguishes a deletion from a missing book. See [EF Core updates and deletes](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete).

Microsoft references: [controller-based APIs](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api?view=aspnetcore-10.0), [integration testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0), and [SDK selection with global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).

## Database and secrets

Use PostgreSQL 17 at `localhost:5433` with database **`library_management_cs`**. The Java database `library_management` is separate and must not be used for C# migrations. Port 5432 may belong to a different PostgreSQL installation.

### This MacBook

The development database and its dedicated login role, both named `library_management_cs`, have been created. The role owns the C# development and test databases and has neither superuser nor database-creation privileges. Its fresh password is stored as part of the connection string in macOS Keychain, outside the repository.

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

`InitialDatabase` establishes the baseline and EF Core's `__EFMigrationsHistory` table. `AddBooks` creates the `books` table with an identity primary key and unique ISBN index. `AddAuthors` creates the independent `authors` table with an identity primary key and required first/last names. `LinkBooksToAuthors` adds the join table without altering existing books or authors. Members and loans are not modeled yet. Re-running `database update` is safe when all migrations are already applied.

`LibraryDbContext` is EF Core's database session and change tracker, comparable to Hibernate's persistence context. Dependency injection creates one context per request scope. Npgsql translates EF operations for PostgreSQL. Migrations are versioned schema changes; unlike Hibernate automatic schema updates, they are explicitly generated, reviewed, and applied. The API does not automatically create or migrate the database at startup, and startup alone does not verify database connectivity.

When a future milestone adds or changes an entity, generate its migration, review it, and then apply it:

```bash
dotnet ef migrations add DescribeSchemaChange --project src/LibraryManagement.Api --output-dir Data/Migrations
dotnet ef database update --project src/LibraryManagement.Api
```

Never commit passwords or connection strings containing credentials. The previously exposed Java password is not reused for C#. Local secret files and build output are ignored by Git, but review staged changes before committing.

References: [Npgsql EF Core provider](https://www.npgsql.org/efcore/) and [EF Core CLI migrations](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

## Future features

- Add author update and deletion.
- Add member CRUD with unique email addresses.
- Implement borrowing and returns with copy availability tracking and transactions.
- Add book search/filtering and pagination where useful.
- Add interactive Swagger UI and endpoint documentation.
- Create a Postman collection near the end of the backend milestones.
- Expand xUnit service and database integration tests.
- Update the README as each milestone is completed.

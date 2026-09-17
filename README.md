# Library Management REST API

A C# REST API for managing books and authors, built with ASP.NET Core and PostgreSQL. Supports shared authors, request validation, and automated tests. Use Postman or another HTTP client to interact with the API.

## Stack

.NET 10 · ASP.NET Core Web API · Entity Framework Core · Npgsql · PostgreSQL 17 · OpenAPI · xUnit

```text
Controller → Service → Repository → EF Core → PostgreSQL
```

Application code is in `src/LibraryManagement.Api`; tests are in `tests/LibraryManagement.Api.Tests`.

## Features

- Create, read, update, and delete books, with unique ISBNs.
- Create and read authors.
- Link multiple authors to a book and share authors across books.
- Validate requests and return consistent ProblemDetails errors.
- Persist data in PostgreSQL with versioned EF Core schema changes.

## Run locally

### 1. Prerequisites

Install the **.NET 10 SDK** and **PostgreSQL 17**. Postman is optional.

Run PostgreSQL on `localhost:5433`. All commands below start from the repository root.

### 2. Set up the database

For a new installation, run these commands in `psql` as a PostgreSQL administrator:

```sql
CREATE ROLE library_management_cs LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE;
\password library_management_cs
CREATE DATABASE library_management_cs OWNER library_management_cs;
```

`\password` prompts for a password. Skip this step if the role and database already exist.

Set the environment variable `ConnectionStrings__DefaultConnection` using this format:

```text
Host=localhost;Port=5433;Database=library_management_cs;Username=library_management_cs;Password=<your-password>
```

To enter it without displaying it or saving it in shell history, use **macOS zsh**:

```zsh
read -rs 'ConnectionStrings__DefaultConnection?Connection string: '
echo
export ConnectionStrings__DefaultConnection
```

If you already have this project's connection saved in macOS Keychain, load it instead:

```bash
export ConnectionStrings__DefaultConnection="$(security find-generic-password -s library-management-api-cs-connection -a library_management_cs -w)"
```

The Keychain entry is machine-specific. Never commit passwords or connection strings containing credentials.

### 3. Build and start

```bash
dotnet restore LibraryManagement.sln
dotnet tool restore
dotnet build LibraryManagement.sln --no-restore
dotnet ef database update --project src/LibraryManagement.Api
dotnet run --project src/LibraryManagement.Api --launch-profile http
```

- **API:** `http://localhost:5080/api/books`
- **OpenAPI JSON:** `http://localhost:5080/openapi/v1.json` (Development only)

Press **Ctrl+C** to stop. The root URL has no homepage, and interactive Swagger UI is planned.

## Endpoints

| Method | Endpoint | Success |
| --- | --- | --- |
| GET | `/api/books` | `200` — list books |
| GET | `/api/books/{id}` | `200` — retrieve a book |
| POST | `/api/books` | `201` — create a book |
| PUT | `/api/books/{id}` | `200` — update a book |
| DELETE | `/api/books/{id}` | `204` — delete a book |
| GET | `/api/authors` | `200` — list authors |
| GET | `/api/authors/{id}` | `200` — retrieve an author |
| POST | `/api/authors` | `201` — create an author |

### Example requests

In Postman, select **Body → raw → JSON** and use `Content-Type: application/json`.

Create an author with POST `/api/authors`:

```json
{
  "firstName": "Ursula K.",
  "lastName": "Le Guin"
}
```

Create a book with POST `/api/books`, replacing `1` with the returned author ID:

```json
{
  "title": "A Wizard of Earthsea",
  "isbn": "9780547773742",
  "publicationYear": 1968,
  "availableCopies": 3,
  "authorIds": [1]
}
```

Creation responses include the saved record and a `Location` header for retrieving it. Book responses include an `authors` array. Use `[]` for a book without authors.

**PUT replaces all editable fields.** Include title, ISBN, and available copies. Omitting `publicationYear` clears it; omitting `authorIds` or sending `[]` removes all author links. Deleting a book preserves its authors.

### Validation and errors

- Title and ISBN are required, with limits of 255 and 32 characters. ISBNs must be unique.
- Publication year is optional, between 1 and 9999. Available copies must be a nonnegative integer.
- Both author names are required, up to 100 characters each; names need not be unique.
- Author IDs must be distinct, positive, and reference existing authors. Unknown JSON fields are rejected.

Errors use `application/problem+json`, with a request path and trace ID. Invalid input returns `400`, missing resources return `404`, and duplicate ISBNs return `409`. Validation responses include field-level messages.

## Tests

After building:

```bash
dotnet test LibraryManagement.sln --no-build --no-restore
```

Without a test connection, database-independent tests run and PostgreSQL tests are skipped.

For the full suite, create a separate test database once, as a PostgreSQL administrator:

```sql
CREATE DATABASE library_management_cs_tests OWNER library_management_cs;
```

With the development connection set as shown above, run in zsh/bash:

```bash
export LibraryManagement__TestConnection="${ConnectionStrings__DefaultConnection/Database=library_management_cs;/Database=library_management_cs_tests;}"
dotnet test LibraryManagement.sln --no-build --no-restore
```

Database tests require the exact database name `library_management_cs_tests`. Each test applies the EF schema in its own temporary schema and removes it afterward. Tests cover CRUD, validation, error handling, author relationships, and database constraints.

## Future features

- Author update and deletion.
- Member management with unique email addresses.
- Borrowing and returns with total/available copy tracking.
- Book search, filtering, and pagination.
- Interactive Swagger UI.
- Postman collection.
- Additional service and integration tests.

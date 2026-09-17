# Java to C# migration review

**Result: complete for the existing Java application's features. Java source/build cleanup has subsequently been completed.**

Reviewed on 2026-09-17 against C# commit `ff078d9f7c575f7cedb6edcf91450860ee083853` and the preserved Java baseline `8bf6e801b273a9653229239af24d795e10431e69`. No application code changes were needed in this review.

This is feature replacement with intentional API changes, not wire-compatible replacement or database-record migration. The wider portfolio roadmap remains unfinished.

## Feature comparison

| Java functionality | C# replacement | Verification |
| --- | --- | --- |
| GET `/api/books` | List books with authors | PostgreSQL tests and live HTTP |
| GET `/api/books/{id}` | Read a book with authors; missing book returns 404 | PostgreSQL tests and live HTTP |
| POST `/api/books` | Persist book and optional author links | PostgreSQL tests and live HTTP |
| PUT `/api/books/{id}` | Replace editable fields and author links | PostgreSQL tests and live HTTP |
| DELETE `/api/books/{id}` | Delete book and links, retain authors | PostgreSQL tests and live HTTP |
| GET `/api/authors` | List authors | PostgreSQL tests and live HTTP |
| GET `/api/authors/{id}` | Read author; missing author returns 404 | PostgreSQL tests and live HTTP |
| POST `/api/authors` | Persist author | PostgreSQL tests and live HTTP |
| Optional/shared author, attach/reassign/clear | Zero or multiple authors, shared across books | Relationship tests and live attach/clear workflow |
| Invalid requests and missing resources | ValidationProblemDetails and centralized ProblemDetails | HTTP tests, with and without PostgreSQL |
| PostgreSQL persistence | Npgsql/EF Core with four migrations | Fresh-schema migration, repeat migration, model consistency check |

The 103 passing cases include missing resources, invalid fields, duplicate ISBNs, concurrent ISBN conflicts, invalid/missing author IDs, failed-update rollback, link constraints, cascading book deletion, and migration preservation of existing C# records. The original Java tests/source were inspected for coverage; Java tests were not rerun as part of this C# independence check.

## Intentional API differences

Existing clients must adapt to these changes:

| Area | Java | C# |
| --- | --- | --- |
| Development URL | `http://localhost:8080` | `http://localhost:5080` |
| Book creation | `200` response | `201` with a `Location` header |
| Author names | Required `name`, up to 255 characters | Required `firstName` and `lastName`, up to 100 characters each |
| Book relationship input | Optional single `authorId` | Optional `authorIds` array of distinct positive IDs |
| Book relationship output | One nested `author`, or null | `authors` array ordered by ID; empty when unlinked |
| Clear authors on PUT | Omit `authorId` or use null | Omit `authorIds` or use `[]`; explicit null is invalid |
| Client-supplied IDs/unknown body fields | Ignored by the existing DTO endpoints | Rejected with `400`; IDs come from the database or route |
| Text validation | Nonblank book title/ISBN | Same requirement plus title length 255 and ISBN length 32; text is trimmed before saving |
| Publication year | Optional positive integer | Optional integer from 1 to 9999 |
| Route IDs | No explicit positive route validation | Nonpositive numeric IDs return `400`; nonnumeric IDs do not match and return `404` |
| Duplicate ISBN | Database uniqueness, no dedicated Java exception handler | Database uniqueness with explicit `409` ProblemDetails |
| Error body | `timestamp`, `status`, `error`, `message`, `path`, `fieldErrors` | `type`, `title`, `status`, `instance`, `traceId`, and validation `errors` |

Both versions support zero available copies and an optional publication year. C# still uses `availableCopies`; `totalCopies` and loan-driven availability are later features. C# requires both author name fields, so names must be mapped deliberately if Java data is imported later; no automatic name splitting is provided.

## Setup independence check

The review exported only the tracked .NET solution, SDK configuration, local tool manifest, API project, and test project into a new temporary directory. There were no Java sources, Maven build files, or copied `bin`/`obj` directories. Java/Maven command shims failed on invocation, and none were invoked. `JAVA_HOME` and `MAVEN_HOME` were removed from the child environment.

Environment: Apple Silicon macOS, .NET SDK `10.0.401`, EF tool `10.0.12`, PostgreSQL `17.11` on port `5433`. Installed tooling and the existing NuGet package cache were available; this was not a fresh operating-system installation or a Windows/Linux run.

The documented commands succeeded in that export:

```bash
dotnet tool restore
dotnet restore LibraryManagement.sln --disable-parallel -m:1
dotnet build LibraryManagement.sln --no-restore -m:1
dotnet test LibraryManagement.sln --no-build --no-restore -m:1
dotnet ef database update --project src/LibraryManagement.Api --no-build
dotnet ef database update --project src/LibraryManagement.Api --no-build
dotnet ef migrations has-pending-model-changes --project src/LibraryManagement.Api --no-build
```

Results: **0 build warnings, 0 errors, 103 passed tests, 0 skipped tests**, all four migrations applied, the second update made no changes, and no pending model changes were detected.

Credentials were loaded from macOS Keychain into environment variables without printing them. Tests used `library_management_cs_tests` with isolated schemas. A separate fresh schema in that test database was used for the CLI migration and live HTTP checks. The temporary API ran on port 5097 to avoid the normal development port. Its OpenAPI document exposed all eight operations. Live checks covered author creation/read, an initially unlinked book, attaching/clearing an author, book CRUD, and 400/404/409 responses. The temporary process, schemas, and exported files were cleaned up afterward.

No writes were made to `library_management` or `library_management_cs` during this review. No data was imported from Java.

## Completed Java cleanup

At review time, the tracked Java application and Maven files still matched baseline `8bf6e80`. The subsequent cleanup removed:

- `src/main/` — Java application and resources.
- `src/test/` — Java tests; retain the separate C# `tests/` directory.
- `pom.xml`, `mvnw`, `mvnw.cmd`, and `.mvn/`.
- The Maven-only `.gitattributes` file and Java-specific `.gitignore` rules; shared editor, .NET, and secret-file ignores remain.
- Local generated Maven output in `target/` (previously ignored by Git).

The root README now describes the C#-only working tree. `docs/java/README.md` remains as historical documentation, with instructions to retrieve the Java baseline in a separate Git worktree before following its commands.

Git history, .NET source/tests/migrations, and all databases were retained. Removing source files does not require dropping `library_management`, removing PostgreSQL, uninstalling Java, or deleting credentials. Java can be retrieved from its preserved commit without rewriting history.

After cleanup, the C# solution built with zero warnings/errors and all 103 tests passed with none skipped. The application source and EF migrations were unchanged.

Cleanup is kept separate from application feature changes. Suggested commit: `remove legacy Java implementation after C# migration`.

## Remaining product work

Author update/deletion, members, loans, total-copy tracking, search/filtering, interactive Swagger UI, the Postman collection, and further tests/documentation are new portfolio features. They were not implemented in the Java baseline and do not block Java source cleanup.

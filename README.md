# Library Management REST API

A Spring Boot backend for managing a library, built in small milestones. The API currently manages books and authors, persists data in PostgreSQL, and returns JSON responses. Use Postman or another HTTP client to interact with it; a frontend UI is not included.

## Implemented features

- Create, list, retrieve, update, and delete books.
- Create, list, and retrieve authors.
- Validate book and author requests.
- Return structured errors for validation failures and missing books or authors.
- Persist books and authors in PostgreSQL.
- Test endpoint behavior with MockMvc and Mockito.

Books and authors are currently separate resources. Linking them is the next planned milestone.

## Tech stack

| Technology | Usage |
| --- | --- |
| Java 21 | Application language |
| Spring Boot 4.1.1 | Application framework |
| Spring Web MVC | REST endpoints |
| Spring Data JPA / Hibernate | Database access and entity mapping |
| PostgreSQL 17 | Persistent storage |
| Jakarta Bean Validation | Request validation |
| Maven Wrapper | Build and test commands |
| JUnit, MockMvc, Mockito | Automated tests |
| Postman | Manual API testing |

## Project structure

```text
src/main/java/com/ayush/library_management_api/
├── controller/     HTTP routes and request handling
├── service/        Application logic
├── repository/     Spring Data JPA repositories
├── model/          Book and Author entities
├── dto/            Author creation request and API error response
└── exception/      Custom exceptions and shared error handler

src/main/resources/application.properties
src/test/java/com/ayush/library_management_api/
```

Requests flow through the controller, service, and repository to PostgreSQL.

## Run locally

### 1. Prerequisites and database

Install Java 21 and PostgreSQL 17. A separate Maven installation is not needed: the repository includes the Maven Wrapper. Postman is optional for manual testing.

Run the commands below from the repository root. The configuration in [application.properties](src/main/resources/application.properties) expects:

| Setting | Value |
| --- | --- |
| Database host | `localhost` |
| Database port | `5433` |
| Database name | `library_management` |
| Database user | `postgres` |
| Database password | Supplied through `DB_PASSWORD` |
| API port | `8080` |

Ensure PostgreSQL is listening on port **5433**, the `postgres` role can log in with a password, and the database exists. If the database is missing, connect as a PostgreSQL administrator and run:

```sql
CREATE DATABASE library_management OWNER postgres;
```

On the Mac configured for this project, PostgreSQL is managed by Homebrew:

```bash
brew services start postgresql@17
```

That local installation is already configured for port 5433. A fresh PostgreSQL installation may require its port and login role to be configured first.

Hibernate uses `spring.jpa.hibernate.ddl-auto=update` to create or update tables when the application starts. This is the current local development setup; versioned database migrations are not included yet.

### 2. Set the database password

Supply the password through the environment. Do not put real passwords in source files, this README, or Git.

In **zsh**, the default macOS shell, enter it without displaying it:

```zsh
read -rs 'DB_PASSWORD?PostgreSQL password: '
echo
export DB_PASSWORD
```

Alternatively, on the Mac where the project's password was saved in **macOS Keychain**, use:

```bash
export DB_PASSWORD="$(security find-generic-password -s library-management-api-db -a postgres -w)"
```

The Keychain command requires that existing local entry; cloning the repository does not create it. Set `DB_PASSWORD` in each terminal session where you run the application or database-dependent tests.

### 3. Start Spring Boot

```bash
sh mvnw spring-boot:run
```

The base URL is `http://localhost:8080`. Try `http://localhost:8080/api/books` to list books. An empty database returns `[]`.

Keep the terminal open while testing. Press **Ctrl+C** to stop the application.

## API endpoints

### Books

| Method | Endpoint | Action | Success status |
| --- | --- | --- | --- |
| GET | `/api/books` | List all books | `200 OK` |
| GET | `/api/books/{id}` | Retrieve a book | `200 OK` |
| POST | `/api/books` | Create a book | `200 OK` |
| PUT | `/api/books/{id}` | Replace a book's editable fields | `200 OK` |
| DELETE | `/api/books/{id}` | Delete a book | `204 No Content` |

GET by ID, PUT, and DELETE return `404 Not Found` if the book does not exist. DELETE returns an empty body on success.

### Authors

| Method | Endpoint | Action | Success status |
| --- | --- | --- | --- |
| GET | `/api/authors` | List all authors | `200 OK` |
| GET | `/api/authors/{id}` | Retrieve an author | `200 OK` |
| POST | `/api/authors` | Create an author | `201 Created` |

GET by ID returns `404 Not Found` if the author does not exist. Author update and deletion endpoints are not implemented yet.

## Request examples and validation

For POST and PUT in Postman, select **Body → raw → JSON**. Requests should use `Content-Type: application/json`.

### Create a book

Send **POST** to `http://localhost:8080/api/books`:

```json
{
  "title": "Clean Code",
  "isbn": "9780132350884",
  "publicationYear": 2008,
  "availableCopies": 3
}
```

The response includes a generated `id`. Use that returned ID in subsequent requests; do not assume IDs start at 1.

| Field | Rule |
| --- | --- |
| `title` | Required; cannot be empty or whitespace-only |
| `isbn` | Required; cannot be empty or whitespace-only; unique in the database |
| `publicationYear` | Optional; must be positive when supplied |
| `availableCopies` | Required; must be zero or greater |

ISBN format and checksum validation are not implemented. The database enforces ISBN uniqueness, but duplicate ISBN errors do not yet have a custom conflict response. Use a different ISBN when creating another book.

### Update a book

Send **PUT** to `http://localhost:8080/api/books/<id>`, replacing `<id>` with the book's ID:

```json
{
  "title": "Clean Code - Updated",
  "isbn": "9780132350884",
  "publicationYear": 2008,
  "availableCopies": 5
}
```

PUT replaces all editable fields and preserves the ID in the URL. Include all required fields. Omitting `publicationYear`, or setting it to `null`, clears its previous value. PUT does not create a book when the ID is missing.

### Create an author

Send **POST** to `http://localhost:8080/api/authors`:

```json
{
  "name": "Robert C. Martin"
}
```

The name is required, cannot be blank, and must be no longer than 255 characters. The response contains the generated `id` and `name`. Author names are not required to be unique. A client-supplied ID is ignored during author creation.

### Suggested manual test flow

1. Create a book and note its ID.
2. Retrieve it by ID and confirm the fields.
3. Update it, then retrieve it again to confirm the saved changes.
4. Submit an update with a blank title and confirm a `400` response. The saved book should remain unchanged.
5. Delete the book and confirm `204`, then retrieve it again and confirm `404`.
6. Create an author, then use the list and lookup endpoints to retrieve it.

## Error responses

The shared exception handler returns the following structure for request validation failures and missing books or authors. Other errors are not yet covered by this custom format.

Example validation failure (`400 Bad Request`):

```json
{
  "timestamp": "2026-09-16T14:20:00Z",
  "status": 400,
  "error": "Bad Request",
  "message": "Validation failed",
  "path": "/api/books",
  "fieldErrors": {
    "title": "Title is required"
  }
}
```

Example missing author (`404 Not Found`):

```json
{
  "timestamp": "2026-09-16T14:20:00Z",
  "status": 404,
  "error": "Not Found",
  "message": "Author not found with id: 42",
  "path": "/api/authors/42",
  "fieldErrors": {}
}
```

Timestamps and IDs above are illustrative. Missing books use the message `Book not found with id: <id>`.

## Automated tests

With PostgreSQL running and `DB_PASSWORD` set, run the full suite:

```bash
sh mvnw test
```

The current suite has **52 test cases**:

- 39 book controller cases covering lookup, updates, deletion, validation, and structured errors.
- 12 author controller cases covering creation, lists, lookup, validation, and generated IDs.
- 1 Spring application context startup test.

Controller tests use MockMvc with real controllers, services, and exception advice, plus mocked repositories. They do not require PostgreSQL and do not verify database persistence. Run just those tests with:

```bash
sh mvnw -Dtest=BookControllerTest,AuthorControllerTest test
```

The application context test connects to the configured PostgreSQL database and may update its schema through Hibernate. It verifies application startup; it is not a full database integration test suite.

## Future features

Planned work will continue one milestone at a time:

- Link books to authors.
- Add author update and deletion endpoints.
- Add member management.
- Implement borrowing and returning books, including availability checks.
- Add book search and filtering.
- Provide clear conflict responses for duplicate ISBNs.
- Expand request and response DTOs for books.
- Add a reusable Postman collection.
- Expand service and repository tests, including database integration tests.
- Introduce versioned database migrations.
- Add an optional frontend UI after the core backend features are complete.

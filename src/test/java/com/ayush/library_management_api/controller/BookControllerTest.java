package com.ayush.library_management_api.controller;

import com.ayush.library_management_api.exception.GlobalExceptionHandler;
import com.ayush.library_management_api.model.Author;
import com.ayush.library_management_api.model.Book;
import com.ayush.library_management_api.repository.AuthorRepository;
import com.ayush.library_management_api.repository.BookRepository;
import com.ayush.library_management_api.service.BookService;
import com.jayway.jsonpath.JsonPath;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;
import org.junit.jupiter.params.provider.NullSource;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.http.MediaType;
import org.springframework.test.util.ReflectionTestUtils;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultMatcher;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;
import java.util.stream.Stream;

import static org.hamcrest.Matchers.equalTo;
import static org.hamcrest.Matchers.nullValue;
import static org.junit.jupiter.api.Assertions.assertAll;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.verifyNoMoreInteractions;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

class BookControllerTest {

    private BookRepository bookRepository;
    private AuthorRepository authorRepository;
    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        bookRepository = mock(BookRepository.class);
        authorRepository = mock(AuthorRepository.class);
        BookService bookService = new BookService(bookRepository, authorRepository);
        mockMvc = MockMvcBuilders.standaloneSetup(new BookController(bookService))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    void getBookByIdReturnsExistingBook() throws Exception {
        when(bookRepository.findById(1L)).thenReturn(Optional.of(existingBook()));

        mockMvc.perform(get("/api/books/1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.id").value(1))
                .andExpect(jsonPath("$.title").value("Original title"))
                .andExpect(jsonPath("$.isbn").value("9780132350884"))
                .andExpect(jsonPath("$.publicationYear").value(2008))
                .andExpect(jsonPath("$.availableCopies").value(2))
                .andExpect(jsonPath("$.author").value(nullValue()));

        verify(bookRepository).findById(1L);
        verifyNoMoreInteractions(bookRepository);
    }

    @Test
    void getBookByIdIncludesLinkedAuthor() throws Exception {
        Book book = existingBook();
        book.setAuthor(author(10L, "Robert C. Martin"));
        when(bookRepository.findById(1L)).thenReturn(Optional.of(book));

        mockMvc.perform(get("/api/books/1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.id").value(1))
                .andExpect(jsonPath("$.author").value(equalTo(Map.of("id", 10, "name", "Robert C. Martin"))));

        verify(bookRepository).findById(1L);
        verifyNoInteractions(authorRepository);
    }

    @Test
    void getAllBooksIncludesLinkedAndUnlinkedBooks() throws Exception {
        Book linkedBook = existingBook();
        linkedBook.setAuthor(author(10L, "Robert C. Martin"));
        Book unlinkedBook = new Book("Other title", "9780134685991", 2018, 0);
        ReflectionTestUtils.setField(unlinkedBook, "id", 2L);
        when(bookRepository.findAll()).thenReturn(List.of(linkedBook, unlinkedBook));

        mockMvc.perform(get("/api/books"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.length()").value(2))
                .andExpect(jsonPath("$[0].id").value(1))
                .andExpect(jsonPath("$[0].author").value(equalTo(Map.of("id", 10, "name", "Robert C. Martin"))))
                .andExpect(jsonPath("$[1].id").value(2))
                .andExpect(jsonPath("$[1].author").value(nullValue()));

        verify(bookRepository).findAll();
        verifyNoInteractions(authorRepository);
    }

    @Test
    void createBookLinksAuthorAndIgnoresClientProvidedId() throws Exception {
        Author author = author(10L, "Robert C. Martin");
        when(authorRepository.findById(10L)).thenReturn(Optional.of(author));
        when(bookRepository.save(any(Book.class))).thenAnswer(invocation -> {
            Book savedBook = invocation.getArgument(0);
            assertNull(savedBook.getId());
            assertSame(author, savedBook.getAuthor());
            ReflectionTestUtils.setField(savedBook, "id", 7L);
            return savedBook;
        });

        mockMvc.perform(post("/api/books")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(bookRequest("authorId", "10").replaceFirst("\\{", "{\"id\":999,")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.id").value(7))
                .andExpect(jsonPath("$.title").value("Updated title"))
                .andExpect(jsonPath("$.isbn").value("9780134685991"))
                .andExpect(jsonPath("$.publicationYear").value(2018))
                .andExpect(jsonPath("$.availableCopies").value(0))
                .andExpect(jsonPath("$.author").value(equalTo(Map.of("id", 10, "name", "Robert C. Martin"))));

        verify(authorRepository).findById(10L);
        verify(bookRepository).save(any(Book.class));
        verifyNoMoreInteractions(bookRepository, authorRepository);
    }

    @ParameterizedTest
    @NullSource
    @ValueSource(strings = "null")
    void createBookAcceptsOmittedOrNullAuthor(String authorId) throws Exception {
        when(bookRepository.save(any(Book.class))).thenAnswer(invocation -> {
            Book savedBook = invocation.getArgument(0);
            assertNull(savedBook.getAuthor());
            return savedBook;
        });

        mockMvc.perform(post("/api/books")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(bookRequest("authorId", authorId)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.title").value("Updated title"))
                .andExpect(jsonPath("$.author").value(nullValue()));

        verify(bookRepository).save(any(Book.class));
        verifyNoInteractions(authorRepository);
    }

    @ParameterizedTest
    @ValueSource(booleans = {false, true})
    void updateBookAttachesOrReassignsAuthor(boolean alreadyLinked) throws Exception {
        Book book = existingBook();
        if (alreadyLinked) {
            book.setAuthor(author(10L, "Original author"));
        }
        Author replacement = author(20L, "Replacement author");
        when(bookRepository.findById(1L)).thenReturn(Optional.of(book));
        when(authorRepository.findById(20L)).thenReturn(Optional.of(replacement));
        when(bookRepository.save(any(Book.class))).thenAnswer(invocation -> invocation.getArgument(0));

        mockMvc.perform(put("/api/books/1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(bookRequest("authorId", "20")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.id").value(1))
                .andExpect(jsonPath("$.title").value("Updated title"))
                .andExpect(jsonPath("$.author").value(equalTo(Map.of("id", 20, "name", "Replacement author"))));

        assertSame(replacement, book.getAuthor());
        verify(bookRepository).findById(1L);
        verify(authorRepository).findById(20L);
        verify(bookRepository).save(book);
        verifyNoMoreInteractions(bookRepository, authorRepository);
    }

    @ParameterizedTest
    @NullSource
    @ValueSource(strings = "null")
    void updateBookClearsOmittedOrNullAuthor(String authorId) throws Exception {
        Book book = existingBook();
        book.setAuthor(author(10L, "Original author"));
        when(bookRepository.findById(1L)).thenReturn(Optional.of(book));
        when(bookRepository.save(any(Book.class))).thenAnswer(invocation -> invocation.getArgument(0));

        mockMvc.perform(put("/api/books/1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(bookRequest("authorId", authorId)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.id").value(1))
                .andExpect(jsonPath("$.author").value(nullValue()));

        assertNull(book.getAuthor());
        verify(bookRepository).save(book);
        verifyNoInteractions(authorRepository);
    }

    @Test
    void createBookRejectsMissingAuthorWithoutSaving() throws Exception {
        when(authorRepository.findById(42L)).thenReturn(Optional.empty());

        mockMvc.perform(post("/api/books")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(bookRequest("authorId", "42")))
                .andExpect(apiError(404, "Not Found", "Author not found with id: 42", "/api/books"))
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of())));

        verify(authorRepository).findById(42L);
        verifyNoMoreInteractions(authorRepository);
        verifyNoInteractions(bookRepository);
    }

    @Test
    void updateBookRejectsMissingAuthorWithoutChangingOrSavingBook() throws Exception {
        Book book = existingBook();
        Author originalAuthor = author(10L, "Original author");
        book.setAuthor(originalAuthor);
        when(bookRepository.findById(1L)).thenReturn(Optional.of(book));
        when(authorRepository.findById(42L)).thenReturn(Optional.empty());

        mockMvc.perform(put("/api/books/1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(bookRequest("authorId", "42")))
                .andExpect(apiError(404, "Not Found", "Author not found with id: 42", "/api/books/1"))
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of())));

        assertAll(
                () -> assertEquals(1L, book.getId()),
                () -> assertEquals("Original title", book.getTitle()),
                () -> assertEquals("9780132350884", book.getIsbn()),
                () -> assertEquals(2008, book.getPublicationYear()),
                () -> assertEquals(2, book.getAvailableCopies()),
                () -> assertSame(originalAuthor, book.getAuthor())
        );
        verify(bookRepository).findById(1L);
        verify(authorRepository).findById(42L);
        verifyNoMoreInteractions(bookRepository, authorRepository);
    }

    @Test
    void getMissingBookReturnsNotFound() throws Exception {
        when(bookRepository.findById(42L)).thenReturn(Optional.empty());

        mockMvc.perform(get("/api/books/42"))
                .andExpect(apiError(404, "Not Found", "Book not found with id: 42", "/api/books/42"))
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of())));

        verify(bookRepository).findById(42L);
        verifyNoMoreInteractions(bookRepository);
    }

    @ParameterizedTest(name = "POST rejects {0}")
    @MethodSource("invalidBookRequests")
    void createBookRejectsInvalidRequest(String description, String requestBody,
                                         String field, String message) throws Exception {
        mockMvc.perform(post("/api/books")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(apiError(400, "Bad Request", "Validation failed", "/api/books"))
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of(field, message))));

        verifyNoInteractions(bookRepository, authorRepository);
    }

    @ParameterizedTest(name = "PUT rejects {0}")
    @MethodSource("invalidBookRequests")
    void updateBookRejectsInvalidRequest(String description, String requestBody,
                                         String field, String message) throws Exception {
        mockMvc.perform(put("/api/books/1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(apiError(400, "Bad Request", "Validation failed", "/api/books/1"))
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of(field, message))));

        verifyNoInteractions(bookRepository, authorRepository);
    }

    @ParameterizedTest
    @ValueSource(strings = {"POST", "PUT"})
    void invalidBookReturnsAllFieldErrors(String method) throws Exception {
        String path = method.equals("POST") ? "/api/books" : "/api/books/1";

        mockMvc.perform((method.equals("POST") ? post(path) : put(path))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "title": " ",
                                  "isbn": "",
                                  "publicationYear": 0,
                                  "availableCopies": -1
                                }
                                """))
                .andExpect(apiError(400, "Bad Request", "Validation failed", path))
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of(
                        "title", "Title is required",
                        "isbn", "ISBN is required",
                        "publicationYear", "Publication year must be positive",
                        "availableCopies", "Available copies must be zero or greater"))));

        verifyNoInteractions(bookRepository, authorRepository);
    }

    @ParameterizedTest
    @NullSource
    @ValueSource(strings = "null")
    void createBookAcceptsZeroCopiesAndOptionalPublicationYear(String publicationYear) throws Exception {
        when(bookRepository.save(any(Book.class))).thenAnswer(invocation -> invocation.getArgument(0));

        mockMvc.perform(post("/api/books")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(bookRequest("publicationYear", publicationYear)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.title").value("Updated title"))
                .andExpect(jsonPath("$.publicationYear").doesNotExist())
                .andExpect(jsonPath("$.availableCopies").value(0));

        verify(bookRepository).save(any(Book.class));
        verifyNoMoreInteractions(bookRepository);
    }

    @ParameterizedTest
    @NullSource
    @ValueSource(strings = "null")
    void updateBookAcceptsZeroCopiesAndOptionalPublicationYear(String publicationYear) throws Exception {
        Book existingBook = existingBook();
        when(bookRepository.findById(1L)).thenReturn(Optional.of(existingBook));
        when(bookRepository.save(any(Book.class))).thenAnswer(invocation -> invocation.getArgument(0));

        mockMvc.perform(put("/api/books/1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(bookRequest("publicationYear", publicationYear)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.id").value(1))
                .andExpect(jsonPath("$.publicationYear").doesNotExist())
                .andExpect(jsonPath("$.availableCopies").value(0));

        verify(bookRepository).findById(1L);
        verify(bookRepository).save(existingBook);
        verifyNoMoreInteractions(bookRepository);
        assertNull(existingBook.getPublicationYear());
    }

    @Test
    void updateBookReplacesFieldsAndPreservesPathId() throws Exception {
        Book existingBook = existingBook();
        when(bookRepository.findById(1L)).thenReturn(Optional.of(existingBook));
        when(bookRepository.save(any(Book.class))).thenAnswer(invocation -> invocation.getArgument(0));

        mockMvc.perform(put("/api/books/1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "id": 999,
                                  "title": "Updated title",
                                  "isbn": "9780134685991",
                                  "publicationYear": 2018,
                                  "availableCopies": 5
                                }
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.id").value(1))
                .andExpect(jsonPath("$.title").value("Updated title"))
                .andExpect(jsonPath("$.isbn").value("9780134685991"))
                .andExpect(jsonPath("$.publicationYear").value(2018))
                .andExpect(jsonPath("$.availableCopies").value(5));

        verify(bookRepository).save(existingBook);
        assertAll(
                () -> assertEquals(1L, existingBook.getId()),
                () -> assertEquals("Updated title", existingBook.getTitle()),
                () -> assertEquals("9780134685991", existingBook.getIsbn()),
                () -> assertEquals(2018, existingBook.getPublicationYear()),
                () -> assertEquals(5, existingBook.getAvailableCopies())
        );
    }

    @Test
    void updateBookClearsOmittedOptionalPublicationYear() throws Exception {
        Book existingBook = existingBook();
        when(bookRepository.findById(1L)).thenReturn(Optional.of(existingBook));
        when(bookRepository.save(any(Book.class))).thenAnswer(invocation -> invocation.getArgument(0));

        mockMvc.perform(put("/api/books/1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "title": "Updated title",
                                  "isbn": "9780134685991",
                                  "availableCopies": 5
                                }
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.id").value(1))
                .andExpect(jsonPath("$.publicationYear").doesNotExist());

        verify(bookRepository).save(existingBook);
        assertNull(existingBook.getPublicationYear());
    }

    @Test
    void updateMissingBookReturnsNotFoundWithoutSaving() throws Exception {
        when(bookRepository.findById(42L)).thenReturn(Optional.empty());

        mockMvc.perform(put("/api/books/42")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "title": "Updated title",
                                  "isbn": "9780134685991",
                                  "publicationYear": 2018,
                                  "availableCopies": 5,
                                  "authorId": 42
                                }
                                """))
                .andExpect(apiError(404, "Not Found", "Book not found with id: 42", "/api/books/42"))
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of())));

        verify(bookRepository).findById(42L);
        verify(bookRepository, never()).save(any(Book.class));
        verifyNoInteractions(authorRepository);
    }

    @Test
    void deleteBookReturnsNoContentAndDeletesExistingBook() throws Exception {
        Book existingBook = existingBook();
        existingBook.setAuthor(author(10L, "Original author"));
        when(bookRepository.findById(1L)).thenReturn(Optional.of(existingBook));

        mockMvc.perform(delete("/api/books/1"))
                .andExpect(status().isNoContent())
                .andExpect(content().string(""));

        verify(bookRepository).delete(existingBook);
        verifyNoInteractions(authorRepository);
    }

    @Test
    void deleteMissingBookReturnsNotFoundWithoutDeleting() throws Exception {
        when(bookRepository.findById(42L)).thenReturn(Optional.empty());

        mockMvc.perform(delete("/api/books/42"))
                .andExpect(apiError(404, "Not Found", "Book not found with id: 42", "/api/books/42"))
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of())));

        verify(bookRepository).findById(42L);
        verifyNoMoreInteractions(bookRepository);
    }

    private static Stream<Arguments> invalidBookRequests() {
        return Stream.of(
                invalidRequest("omitted title", "title", null, "Title is required"),
                invalidRequest("null title", "title", "null", "Title is required"),
                invalidRequest("empty title", "title", "\"\"", "Title is required"),
                invalidRequest("whitespace title", "title", "\"   \"", "Title is required"),
                invalidRequest("omitted ISBN", "isbn", null, "ISBN is required"),
                invalidRequest("null ISBN", "isbn", "null", "ISBN is required"),
                invalidRequest("empty ISBN", "isbn", "\"\"", "ISBN is required"),
                invalidRequest("whitespace ISBN", "isbn", "\"   \"", "ISBN is required"),
                invalidRequest("zero publication year", "publicationYear", "0", "Publication year must be positive"),
                invalidRequest("negative publication year", "publicationYear", "-1", "Publication year must be positive"),
                invalidRequest("omitted available copies", "availableCopies", null, "Available copies is required"),
                invalidRequest("null available copies", "availableCopies", "null", "Available copies is required"),
                invalidRequest("negative available copies", "availableCopies", "-1", "Available copies must be zero or greater"),
                invalidRequest("zero author ID", "authorId", "0", "Author ID must be positive"),
                invalidRequest("negative author ID", "authorId", "-1", "Author ID must be positive")
        );
    }

    private static Arguments invalidRequest(String description, String field, String rawJsonValue, String message) {
        return Arguments.of(description, bookRequest(field, rawJsonValue), field, message);
    }

    private static ResultMatcher apiError(int statusCode, String error, String message, String path) {
        return result -> {
            status().is(statusCode).match(result);
            content().contentTypeCompatibleWith(MediaType.APPLICATION_JSON).match(result);
            jsonPath("$.status").value(statusCode).match(result);
            jsonPath("$.error").value(error).match(result);
            jsonPath("$.message").value(message).match(result);
            jsonPath("$.path").value(path).match(result);
            jsonPath("$.timestamp").isString().match(result);
            Instant.parse(JsonPath.parse(result.getResponse().getContentAsString()).read("$.timestamp", String.class));
        };
    }

    private static String bookRequest(String field, String rawJsonValue) {
        Map<String, String> fields = new LinkedHashMap<>();
        fields.put("title", "\"Updated title\"");
        fields.put("isbn", "\"9780134685991\"");
        fields.put("publicationYear", "2018");
        fields.put("availableCopies", "0");
        if (rawJsonValue == null) {
            fields.remove(field);
        } else {
            fields.put(field, rawJsonValue);
        }
        return fields.entrySet().stream()
                .map(entry -> "\"" + entry.getKey() + "\":" + entry.getValue())
                .collect(Collectors.joining(",", "{", "}"));
    }

    private Book existingBook() {
        Book book = new Book("Original title", "9780132350884", 2008, 2);
        ReflectionTestUtils.setField(book, "id", 1L);
        return book;
    }

    private Author author(Long id, String name) {
        Author author = new Author(name);
        ReflectionTestUtils.setField(author, "id", id);
        return author;
    }
}

package com.ayush.library_management_api.controller;

import com.ayush.library_management_api.exception.GlobalExceptionHandler;
import com.ayush.library_management_api.model.Book;
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
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;
import java.util.stream.Stream;

import static org.hamcrest.Matchers.equalTo;
import static org.junit.jupiter.api.Assertions.assertAll;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
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
    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        bookRepository = mock(BookRepository.class);
        BookService bookService = new BookService(bookRepository);
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
                .andExpect(jsonPath("$.availableCopies").value(2));

        verify(bookRepository).findById(1L);
        verifyNoMoreInteractions(bookRepository);
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

        verifyNoInteractions(bookRepository);
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

        verifyNoInteractions(bookRepository);
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

        verifyNoInteractions(bookRepository);
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
                                  "availableCopies": 5
                                }
                                """))
                .andExpect(apiError(404, "Not Found", "Book not found with id: 42", "/api/books/42"))
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of())));

        verify(bookRepository).findById(42L);
        verify(bookRepository, never()).save(any(Book.class));
    }

    @Test
    void deleteBookReturnsNoContentAndDeletesExistingBook() throws Exception {
        Book existingBook = existingBook();
        when(bookRepository.findById(1L)).thenReturn(Optional.of(existingBook));

        mockMvc.perform(delete("/api/books/1"))
                .andExpect(status().isNoContent())
                .andExpect(content().string(""));

        verify(bookRepository).delete(existingBook);
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
                invalidRequest("negative available copies", "availableCopies", "-1", "Available copies must be zero or greater")
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
}

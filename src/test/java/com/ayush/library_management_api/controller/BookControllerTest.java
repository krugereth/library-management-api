package com.ayush.library_management_api.controller;

import com.ayush.library_management_api.model.Book;
import com.ayush.library_management_api.repository.BookRepository;
import com.ayush.library_management_api.service.BookService;
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
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;
import java.util.stream.Stream;

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
        mockMvc = MockMvcBuilders.standaloneSetup(new BookController(bookService)).build();
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
                .andExpect(status().isNotFound());

        verify(bookRepository).findById(42L);
        verifyNoMoreInteractions(bookRepository);
    }

    @ParameterizedTest(name = "POST rejects {0}")
    @MethodSource("invalidBookRequests")
    void createBookRejectsInvalidRequest(String description, String requestBody) throws Exception {
        mockMvc.perform(post("/api/books")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(status().isBadRequest());

        verifyNoInteractions(bookRepository);
    }

    @ParameterizedTest(name = "PUT rejects {0}")
    @MethodSource("invalidBookRequests")
    void updateBookRejectsInvalidRequest(String description, String requestBody) throws Exception {
        mockMvc.perform(put("/api/books/1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(status().isBadRequest());

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
                .andExpect(status().isNotFound());

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
                .andExpect(status().isNotFound());

        verify(bookRepository).findById(42L);
        verifyNoMoreInteractions(bookRepository);
    }

    private static Stream<Arguments> invalidBookRequests() {
        return Stream.of(
                Arguments.of("omitted title", bookRequest("title", null)),
                Arguments.of("null title", bookRequest("title", "null")),
                Arguments.of("empty title", bookRequest("title", "\"\"")),
                Arguments.of("whitespace title", bookRequest("title", "\"   \"")),
                Arguments.of("omitted ISBN", bookRequest("isbn", null)),
                Arguments.of("null ISBN", bookRequest("isbn", "null")),
                Arguments.of("empty ISBN", bookRequest("isbn", "\"\"")),
                Arguments.of("whitespace ISBN", bookRequest("isbn", "\"   \"")),
                Arguments.of("zero publication year", bookRequest("publicationYear", "0")),
                Arguments.of("negative publication year", bookRequest("publicationYear", "-1")),
                Arguments.of("omitted available copies", bookRequest("availableCopies", null)),
                Arguments.of("null available copies", bookRequest("availableCopies", "null")),
                Arguments.of("negative available copies", bookRequest("availableCopies", "-1"))
        );
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

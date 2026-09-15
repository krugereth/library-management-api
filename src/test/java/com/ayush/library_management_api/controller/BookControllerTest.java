package com.ayush.library_management_api.controller;

import com.ayush.library_management_api.model.Book;
import com.ayush.library_management_api.repository.BookRepository;
import com.ayush.library_management_api.service.BookService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.http.MediaType;
import org.springframework.test.util.ReflectionTestUtils;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import java.util.Optional;

import static org.junit.jupiter.api.Assertions.assertAll;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
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

    private Book existingBook() {
        Book book = new Book("Original title", "9780132350884", 2008, 2);
        ReflectionTestUtils.setField(book, "id", 1L);
        return book;
    }
}

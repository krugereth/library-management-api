package com.ayush.library_management_api;

import com.ayush.library_management_api.dto.BookRequest;
import com.ayush.library_management_api.dto.BookResponse;
import com.ayush.library_management_api.model.Author;
import com.ayush.library_management_api.repository.AuthorRepository;
import com.ayush.library_management_api.repository.BookRepository;
import com.ayush.library_management_api.service.BookService;
import jakarta.persistence.EntityManager;
import jakarta.persistence.PersistenceContext;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.transaction.annotation.Transactional;

import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;

@SpringBootTest
@Transactional
class BookAuthorPersistenceTests {

    @Autowired
    private BookService bookService;

    @Autowired
    private BookRepository bookRepository;

    @Autowired
    private AuthorRepository authorRepository;

    @PersistenceContext
    private EntityManager entityManager;

    @Test
    void bookCanPersistWithoutAuthorThenLinkReassignAndClearAuthor() {
        Long firstAuthorId = authorRepository.save(new Author("First test author")).getId();
        Long secondAuthorId = authorRepository.save(new Author("Second test author")).getId();
        String isbn = UUID.randomUUID().toString();
        Long bookId = bookService.createBook(request(isbn, null)).id();
        flushAndClear();

        assertNull(bookService.getBookById(bookId).author());

        bookService.updateBook(bookId, request(isbn, firstAuthorId));
        flushAndClear();

        BookResponse.AuthorSummary firstAuthor =
                new BookResponse.AuthorSummary(firstAuthorId, "First test author");
        assertEquals(firstAuthor, bookService.getBookById(bookId).author());
        assertEquals(firstAuthor, bookService.getAllBooks().stream()
                .filter(book -> book.id().equals(bookId)).findFirst().orElseThrow().author());

        bookService.updateBook(bookId, request(isbn, secondAuthorId));
        flushAndClear();

        assertEquals(new BookResponse.AuthorSummary(secondAuthorId, "Second test author"),
                bookService.getBookById(bookId).author());

        bookService.updateBook(bookId, request(isbn, null));
        flushAndClear();

        assertNull(bookService.getBookById(bookId).author());
        assertEquals("First test author", authorRepository.findById(firstAuthorId).orElseThrow().getName());
        assertEquals("Second test author", authorRepository.findById(secondAuthorId).orElseThrow().getName());
    }

    @Test
    void deletingBookPreservesSharedAuthorAndOtherBook() {
        Long authorId = authorRepository.save(new Author("Shared test author")).getId();
        Long deletedBookId = bookService.createBook(request(UUID.randomUUID().toString(), authorId)).id();
        Long retainedBookId = bookService.createBook(request(UUID.randomUUID().toString(), authorId)).id();
        flushAndClear();

        bookService.deleteBook(deletedBookId);
        flushAndClear();

        assertFalse(bookRepository.existsById(deletedBookId));
        assertEquals("Shared test author", authorRepository.findById(authorId).orElseThrow().getName());
        assertEquals(new BookResponse.AuthorSummary(authorId, "Shared test author"),
                bookService.getBookById(retainedBookId).author());
    }

    private BookRequest request(String isbn, Long authorId) {
        return new BookRequest("Persistence test book", isbn, 2020, 2, authorId);
    }

    private void flushAndClear() {
        entityManager.flush();
        entityManager.clear();
    }
}

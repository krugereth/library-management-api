package com.ayush.library_management_api.service;

import com.ayush.library_management_api.dto.BookRequest;
import com.ayush.library_management_api.dto.BookResponse;
import com.ayush.library_management_api.exception.AuthorNotFoundException;
import com.ayush.library_management_api.exception.BookNotFoundException;
import com.ayush.library_management_api.model.Author;
import com.ayush.library_management_api.model.Book;
import com.ayush.library_management_api.repository.AuthorRepository;
import com.ayush.library_management_api.repository.BookRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Service
public class BookService {

    private final BookRepository bookRepository;
    private final AuthorRepository authorRepository;

    public BookService(BookRepository bookRepository, AuthorRepository authorRepository) {
        this.bookRepository = bookRepository;
        this.authorRepository = authorRepository;
    }

    @Transactional(readOnly = true)
    public List<BookResponse> getAllBooks() {
        return bookRepository.findAll().stream().map(BookResponse::from).toList();
    }

    @Transactional(readOnly = true)
    public BookResponse getBookById(Long id) {
        return BookResponse.from(findBook(id));
    }

    @Transactional
    public BookResponse createBook(BookRequest request) {
        Author author = findAuthor(request.authorId());
        Book book = new Book(request.title(), request.isbn(), request.publicationYear(), request.availableCopies());
        book.setAuthor(author);
        return BookResponse.from(bookRepository.save(book));
    }

    @Transactional
    public BookResponse updateBook(Long id, BookRequest request) {
        Book existingBook = findBook(id);
        Author author = findAuthor(request.authorId());

        existingBook.setTitle(request.title());
        existingBook.setIsbn(request.isbn());
        existingBook.setPublicationYear(request.publicationYear());
        existingBook.setAvailableCopies(request.availableCopies());
        existingBook.setAuthor(author);

        return BookResponse.from(bookRepository.save(existingBook));
    }

    @Transactional
    public void deleteBook(Long id) {
        Book existingBook = findBook(id);

        bookRepository.delete(existingBook);
    }

    private Book findBook(Long id) {
        return bookRepository.findById(id)
                .orElseThrow(() -> new BookNotFoundException(id));
    }

    private Author findAuthor(Long id) {
        return id == null ? null : authorRepository.findById(id)
                .orElseThrow(() -> new AuthorNotFoundException(id));
    }
}

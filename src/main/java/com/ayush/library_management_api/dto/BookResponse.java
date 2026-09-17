package com.ayush.library_management_api.dto;

import com.ayush.library_management_api.model.Author;
import com.ayush.library_management_api.model.Book;

public record BookResponse(
        Long id,
        String title,
        String isbn,
        Integer publicationYear,
        Integer availableCopies,
        AuthorSummary author
) {
    public static BookResponse from(Book book) {
        Author author = book.getAuthor();
        AuthorSummary summary = author == null ? null : new AuthorSummary(author.getId(), author.getName());
        return new BookResponse(book.getId(), book.getTitle(), book.getIsbn(),
                book.getPublicationYear(), book.getAvailableCopies(), summary);
    }

    public record AuthorSummary(Long id, String name) {
    }
}

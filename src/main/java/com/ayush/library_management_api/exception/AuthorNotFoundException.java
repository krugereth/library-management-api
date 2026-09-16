package com.ayush.library_management_api.exception;

public class AuthorNotFoundException extends RuntimeException {

    public AuthorNotFoundException(Long id) {
        super("Author not found with id: " + id);
    }
}

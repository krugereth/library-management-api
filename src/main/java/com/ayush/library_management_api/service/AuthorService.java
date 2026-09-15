package com.ayush.library_management_api.service;

import com.ayush.library_management_api.model.Author;
import com.ayush.library_management_api.repository.AuthorRepository;
import org.springframework.stereotype.Service;

import java.util.List;

@Service
public class AuthorService {

    private final AuthorRepository authorRepository;

    public AuthorService(AuthorRepository authorRepository) {
        this.authorRepository = authorRepository;
    }

    public List<Author> getAllAuthors() {
        return authorRepository.findAll();
    }

    public Author createAuthor(String name) {
        return authorRepository.save(new Author(name));
    }
}

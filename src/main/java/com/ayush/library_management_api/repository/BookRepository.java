package com.ayush.library_management_api.repository;

import com.ayush.library_management_api.model.Book;
import org.springframework.data.jpa.repository.EntityGraph;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.Optional;

public interface BookRepository extends JpaRepository<Book, Long> {
    @Override
    @EntityGraph(attributePaths = "author")
    List<Book> findAll();

    @Override
    @EntityGraph(attributePaths = "author")
    Optional<Book> findById(Long id);
}

package com.ayush.library_management_api.repository;

import com.ayush.library_management_api.model.Book;
import org.springframework.data.jpa.repository.JpaRepository;

public interface BookRepository extends JpaRepository<Book, Long> {
}
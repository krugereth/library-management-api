package com.ayush.library_management_api.repository;

import com.ayush.library_management_api.model.Author;
import org.springframework.data.jpa.repository.JpaRepository;

public interface AuthorRepository extends JpaRepository<Author, Long> {
}

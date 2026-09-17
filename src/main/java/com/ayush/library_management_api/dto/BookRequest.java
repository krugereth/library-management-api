package com.ayush.library_management_api.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.PositiveOrZero;

public record BookRequest(
        @NotBlank(message = "Title is required")
        String title,
        @NotBlank(message = "ISBN is required")
        String isbn,
        @Positive(message = "Publication year must be positive")
        Integer publicationYear,
        @NotNull(message = "Available copies is required")
        @PositiveOrZero(message = "Available copies must be zero or greater")
        Integer availableCopies,
        @Positive(message = "Author ID must be positive")
        Long authorId
) {
}

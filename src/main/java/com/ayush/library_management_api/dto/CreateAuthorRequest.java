package com.ayush.library_management_api.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record CreateAuthorRequest(
        @NotBlank(message = "Author name is required")
        @Size(max = 255, message = "Author name must be 255 characters or fewer")
        String name
) {
}

package com.ayush.library_management_api.controller;

import com.ayush.library_management_api.exception.GlobalExceptionHandler;
import com.ayush.library_management_api.model.Author;
import com.ayush.library_management_api.repository.AuthorRepository;
import com.ayush.library_management_api.service.AuthorService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;
import org.springframework.http.MediaType;
import org.springframework.test.util.ReflectionTestUtils;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Stream;

import static org.hamcrest.Matchers.equalTo;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.verifyNoMoreInteractions;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

class AuthorControllerTest {

    private AuthorRepository authorRepository;
    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        authorRepository = mock(AuthorRepository.class);
        AuthorService authorService = new AuthorService(authorRepository);
        mockMvc = MockMvcBuilders.standaloneSetup(new AuthorController(authorService))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    void getAuthorsReturnsEmptyList() throws Exception {
        when(authorRepository.findAll()).thenReturn(List.of());

        mockMvc.perform(get("/api/authors"))
                .andExpect(status().isOk())
                .andExpect(content().json("[]"));

        verify(authorRepository).findAll();
        verifyNoMoreInteractions(authorRepository);
    }

    @Test
    void getAuthorsReturnsSavedAuthors() throws Exception {
        when(authorRepository.findAll()).thenReturn(List.of(
                savedAuthor(1L, "Jane Austen"), savedAuthor(2L, "George Orwell")));

        mockMvc.perform(get("/api/authors"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.length()").value(2))
                .andExpect(jsonPath("$[0].id").value(1))
                .andExpect(jsonPath("$[0].name").value("Jane Austen"))
                .andExpect(jsonPath("$[1].id").value(2))
                .andExpect(jsonPath("$[1].name").value("George Orwell"));

        verify(authorRepository).findAll();
        verifyNoMoreInteractions(authorRepository);
    }

    @Test
    void getAuthorByIdReturnsExistingAuthor() throws Exception {
        when(authorRepository.findById(1L)).thenReturn(Optional.of(savedAuthor(1L, "Jane Austen")));

        mockMvc.perform(get("/api/authors/1"))
                .andExpect(status().isOk())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_JSON))
                .andExpect(jsonPath("$.id").value(1))
                .andExpect(jsonPath("$.name").value("Jane Austen"));

        verify(authorRepository).findById(1L);
        verifyNoMoreInteractions(authorRepository);
    }

    @Test
    void getMissingAuthorReturnsNotFound() throws Exception {
        when(authorRepository.findById(42L)).thenReturn(Optional.empty());

        mockMvc.perform(get("/api/authors/42"))
                .andExpect(status().isNotFound())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_JSON))
                .andExpect(jsonPath("$.status").value(404))
                .andExpect(jsonPath("$.error").value("Not Found"))
                .andExpect(jsonPath("$.message").value("Author not found with id: 42"))
                .andExpect(jsonPath("$.path").value("/api/authors/42"))
                .andExpect(jsonPath("$.timestamp").isString())
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of())));

        verify(authorRepository).findById(42L);
        verifyNoMoreInteractions(authorRepository);
    }

    @Test
    void createAuthorReturnsCreatedAuthor() throws Exception {
        assignGeneratedIdOnSave("Jane Austen");

        mockMvc.perform(post("/api/authors")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"name": "Jane Austen"}
                                """))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.id").value(7))
                .andExpect(jsonPath("$.name").value("Jane Austen"));

        verify(authorRepository).save(any(Author.class));
        verifyNoMoreInteractions(authorRepository);
    }

    @Test
    void createAuthorIgnoresSuppliedIdAndSavesNewAuthor() throws Exception {
        assignGeneratedIdOnSave("Jane Austen");

        mockMvc.perform(post("/api/authors")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"id": 42, "name": "Jane Austen"}
                                """))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.id").value(7))
                .andExpect(jsonPath("$.name").value("Jane Austen"));

        verify(authorRepository).save(any(Author.class));
        verifyNoMoreInteractions(authorRepository);
    }

    @ParameterizedTest(name = "POST rejects {0}")
    @MethodSource("invalidAuthorRequests")
    void createAuthorRejectsInvalidName(String description, String requestBody, String message) throws Exception {
        mockMvc.perform(post("/api/authors")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(status().isBadRequest())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_JSON))
                .andExpect(jsonPath("$.status").value(400))
                .andExpect(jsonPath("$.error").value("Bad Request"))
                .andExpect(jsonPath("$.message").value("Validation failed"))
                .andExpect(jsonPath("$.path").value("/api/authors"))
                .andExpect(jsonPath("$.timestamp").isString())
                .andExpect(jsonPath("$.fieldErrors").value(equalTo(Map.of("name", message))));

        verifyNoInteractions(authorRepository);
    }

    @Test
    void createAuthorAcceptsMaximumNameLength() throws Exception {
        String name = "a".repeat(255);
        assignGeneratedIdOnSave(name);

        mockMvc.perform(post("/api/authors")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"name\":\"" + name + "\"}"))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.id").value(7))
                .andExpect(jsonPath("$.name").value(name));

        verify(authorRepository).save(any(Author.class));
        verifyNoMoreInteractions(authorRepository);
    }

    private static Stream<Arguments> invalidAuthorRequests() {
        return Stream.of(
                Arguments.of("omitted name", "{}", "Author name is required"),
                Arguments.of("null name", "{\"name\":null}", "Author name is required"),
                Arguments.of("empty name", "{\"name\":\"\"}", "Author name is required"),
                Arguments.of("whitespace name", "{\"name\":\"   \"}", "Author name is required"),
                Arguments.of("name longer than 255 characters", "{\"name\":\"" + "a".repeat(256) + "\"}",
                        "Author name must be 255 characters or fewer")
        );
    }

    private void assignGeneratedIdOnSave(String expectedName) {
        when(authorRepository.save(any(Author.class))).thenAnswer(invocation -> {
            Author author = invocation.getArgument(0);
            assertNull(author.getId(), "A new author must not have a client-supplied ID");
            assertEquals(expectedName, author.getName());
            ReflectionTestUtils.setField(author, "id", 7L);
            return author;
        });
    }

    private static Author savedAuthor(Long id, String name) {
        Author author = new Author(name);
        ReflectionTestUtils.setField(author, "id", id);
        return author;
    }
}

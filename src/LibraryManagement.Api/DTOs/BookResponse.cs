namespace LibraryManagement.Api.DTOs;

public record BookResponse(long Id, string Title, string Isbn, int? PublicationYear, int AvailableCopies)
{
    public IReadOnlyList<AuthorResponse> Authors { get; init; } = [];
}

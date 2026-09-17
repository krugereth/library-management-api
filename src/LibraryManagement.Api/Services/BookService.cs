using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Models;
using LibraryManagement.Api.Repositories;

namespace LibraryManagement.Api.Services;

public class BookService(IBookRepository repository) : IBookService
{
    public async Task<List<BookResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<BookResponse?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var book = await repository.GetByIdAsync(id, cancellationToken);
        return book is null ? null : ToResponse(book);
    }

    public async Task<BookResponse> CreateAsync(CreateBookRequest request, CancellationToken cancellationToken)
    {
        var book = new Book
        {
            Title = request.Title.Trim(),
            Isbn = request.Isbn.Trim(),
            PublicationYear = request.PublicationYear,
            AvailableCopies = request.AvailableCopies!.Value
        };

        await repository.AddAsync(book, cancellationToken);
        return ToResponse(book);
    }

    private static BookResponse ToResponse(Book book) =>
        new(book.Id, book.Title, book.Isbn, book.PublicationYear, book.AvailableCopies);
}

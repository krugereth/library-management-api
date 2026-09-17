using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Exceptions;
using LibraryManagement.Api.Models;
using LibraryManagement.Api.Repositories;

namespace LibraryManagement.Api.Services;

public class BookService(IBookRepository repository) : IBookService
{
    public async Task<List<BookResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<BookResponse> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var book = await repository.GetByIdAsync(id, cancellationToken);
        return ToResponse(book ?? throw new BookNotFoundException());
    }

    public async Task<BookResponse> CreateAsync(BookRequest request, CancellationToken cancellationToken)
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

    public async Task<BookResponse> UpdateAsync(long id, BookRequest request, CancellationToken cancellationToken)
    {
        var book = await repository.GetByIdAsync(id, cancellationToken);
        if (book is null) throw new BookNotFoundException();

        book.Title = request.Title.Trim();
        book.Isbn = request.Isbn.Trim();
        book.PublicationYear = request.PublicationYear;
        book.AvailableCopies = request.AvailableCopies!.Value;

        if (!await repository.UpdateAsync(book, cancellationToken)) throw new BookNotFoundException();
        return ToResponse(book);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteAsync(id, cancellationToken)) throw new BookNotFoundException();
    }

    private static BookResponse ToResponse(Book book) =>
        new(book.Id, book.Title, book.Isbn, book.PublicationYear, book.AvailableCopies);
}

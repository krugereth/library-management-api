using LibraryManagement.Api.Data;
using LibraryManagement.Api.Exceptions;
using LibraryManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LibraryManagement.Api.Repositories;

public class BookRepository(LibraryDbContext context) : IBookRepository
{
    public Task<List<Book>> GetAllAsync(CancellationToken cancellationToken) =>
        context.Books.AsNoTracking().OrderBy(book => book.Id).ToListAsync(cancellationToken);

    public Task<Book?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        context.Books.AsNoTracking().SingleOrDefaultAsync(book => book.Id == id, cancellationToken);

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        context.Books.Add(book);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_books_Isbn"
        })
        {
            // The unique index also protects concurrent requests with the same ISBN.
            throw new DuplicateIsbnException(exception);
        }
    }
}

using LibraryManagement.Api.Data;
using LibraryManagement.Api.Exceptions;
using LibraryManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LibraryManagement.Api.Repositories;

public class BookRepository(LibraryDbContext context) : IBookRepository
{
    public Task<List<Book>> GetAllAsync(CancellationToken cancellationToken) =>
        context.Books.AsNoTracking().Include(book => book.Authors)
            .OrderBy(book => book.Id).ToListAsync(cancellationToken);

    public Task<Book?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        context.Books.Include(book => book.Authors).SingleOrDefaultAsync(book => book.Id == id, cancellationToken);

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        context.Books.Add(book);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(Book book, CancellationToken cancellationToken)
    {
        // The tracked collection lets EF detect added/removed links. Mark only
        // the book as modified, so existing author names are never overwritten.
        context.Entry(book).State = EntityState.Modified;
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // With no concurrency token, zero affected rows means it was deleted.
            return false;
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken) =>
        await context.Books.Where(book => book.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
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
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation,
            ConstraintName: "FK_book_authors_authors_AuthorId"
        })
        {
            // An author can now be deleted between the lookup and saving links.
            throw new AuthorNotFoundException();
        }
    }
}

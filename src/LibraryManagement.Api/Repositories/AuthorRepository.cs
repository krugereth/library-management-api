using LibraryManagement.Api.Data;
using LibraryManagement.Api.Exceptions;
using LibraryManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LibraryManagement.Api.Repositories;

public class AuthorRepository(LibraryDbContext context) : IAuthorRepository
{
    public Task<List<Author>> GetAllAsync(CancellationToken cancellationToken) =>
        context.Authors.AsNoTracking().OrderBy(author => author.Id).ToListAsync(cancellationToken);

    public Task<Author?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        context.Authors.AsNoTracking().SingleOrDefaultAsync(author => author.Id == id, cancellationToken);

    // Tracked instances let EF attach existing authors without inserting them again.
    public Task<List<Author>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken) =>
        context.Authors.Where(author => ids.Contains(author.Id)).ToListAsync(cancellationToken);

    public async Task AddAsync(Author author, CancellationToken cancellationToken)
    {
        context.Authors.Add(author);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(Author author, CancellationToken cancellationToken)
    {
        context.Entry(author).State = EntityState.Modified;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // No concurrency token is configured: zero affected rows means deletion.
            return false;
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        try
        {
            return await context.Authors.Where(author => author.Id == id)
                .ExecuteDeleteAsync(cancellationToken) > 0;
        }
        catch (PostgresException exception) when (exception is
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation,
            ConstraintName: "FK_book_authors_authors_AuthorId"
        })
        {
            // Let the database guard against links, including concurrent inserts.
            throw new AuthorInUseException(exception);
        }
    }
}

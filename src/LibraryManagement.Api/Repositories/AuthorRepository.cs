using LibraryManagement.Api.Data;
using LibraryManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

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
}

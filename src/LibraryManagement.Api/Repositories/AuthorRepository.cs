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

    public async Task AddAsync(Author author, CancellationToken cancellationToken)
    {
        context.Authors.Add(author);
        await context.SaveChangesAsync(cancellationToken);
    }
}

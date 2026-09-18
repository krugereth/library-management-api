using LibraryManagement.Api.Models;

namespace LibraryManagement.Api.Repositories;

public interface IAuthorRepository
{
    Task<List<Author>> GetAllAsync(CancellationToken cancellationToken);
    Task<Author?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<List<Author>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken);
    Task AddAsync(Author author, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Author author, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);
}

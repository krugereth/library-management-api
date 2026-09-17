using LibraryManagement.Api.Models;

namespace LibraryManagement.Api.Repositories;

public interface IBookRepository
{
    Task<List<Book>> GetAllAsync(CancellationToken cancellationToken);
    Task<Book?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task AddAsync(Book book, CancellationToken cancellationToken);
}

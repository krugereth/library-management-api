using LibraryManagement.Api.DTOs;

namespace LibraryManagement.Api.Services;

public interface IBookService
{
    Task<List<BookResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<BookResponse> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<BookResponse> CreateAsync(BookRequest request, CancellationToken cancellationToken);
    Task<BookResponse> UpdateAsync(long id, BookRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(long id, CancellationToken cancellationToken);
}

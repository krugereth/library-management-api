using LibraryManagement.Api.DTOs;

namespace LibraryManagement.Api.Services;

public interface IBookService
{
    Task<List<BookResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<BookResponse?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<BookResponse> CreateAsync(CreateBookRequest request, CancellationToken cancellationToken);
}

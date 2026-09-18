using LibraryManagement.Api.DTOs;

namespace LibraryManagement.Api.Services;

public interface IAuthorService
{
    Task<List<AuthorResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<AuthorResponse> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<AuthorResponse> CreateAsync(AuthorRequest request, CancellationToken cancellationToken);
    Task<AuthorResponse> UpdateAsync(long id, AuthorRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(long id, CancellationToken cancellationToken);
}

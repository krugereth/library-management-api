using LibraryManagement.Api.DTOs;

namespace LibraryManagement.Api.Services;

public interface IAuthorService
{
    Task<List<AuthorResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<AuthorResponse> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<AuthorResponse> CreateAsync(CreateAuthorRequest request, CancellationToken cancellationToken);
}

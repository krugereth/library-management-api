using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Exceptions;
using LibraryManagement.Api.Models;
using LibraryManagement.Api.Repositories;

namespace LibraryManagement.Api.Services;

public class AuthorService(IAuthorRepository repository) : IAuthorService
{
    public async Task<List<AuthorResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<AuthorResponse> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var author = await repository.GetByIdAsync(id, cancellationToken);
        return ToResponse(author ?? throw new AuthorNotFoundException());
    }

    public async Task<AuthorResponse> CreateAsync(CreateAuthorRequest request, CancellationToken cancellationToken)
    {
        var author = new Author
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim()
        };
        await repository.AddAsync(author, cancellationToken);
        return ToResponse(author);
    }

    private static AuthorResponse ToResponse(Author author) =>
        new(author.Id, author.FirstName, author.LastName);
}

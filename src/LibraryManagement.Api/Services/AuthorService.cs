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

    public async Task<AuthorResponse> CreateAsync(AuthorRequest request, CancellationToken cancellationToken)
    {
        var author = new Author
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim()
        };
        await repository.AddAsync(author, cancellationToken);
        return ToResponse(author);
    }

    public async Task<AuthorResponse> UpdateAsync(long id, AuthorRequest request, CancellationToken cancellationToken)
    {
        var author = await repository.GetByIdAsync(id, cancellationToken);
        if (author is null) throw new AuthorNotFoundException();

        author.FirstName = request.FirstName.Trim();
        author.LastName = request.LastName.Trim();
        if (!await repository.UpdateAsync(author, cancellationToken)) throw new AuthorNotFoundException();
        return ToResponse(author);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteAsync(id, cancellationToken)) throw new AuthorNotFoundException();
    }

    private static AuthorResponse ToResponse(Author author) =>
        new(author.Id, author.FirstName, author.LastName);
}

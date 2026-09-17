using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LibraryManagement.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class BookRequest : IValidatableObject
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255, ErrorMessage = "Title must be 255 characters or fewer.")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "ISBN is required.")]
    [StringLength(32, ErrorMessage = "ISBN must be 32 characters or fewer.")]
    public string Isbn { get; init; } = string.Empty;

    [Range(1, 9999, ErrorMessage = "Publication year must be between 1 and 9999.")]
    public int? PublicationYear { get; init; }

    [Required(ErrorMessage = "Available copies is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Available copies must be zero or greater.")]
    public int? AvailableCopies { get; init; }

    [Required(ErrorMessage = "Author IDs must be an array; use [] for no authors.")]
    public List<long> AuthorIds { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AuthorIds is null) yield break; // Required handles explicit JSON null.
        if (AuthorIds.Any(id => id <= 0))
            yield return new ValidationResult("Author IDs must be positive.", [nameof(AuthorIds)]);
        if (AuthorIds.Count != AuthorIds.Distinct().Count())
            yield return new ValidationResult("Author IDs must not contain duplicates.", [nameof(AuthorIds)]);
    }
}

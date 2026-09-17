using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Api.DTOs;

public class BookRequest
{
    [Required]
    [StringLength(255)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string Isbn { get; init; } = string.Empty;

    [Range(1, 9999)]
    public int? PublicationYear { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? AvailableCopies { get; init; }
}

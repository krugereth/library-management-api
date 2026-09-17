namespace LibraryManagement.Api.Models;

public class Book
{
    public long Id { get; set; }
    public required string Title { get; set; }
    public required string Isbn { get; set; }
    public int? PublicationYear { get; set; }
    public int AvailableCopies { get; set; }
    public ICollection<Author> Authors { get; set; } = new List<Author>();
}

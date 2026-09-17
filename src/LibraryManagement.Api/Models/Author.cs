namespace LibraryManagement.Api.Models;

public class Author
{
    public long Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}

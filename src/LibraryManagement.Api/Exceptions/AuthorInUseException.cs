namespace LibraryManagement.Api.Exceptions;

public class AuthorInUseException(Exception innerException)
    : Exception("Author is linked to books. Remove those links before deleting the author.", innerException);

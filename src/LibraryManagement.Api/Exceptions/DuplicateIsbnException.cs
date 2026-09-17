namespace LibraryManagement.Api.Exceptions;

public class DuplicateIsbnException(Exception innerException)
    : Exception("A book with this ISBN already exists.", innerException);

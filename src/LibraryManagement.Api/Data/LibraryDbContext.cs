using Microsoft.EntityFrameworkCore;
using LibraryManagement.Api.Models;

namespace LibraryManagement.Api.Data;

public class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var book = modelBuilder.Entity<Book>();
        book.ToTable("books");
        book.Property(book => book.Title).HasMaxLength(255).IsRequired();
        book.Property(book => book.Isbn).HasMaxLength(32).IsRequired();
        book.HasIndex(book => book.Isbn).IsUnique().HasDatabaseName("IX_books_Isbn");
    }
}

using Microsoft.EntityFrameworkCore;
using LibraryManagement.Api.Models;

namespace LibraryManagement.Api.Data;

public class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Author> Authors => Set<Author>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var book = modelBuilder.Entity<Book>();
        book.ToTable("books");
        book.Property(book => book.Title).HasMaxLength(255).IsRequired();
        book.Property(book => book.Isbn).HasMaxLength(32).IsRequired();
        book.HasIndex(book => book.Isbn).IsUnique().HasDatabaseName("IX_books_Isbn");

        var author = modelBuilder.Entity<Author>();
        author.ToTable("authors");
        author.Property(author => author.FirstName).HasMaxLength(100).IsRequired();
        author.Property(author => author.LastName).HasMaxLength(100).IsRequired();

        book.HasMany(book => book.Authors).WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "book_authors",
                link => link.HasOne<Author>().WithMany().HasForeignKey("AuthorId")
                    .OnDelete(DeleteBehavior.Restrict),
                link => link.HasOne<Book>().WithMany().HasForeignKey("BookId")
                    .OnDelete(DeleteBehavior.Cascade),
                link => link.HasKey("BookId", "AuthorId"));
    }
}

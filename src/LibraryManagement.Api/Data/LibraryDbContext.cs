using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Api.Data;

public class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    // Entity sets and mappings will be added with their feature milestones.
}

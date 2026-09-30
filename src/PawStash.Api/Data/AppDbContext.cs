using Microsoft.EntityFrameworkCore;
using PawStash.Shared;

namespace PawStash.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Email);
            e.Property(x => x.Email).HasMaxLength(EmailRules.MaxLength);
            // Stored normalized (lowercase), matching what the login endpoint looks up.
            e.HasData(new User { Email = "1979stetsenko@gmail.com" });
        });
    }
}

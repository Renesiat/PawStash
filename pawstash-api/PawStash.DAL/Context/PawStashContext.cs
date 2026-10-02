using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PawStash.DAL.Context.Interfaces;
using PawStash.DAL.Entities;
using PawStash.DAL.Entities.FileSystem;

namespace PawStash.DAL.Context
{
    public class PawStashContext : DbContext, IPawStashContext
    {
        public PawStashContext(DbContextOptions<PawStashContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<FileSystemItem> FileSystemItems => Set<FileSystemItem>();

        public DbSet<Folder> Folders => Set<Folder>();

        public DbSet<Link> Links => Set<Link>();

        public DbSet<Note> Notes => Set<Note>();

        public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SetTimestamps();

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SetTimestamps();

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(PawStashContext).Assembly);
        }

        private void SetTimestamps()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            foreach (EntityEntry<FileSystemItem> entry in ChangeTracker.Entries<FileSystemItem>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                }
            }
        }
    }
}

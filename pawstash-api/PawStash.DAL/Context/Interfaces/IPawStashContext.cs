using Microsoft.EntityFrameworkCore;
using PawStash.DAL.Entities;
using PawStash.DAL.Entities.FileSystem;

namespace PawStash.DAL.Context.Interfaces
{
    public interface IPawStashContext
    {
        DbSet<User> Users { get; }

        DbSet<FileSystemItem> FileSystemItems { get; }

        DbSet<Folder> Folders { get; }

        DbSet<Link> Links { get; }

        DbSet<Note> Notes { get; }

        DbSet<UploadedFile> UploadedFiles { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

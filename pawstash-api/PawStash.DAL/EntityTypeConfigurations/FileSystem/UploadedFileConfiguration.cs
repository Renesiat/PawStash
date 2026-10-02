using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawStash.DAL.Entities.FileSystem;

namespace PawStash.DAL.EntityTypeConfigurations.FileSystem
{
    public class UploadedFileConfiguration : IEntityTypeConfiguration<UploadedFile>
    {
        public void Configure(EntityTypeBuilder<UploadedFile> builder)
        {
            builder.Property(x => x.FilePath)
                .HasColumnName("file_path")
                .HasMaxLength(300);

            builder.Property(x => x.MimeType)
                .HasColumnName("file_mime_type")
                .HasMaxLength(100);

            builder.Property(x => x.SizeBytes)
                .HasColumnName("file_size_bytes");
        }
    }
}

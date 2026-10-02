using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawStash.DAL.Entities.FileSystem;

namespace PawStash.DAL.EntityTypeConfigurations.FileSystem
{
    public class NoteConfiguration : IEntityTypeConfiguration<Note>
    {
        public void Configure(EntityTypeBuilder<Note> builder)
        {
            builder.Property(x => x.Text)
                .HasColumnName("note_text");
        }
    }
}

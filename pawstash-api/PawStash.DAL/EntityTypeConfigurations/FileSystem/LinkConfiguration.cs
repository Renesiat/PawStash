using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawStash.Common.Rules;
using PawStash.DAL.Entities.FileSystem;

namespace PawStash.DAL.EntityTypeConfigurations.FileSystem
{
    public class LinkConfiguration : IEntityTypeConfiguration<Link>
    {
        public void Configure(EntityTypeBuilder<Link> builder)
        {
            builder.Property(x => x.Url)
                .HasColumnName("link_url")
                .HasMaxLength(FileSystemItemRules.MaxLinkUrlLength);

            builder.Property(x => x.Description)
                .HasColumnName("link_description");
        }
    }
}

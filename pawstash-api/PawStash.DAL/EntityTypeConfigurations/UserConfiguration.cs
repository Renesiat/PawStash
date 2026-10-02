using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawStash.Common.Rules;
using PawStash.DAL.Entities;

namespace PawStash.DAL.EntityTypeConfigurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("users");

            builder.HasKey(x => x.Email)
                .HasName("pk_users");

            builder.Property(x => x.Email)
                .IsRequired()
                .HasColumnName("email")
                .HasMaxLength(EmailRules.MaxLength);

            builder.HasData(new User { Email = "1979stetsenko@gmail.com" });
        }
    }
}

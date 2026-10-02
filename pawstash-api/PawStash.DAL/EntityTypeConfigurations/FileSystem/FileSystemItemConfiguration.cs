using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawStash.Common.Rules;
using PawStash.DAL.Entities;
using PawStash.DAL.Entities.FileSystem;

namespace PawStash.DAL.EntityTypeConfigurations.FileSystem
{
    public class FileSystemItemConfiguration : IEntityTypeConfiguration<FileSystemItem>
    {
        private const string ItemTypeProperty = "ItemType";

        public void Configure(EntityTypeBuilder<FileSystemItem> builder)
        {
            builder.ToTable("file_system_items", table =>
            {
                table.HasCheckConstraint(
                    "ck_file_system_items_item_type",
                    "item_type IN ('folder', 'link', 'note', 'photo', 'document')");

                table.HasCheckConstraint(
                    "ck_file_system_items_link_url",
                    "item_type <> 'link' OR link_url IS NOT NULL");

                table.HasCheckConstraint(
                    "ck_file_system_items_note_text",
                    "item_type <> 'note' OR note_text IS NOT NULL");

                table.HasCheckConstraint(
                    "ck_file_system_items_uploaded_file",
                    "item_type NOT IN ('photo', 'document') OR (file_path IS NOT NULL AND file_mime_type IS NOT NULL AND file_size_bytes IS NOT NULL)");

                table.HasCheckConstraint(
                    "ck_file_system_items_cover_image",
                    "(cover_image_path IS NULL) = (cover_image_mime_type IS NULL)");
            });

            builder.HasKey(x => x.ItemId)
                .HasName("pk_file_system_items");

            builder.HasDiscriminator<string>(ItemTypeProperty)
                .HasValue<Folder>("folder")
                .HasValue<Link>("link")
                .HasValue<Note>("note")
                .HasValue<Photo>("photo")
                .HasValue<Document>("document");

            builder.Property<string>(ItemTypeProperty)
                .HasColumnName("item_type")
                .HasMaxLength(10);

            builder.Property(x => x.ItemId)
                .HasColumnName("item_id");

            builder.Property(x => x.OwnerEmail)
                .IsRequired()
                .HasColumnName("owner_email")
                .HasMaxLength(EmailRules.MaxLength);

            builder.Property(x => x.ParentFolderId)
                .HasColumnName("parent_folder_id");

            builder.Property(x => x.Name)
                .IsRequired()
                .HasColumnName("name")
                .HasMaxLength(FileSystemItemRules.MaxNameLength);

            builder.Property(x => x.NameLowercase)
                .HasColumnName("name_lowercase")
                .HasMaxLength(FileSystemItemRules.MaxNameLength)
                .HasComputedColumnSql("lower(name)", stored: true);

            builder.Property(x => x.Description)
                .HasColumnName("description");

            builder.Property(x => x.CoverImagePath)
                .HasColumnName("cover_image_path")
                .HasMaxLength(300);

            builder.Property(x => x.CoverImageMimeType)
                .HasColumnName("cover_image_mime_type")
                .HasMaxLength(100);

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at");

            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at");

            builder.HasIndex(x => new { x.OwnerEmail, x.ParentFolderId, x.NameLowercase })
                .IsUnique()
                .AreNullsDistinct(false)
                .HasDatabaseName("uq_file_system_items_name_in_folder");

            builder.HasIndex(x => x.ParentFolderId)
                .HasDatabaseName("ix_file_system_items_parent_folder_id");

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.OwnerEmail)
                .HasConstraintName("fk_file_system_items_owner")
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.ParentFolder)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.ParentFolderId)
                .HasConstraintName("fk_file_system_items_parent_folder")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

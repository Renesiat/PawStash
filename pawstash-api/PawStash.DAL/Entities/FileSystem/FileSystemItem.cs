namespace PawStash.DAL.Entities.FileSystem
{
    public abstract class FileSystemItem
    {
        public Guid ItemId { get; set; }

        public string OwnerEmail { get; set; } = string.Empty;

        public Guid? ParentFolderId { get; set; }

        public Folder? ParentFolder { get; set; }

        public string Name { get; set; } = string.Empty;

        public string NameLowercase { get; private set; } = string.Empty;

        public string? Description { get; set; }

        public string? CoverImagePath { get; set; }

        public string? CoverImageMimeType { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public int PositionInFolder { get; set; }
    }
}

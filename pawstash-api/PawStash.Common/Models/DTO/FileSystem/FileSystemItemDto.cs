using PawStash.Common.Enums;

namespace PawStash.Common.Models.DTO.FileSystem
{
    public class FileSystemItemDto
    {
        public Guid ItemId { get; set; }

        public Guid? ParentFolderId { get; set; }

        public FileSystemItemType ItemType { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool HasCoverImage { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public string? LinkUrl { get; set; }

        public long? FileSizeBytes { get; set; }
    }
}

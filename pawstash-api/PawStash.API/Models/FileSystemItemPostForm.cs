using PawStash.Common.Enums;

namespace PawStash.API.Models
{
    public class FileSystemItemPostForm
    {
        public FileSystemItemType? ItemType { get; set; }

        public Guid? ParentFolderId { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? LinkUrl { get; set; }

        public string? NoteText { get; set; }

        public IFormFile? File { get; set; }

        public IFormFile? CoverImage { get; set; }
    }
}

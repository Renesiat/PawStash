using PawStash.Common.Enums;

namespace PawStash.BLL.Models
{
    public class FileSystemItemInput
    {
        public FileSystemItemType? ItemType { get; set; }

        public Guid? ParentFolderId { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? LinkUrl { get; set; }

        public string? NoteText { get; set; }

        public FileUpload? File { get; set; }

        public FileUpload? CoverImage { get; set; }

        public bool RemoveCoverImage { get; set; }
    }
}

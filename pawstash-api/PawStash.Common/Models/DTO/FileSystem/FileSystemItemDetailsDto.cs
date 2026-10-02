namespace PawStash.Common.Models.DTO.FileSystem
{
    public class FileSystemItemDetailsDto : FileSystemItemDto
    {
        public string? Description { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public string? NoteText { get; set; }

        public string? FileMimeType { get; set; }
    }
}

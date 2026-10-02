namespace PawStash.DAL.Entities.FileSystem
{
    public abstract class UploadedFile : FileSystemItem
    {
        public string FilePath { get; set; } = string.Empty;

        public string MimeType { get; set; } = string.Empty;

        public long SizeBytes { get; set; }
    }
}

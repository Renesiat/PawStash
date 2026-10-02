namespace PawStash.DAL.Entities.FileSystem
{
    public class Folder : FileSystemItem
    {
        public List<FileSystemItem> Items { get; set; } = [];
    }
}

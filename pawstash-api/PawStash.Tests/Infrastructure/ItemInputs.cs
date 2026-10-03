using PawStash.BLL.Models;
using PawStash.Common.Enums;

namespace PawStash.Tests.Infrastructure
{
    public static class ItemInputs
    {
        public static FileSystemItemInput Folder(string? name = null, Guid? parentFolderId = null)
        {
            return new FileSystemItemInput { ItemType = FileSystemItemType.Folder, Name = name, ParentFolderId = parentFolderId };
        }

        public static FileSystemItemInput Link(string url, string? name = null, Guid? parentFolderId = null)
        {
            return new FileSystemItemInput { ItemType = FileSystemItemType.Link, LinkUrl = url, Name = name, ParentFolderId = parentFolderId };
        }

        public static FileSystemItemInput Note(string text, string? name = null, Guid? parentFolderId = null)
        {
            return new FileSystemItemInput { ItemType = FileSystemItemType.Note, NoteText = text, Name = name, ParentFolderId = parentFolderId };
        }

        public static FileSystemItemInput Photo(string fileName, byte[] content, string? name = null, Guid? parentFolderId = null)
        {
            return new FileSystemItemInput
            {
                ItemType = FileSystemItemType.Photo,
                File = Upload(fileName, content),
                Name = name,
                ParentFolderId = parentFolderId
            };
        }

        public static FileSystemItemInput Document(string fileName, byte[] content, string? name = null, Guid? parentFolderId = null)
        {
            return new FileSystemItemInput
            {
                ItemType = FileSystemItemType.Document,
                File = Upload(fileName, content),
                Name = name,
                ParentFolderId = parentFolderId
            };
        }

        public static FileSystemItemInput Edit(string? name = null, string? description = null)
        {
            return new FileSystemItemInput { Name = name, Description = description };
        }

        public static FileUpload Upload(string fileName, byte[] content)
        {
            return new FileUpload(fileName, content.Length, () => new MemoryStream(content));
        }
    }
}

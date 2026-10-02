using System.Linq.Expressions;
using PawStash.Common.Enums;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.DAL.Entities.FileSystem;

namespace PawStash.BLL.Mappers
{
    public static class FileSystemItemMapper
    {
        public static readonly Expression<Func<FileSystemItem, FileSystemItemDto>> ToFileSystemItemDto = x => new FileSystemItemDto
        {
            ItemId = x.ItemId,
            ParentFolderId = x.ParentFolderId,
            ItemType = x is Folder ? FileSystemItemType.Folder
                : x is Link ? FileSystemItemType.Link
                : x is Note ? FileSystemItemType.Note
                : x is Photo ? FileSystemItemType.Photo
                : FileSystemItemType.Document,
            Name = x.Name,
            HasCoverImage = x.CoverImagePath != null,
            UpdatedAt = x.UpdatedAt,
            LinkUrl = x is Link ? ((Link)x).Url : null,
            FileSizeBytes = x is UploadedFile ? ((UploadedFile)x).SizeBytes : null
        };

        public static readonly Expression<Func<FileSystemItem, FileSystemItemDetailsDto>> ToFileSystemItemDetailsDto = x => new FileSystemItemDetailsDto
        {
            ItemId = x.ItemId,
            ParentFolderId = x.ParentFolderId,
            ItemType = x is Folder ? FileSystemItemType.Folder
                : x is Link ? FileSystemItemType.Link
                : x is Note ? FileSystemItemType.Note
                : x is Photo ? FileSystemItemType.Photo
                : FileSystemItemType.Document,
            Name = x.Name,
            HasCoverImage = x.CoverImagePath != null,
            UpdatedAt = x.UpdatedAt,
            LinkUrl = x is Link ? ((Link)x).Url : null,
            FileSizeBytes = x is UploadedFile ? ((UploadedFile)x).SizeBytes : null,
            Description = x.Description,
            CreatedAt = x.CreatedAt,
            NoteText = x is Note ? ((Note)x).Text : null,
            FileMimeType = x is UploadedFile ? ((UploadedFile)x).MimeType : null
        };

        private static readonly Func<FileSystemItem, FileSystemItemDto> ToFileSystemItemDtoCompiled = ToFileSystemItemDto.Compile();

        private static readonly Func<FileSystemItem, FileSystemItemDetailsDto> ToFileSystemItemDetailsDtoCompiled = ToFileSystemItemDetailsDto.Compile();

        public static FileSystemItemDto ToDto(FileSystemItem item)
        {
            return ToFileSystemItemDtoCompiled(item);
        }

        public static FileSystemItemDetailsDto ToDetailsDto(FileSystemItem item)
        {
            return ToFileSystemItemDetailsDtoCompiled(item);
        }

        public static FileSystemItemType GetItemType(FileSystemItem item)
        {
            return item switch
            {
                Folder => FileSystemItemType.Folder,
                Link => FileSystemItemType.Link,
                Note => FileSystemItemType.Note,
                Photo => FileSystemItemType.Photo,
                _ => FileSystemItemType.Document
            };
        }
    }
}

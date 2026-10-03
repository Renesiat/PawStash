using PawStash.BLL.Models;
using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.FileSystem;

namespace PawStash.BLL.Interfaces
{
    public interface IFileSystemService
    {
        Task<ServiceResult<List<FileSystemItemDto>>> GetFolderContents(Guid? parentFolderId);

        Task<ServiceResult<FileSystemItemDetailsDto>> GetItem(Guid itemId);

        Task<ServiceResult<List<FolderPathItemDto>>> GetPath(Guid itemId);

        Task<ServiceResult<FileSystemItemDetailsDto>> CreateItem(FileSystemItemInput fileSystemItemInput);

        Task<ServiceResult<FileSystemItemDetailsDto>> UpdateItem(Guid itemId, FileSystemItemInput fileSystemItemInput);

        Task<ServiceResult<FileSystemItemDto>> Move(Guid itemId, ItemParentFolderPutDto itemParentFolderPutDto);

        Task<ServiceResult<FileSystemItemDto>> ChangePosition(Guid itemId, ItemPositionPutDto itemPositionPutDto);

        Task<ServiceResult> Delete(Guid itemId);

        Task<ServiceResult<FileContent>> GetFile(Guid itemId);

        Task<ServiceResult<FileContent>> GetCoverImage(Guid itemId);
    }
}

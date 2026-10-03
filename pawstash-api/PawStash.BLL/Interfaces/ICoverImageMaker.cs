using PawStash.BLL.Models;
using PawStash.Common.Enums;

namespace PawStash.BLL.Interfaces
{
    public interface ICoverImageMaker
    {
        Task<FileUpload?> Reduce(FileUpload picture);

        Task<FileUpload?> MakeFromFile(FileSystemItemType itemType, FileUpload file);
    }
}

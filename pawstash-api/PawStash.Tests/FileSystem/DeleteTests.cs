using PawStash.BLL.Models;
using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Tests.Infrastructure;

namespace PawStash.Tests.FileSystem
{
    public class DeleteTests : FileSystemServiceTests
    {
        public DeleteTests(TestDatabase database) : base(database)
        {
        }

        [Fact]
        public async Task Delete_Photo_RemovesItemAndItsFiles()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("sunset.png", TestFiles.Png(40, 40)));
            int filesBefore = StoredFiles(photo.ItemId).Length;

            ServiceResult result = await Request(x => x.Delete(photo.ItemId));
            ServiceResult<FileSystemItemDetailsDto> item = await Request(x => x.GetItem(photo.ItemId));

            Assert.True(result.IsSuccess);
            Assert.Equal(2, filesBefore);
            Assert.Empty(StoredFiles(photo.ItemId));
            AssertError(item, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task Delete_Folder_RemovesEverythingInsideWithFiles()
        {
            FileSystemItemInput folderInput = ItemInputs.Folder("Рецепти");
            folderInput.CoverImage = ItemInputs.Upload("cover.png", TestFiles.Png(20, 20));
            FileSystemItemDetailsDto folder = await Create(folderInput);
            FileSystemItemDetailsDto child = await Create(ItemInputs.Folder("Супи", folder.ItemId));
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("borsch.png", TestFiles.Png(30, 30), parentFolderId: child.ItemId));
            FileSystemItemDetailsDto link = await Create(ItemInputs.Link("https://example.com", parentFolderId: folder.ItemId));
            FileSystemItemDetailsDto outside = await Create(ItemInputs.Note("лишається"));

            ServiceResult result = await Request(x => x.Delete(folder.ItemId));
            ServiceResult<List<FileSystemItemDto>> root = await Request(x => x.GetFolderContents(null));

            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { outside.ItemId }, root.Data!.Select(x => x.ItemId));

            foreach (Guid itemId in new[] { folder.ItemId, child.ItemId, photo.ItemId, link.ItemId })
            {
                AssertError(await Request(x => x.GetItem(itemId)), ServiceErrorType.NotFound);
                Assert.Empty(StoredFiles(itemId));
            }
        }

        [Fact]
        public async Task Delete_MissingItem_ReturnsNotFound()
        {
            ServiceResult result = await Request(x => x.Delete(Guid.NewGuid()));

            AssertError(result, ServiceErrorType.NotFound);
        }
    }
}

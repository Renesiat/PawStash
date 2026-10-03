using PawStash.BLL.Models;
using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Tests.Infrastructure;

namespace PawStash.Tests.FileSystem
{
    public class ReadTests : FileSystemServiceTests
    {
        public ReadTests(TestDatabase database) : base(database)
        {
        }

        [Fact]
        public async Task GetFolderContents_ListsNewestFirstWithoutPinningFolders()
        {
            await Create(ItemInputs.Note("текст", "борщ"));
            await Create(ItemInputs.Folder("Супи"));
            await Create(ItemInputs.Link("https://example.com", "Вареники"));
            await Create(ItemInputs.Folder("аперитиви"));

            ServiceResult<List<FileSystemItemDto>> result = await Request(x => x.GetFolderContents(null));

            Assert.Equal(new[] { "аперитиви", "Вареники", "Супи", "борщ" }, result.Data!.Select(x => x.Name));
        }

        [Fact]
        public async Task GetFolderContents_OnlyDirectChildren()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            FileSystemItemDetailsDto child = await Create(ItemInputs.Folder("Супи", folder.ItemId));
            await Create(ItemInputs.Note("борщ", parentFolderId: child.ItemId));

            ServiceResult<List<FileSystemItemDto>> result = await Request(x => x.GetFolderContents(folder.ItemId));

            Assert.Equal(new[] { child.ItemId }, result.Data!.Select(x => x.ItemId));
        }

        [Fact]
        public async Task GetFolderContents_MissingFolder_ReturnsNotFound()
        {
            ServiceResult<List<FileSystemItemDto>> result = await Request(x => x.GetFolderContents(Guid.NewGuid()));

            AssertError(result, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task GetPath_NestedItem_ReturnsFoldersFromRootToItem()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            FileSystemItemDetailsDto child = await Create(ItemInputs.Folder("Супи", folder.ItemId));
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("текст", "Борщ", child.ItemId));

            ServiceResult<List<FolderPathItemDto>> result = await Request(x => x.GetPath(note.ItemId));

            Assert.Equal(new[] { "Рецепти", "Супи", "Борщ" }, result.Data!.Select(x => x.Name));
        }

        [Fact]
        public async Task GetPath_MissingItem_ReturnsNotFound()
        {
            ServiceResult<List<FolderPathItemDto>> result = await Request(x => x.GetPath(Guid.NewGuid()));

            AssertError(result, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task GetItem_Document_ReturnsMimeTypeAndSize()
        {
            byte[] content = TestFiles.Pdf();
            FileSystemItemDetailsDto document = await Create(ItemInputs.Document("menu.pdf", content));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.GetItem(document.ItemId));

            Assert.Equal("application/pdf", result.Data!.FileMimeType);
            Assert.Equal(content.Length, result.Data.FileSizeBytes);
        }

        [Fact]
        public async Task GetFile_Folder_ReturnsNotFound()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));

            ServiceResult<FileContent> result = await Request(x => x.GetFile(folder.ItemId));

            AssertError(result, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task GetCoverImage_ItemWithoutCover_ReturnsNotFound()
        {
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("текст"));

            ServiceResult<FileContent> result = await Request(x => x.GetCoverImage(note.ItemId));

            AssertError(result, ServiceErrorType.NotFound);
        }
    }
}

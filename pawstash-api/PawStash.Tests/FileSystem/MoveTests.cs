using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Tests.Infrastructure;

namespace PawStash.Tests.FileSystem
{
    public class MoveTests : FileSystemServiceTests
    {
        public MoveTests(TestDatabase database) : base(database)
        {
        }

        [Fact]
        public async Task Move_ToFolder_ChangesParent()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("борщ"));

            ServiceResult<FileSystemItemDto> result = await Request(x => x.Move(note.ItemId, To(folder.ItemId)));
            ServiceResult<List<FileSystemItemDto>> contents = await Request(x => x.GetFolderContents(folder.ItemId));

            Assert.Equal(folder.ItemId, result.Data!.ParentFolderId);
            Assert.Contains(contents.Data!, x => x.ItemId == note.ItemId);
        }

        [Fact]
        public async Task Move_ToRoot_ClearsParent()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("борщ", parentFolderId: folder.ItemId));

            ServiceResult<FileSystemItemDto> result = await Request(x => x.Move(note.ItemId, To(null)));

            Assert.Null(result.Data!.ParentFolderId);
        }

        [Fact]
        public async Task Move_ToSameFolder_Succeeds()
        {
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("борщ"));

            ServiceResult<FileSystemItemDto> result = await Request(x => x.Move(note.ItemId, To(null)));

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Move_FolderIntoItself_ReturnsConflict()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));

            ServiceResult<FileSystemItemDto> result = await Request(x => x.Move(folder.ItemId, To(folder.ItemId)));

            AssertError(result, ServiceErrorType.Conflict);
        }

        [Fact]
        public async Task Move_FolderIntoNestedFolder_ReturnsConflict()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            FileSystemItemDetailsDto child = await Create(ItemInputs.Folder("Супи", folder.ItemId));
            FileSystemItemDetailsDto grandchild = await Create(ItemInputs.Folder("Холодні", child.ItemId));

            ServiceResult<FileSystemItemDto> result = await Request(x => x.Move(folder.ItemId, To(grandchild.ItemId)));

            AssertError(result, ServiceErrorType.Conflict);
        }

        [Fact]
        public async Task Move_NameTakenInTarget_ReturnsConflict()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            await Create(ItemInputs.Note("текст", "Борщ", folder.ItemId));
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("текст", "борщ"));

            ServiceResult<FileSystemItemDto> result = await Request(x => x.Move(note.ItemId, To(folder.ItemId)));

            AssertError(result, ServiceErrorType.Conflict);
        }

        [Fact]
        public async Task Move_TargetThatIsNotFolder_ReturnsNotFound()
        {
            FileSystemItemDetailsDto link = await Create(ItemInputs.Link("https://example.com"));
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("борщ"));

            ServiceResult<FileSystemItemDto> result = await Request(x => x.Move(note.ItemId, To(link.ItemId)));

            AssertError(result, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task Move_MissingItem_ReturnsNotFound()
        {
            ServiceResult<FileSystemItemDto> result = await Request(x => x.Move(Guid.NewGuid(), To(null)));

            AssertError(result, ServiceErrorType.NotFound);
        }

        private static ItemParentFolderPutDto To(Guid? folderId)
        {
            return new ItemParentFolderPutDto { TargetFolderId = folderId };
        }
    }
}

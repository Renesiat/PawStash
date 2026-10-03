using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Tests.Infrastructure;

namespace PawStash.Tests.FileSystem
{
    public class IsolationTests : FileSystemServiceTests
    {
        private readonly string _otherEmail = NewEmail();

        public IsolationTests(TestDatabase database) : base(database)
        {
        }

        [Fact]
        public async Task OtherEmail_DoesNotSeeItemsInRoot()
        {
            await AddUser(_otherEmail);
            await Create(ItemInputs.Folder("Рецепти"));

            ServiceResult<List<FileSystemItemDto>> result = await Request(x => x.GetFolderContents(null), _otherEmail);

            Assert.Empty(result.Data!);
        }

        [Fact]
        public async Task OtherEmail_CannotReadChangeMoveOrDeleteItem()
        {
            await AddUser(_otherEmail);
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("sunset.png", TestFiles.Png(20, 20)));

            AssertError(await Request(x => x.GetItem(photo.ItemId), _otherEmail), ServiceErrorType.NotFound);
            AssertError(await Request(x => x.GetFile(photo.ItemId), _otherEmail), ServiceErrorType.NotFound);
            AssertError(await Request(x => x.GetCoverImage(photo.ItemId), _otherEmail), ServiceErrorType.NotFound);
            AssertError(await Request(x => x.GetPath(photo.ItemId), _otherEmail), ServiceErrorType.NotFound);
            AssertError(await Request(x => x.UpdateItem(photo.ItemId, ItemInputs.Edit("x")), _otherEmail), ServiceErrorType.NotFound);
            AssertError(await Request(x => x.Move(photo.ItemId, new ItemParentFolderPutDto()), _otherEmail), ServiceErrorType.NotFound);
            AssertError(await Request(x => x.Delete(photo.ItemId), _otherEmail), ServiceErrorType.NotFound);
            Assert.True((await Request(x => x.GetItem(photo.ItemId))).IsSuccess);
        }

        [Fact]
        public async Task OtherEmail_CannotUseFolderAsParentOrTarget()
        {
            await AddUser(_otherEmail);
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            FileSystemItemDetailsDto otherNote = await Create(ItemInputs.Note("текст"), _otherEmail);

            ServiceResult<FileSystemItemDetailsDto> create = await Request(x => x.CreateItem(ItemInputs.Folder("Супи", folder.ItemId)), _otherEmail);
            ServiceResult<FileSystemItemDto> move = await Request(
                x => x.Move(otherNote.ItemId, new ItemParentFolderPutDto { TargetFolderId = folder.ItemId }),
                _otherEmail);

            AssertError(create, ServiceErrorType.NotFound);
            AssertError(move, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task SameNameInRoot_ForTwoEmails_Succeeds()
        {
            await AddUser(_otherEmail);
            await Create(ItemInputs.Folder("Рецепти"));

            FileSystemItemDetailsDto other = await Create(ItemInputs.Folder("Рецепти"), _otherEmail);

            Assert.Equal("Рецепти", other.Name);
        }
    }
}

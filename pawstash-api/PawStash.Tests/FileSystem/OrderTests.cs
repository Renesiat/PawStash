using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Tests.Infrastructure;

namespace PawStash.Tests.FileSystem
{
    public class OrderTests : FileSystemServiceTests
    {
        public OrderTests(TestDatabase database) : base(database)
        {
        }

        [Fact]
        public async Task CreateItem_GoesToTopOfFolder()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            await Create(ItemInputs.Note("1", "Перша", folder.ItemId));
            await Create(ItemInputs.Note("2", "Друга", folder.ItemId));

            Assert.Equal(new[] { "Друга", "Перша" }, await Names(folder.ItemId));
        }

        [Fact]
        public async Task Move_ItemGoesToTopOfTargetFolder()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            await Create(ItemInputs.Note("1", "Стара", folder.ItemId));
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("2", "Перенесена"));

            await Request(x => x.Move(note.ItemId, new ItemParentFolderPutDto { TargetFolderId = folder.ItemId }));

            Assert.Equal(new[] { "Перенесена", "Стара" }, await Names(folder.ItemId));
        }

        [Fact]
        public async Task ChangePosition_BeforeAnotherItem_PutsItThere()
        {
            await CreateNotes("C", "B", "A");

            FileSystemItemDto c = await Find("C");
            FileSystemItemDto b = await Find("B");
            ServiceResult<FileSystemItemDto> result = await Request(x => x.ChangePosition(c.ItemId, Before(b.ItemId)));

            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { "A", "C", "B" }, await Names(null));
        }

        [Fact]
        public async Task ChangePosition_WithoutBeforeItem_PutsItAtTheEnd()
        {
            await CreateNotes("C", "B", "A");

            FileSystemItemDto a = await Find("A");
            await Request(x => x.ChangePosition(a.ItemId, Before(null)));

            Assert.Equal(new[] { "B", "C", "A" }, await Names(null));
        }

        [Fact]
        public async Task ChangePosition_UpTheList_PutsItThere()
        {
            await CreateNotes("C", "B", "A");

            FileSystemItemDto c = await Find("C");
            FileSystemItemDto a = await Find("A");
            await Request(x => x.ChangePosition(c.ItemId, Before(a.ItemId)));

            Assert.Equal(new[] { "C", "A", "B" }, await Names(null));
        }

        [Fact]
        public async Task ChangePosition_BeforeItself_ChangesNothing()
        {
            await CreateNotes("C", "B", "A");

            FileSystemItemDto b = await Find("B");
            ServiceResult<FileSystemItemDto> result = await Request(x => x.ChangePosition(b.ItemId, Before(b.ItemId)));

            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { "A", "B", "C" }, await Names(null));
        }

        [Fact]
        public async Task ChangePosition_FolderBetweenOtherItems_IsAllowed()
        {
            await Create(ItemInputs.Note("1", "Нотатка"));
            await Create(ItemInputs.Link("https://example.com", "Посилання"));
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Папка"));

            FileSystemItemDto note = await Find("Нотатка");
            await Request(x => x.ChangePosition(folder.ItemId, Before(note.ItemId)));

            Assert.Equal(new[] { "Посилання", "Папка", "Нотатка" }, await Names(null));
        }

        [Fact]
        public async Task ChangePosition_BeforeItemInAnotherFolder_ReturnsNotFound()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            FileSystemItemDetailsDto inside = await Create(ItemInputs.Note("1", "Всередині", folder.ItemId));
            FileSystemItemDetailsDto outside = await Create(ItemInputs.Note("2", "Зовні"));

            ServiceResult<FileSystemItemDto> result = await Request(x => x.ChangePosition(outside.ItemId, Before(inside.ItemId)));

            AssertError(result, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task ChangePosition_MissingItem_ReturnsNotFound()
        {
            ServiceResult<FileSystemItemDto> result = await Request(x => x.ChangePosition(Guid.NewGuid(), Before(null)));

            AssertError(result, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task ChangePosition_OtherEmailsItem_ReturnsNotFound()
        {
            string otherEmail = NewEmail();
            await AddUser(otherEmail);
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("1", "Чужа"));

            ServiceResult<FileSystemItemDto> result = await Request(x => x.ChangePosition(note.ItemId, Before(null)), otherEmail);

            AssertError(result, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task ChangePosition_DoesNotChangeModifiedDate()
        {
            await CreateNotes("C", "B", "A");
            DateTimeOffset before = (await Find("B")).UpdatedAt;

            FileSystemItemDto c = await Find("C");
            FileSystemItemDto a = await Find("A");
            await Request(x => x.ChangePosition(c.ItemId, Before(a.ItemId)));

            Assert.Equal(before, (await Find("B")).UpdatedAt);
        }

        private async Task CreateNotes(params string[] names)
        {
            foreach (string name in names)
            {
                await Create(ItemInputs.Note("текст", name));
            }
        }

        private async Task<string[]> Names(Guid? folderId)
        {
            ServiceResult<List<FileSystemItemDto>> result = await Request(x => x.GetFolderContents(folderId));

            return result.Data!.Select(x => x.Name).ToArray();
        }

        private async Task<FileSystemItemDto> Find(string name)
        {
            ServiceResult<List<FileSystemItemDto>> result = await Request(x => x.GetFolderContents(null));

            return result.Data!.Single(x => x.Name == name);
        }

        private static ItemPositionPutDto Before(Guid? itemId)
        {
            return new ItemPositionPutDto { BeforeItemId = itemId };
        }
    }
}

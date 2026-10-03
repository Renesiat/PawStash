using PawStash.BLL.Models;
using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Common.Rules;
using PawStash.Tests.Infrastructure;

namespace PawStash.Tests.FileSystem
{
    public class UpdateItemTests : FileSystemServiceTests
    {
        public UpdateItemTests(TestDatabase database) : base(database)
        {
        }

        [Fact]
        public async Task UpdateItem_NameAndDescription_AreSaved()
        {
            FileSystemItemDetailsDto link = await Create(ItemInputs.Link("https://example.com", "Старе"));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(link.ItemId, ItemInputs.Edit("Нове", "опис")));

            Assert.True(result.IsSuccess);
            Assert.Equal("Нове", result.Data!.Name);
            Assert.Equal("опис", result.Data.Description);
            Assert.Equal("https://example.com", result.Data.LinkUrl);
        }

        [Fact]
        public async Task UpdateItem_EmptyDescription_RemovesDescription()
        {
            FileSystemItemInput input = ItemInputs.Folder("Рецепти");
            input.Description = "смачно";
            FileSystemItemDetailsDto folder = await Create(input);

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(folder.ItemId, ItemInputs.Edit("Рецепти")));

            Assert.Null(result.Data!.Description);
        }

        [Fact]
        public async Task UpdateItem_LinkWithEmptyName_UsesSiteAddress()
        {
            FileSystemItemDetailsDto link = await Create(ItemInputs.Link("https://www.example.com/a", "Своя назва"));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(link.ItemId, ItemInputs.Edit()));

            Assert.Equal("www.example.com", result.Data!.Name);
        }

        [Fact]
        public async Task UpdateItem_NoteWithEmptyName_UsesFirstLine()
        {
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("Перший рядок\nдругий", "Своя назва"));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(note.ItemId, ItemInputs.Edit("  ")));

            Assert.Equal("Перший рядок", result.Data!.Name);
        }

        [Fact]
        public async Task UpdateItem_PhotoWithEmptyName_KeepsCurrentName()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("sunset.png", TestFiles.Png(20, 20), "Захід сонця"));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(photo.ItemId, ItemInputs.Edit()));

            Assert.Equal("Захід сонця", result.Data!.Name);
        }

        [Fact]
        public async Task UpdateItem_FolderWithEmptyName_UsesDefaultName()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(folder.ItemId, ItemInputs.Edit()));

            Assert.Equal(FileSystemItemRules.DefaultFolderName, result.Data!.Name);
        }

        [Fact]
        public async Task UpdateItem_NewLinkAddress_ReturnsValidationErrorAndKeepsAddress()
        {
            FileSystemItemDetailsDto link = await Create(ItemInputs.Link("https://example.com/a"));
            FileSystemItemInput input = ItemInputs.Edit("Нове");
            input.LinkUrl = "https://other.org";

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(link.ItemId, input));
            ServiceResult<FileSystemItemDetailsDto> stored = await Request(x => x.GetItem(link.ItemId));

            AssertFieldError(result, nameof(FileSystemItemInput.LinkUrl));
            Assert.Equal("https://example.com/a", stored.Data!.LinkUrl);
            Assert.Equal(link.Name, stored.Data.Name);
        }

        [Fact]
        public async Task UpdateItem_NewNoteText_ReturnsValidationError()
        {
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("текст"));
            FileSystemItemInput input = ItemInputs.Edit("Нове");
            input.NoteText = "інший текст";

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(note.ItemId, input));

            AssertFieldError(result, nameof(FileSystemItemInput.NoteText));
        }

        [Fact]
        public async Task UpdateItem_NewFile_ReturnsValidationErrorAndKeepsFile()
        {
            byte[] content = TestFiles.Png(20, 20);
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("sunset.png", content));
            FileSystemItemInput input = ItemInputs.Edit("sunset.png");
            input.File = ItemInputs.Upload("other.png", TestFiles.Png(30, 30));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(photo.ItemId, input));
            (byte[] Content, string MimeType) file = await ReadFile(photo.ItemId);

            AssertFieldError(result, nameof(FileSystemItemInput.File));
            Assert.Equal(content, file.Content);
        }

        [Fact]
        public async Task UpdateItem_NameTakenInFolder_ReturnsConflict()
        {
            await Create(ItemInputs.Folder("Рецепти"));
            FileSystemItemDetailsDto other = await Create(ItemInputs.Folder("Супи"));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(other.ItemId, ItemInputs.Edit("рецепти")));

            AssertError(result, ServiceErrorType.Conflict);
        }

        [Fact]
        public async Task UpdateItem_SameNameInOtherCase_Succeeds()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("рецепти"));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(folder.ItemId, ItemInputs.Edit("Рецепти")));

            Assert.Equal("Рецепти", result.Data!.Name);
        }

        [Fact]
        public async Task UpdateItem_MissingItem_ReturnsNotFound()
        {
            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(Guid.NewGuid(), ItemInputs.Edit("x")));

            AssertError(result, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task UpdateItem_NewCoverImage_ReplacesOldCoverFile()
        {
            FileSystemItemInput input = ItemInputs.Folder("Рецепти");
            input.CoverImage = ItemInputs.Upload("cover.png", TestFiles.Png(20, 20));
            FileSystemItemDetailsDto folder = await Create(input);
            string[] coversBefore = StoredFiles(folder.ItemId);

            FileSystemItemInput edit = ItemInputs.Edit("Рецепти");
            edit.CoverImage = ItemInputs.Upload("cover.jpg", TestFiles.Jpeg(40, 40));
            await Request(x => x.UpdateItem(folder.ItemId, edit));
            string[] coversAfter = StoredFiles(folder.ItemId);

            Assert.Single(coversBefore);
            Assert.Single(coversAfter);
            Assert.NotEqual(coversBefore[0], coversAfter[0]);
        }

        [Fact]
        public async Task UpdateItem_RemoveCoverImage_DeletesCoverFile()
        {
            FileSystemItemInput input = ItemInputs.Folder("Рецепти");
            input.CoverImage = ItemInputs.Upload("cover.png", TestFiles.Png(20, 20));
            FileSystemItemDetailsDto folder = await Create(input);

            FileSystemItemInput edit = ItemInputs.Edit("Рецепти");
            edit.RemoveCoverImage = true;
            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(folder.ItemId, edit));

            Assert.False(result.Data!.HasCoverImage);
            Assert.Empty(StoredFiles(folder.ItemId));
            Assert.Null(await ReadCoverImage(folder.ItemId));
        }

        [Fact]
        public async Task UpdateItem_WithoutCoverChanges_KeepsCover()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("sunset.png", TestFiles.Png(40, 40)));
            (byte[] Content, string MimeType)? coverBefore = await ReadCoverImage(photo.ItemId);

            await Request(x => x.UpdateItem(photo.ItemId, ItemInputs.Edit("Захід")));
            (byte[] Content, string MimeType)? coverAfter = await ReadCoverImage(photo.ItemId);

            Assert.NotNull(coverBefore);
            Assert.Equal(coverBefore.Value.Content, coverAfter!.Value.Content);
        }

        [Fact]
        public async Task UpdateItem_CoverImageAndRemoveCoverImage_ReturnsValidationError()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            FileSystemItemInput edit = ItemInputs.Edit("Рецепти");
            edit.CoverImage = ItemInputs.Upload("cover.png", TestFiles.Png(20, 20));
            edit.RemoveCoverImage = true;

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(folder.ItemId, edit));

            AssertFieldError(result, nameof(FileSystemItemInput.RemoveCoverImage));
        }
    }
}

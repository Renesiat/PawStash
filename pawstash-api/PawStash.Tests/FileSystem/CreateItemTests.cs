using PawStash.BLL.Models;
using PawStash.BLL.Results;
using PawStash.Common.Enums;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Common.Rules;
using PawStash.Tests.Infrastructure;

namespace PawStash.Tests.FileSystem
{
    public class CreateItemTests : FileSystemServiceTests
    {
        public CreateItemTests(TestDatabase database) : base(database)
        {
        }

        [Fact]
        public async Task CreateItem_FolderWithoutName_UsesDefaultName()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder());

            Assert.Equal(FileSystemItemRules.DefaultFolderName, folder.Name);
            Assert.Equal(FileSystemItemType.Folder, folder.ItemType);
        }

        [Fact]
        public async Task CreateItem_LinkWithoutName_UsesSiteAddress()
        {
            FileSystemItemDetailsDto link = await Create(ItemInputs.Link("  https://www.example.com/borsch?x=1  "));

            Assert.Equal("www.example.com", link.Name);
            Assert.Equal("https://www.example.com/borsch?x=1", link.LinkUrl);
        }

        [Fact]
        public async Task CreateItem_NoteWithoutName_UsesFirstNonEmptyLine()
        {
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note("\n\n  Перший рядок  \nдругий рядок"));

            Assert.Equal("Перший рядок", note.Name);
            Assert.Equal("\n\n  Перший рядок  \nдругий рядок", note.NoteText);
        }

        [Fact]
        public async Task CreateItem_NoteWithLongFirstLine_CutsNameToMaxLength()
        {
            FileSystemItemDetailsDto note = await Create(ItemInputs.Note(new string('я', 300)));

            Assert.Equal(FileSystemItemRules.MaxNameLength, note.Name.Length);
        }

        [Fact]
        public async Task CreateItem_PhotoWithoutName_UsesFileName()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("sunset.png", TestFiles.Png(40, 30)));

            Assert.Equal("sunset.png", photo.Name);
        }

        [Fact]
        public async Task CreateItem_NameAndDescriptionWithSpaces_AreTrimmed()
        {
            FileSystemItemInput input = ItemInputs.Folder("  Рецепти  ");
            input.Description = "  смачно  ";

            FileSystemItemDetailsDto folder = await Create(input);

            Assert.Equal("Рецепти", folder.Name);
            Assert.Equal("смачно", folder.Description);
        }

        [Fact]
        public async Task CreateItem_DescriptionOfSpaces_IsDropped()
        {
            FileSystemItemInput input = ItemInputs.Folder("Рецепти");
            input.Description = "   ";

            FileSystemItemDetailsDto folder = await Create(input);

            Assert.Null(folder.Description);
        }

        [Fact]
        public async Task CreateItem_WithoutItemType_ReturnsValidationError()
        {
            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(new FileSystemItemInput { Name = "x" }));

            AssertFieldError(result, nameof(FileSystemItemInput.ItemType));
        }

        [Theory]
        [InlineData("")]
        [InlineData("ftp://example.com")]
        [InlineData("example.com")]
        [InlineData("not an address")]
        public async Task CreateItem_LinkWithBadAddress_ReturnsValidationError(string url)
        {
            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(ItemInputs.Link(url)));

            AssertFieldError(result, nameof(FileSystemItemInput.LinkUrl));
        }

        [Fact]
        public async Task CreateItem_LinkAddressTooLong_ReturnsValidationError()
        {
            string url = "https://example.com/" + new string('a', FileSystemItemRules.MaxLinkUrlLength);

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(ItemInputs.Link(url)));

            AssertFieldError(result, nameof(FileSystemItemInput.LinkUrl));
        }

        [Fact]
        public async Task CreateItem_EmptyNote_ReturnsValidationError()
        {
            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(ItemInputs.Note("  \n ")));

            AssertFieldError(result, nameof(FileSystemItemInput.NoteText));
        }

        [Fact]
        public async Task CreateItem_PhotoWithPdfFile_ReturnsValidationError()
        {
            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(ItemInputs.Photo("menu.pdf", TestFiles.Pdf())));

            AssertFieldError(result, nameof(FileSystemItemInput.File));
        }

        [Fact]
        public async Task CreateItem_DocumentWithPngFile_ReturnsValidationError()
        {
            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(ItemInputs.Document("photo.png", TestFiles.Png(10, 10))));

            AssertFieldError(result, nameof(FileSystemItemInput.File));
        }

        [Fact]
        public async Task CreateItem_PhotoWithoutFile_ReturnsValidationError()
        {
            FileSystemItemInput input = new() { ItemType = FileSystemItemType.Photo };

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(input));

            AssertFieldError(result, nameof(FileSystemItemInput.File));
        }

        [Fact]
        public async Task CreateItem_FileTooBig_ReturnsValidationError()
        {
            FileSystemItemInput input = new()
            {
                ItemType = FileSystemItemType.Document,
                File = new FileUpload("big.pdf", FileSystemItemRules.MaxUploadedFileSizeBytes + 1, () => Stream.Null)
            };

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(input));

            AssertFieldError(result, nameof(FileSystemItemInput.File));
        }

        [Fact]
        public async Task CreateItem_FolderWithLinkAddress_ReturnsValidationError()
        {
            FileSystemItemInput input = ItemInputs.Folder("Рецепти");
            input.LinkUrl = "https://example.com";

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(input));

            AssertFieldError(result, nameof(FileSystemItemInput.LinkUrl));
        }

        [Fact]
        public async Task CreateItem_NameTooLong_ReturnsValidationError()
        {
            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(ItemInputs.Folder(new string('a', 256))));

            AssertFieldError(result, nameof(FileSystemItemInput.Name));
        }

        [Fact]
        public async Task CreateItem_CoverImageOfWrongFormat_ReturnsValidationError()
        {
            FileSystemItemInput input = ItemInputs.Folder("Рецепти");
            input.CoverImage = ItemInputs.Upload("cover.pdf", TestFiles.Pdf());

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(input));

            AssertFieldError(result, nameof(FileSystemItemInput.CoverImage));
        }

        [Fact]
        public async Task CreateItem_CoverImageAndRemoveCoverImage_ReturnsValidationError()
        {
            FileSystemItemInput input = ItemInputs.Folder("Рецепти");
            input.CoverImage = ItemInputs.Upload("cover.png", TestFiles.Png(10, 10));
            input.RemoveCoverImage = true;

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(input));

            AssertFieldError(result, nameof(FileSystemItemInput.RemoveCoverImage));
        }

        [Fact]
        public async Task CreateItem_MissingParentFolder_ReturnsNotFound()
        {
            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(ItemInputs.Folder("Супи", Guid.NewGuid())));

            AssertError(result, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task CreateItem_ParentThatIsNotFolder_ReturnsNotFound()
        {
            FileSystemItemDetailsDto link = await Create(ItemInputs.Link("https://example.com"));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(ItemInputs.Folder("Супи", link.ItemId)));

            AssertError(result, ServiceErrorType.NotFound);
        }

        [Fact]
        public async Task CreateItem_NameTakenInFolderWithOtherCase_ReturnsConflict()
        {
            await Create(ItemInputs.Folder("Рецепти"));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(ItemInputs.Note("текст", "РЕЦЕПТИ")));

            AssertError(result, ServiceErrorType.Conflict);
        }

        [Fact]
        public async Task CreateItem_SameNameInAnotherFolder_Succeeds()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            await Create(ItemInputs.Folder("Супи"));

            FileSystemItemDetailsDto nested = await Create(ItemInputs.Folder("Супи", folder.ItemId));

            Assert.Equal(folder.ItemId, nested.ParentFolderId);
        }

        [Fact]
        public async Task CreateItem_Photo_StoresFileThatCanBeRead()
        {
            byte[] content = TestFiles.Png(40, 30);

            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("sunset.png", content));
            (byte[] Content, string MimeType) file = await ReadFile(photo.ItemId);

            Assert.Equal(content, file.Content);
            Assert.Equal("image/png", file.MimeType);
            Assert.Equal(content.Length, photo.FileSizeBytes);
        }

        [Fact]
        public async Task CreateItem_TextDocument_IsStoredAsUtf8Text()
        {
            FileSystemItemDetailsDto document = await Create(ItemInputs.Document("pokupky.txt", TestFiles.Text("Буряк, капуста")));
            (byte[] Content, string MimeType) file = await ReadFile(document.ItemId);

            Assert.Equal("text/plain; charset=utf-8", file.MimeType);
            Assert.Equal("Буряк, капуста", System.Text.Encoding.UTF8.GetString(file.Content));
        }
    }
}

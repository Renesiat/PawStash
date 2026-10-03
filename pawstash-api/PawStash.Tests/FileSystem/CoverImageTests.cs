using PawStash.BLL.Models;
using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Tests.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PawStash.Tests.FileSystem
{
    public class CoverImageTests : FileSystemServiceTests
    {
        public CoverImageTests(TestDatabase database) : base(database)
        {
        }

        [Fact]
        public async Task CreateItem_BigPhoto_MakesReducedJpegCover()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("big.jpg", TestFiles.Jpeg(2000, 1000)));

            (byte[] Content, string MimeType) cover = await ReadExistingCover(photo);

            Assert.Equal("image/jpeg", cover.MimeType);
            AssertSize(cover.Content, 512, 256);
        }

        [Fact]
        public async Task CreateItem_PhotoTurnedByCamera_MakesUprightCover()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("camera.jpg", TestFiles.Jpeg(1200, 800, exifOrientation: 6)));

            (byte[] Content, string MimeType) cover = await ReadExistingCover(photo);
            using Image<Rgba32> image = Image.Load<Rgba32>(cover.Content);

            Assert.Equal(341, image.Width);
            Assert.Equal(512, image.Height);
            Assert.True(IsMarker(image[image.Width - 10, 10]), "The corner marker should move to the top right.");
            Assert.False(IsMarker(image[10, 10]), "The top left should no longer hold the marker.");
        }

        [Fact]
        public async Task CreateItem_SmallPhoto_DoesNotEnlargeCover()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("small.png", TestFiles.Png(300, 200)));

            (byte[] Content, string MimeType) cover = await ReadExistingCover(photo);

            Assert.Equal("image/jpeg", cover.MimeType);
            AssertSize(cover.Content, 300, 200);
        }

        [Fact]
        public async Task CreateItem_TransparentPng_MakesPngCover()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("logo.png", TestFiles.Png(800, 800, transparent: true)));

            (byte[] Content, string MimeType) cover = await ReadExistingCover(photo);

            Assert.Equal("image/png", cover.MimeType);
            AssertSize(cover.Content, 512, 512);
        }

        [Fact]
        public async Task CreateItem_AnimatedGif_MakesSingleFrameJpegCover()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("anim.gif", TestFiles.AnimatedGif(600, 400)));

            (byte[] Content, string MimeType) cover = await ReadExistingCover(photo);

            Assert.Equal("image/jpeg", cover.MimeType);
            AssertSize(cover.Content, 512, 341);
        }

        [Fact]
        public async Task CreateItem_WebpPhoto_MakesJpegCover()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("photo.webp", TestFiles.Webp(1000, 1000)));

            (byte[] Content, string MimeType) cover = await ReadExistingCover(photo);

            Assert.Equal("image/jpeg", cover.MimeType);
            AssertSize(cover.Content, 512, 512);
        }

        [Fact]
        public async Task CreateItem_BrokenPhoto_IsSavedWithoutCover()
        {
            FileSystemItemDetailsDto photo = await Create(ItemInputs.Photo("broken.png", TestFiles.Broken()));

            Assert.False(photo.HasCoverImage);
            Assert.Null(await ReadCoverImage(photo.ItemId));
        }

        [Fact]
        public async Task CreateItem_PhotoWithRemoveCoverImage_HasNoCover()
        {
            FileSystemItemInput input = ItemInputs.Photo("sunset.png", TestFiles.Png(100, 100));
            input.RemoveCoverImage = true;

            FileSystemItemDetailsDto photo = await Create(input);

            Assert.False(photo.HasCoverImage);
            Assert.Single(StoredFiles(photo.ItemId));
        }

        [Fact]
        public async Task CreateItem_PhotoWithOwnCover_UsesOwnCoverReduced()
        {
            FileSystemItemInput input = ItemInputs.Photo("sunset.png", TestFiles.Png(100, 100));
            input.CoverImage = ItemInputs.Upload("own.jpg", TestFiles.Jpeg(1200, 630));

            FileSystemItemDetailsDto photo = await Create(input);
            (byte[] Content, string MimeType) cover = await ReadExistingCover(photo);

            AssertSize(cover.Content, 512, 269);
        }

        [Fact]
        public async Task CreateItem_PdfWithPicture_UsesPictureAndSkipsSmallLogo()
        {
            byte[] pdf = TestFiles.PdfWithJpeg(TestFiles.Png(40, 40), TestFiles.Jpeg(1000, 700));

            FileSystemItemDetailsDto document = await Create(ItemInputs.Document("menu.pdf", pdf));
            (byte[] Content, string MimeType) cover = await ReadExistingCover(document);

            Assert.Equal("image/jpeg", cover.MimeType);
            AssertSize(cover.Content, 512, 358);
        }

        [Fact]
        public async Task CreateItem_PdfWithPngPicture_UsesPicture()
        {
            byte[] pdf = TestFiles.Pdf(TestFiles.Png(600, 300));

            FileSystemItemDetailsDto document = await Create(ItemInputs.Document("menu.pdf", pdf));
            (byte[] Content, string MimeType) cover = await ReadExistingCover(document);

            AssertSize(cover.Content, 512, 256);
        }

        [Fact]
        public async Task CreateItem_PdfWithOnlySmallLogo_HasNoCover()
        {
            byte[] pdf = TestFiles.Pdf(TestFiles.Png(40, 40));

            FileSystemItemDetailsDto document = await Create(ItemInputs.Document("letter.pdf", pdf));

            Assert.False(document.HasCoverImage);
        }

        [Fact]
        public async Task CreateItem_PdfWithoutPictures_HasNoCover()
        {
            FileSystemItemDetailsDto document = await Create(ItemInputs.Document("text.pdf", TestFiles.Pdf()));

            Assert.False(document.HasCoverImage);
        }

        [Fact]
        public async Task CreateItem_TextDocument_HasNoCover()
        {
            FileSystemItemDetailsDto document = await Create(ItemInputs.Document("pokupky.txt", TestFiles.Text("буряк")));

            Assert.False(document.HasCoverImage);
        }

        [Fact]
        public async Task CreateItem_LinkWithPicture_StoresPictureReduced()
        {
            FileSystemItemInput input = ItemInputs.Link("https://example.com");
            input.CoverImage = ItemInputs.Upload("cover.jpg", TestFiles.Jpeg(1200, 630));

            FileSystemItemDetailsDto link = await Create(input);
            (byte[] Content, string MimeType) cover = await ReadExistingCover(link);

            AssertSize(cover.Content, 512, 269);
        }

        [Fact]
        public async Task CreateItem_BrokenOwnCover_IsStoredAsSent()
        {
            byte[] broken = TestFiles.Broken();
            FileSystemItemInput input = ItemInputs.Folder("Рецепти");
            input.CoverImage = ItemInputs.Upload("cover.png", broken);

            FileSystemItemDetailsDto folder = await Create(input);
            (byte[] Content, string MimeType) cover = await ReadExistingCover(folder);

            Assert.Equal(broken, cover.Content);
        }

        [Fact]
        public async Task UpdateItem_NewCover_IsReduced()
        {
            FileSystemItemDetailsDto folder = await Create(ItemInputs.Folder("Рецепти"));
            FileSystemItemInput edit = ItemInputs.Edit("Рецепти");
            edit.CoverImage = ItemInputs.Upload("cover.webp", TestFiles.Webp(2000, 2000));

            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.UpdateItem(folder.ItemId, edit));
            (byte[] Content, string MimeType) cover = await ReadExistingCover(result.Data!);

            Assert.Equal("image/jpeg", cover.MimeType);
            AssertSize(cover.Content, 512, 512);
        }

        private async Task<(byte[] Content, string MimeType)> ReadExistingCover(FileSystemItemDto item)
        {
            Assert.True(item.HasCoverImage, $"«{item.Name}» should have a cover image.");

            (byte[] Content, string MimeType)? cover = await ReadCoverImage(item.ItemId);

            Assert.NotNull(cover);

            return cover.Value;
        }

        private static void AssertSize(byte[] picture, int width, int height)
        {
            ImageInfo info = Image.Identify(picture);

            Assert.Equal((width, height), (info.Width, info.Height));
        }

        private static bool IsMarker(Rgba32 pixel)
        {
            return Math.Abs(pixel.R - TestFiles.Marker.R) < 40
                && Math.Abs(pixel.G - TestFiles.Marker.G) < 40
                && Math.Abs(pixel.B - TestFiles.Marker.B) < 40;
        }
    }
}

using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace PawStash.Tests.Infrastructure
{
    public static class TestFiles
    {
        public static readonly Rgba32 Marker = new(220, 20, 20);

        public static byte[] Jpeg(int width, int height, ushort? exifOrientation = null)
        {
            using Image<Rgba32> image = Picture(width, height);

            if (exifOrientation is ushort orientation)
            {
                image.Metadata.ExifProfile = new ExifProfile();
                image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, orientation);
            }

            using MemoryStream output = new();
            image.SaveAsJpeg(output);

            return output.ToArray();
        }

        public static byte[] Png(int width, int height, bool transparent = false)
        {
            using Image<Rgba32> image = transparent ? Circle(width, height) : Picture(width, height);
            using MemoryStream output = new();
            image.SaveAsPng(output);

            return output.ToArray();
        }

        public static byte[] AnimatedGif(int width, int height)
        {
            using Image<Rgba32> image = Picture(width, height);
            using Image<Rgba32> secondFrame = Circle(width, height);
            image.Frames.AddFrame(secondFrame.Frames.RootFrame);

            using MemoryStream output = new();
            image.SaveAsGif(output);

            return output.ToArray();
        }

        public static byte[] Webp(int width, int height)
        {
            using Image<Rgba32> image = Picture(width, height);
            using MemoryStream output = new();
            image.SaveAsWebp(output);

            return output.ToArray();
        }

        public static byte[] Pdf(params byte[][] pngPictures)
        {
            PdfDocumentBuilder builder = new();
            PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
            PdfPageBuilder firstPage = builder.AddPage(PageSize.A4);
            firstPage.AddText("Recipe book", 24, new PdfPoint(72, 760), font);

            foreach (byte[] picture in pngPictures)
            {
                PdfPageBuilder page = builder.AddPage(PageSize.A4);
                page.AddPng(picture, new PdfRectangle(72, 300, 522, 615));
            }

            return builder.Build();
        }

        public static byte[] PdfWithJpeg(byte[] smallLogo, byte[] jpegPhoto)
        {
            PdfDocumentBuilder builder = new();
            PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
            PdfPageBuilder firstPage = builder.AddPage(PageSize.A4);
            firstPage.AddText("Recipe book", 24, new PdfPoint(72, 760), font);
            firstPage.AddPng(smallLogo, new PdfRectangle(72, 690, 112, 730));

            PdfPageBuilder secondPage = builder.AddPage(PageSize.A4);
            secondPage.AddJpeg(jpegPhoto, new PdfRectangle(72, 300, 522, 615));

            return builder.Build();
        }

        public static byte[] Text(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }

        public static byte[] Broken()
        {
            return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8];
        }

        private static Image<Rgba32> Picture(int width, int height)
        {
            Image<Rgba32> image = new(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isMarker = x < width / 5 && y < height / 5;
                    image[x, y] = isMarker ? Marker : new Rgba32((byte)(40 + 200 * x / width), (byte)(120 + 100 * y / height), 200);
                }
            }

            return image;
        }

        private static Image<Rgba32> Circle(int width, int height)
        {
            Image<Rgba32> image = new(width, height);
            double radius = Math.Min(width, height) * 0.4;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double distance = Math.Sqrt(Math.Pow(x - width / 2.0, 2) + Math.Pow(y - height / 2.0, 2));
                    image[x, y] = distance < radius ? new Rgba32(81, 43, 212, 255) : new Rgba32(0, 0, 0, 0);
                }
            }

            return image;
        }
    }
}

using Microsoft.Extensions.Logging;
using PawStash.BLL.Interfaces;
using PawStash.BLL.Models;
using PawStash.Common.Enums;
using PawStash.Common.Rules;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PawStash.BLL.Implementations
{
    public class CoverImageMaker : ICoverImageMaker
    {
        private const int MaxSidePixels = 512;

        private const int JpegQuality = 85;

        private const int MaxPdfPages = 10;

        private const int MinPdfPicturePixels = 100;

        private const string PdfMimeType = "application/pdf";

        private readonly ILogger<CoverImageMaker> _logger;

        public CoverImageMaker(ILogger<CoverImageMaker> logger)
        {
            _logger = logger;
        }

        public async Task<FileUpload?> Reduce(FileUpload picture)
        {
            try
            {
                return await Reduce(picture.OpenReadStream);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not reduce the picture {FileName}.", picture.FileName);

                return null;
            }
        }

        public async Task<FileUpload?> MakeFromFile(FileSystemItemType itemType, FileUpload file)
        {
            if (itemType == FileSystemItemType.Photo)
            {
                return await Reduce(file);
            }

            if (itemType != FileSystemItemType.Document || FileSystemItemRules.GetMimeType(file.FileName) != PdfMimeType)
            {
                return null;
            }

            try
            {
                return await MakeFromPdf(file);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not take a picture from the PDF {FileName}.", file.FileName);

                return null;
            }
        }

        private static async Task<FileUpload?> MakeFromPdf(FileUpload file)
        {
            byte[] content;

            await using (Stream stream = file.OpenReadStream())
            {
                using MemoryStream buffer = new();
                await stream.CopyToAsync(buffer);
                content = buffer.ToArray();
            }

            using PdfDocument document = PdfDocument.Open(content);

            foreach (Page page in document.GetPages().Take(MaxPdfPages))
            {
                foreach (IPdfImage picture in page.GetImages())
                {
                    if (picture.IsImageMask
                        || picture.WidthInSamples < MinPdfPicturePixels
                        || picture.HeightInSamples < MinPdfPicturePixels)
                    {
                        continue;
                    }

                    byte[] pictureBytes = picture.TryGetPng(out byte[]? png) ? png : picture.RawMemory.ToArray();
                    FileUpload? coverImage = await TryReduce(pictureBytes);

                    if (coverImage is not null)
                    {
                        return coverImage;
                    }
                }
            }

            return null;
        }

        private static async Task<FileUpload?> TryReduce(byte[] picture)
        {
            try
            {
                return await Reduce(() => new MemoryStream(picture));
            }
            catch (Exception exception) when (exception is ImageFormatException or NotSupportedException)
            {
                return null;
            }
        }

        private static async Task<FileUpload> Reduce(Func<Stream> openStream)
        {
            DecoderOptions decoderOptions = await IsBiggerThanCover(openStream)
                ? new DecoderOptions { MaxFrames = 1, TargetSize = new Size(MaxSidePixels, MaxSidePixels) }
                : new DecoderOptions { MaxFrames = 1 };

            await using Stream stream = openStream();
            using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(decoderOptions, stream);

            image.Mutate(x => x.AutoOrient());

            if (image.Width > MaxSidePixels || image.Height > MaxSidePixels)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxSidePixels, MaxSidePixels),
                    Mode = ResizeMode.Max
                }));
            }

            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;

            using MemoryStream output = new();
            string fileName;

            if (HasTransparency(image))
            {
                await image.SaveAsPngAsync(output);
                fileName = "cover.png";
            }
            else
            {
                await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = JpegQuality });
                fileName = "cover.jpg";
            }

            byte[] coverImage = output.ToArray();

            return new FileUpload(fileName, coverImage.Length, () => new MemoryStream(coverImage));
        }

        private static async Task<bool> IsBiggerThanCover(Func<Stream> openStream)
        {
            await using Stream stream = openStream();
            ImageInfo imageInfo = await Image.IdentifyAsync(stream);

            return imageInfo.Width > MaxSidePixels || imageInfo.Height > MaxSidePixels;
        }

        private static bool HasTransparency(Image<Rgba32> image)
        {
            bool hasTransparency = false;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height && !hasTransparency; y++)
                {
                    foreach (Rgba32 pixel in accessor.GetRowSpan(y))
                    {
                        if (pixel.A < byte.MaxValue)
                        {
                            hasTransparency = true;
                            break;
                        }
                    }
                }
            });

            return hasTransparency;
        }
    }
}

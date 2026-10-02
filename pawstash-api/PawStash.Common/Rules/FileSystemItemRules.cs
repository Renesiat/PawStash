using PawStash.Common.Enums;

namespace PawStash.Common.Rules
{
    public static class FileSystemItemRules
    {
        public const int MaxNameLength = 255;

        public const int MaxDescriptionLength = 2000;

        public const int MaxLinkUrlLength = 2048;

        public const int MaxNoteTextLength = 100_000;

        public const long MaxUploadedFileSizeBytes = 25L * 1024 * 1024;

        public const long MaxCoverImageSizeBytes = 5L * 1024 * 1024;

        public const string DefaultFolderName = "Нова папка";

        private static readonly Dictionary<string, string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif"
        };

        private static readonly Dictionary<string, string> DocumentMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".txt"] = "text/plain; charset=utf-8",
            [".md"] = "text/markdown; charset=utf-8",
            [".csv"] = "text/csv; charset=utf-8",
            [".json"] = "application/json"
        };

        public static string GetTypeName(FileSystemItemType itemType)
        {
            return itemType switch
            {
                FileSystemItemType.Folder => "папка",
                FileSystemItemType.Link => "посилання",
                FileSystemItemType.Note => "нотатка",
                FileSystemItemType.Photo => "фото",
                _ => "документ"
            };
        }

        public static string NormalizeName(string name)
        {
            return name.Trim();
        }

        public static string? NormalizeDescription(string? description)
        {
            return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }

        public static string? ValidateName(string? name)
        {
            if (name is not null && name.Trim().Length > MaxNameLength)
            {
                return $"Назва задовга (максимум {MaxNameLength} символів).";
            }

            return null;
        }

        public static string? ValidateDescription(string? description)
        {
            if (description is not null && description.Trim().Length > MaxDescriptionLength)
            {
                return $"Опис задовгий (максимум {MaxDescriptionLength} символів).";
            }

            return null;
        }

        public static string? ValidateLinkUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "Введіть адресу посилання.";
            }

            string trimmed = url.Trim();

            if (trimmed.Length > MaxLinkUrlLength)
            {
                return $"Адреса задовга (максимум {MaxLinkUrlLength} символів).";
            }

            if (GetWebAddress(trimmed) is null)
            {
                return "Адреса має починатися з http:// або https://";
            }

            return null;
        }

        public static string? ValidateNoteText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Введіть текст нотатки.";
            }

            if (text.Length > MaxNoteTextLength)
            {
                return $"Текст задовгий (максимум {MaxNoteTextLength} символів).";
            }

            return null;
        }

        public static string? ValidatePhotoFile(string? fileName, long sizeBytes)
        {
            return ValidateFile(fileName, sizeBytes, ImageMimeTypes, MaxUploadedFileSizeBytes, "Для фото підходять");
        }

        public static string? ValidateDocumentFile(string? fileName, long sizeBytes)
        {
            return ValidateFile(fileName, sizeBytes, DocumentMimeTypes, MaxUploadedFileSizeBytes, "Для документа підходять");
        }

        public static string? ValidateCoverImage(string? fileName, long sizeBytes)
        {
            return ValidateFile(fileName, sizeBytes, ImageMimeTypes, MaxCoverImageSizeBytes, "Для картинки підходять");
        }

        public static IReadOnlyCollection<string> UploadFileExtensions { get; } =
            [.. ImageMimeTypes.Keys, .. DocumentMimeTypes.Keys];

        public static IReadOnlyCollection<string> UploadMimeTypes { get; } =
            ImageMimeTypes.Values.Concat(DocumentMimeTypes.Values).Select(x => x.Split(';')[0]).Distinct().ToList();

        public static IReadOnlyCollection<string> GetFileExtensions(FileSystemItemType itemType)
        {
            return itemType == FileSystemItemType.Photo ? ImageMimeTypes.Keys : DocumentMimeTypes.Keys;
        }

        public static IReadOnlyCollection<string> GetFileMimeTypes(FileSystemItemType itemType)
        {
            Dictionary<string, string> mimeTypes = itemType == FileSystemItemType.Photo ? ImageMimeTypes : DocumentMimeTypes;

            return mimeTypes.Values.Select(x => x.Split(';')[0]).Distinct().ToList();
        }

        public static FileSystemItemType? GetUploadedFileType(string fileName)
        {
            string extension = Path.GetExtension(fileName);

            if (ImageMimeTypes.ContainsKey(extension))
            {
                return FileSystemItemType.Photo;
            }

            if (DocumentMimeTypes.ContainsKey(extension))
            {
                return FileSystemItemType.Document;
            }

            return null;
        }

        public static string? GetMimeType(string fileName)
        {
            string extension = Path.GetExtension(fileName);

            if (ImageMimeTypes.TryGetValue(extension, out string? imageMimeType))
            {
                return imageMimeType;
            }

            if (DocumentMimeTypes.TryGetValue(extension, out string? documentMimeType))
            {
                return documentMimeType;
            }

            return null;
        }

        public static string GetLinkDefaultName(string url)
        {
            return GetWebAddress(url.Trim())?.Host ?? url.Trim();
        }

        public static string GetNoteDefaultName(string text)
        {
            string firstLine = text
                .Split('\n')
                .Select(x => x.Trim())
                .FirstOrDefault(x => x.Length > 0) ?? string.Empty;

            return firstLine.Length > MaxNameLength ? firstLine[..MaxNameLength].TrimEnd() : firstLine;
        }

        public static string GetFileDefaultName(string fileName)
        {
            return Path.GetFileName(fileName).Trim();
        }

        private static Uri? GetWebAddress(string url)
        {
            bool isWebAddress = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

            return isWebAddress ? uri : null;
        }

        private static string? ValidateFile(
            string? fileName,
            long sizeBytes,
            Dictionary<string, string> allowedTypes,
            long maxSizeBytes,
            string allowedFormatsMessage)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !allowedTypes.ContainsKey(Path.GetExtension(fileName)))
            {
                return $"{allowedFormatsMessage}: {string.Join(", ", allowedTypes.Keys)}.";
            }

            if (sizeBytes <= 0)
            {
                return "Файл порожній.";
            }

            if (sizeBytes > maxSizeBytes)
            {
                return $"Файл більший за {maxSizeBytes / 1024 / 1024} МБ.";
            }

            return null;
        }
    }
}

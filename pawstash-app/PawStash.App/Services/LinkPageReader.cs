using System.Text;
using System.Text.RegularExpressions;
using PawStash.Common.Models;
using PawStash.Common.Parsers;
using PawStash.Common.Rules;

namespace PawStash.Services;

public record LinkPagePreview(string? Title, string? Description, PickedFile? Picture);

public partial class LinkPageReader(HttpClient http)
{
	const int MaxHtmlBytes = 1024 * 1024;

	const int MaxPictureAttempts = 5;

	const int ReadChunkBytes = 16 * 1024;

	static readonly byte[][] HeadEndMarkers = ["</head"u8.ToArray(), "</HEAD"u8.ToArray()];

	static readonly Dictionary<string, string> PictureExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		["image/jpeg"] = ".jpg",
		["image/png"] = ".png",
		["image/webp"] = ".webp",
		["image/gif"] = ".gif"
	};

	static LinkPageReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

	public Task<LinkPagePreview?> ReadAsync(string url, CancellationToken cancellationToken) =>
		Task.Run(() => ReadPageAsync(url, cancellationToken), cancellationToken);

	async Task<LinkPagePreview?> ReadPageAsync(string url, CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

		if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "text/html")
		{
			return null;
		}

		byte[] html = await ReadHtmlAsync(response.Content, cancellationToken);
		Uri pageUri = response.RequestMessage?.RequestUri ?? new Uri(url);
		LinkPageInfo page = LinkPageParser.Parse(Decode(html, response.Content.Headers.ContentType.CharSet), pageUri);
		PickedFile? picture = await DownloadPictureAsync(page.PictureUris, cancellationToken);

		return new LinkPagePreview(page.Title, page.Description, picture);
	}

	async Task<PickedFile?> DownloadPictureAsync(IReadOnlyList<Uri> pictureUris, CancellationToken cancellationToken)
	{
		foreach (Uri pictureUri in pictureUris.Take(MaxPictureAttempts))
		{
			PickedFile? picture = await TryDownloadPictureAsync(pictureUri, cancellationToken);

			if (picture is not null)
			{
				return picture;
			}
		}

		return null;
	}

	async Task<PickedFile?> TryDownloadPictureAsync(Uri pictureUri, CancellationToken cancellationToken)
	{
		try
		{
			using HttpResponseMessage response = await http.GetAsync(pictureUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			string? mediaType = response.Content.Headers.ContentType?.MediaType;

			if (!response.IsSuccessStatusCode || mediaType is null || !PictureExtensions.TryGetValue(mediaType, out string? extension))
			{
				return null;
			}

			byte[]? content = await ReadPictureAsync(response.Content, cancellationToken);
			PickedFile? picture = content is null ? null : new PickedFile($"cover{extension}", content);

			return picture is not null && FileSystemItemRules.ValidateCoverImage(picture.FileName, picture.Content.Length) is null
				? picture
				: null;
		}
		catch (Exception exception) when (exception is HttpRequestException
			|| (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
		{
			return null;
		}
	}

	static async Task<byte[]> ReadHtmlAsync(HttpContent content, CancellationToken cancellationToken)
	{
		await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
		using MemoryStream buffer = new();
		byte[] chunk = new byte[ReadChunkBytes];
		int read;

		while (buffer.Length < MaxHtmlBytes && (read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
		{
			int searchFrom = (int)Math.Max(0, buffer.Length - HeadEndMarkers[0].Length);

			buffer.Write(chunk, 0, (int)Math.Min(read, MaxHtmlBytes - buffer.Length));

			if (ContainsHeadEnd(buffer.GetBuffer().AsSpan(searchFrom, (int)buffer.Length - searchFrom)))
			{
				break;
			}
		}

		return buffer.ToArray();
	}

	static async Task<byte[]?> ReadPictureAsync(HttpContent content, CancellationToken cancellationToken)
	{
		if (content.Headers.ContentLength > FileSystemItemRules.MaxCoverImageSizeBytes)
		{
			return null;
		}

		await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
		using MemoryStream buffer = new();
		byte[] chunk = new byte[ReadChunkBytes];
		int read;

		while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
		{
			if (buffer.Length + read > FileSystemItemRules.MaxCoverImageSizeBytes)
			{
				return null;
			}

			buffer.Write(chunk, 0, read);
		}

		return buffer.ToArray();
	}

	static bool ContainsHeadEnd(ReadOnlySpan<byte> html)
	{
		foreach (byte[] marker in HeadEndMarkers)
		{
			if (html.IndexOf(marker) >= 0)
			{
				return true;
			}
		}

		return false;
	}

	static string Decode(byte[] html, string? charset)
	{
		Encoding encoding = GetEncoding(charset) ?? GetEncoding(FindMetaCharset(html)) ?? Encoding.UTF8;

		return encoding.GetString(html);
	}

	static Encoding? GetEncoding(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}

		try
		{
			return Encoding.GetEncoding(name.Trim('"', '\'', ' '));
		}
		catch (ArgumentException)
		{
			return null;
		}
	}

	static string? FindMetaCharset(byte[] html)
	{
		Match charset = MetaCharsetRegex().Match(Encoding.Latin1.GetString(html, 0, Math.Min(html.Length, 4096)));

		return charset.Success ? charset.Groups[1].Value : null;
	}

	[GeneratedRegex(@"<meta[^>]+charset\s*=\s*[""']?([\w-]+)", RegexOptions.IgnoreCase)]
	private static partial Regex MetaCharsetRegex();
}

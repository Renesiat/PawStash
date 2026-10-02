namespace PawStash.Services;

public static partial class FileOpener
{
	public const string PdfMimeType = "application/pdf";

	const string PdfExtension = ".pdf";

	const int MaxFileNameLength = 100;

	public static async Task<bool> OpenPdfAsync(Guid itemId, string name, byte[] content)
	{
		string directory = Path.Combine(FileSystem.CacheDirectory, "opened-files", itemId.ToString());
		Directory.CreateDirectory(directory);

		string filePath = Path.Combine(directory, GetFileName(name));
		await File.WriteAllBytesAsync(filePath, content);

		return await OpenFileAsync(filePath, PdfMimeType);
	}

	static string GetFileName(string name)
	{
		char[] invalidChars = Path.GetInvalidFileNameChars();
		string safeName = new(name.Take(MaxFileNameLength).Select(x => invalidChars.Contains(x) ? '_' : x).ToArray());

		return safeName.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase) ? safeName : safeName + PdfExtension;
	}

	private static partial Task<bool> OpenFileAsync(string filePath, string mimeType);
}

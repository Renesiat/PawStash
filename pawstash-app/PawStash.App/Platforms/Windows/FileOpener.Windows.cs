namespace PawStash.Services;

public static partial class FileOpener
{
	private static partial Task<bool> OpenFileAsync(string filePath, string mimeType) =>
		Launcher.Default.OpenAsync(new OpenFileRequest(Path.GetFileName(filePath), new ReadOnlyFile(filePath, mimeType)));
}

namespace PawStash.Services;

public record PickedFile(string FileName, byte[] Content)
{
	public static async Task<PickedFile> ReadAsync(FileResult file)
	{
		await using Stream stream = await file.OpenReadAsync();
		using MemoryStream buffer = new();
		await stream.CopyToAsync(buffer);

		return new PickedFile(file.FileName, buffer.ToArray());
	}
}

public class ItemFormData
{
	public string? Name { get; set; }

	public string? Description { get; set; }

	public string? LinkUrl { get; set; }

	public string? NoteText { get; set; }

	public PickedFile? File { get; set; }

	public PickedFile? CoverImage { get; set; }

	public bool RemoveCoverImage { get; set; }
}

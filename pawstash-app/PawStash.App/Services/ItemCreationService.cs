using PawStash.Common.Enums;
using PawStash.Common.Rules;

namespace PawStash.Services;

public class ItemCreationService
{
	static readonly FilePickerFileType UploadFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
	{
		[DevicePlatform.Android] = FileSystemItemRules.UploadMimeTypes,
		[DevicePlatform.WinUI] = FileSystemItemRules.UploadFileExtensions
	});

	public async Task<PickedFile?> PickFileAsync()
	{
		FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
		{
			PickerTitle = "Виберіть файл",
			FileTypes = UploadFileTypes
		});

		return file is null ? null : await PickedFile.ReadAsync(file);
	}

	public static string? Validate(PickedFile file) => FileSystemItemRules.GetUploadedFileType(file.FileName) switch
	{
		FileSystemItemType.Photo => FileSystemItemRules.ValidatePhotoFile(file.FileName, file.Content.Length),
		FileSystemItemType.Document => FileSystemItemRules.ValidateDocumentFile(file.FileName, file.Content.Length),
		_ => "Цей формат не підтримується."
	};
}

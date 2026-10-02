using PawStash.Common.Enums;
using PawStash.Common.Rules;

namespace PawStash.Services;

public class ItemCreationService(FileSystemApi api)
{
	static readonly FilePickerFileType UploadFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
	{
		[DevicePlatform.Android] = FileSystemItemRules.UploadMimeTypes,
		[DevicePlatform.WinUI] = FileSystemItemRules.UploadFileExtensions
	});

	public async Task<IReadOnlyList<string>> UploadFilesAsync(Guid? parentFolderId)
	{
		IEnumerable<FileResult?>? picked = await FilePicker.Default.PickMultipleAsync(new PickOptions
		{
			PickerTitle = "Виберіть файли",
			FileTypes = UploadFileTypes
		});

		List<string> errors = [];

		foreach (FileResult? file in picked ?? [])
		{
			if (file is null)
			{
				continue;
			}

			string? error = await UploadFileAsync(file, parentFolderId);

			if (error is not null)
			{
				errors.Add($"{file.FileName}: {error}");
			}
		}

		return errors;
	}

	async Task<string?> UploadFileAsync(FileResult file, Guid? parentFolderId)
	{
		FileSystemItemType? itemType = FileSystemItemRules.GetUploadedFileType(file.FileName);

		if (itemType is null)
		{
			return "цей формат не підтримується.";
		}

		PickedFile pickedFile = await PickedFile.ReadAsync(file);

		string? error = itemType == FileSystemItemType.Photo
			? FileSystemItemRules.ValidatePhotoFile(pickedFile.FileName, pickedFile.Content.Length)
			: FileSystemItemRules.ValidateDocumentFile(pickedFile.FileName, pickedFile.Content.Length);

		if (error is not null)
		{
			return error;
		}

		try
		{
			await api.CreateItemAsync(itemType.Value, parentFolderId, new ItemFormData { File = pickedFile });

			return null;
		}
		catch (ApiException ex)
		{
			return ex.Message;
		}
	}
}

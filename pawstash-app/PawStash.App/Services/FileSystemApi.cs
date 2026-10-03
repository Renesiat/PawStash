using PawStash.Common.Enums;
using PawStash.Common.Models.DTO.FileSystem;

namespace PawStash.Services;

public class FileSystemApi(ApiClient api)
{
	const string ItemsUrl = "api/file-system-items";

	public Task<List<FileSystemItemDto>> GetFolderContentsAsync(Guid? parentFolderId) =>
		api.GetJsonAsync<List<FileSystemItemDto>>(parentFolderId is Guid id ? $"{ItemsUrl}?parentFolderId={id}" : ItemsUrl);

	public Task<FileSystemItemDetailsDto> GetItemAsync(Guid itemId) =>
		api.GetJsonAsync<FileSystemItemDetailsDto>($"{ItemsUrl}/{itemId}");

	public Task<List<FolderPathItemDto>> GetPathAsync(Guid itemId) =>
		api.GetJsonAsync<List<FolderPathItemDto>>($"{ItemsUrl}/{itemId}/path");

	public Task<FileSystemItemDetailsDto> CreateItemAsync(FileSystemItemType itemType, Guid? parentFolderId, ItemFormData data)
	{
		MultipartFormDataContent form = BuildForm(data);

		form.Add(new StringContent(itemType.ToString().ToLowerInvariant()), "itemType");

		if (parentFolderId is Guid id)
		{
			form.Add(new StringContent(id.ToString()), "parentFolderId");
		}

		return api.SendFormAsync<FileSystemItemDetailsDto>(HttpMethod.Post, ItemsUrl, form);
	}

	public Task<FileSystemItemDetailsDto> UpdateItemAsync(Guid itemId, ItemFormData data) =>
		api.SendFormAsync<FileSystemItemDetailsDto>(HttpMethod.Put, $"{ItemsUrl}/{itemId}", BuildForm(data));

	public Task DeleteAsync(Guid itemId) => api.DeleteAsync($"{ItemsUrl}/{itemId}");

	public Task<byte[]> GetFileAsync(Guid itemId) => api.GetBytesAsync($"{ItemsUrl}/{itemId}/file");

	public Task<byte[]> GetCoverImageAsync(Guid itemId) => api.GetBytesAsync($"{ItemsUrl}/{itemId}/cover-image");

	static MultipartFormDataContent BuildForm(ItemFormData data)
	{
		MultipartFormDataContent form = new();

		AddText(form, "name", data.Name);
		AddText(form, "description", data.Description);
		AddText(form, "linkUrl", data.LinkUrl);
		AddText(form, "noteText", data.NoteText);

		if (data.File is PickedFile file)
		{
			form.Add(new ByteArrayContent(file.Content), "file", file.FileName);
		}

		if (data.CoverImage is PickedFile coverImage)
		{
			form.Add(new ByteArrayContent(coverImage.Content), "coverImage", coverImage.FileName);
		}

		if (data.RemoveCoverImage)
		{
			form.Add(new StringContent("true"), "removeCoverImage");
		}

		return form;
	}

	static void AddText(MultipartFormDataContent form, string name, string? value)
	{
		if (value is not null)
		{
			form.Add(new StringContent(value), name);
		}
	}
}

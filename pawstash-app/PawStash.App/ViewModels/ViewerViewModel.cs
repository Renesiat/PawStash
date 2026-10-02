using CommunityToolkit.Mvvm.ComponentModel;
using PawStash.Common.Enums;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Services;

namespace PawStash.ViewModels;

public partial class ViewerViewModel(FileSystemApi api) : ObservableObject, IQueryAttributable
{
	Guid _itemId;

	bool _isLoaded;

	[ObservableProperty]
	public partial string? Name { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasPhoto))]
	public partial ImageSource? Photo { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasText))]
	public partial string? Text { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasError))]
	public partial string? Error { get; set; }

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	public bool HasPhoto => Photo is not null;

	public bool HasText => Text is not null;

	public bool HasError => Error is not null;

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("itemId", out object? itemId) && itemId is Guid id)
		{
			_itemId = id;
		}
	}

	public async Task LoadAsync()
	{
		if (_isLoaded)
		{
			return;
		}

		_isLoaded = true;
		IsBusy = true;

		try
		{
			FileSystemItemDetailsDto item = await api.GetItemAsync(_itemId);

			Name = item.Name;

			if (item.ItemType == FileSystemItemType.Note)
			{
				Text = item.NoteText;
			}
			else
			{
				byte[] content = await api.GetFileAsync(_itemId);

				if (item.ItemType == FileSystemItemType.Photo)
				{
					Photo = ImageSource.FromStream(() => new MemoryStream(content));
				}
				else
				{
					using StreamReader reader = new(new MemoryStream(content));
					Text = await reader.ReadToEndAsync();
				}
			}
		}
		catch (ApiException ex)
		{
			Error = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PawStash.Common.Enums;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Services;

namespace PawStash.ViewModels;

public partial class FolderViewModel(FileSystemApi api) : ObservableObject, IQueryAttributable
{
	const string RootName = "Головна";

	bool _isOpening;

	public ObservableCollection<FolderItemViewModel> Items { get; } = [];

	public ObservableCollection<FolderPathSegment> Path { get; } = [];

	[ObservableProperty]
	public partial Guid? FolderId { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasError))]
	public partial string? Error { get; set; }

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	public bool HasError => Error is not null;

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("itemId", out object? itemId) && itemId is Guid folderId)
		{
			FolderId = folderId;
		}
	}

	public async Task LoadAsync()
	{
		IsBusy = true;
		Error = null;

		try
		{
			List<FileSystemItemDto> items = await api.GetFolderContentsAsync(FolderId);
			List<FolderPathItemDto> path = FolderId is Guid folderId ? await api.GetPathAsync(folderId) : [];

			Items.Clear();

			foreach (FileSystemItemDto item in items)
			{
				Items.Add(new FolderItemViewModel(item));
			}

			Path.Clear();
			Path.Add(new FolderPathSegment(null, RootName, 0, path.Count == 0));

			for (int index = 0; index < path.Count; index++)
			{
				Path.Add(new FolderPathSegment(path[index].ItemId, path[index].Name, index + 1, index == path.Count - 1));
			}

			_ = LoadCoverImagesAsync(Items.Where(x => x.HasCover).ToList());
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

	public async Task OpenAsync(FolderItemViewModel item)
	{
		if (_isOpening)
		{
			return;
		}

		_isOpening = true;

		try
		{
			switch (item.ItemType)
			{
				case FileSystemItemType.Folder:
					await Shell.Current.GoToAsync("folder", new ShellNavigationQueryParameters { ["itemId"] = item.ItemId });
					break;
				case FileSystemItemType.Link:
					await OpenLinkAsync(item);
					break;
				case FileSystemItemType.Document:
					await OpenDocumentAsync(item);
					break;
				default:
					await OpenInViewerAsync(item);
					break;
			}
		}
		finally
		{
			_isOpening = false;
		}
	}

	public async Task ShowMenuAsync(FolderItemViewModel item)
	{
		const string edit = "Редагувати";
		const string move = "Перемістити";
		const string delete = "Видалити";

		string? action = await Shell.Current.DisplayActionSheetAsync(item.Name, "Скасувати", null, edit, move, delete);

		switch (action)
		{
			case edit:
				await Shell.Current.GoToAsync("item", new ShellNavigationQueryParameters { ["itemId"] = item.ItemId });
				break;
			case move:
				await ShowNotYetAsync("Переміщення з'явиться на етапі 4.");
				break;
			case delete:
				await DeleteAsync(item);
				break;
		}
	}

	public async Task GoToAsync(FolderPathSegment segment)
	{
		if (segment.IsCurrent)
		{
			return;
		}

		if (segment.ItemId is null)
		{
			await Shell.Current.GoToAsync("//home");
			return;
		}

		int levelsUp = Path.Count - 1 - segment.Index;

		await Shell.Current.GoToAsync(string.Join("/", Enumerable.Repeat("..", levelsUp)));
	}

	static async Task OpenLinkAsync(FolderItemViewModel item)
	{
		bool isOpened;

		try
		{
			isOpened = await Launcher.Default.OpenAsync(new Uri(item.LinkUrl!));
		}
		catch (Exception)
		{
			isOpened = false;
		}

		if (!isOpened)
		{
			await Shell.Current.DisplayAlertAsync("Не вдалося відкрити", $"Не вдалося відкрити посилання {item.LinkUrl}", "OK");
		}
	}

	async Task OpenDocumentAsync(FolderItemViewModel item)
	{
		Error = null;

		try
		{
			FileSystemItemDetailsDto details = await api.GetItemAsync(item.ItemId);

			if (details.FileMimeType != FileOpener.PdfMimeType)
			{
				await OpenInViewerAsync(item);
				return;
			}

			byte[] content = await api.GetFileAsync(item.ItemId);

			if (!await FileOpener.OpenPdfAsync(item.ItemId, item.Name, content))
			{
				await Shell.Current.DisplayAlertAsync("Немає переглядача PDF", "На цьому пристрої немає програми, яка відкриває PDF.", "OK");
			}
		}
		catch (ApiException ex)
		{
			Error = ex.Message;
		}
	}

	static Task OpenInViewerAsync(FolderItemViewModel item) =>
		Shell.Current.GoToAsync("viewer", new ShellNavigationQueryParameters { ["itemId"] = item.ItemId });

	async Task DeleteAsync(FolderItemViewModel item)
	{
		string message = item.ItemType == FileSystemItemType.Folder
			? $"Папку «{item.Name}» буде видалено разом з усім, що в ній. Це не можна скасувати."
			: $"«{item.Name}» буде видалено. Це не можна скасувати.";

		bool confirmed = await Shell.Current.DisplayAlertAsync("Видалити?", message, "Видалити", "Скасувати");

		if (!confirmed)
		{
			return;
		}

		try
		{
			await api.DeleteAsync(item.ItemId);
			await LoadAsync();
		}
		catch (ApiException ex)
		{
			Error = ex.Message;
		}
	}

	async Task LoadCoverImagesAsync(List<FolderItemViewModel> items)
	{
		foreach (FolderItemViewModel item in items)
		{
			try
			{
				byte[] image = await api.GetCoverImageAsync(item.ItemId);
				item.CoverImage = ImageSource.FromStream(() => new MemoryStream(image));
			}
			catch (ApiException)
			{
			}
		}
	}

	static Task ShowNotYetAsync(string message) => Shell.Current.DisplayAlertAsync("Ще не готово", message, "OK");
}

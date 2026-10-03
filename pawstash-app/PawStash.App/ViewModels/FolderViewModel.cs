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

	FolderItemViewModel? _draggedItem;

	bool _isDropped;

	bool _hasLeftDraggedItem;

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

	public void StartDrag(FolderItemViewModel item)
	{
		_draggedItem = item;
		_isDropped = false;
		_hasLeftDraggedItem = false;
	}

	public DropPlace ShowDropPlace(FolderItemViewModel target, double y, double height)
	{
		if (_draggedItem is null || target == _draggedItem)
		{
			return DropPlace.None;
		}

		_hasLeftDraggedItem = true;

		DropPlace place = target.IsFolder
			? y < height / 4 ? DropPlace.Before : y > height * 3 / 4 ? DropPlace.After : DropPlace.Into
			: y < height / 2 ? DropPlace.Before : DropPlace.After;

		foreach (FolderItemViewModel item in Items)
		{
			item.DropPlace = item == target ? place : DropPlace.None;
		}

		return place;
	}

	public void HideDropPlace(FolderItemViewModel target)
	{
		target.DropPlace = DropPlace.None;
	}

	public bool CanDropOnPath(FolderPathSegment segment)
	{
		if (_draggedItem is null || segment.IsCurrent)
		{
			return false;
		}

		_hasLeftDraggedItem = true;

		return true;
	}

	public async Task DropOnItemAsync(FolderItemViewModel target)
	{
		FolderItemViewModel? item = _draggedItem;
		DropPlace place = target.DropPlace;

		HideDropPlaces();

		if (item is null || target == item || place == DropPlace.None)
		{
			return;
		}

		_isDropped = true;

		if (place == DropPlace.Into)
		{
			await MoveAsync(item, target.ItemId);
			return;
		}

		List<FolderItemViewModel> others = Items.Where(x => x != item).ToList();
		int targetIndex = others.IndexOf(target);
		FolderItemViewModel? before = place == DropPlace.Before
			? target
			: others.ElementAtOrDefault(targetIndex + 1);

		await ReorderAsync(item, before);
	}

	public async Task DropOnPathAsync(FolderPathSegment segment)
	{
		FolderItemViewModel? item = _draggedItem;

		if (item is null || segment.IsCurrent)
		{
			return;
		}

		_isDropped = true;
		await MoveAsync(item, segment.ItemId);
	}

	public async Task EndDragAsync()
	{
		FolderItemViewModel? item = _draggedItem;
		bool showMenu = item is not null && !_isDropped && !_hasLeftDraggedItem;

		_draggedItem = null;
		HideDropPlaces();

		if (showMenu && DeviceInfo.Platform == DevicePlatform.Android)
		{
			await ShowMenuAsync(item!);
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

	async Task MoveAsync(FolderItemViewModel item, Guid? targetFolderId)
	{
		Error = null;
		Items.Remove(item);

		try
		{
			await api.MoveAsync(item.ItemId, targetFolderId);
		}
		catch (ApiException ex)
		{
			await LoadAsync();
			Error = ex.Message;
		}
	}

	async Task ReorderAsync(FolderItemViewModel item, FolderItemViewModel? before)
	{
		Error = null;

		int oldIndex = Items.IndexOf(item);
		int newIndex = before is null ? Items.Count - 1 : Items.Where(x => x != item).ToList().IndexOf(before);

		if (oldIndex == newIndex)
		{
			return;
		}

		Items.Move(oldIndex, newIndex);

		try
		{
			await api.ChangePositionAsync(item.ItemId, before?.ItemId);
		}
		catch (ApiException ex)
		{
			await LoadAsync();
			Error = ex.Message;
		}
	}

	void HideDropPlaces()
	{
		foreach (FolderItemViewModel item in Items)
		{
			item.DropPlace = DropPlace.None;
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

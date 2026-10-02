using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PawStash.Common.Enums;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Common.Rules;
using PawStash.Services;

namespace PawStash.ViewModels;

public partial class ItemViewModel(FileSystemApi api) : ObservableObject, IQueryAttributable
{
	Guid? _itemId;

	Guid? _parentFolderId;

	string? _currentName;

	PickedFile? _newFile;

	PickedFile? _newCoverImage;

	bool _removeCoverImage;

	bool _isLoaded;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Title), nameof(IsLink), nameof(IsNote), nameof(IsUploadedFile), nameof(NamePlaceholder))]
	public partial FileSystemItemType ItemType { get; set; }

	[ObservableProperty]
	public partial string? Name { get; set; }

	[ObservableProperty]
	public partial string? Description { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(NamePlaceholder))]
	public partial string? LinkUrl { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(NamePlaceholder))]
	public partial string? NoteText { get; set; }

	[ObservableProperty]
	public partial string? FileInfo { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasCoverImage))]
	public partial ImageSource? CoverImage { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasError))]
	public partial string? Error { get; set; }

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial bool IsReady { get; set; }

	public bool IsLink => ItemType == FileSystemItemType.Link;

	public bool IsNote => ItemType == FileSystemItemType.Note;

	public bool IsUploadedFile => ItemType is FileSystemItemType.Photo or FileSystemItemType.Document;

	public bool HasCoverImage => CoverImage is not null;

	public bool HasError => Error is not null;

	public string Title => _itemId is not null ? "Редагування" : ItemType switch
	{
		FileSystemItemType.Link => "Нове посилання",
		FileSystemItemType.Note => "Нова нотатка",
		_ => "Нова папка"
	};

	public string NamePlaceholder => ItemType switch
	{
		FileSystemItemType.Folder => FileSystemItemRules.DefaultFolderName,
		FileSystemItemType.Link => string.IsNullOrWhiteSpace(LinkUrl) ? "Адреса сайту" : FileSystemItemRules.GetLinkDefaultName(LinkUrl),
		FileSystemItemType.Note => string.IsNullOrWhiteSpace(NoteText) ? "Перший рядок нотатки" : FileSystemItemRules.GetNoteDefaultName(NoteText),
		_ => _newFile is not null ? FileSystemItemRules.GetFileDefaultName(_newFile.FileName) : _currentName ?? string.Empty
	};

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("itemId", out object? itemId) && itemId is Guid id)
		{
			_itemId = id;
			OnPropertyChanged(nameof(Title));
			return;
		}

		if (query.TryGetValue("itemType", out object? itemType) && itemType is FileSystemItemType type)
		{
			ItemType = type;
		}

		if (query.TryGetValue("parentFolderId", out object? parentFolderId) && parentFolderId is Guid folderId)
		{
			_parentFolderId = folderId;
		}

		IsReady = true;
	}

	public async Task LoadAsync()
	{
		if (_itemId is not Guid itemId || _isLoaded)
		{
			return;
		}

		_isLoaded = true;
		IsBusy = true;

		try
		{
			FileSystemItemDetailsDto item = await api.GetItemAsync(itemId);

			ItemType = item.ItemType;
			Name = item.Name;
			Description = item.Description;
			LinkUrl = item.LinkUrl;
			NoteText = item.NoteText;
			_currentName = item.Name;
			FileInfo = item.FileSizeBytes is long sizeBytes ? $"Поточний файл: {FolderItemViewModel.FormatSize(sizeBytes)}" : null;

			if (item.HasCoverImage)
			{
				byte[] coverImage = await api.GetCoverImageAsync(itemId);
				CoverImage = ImageSource.FromStream(() => new MemoryStream(coverImage));
			}

			IsReady = true;
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

	[RelayCommand]
	async Task PickFileAsync()
	{
		FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
		{
			PickerTitle = "Виберіть файл",
			FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
			{
				[DevicePlatform.Android] = FileSystemItemRules.GetFileMimeTypes(ItemType),
				[DevicePlatform.WinUI] = FileSystemItemRules.GetFileExtensions(ItemType)
			})
		});

		if (file is null)
		{
			return;
		}

		PickedFile pickedFile = await PickedFile.ReadAsync(file);

		Error = ItemType == FileSystemItemType.Photo
			? FileSystemItemRules.ValidatePhotoFile(pickedFile.FileName, pickedFile.Content.Length)
			: FileSystemItemRules.ValidateDocumentFile(pickedFile.FileName, pickedFile.Content.Length);

		if (Error is not null)
		{
			return;
		}

		_newFile = pickedFile;
		FileInfo = $"Новий файл: {pickedFile.FileName}";
		OnPropertyChanged(nameof(NamePlaceholder));
	}

	[RelayCommand]
	async Task PickCoverImageAsync()
	{
		List<FileResult> photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = "Виберіть картинку" });
		FileResult? photo = photos.FirstOrDefault();

		if (photo is null)
		{
			return;
		}

		PickedFile pickedFile = await PickedFile.ReadAsync(photo);

		Error = FileSystemItemRules.ValidateCoverImage(pickedFile.FileName, pickedFile.Content.Length);

		if (Error is not null)
		{
			return;
		}

		_newCoverImage = pickedFile;
		_removeCoverImage = false;
		CoverImage = ImageSource.FromStream(() => new MemoryStream(pickedFile.Content));
	}

	[RelayCommand]
	void RemoveCoverImage()
	{
		_newCoverImage = null;
		_removeCoverImage = _itemId is not null;
		CoverImage = null;
	}

	[RelayCommand]
	async Task SaveAsync()
	{
		Error = Validate();

		if (Error is not null)
		{
			return;
		}

		ItemFormData data = new()
		{
			Name = Name,
			Description = Description,
			LinkUrl = IsLink ? LinkUrl : null,
			NoteText = IsNote ? NoteText : null,
			File = _newFile,
			CoverImage = _newCoverImage,
			RemoveCoverImage = _removeCoverImage
		};

		IsBusy = true;

		try
		{
			if (_itemId is Guid itemId)
			{
				await api.UpdateItemAsync(itemId, data);
			}
			else
			{
				await api.CreateItemAsync(ItemType, _parentFolderId, data);
			}

			await Shell.Current.GoToAsync("..");
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

	string? Validate()
	{
		string?[] errors =
		[
			IsLink ? FileSystemItemRules.ValidateLinkUrl(LinkUrl) : null,
			IsNote ? FileSystemItemRules.ValidateNoteText(NoteText) : null,
			FileSystemItemRules.ValidateName(Name),
			FileSystemItemRules.ValidateDescription(Description)
		];

		string message = string.Join("\n", errors.OfType<string>());

		return message.Length > 0 ? message : null;
	}
}

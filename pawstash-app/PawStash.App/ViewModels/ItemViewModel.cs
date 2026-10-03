using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PawStash.Common.Enums;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Common.Rules;
using PawStash.Services;

namespace PawStash.ViewModels;

public partial class ItemViewModel(FileSystemApi api, LinkPageReader linkPageReader) : ObservableObject, IQueryAttributable
{
	const string PdfCoverNote = "Якщо в PDF є картинка, вона стане обкладинкою після збереження";

	static readonly TimeSpan LinkPageDelay = TimeSpan.FromSeconds(1);

	Guid? _itemId;

	Guid? _parentFolderId;

	string? _currentName;

	string? _autoName;

	string? _autoDescription;

	PickedFile? _newFile;

	PickedFile? _newCoverImage;

	bool _removeCoverImage;

	bool _isCoverChosen;

	bool _isCoverAutoFilled;

	bool _isLoaded;

	CancellationTokenSource? _linkPageReading;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Title), nameof(IsLink), nameof(IsNote), nameof(NamePlaceholder))]
	[NotifyPropertyChangedFor(nameof(ShowLinkContent), nameof(ShowNoteContent), nameof(ShowFileContent))]
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
	[NotifyPropertyChangedFor(nameof(HasLinkPageStatus))]
	public partial string? LinkPageStatus { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasCoverImage), nameof(CanRemoveCoverImage))]
	public partial ImageSource? CoverImage { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasCoverNote), nameof(CanRemoveCoverImage))]
	public partial string? CoverNote { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasError))]
	public partial string? Error { get; set; }

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial bool IsReady { get; set; }

	public bool IsEditing => _itemId is not null;

	public bool IsLink => ItemType == FileSystemItemType.Link;

	public bool IsNote => ItemType == FileSystemItemType.Note;

	public bool ShowLinkContent => IsLink && !IsEditing;

	public bool ShowNoteContent => IsNote && !IsEditing;

	public bool ShowFileContent => (ItemType is FileSystemItemType.Photo or FileSystemItemType.Document) && !IsEditing;

	public bool HasLinkPageStatus => LinkPageStatus is not null;

	public bool HasCoverImage => CoverImage is not null;

	public bool HasCoverNote => CoverNote is not null;

	public bool CanRemoveCoverImage => HasCoverImage || HasCoverNote;

	public bool HasError => Error is not null;

	public string Title => IsEditing ? "Редагування" : ItemType switch
	{
		FileSystemItemType.Link => "Нове посилання",
		FileSystemItemType.Note => "Нова нотатка",
		FileSystemItemType.Photo => "Нове фото",
		FileSystemItemType.Document => "Новий документ",
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

		if (query.TryGetValue("file", out object? file) && file is PickedFile pickedFile)
		{
			UseNewFile(pickedFile);
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

	partial void OnLinkUrlChanged(string? value)
	{
		if (IsReady && ShowLinkContent)
		{
			_ = ReadLinkPageAsync(value);
		}
	}

	async Task ReadLinkPageAsync(string? url)
	{
		_linkPageReading?.Cancel();
		LinkPageStatus = null;

		if (FileSystemItemRules.ValidateLinkUrl(url) is not null)
		{
			return;
		}

		CancellationTokenSource reading = new();
		_linkPageReading = reading;

		try
		{
			await Task.Delay(LinkPageDelay, reading.Token);
			LinkPageStatus = "Отримую дані зі сторінки…";

			LinkPagePreview? preview = await linkPageReader.ReadAsync(url!.Trim(), reading.Token);

			if (preview is null)
			{
				LinkPageStatus = "Не вдалося отримати дані зі сторінки";
				return;
			}

			AutoFillName(preview.Title);
			AutoFillDescription(preview.Description);
			AutoFillCoverImage(preview.Picture);
			LinkPageStatus = null;
		}
		catch (OperationCanceledException) when (reading.IsCancellationRequested)
		{
		}
		catch (Exception)
		{
			LinkPageStatus = "Не вдалося отримати дані зі сторінки";
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

		UseNewFile(pickedFile);
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
		_isCoverChosen = true;
		_isCoverAutoFilled = false;
		CoverImage = ToImageSource(pickedFile);
		CoverNote = null;
	}

	[RelayCommand]
	void RemoveCoverImage()
	{
		_newCoverImage = null;
		_removeCoverImage = true;
		_isCoverChosen = true;
		_isCoverAutoFilled = false;
		CoverImage = null;
		CoverNote = null;
	}

	[RelayCommand]
	async Task SaveAsync()
	{
		_linkPageReading?.Cancel();
		LinkPageStatus = null;
		Error = Validate();

		if (Error is not null)
		{
			return;
		}

		ItemFormData data = new()
		{
			Name = Name,
			Description = Description,
			LinkUrl = ShowLinkContent ? LinkUrl : null,
			NoteText = ShowNoteContent ? NoteText : null,
			File = ShowFileContent ? _newFile : null,
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
			ShowLinkContent ? FileSystemItemRules.ValidateLinkUrl(LinkUrl) : null,
			ShowNoteContent ? FileSystemItemRules.ValidateNoteText(NoteText) : null,
			FileSystemItemRules.ValidateName(Name),
			FileSystemItemRules.ValidateDescription(Description)
		];

		string message = string.Join("\n", errors.OfType<string>());

		return message.Length > 0 ? message : null;
	}

	void UseNewFile(PickedFile file)
	{
		_newFile = file;
		FileInfo = $"Новий файл: {file.FileName}";
		AutoFillName(FileSystemItemRules.GetFileDefaultName(file.FileName));
		OnPropertyChanged(nameof(NamePlaceholder));

		if (_isCoverChosen)
		{
			return;
		}

		if (ItemType == FileSystemItemType.Photo)
		{
			CoverImage = ToImageSource(file);
			CoverNote = null;
		}
		else
		{
			CoverNote = FileSystemItemRules.GetMimeType(file.FileName) == FileOpener.PdfMimeType ? PdfCoverNote : null;
		}
	}

	void AutoFillName(string? value)
	{
		if (!string.IsNullOrWhiteSpace(Name) && Name != _autoName)
		{
			return;
		}

		Name = value;
		_autoName = value;
	}

	void AutoFillDescription(string? value)
	{
		if (!string.IsNullOrWhiteSpace(Description) && Description != _autoDescription)
		{
			return;
		}

		Description = value;
		_autoDescription = value;
	}

	void AutoFillCoverImage(PickedFile? picture)
	{
		if (_isCoverChosen || (CoverImage is not null && !_isCoverAutoFilled))
		{
			return;
		}

		_newCoverImage = picture;
		_isCoverAutoFilled = picture is not null;
		CoverImage = picture is null ? null : ToImageSource(picture);
	}

	static ImageSource ToImageSource(PickedFile file) => ImageSource.FromStream(() => new MemoryStream(file.Content));
}

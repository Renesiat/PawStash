using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PawStash.Common.Enums;
using PawStash.Common.Models.DTO.FileSystem;

namespace PawStash.ViewModels;

public partial class FolderItemViewModel(FileSystemItemDto item) : ObservableObject
{
	static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

	public Guid ItemId => item.ItemId;

	public FileSystemItemType ItemType => item.ItemType;

	public bool IsFolder => item.ItemType == FileSystemItemType.Folder;

	public bool IsDropBefore => DropPlace == DropPlace.Before;

	public bool IsDropInto => DropPlace == DropPlace.Into;

	public bool IsDropAfter => DropPlace == DropPlace.After;

	public string Name => item.Name;

	public string? LinkUrl => item.LinkUrl;

	public bool HasCover => item.HasCoverImage;

	public bool HasNoCover => !item.HasCoverImage;

	public string TypeIcon => $"icon_type_{item.ItemType.ToString().ToLowerInvariant()}.png";

	public string Subtitle => item.ItemType switch
	{
		FileSystemItemType.Folder => "Папка",
		FileSystemItemType.Link => item.LinkUrl ?? string.Empty,
		FileSystemItemType.Note => "Нотатка",
		_ => FormatSize(item.FileSizeBytes ?? 0)
	};

	[ObservableProperty]
	public partial ImageSource? CoverImage { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsDropBefore), nameof(IsDropInto), nameof(IsDropAfter))]
	public partial DropPlace DropPlace { get; set; }

	public static string FormatSize(long bytes)
	{
		if (bytes < 1024)
		{
			return $"{bytes} Б";
		}

		if (bytes < 1024 * 1024)
		{
			return string.Format(Ukrainian, "{0:0.#} КБ", bytes / 1024d);
		}

		return string.Format(Ukrainian, "{0:0.#} МБ", bytes / 1024d / 1024d);
	}
}

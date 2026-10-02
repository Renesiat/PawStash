namespace PawStash.ViewModels;

public record FolderPathSegment(Guid? ItemId, string Name, int Index, bool IsCurrent)
{
	public bool ShowSeparator => Index > 0;

	public FontAttributes Weight => IsCurrent ? FontAttributes.Bold : FontAttributes.None;
}

using PawStash.ViewModels;

namespace PawStash.Views;

public partial class FolderPage : ContentPage
{
	static readonly Color LightDropHighlight = Color.FromArgb("#EDE7FF");

	static readonly Color DarkDropHighlight = Color.FromArgb("#3A2F66");

	readonly FolderViewModel _viewModel;

	public FolderPage(FolderViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.LoadAsync();
	}

	async void OnItemTapped(object? sender, TappedEventArgs e)
	{
		if (ContextOf(sender) is FolderItemViewModel item)
		{
			await _viewModel.OpenAsync(item);
		}
	}

	async void OnItemSecondaryTapped(object? sender, TappedEventArgs e)
	{
		if (ContextOf(sender) is FolderItemViewModel item)
		{
			await _viewModel.ShowMenuAsync(item);
		}
	}

	void OnItemDragStarting(object? sender, DragStartingEventArgs e)
	{
		if (ContextOf(sender) is FolderItemViewModel item)
		{
			_viewModel.StartDrag(item);
		}
		else
		{
			e.Cancel = true;
		}
	}

	async void OnItemDropCompleted(object? sender, DropCompletedEventArgs e) => await _viewModel.EndDragAsync();

	void OnItemDragOver(object? sender, DragEventArgs e)
	{
		if (ContextOf(sender) is not FolderItemViewModel target || ViewOf(sender) is not View row)
		{
			return;
		}

		Point? position = e.GetPosition(row);
		DropPlace place = _viewModel.ShowDropPlace(target, position?.Y ?? row.Height / 2, row.Height);

		e.AcceptedOperation = place == DropPlace.None ? DataPackageOperation.None : DataPackageOperation.Copy;
	}

	void OnItemDragLeave(object? sender, DragEventArgs e)
	{
		if (ContextOf(sender) is FolderItemViewModel target)
		{
			_viewModel.HideDropPlace(target);
		}
	}

	async void OnItemDrop(object? sender, DropEventArgs e)
	{
		e.Handled = true;

		if (ContextOf(sender) is FolderItemViewModel target)
		{
			await _viewModel.DropOnItemAsync(target);
		}
	}

	async void OnPathSegmentTapped(object? sender, TappedEventArgs e) =>
		await _viewModel.GoToAsync((FolderPathSegment)((BindableObject)sender!).BindingContext);

	void OnPathSegmentDragOver(object? sender, DragEventArgs e)
	{
		bool canDrop = ContextOf(sender) is FolderPathSegment segment && _viewModel.CanDropOnPath(segment);

		e.AcceptedOperation = canDrop ? DataPackageOperation.Copy : DataPackageOperation.None;
		SetHighlight(sender, canDrop);
	}

	void OnPathSegmentDragLeave(object? sender, DragEventArgs e) => SetHighlight(sender, false);

	async void OnPathSegmentDrop(object? sender, DropEventArgs e)
	{
		e.Handled = true;
		SetHighlight(sender, false);

		if (ContextOf(sender) is FolderPathSegment segment)
		{
			await _viewModel.DropOnPathAsync(segment);
		}
	}

	static void SetHighlight(object? sender, bool isHighlighted)
	{
		if (ViewOf(sender) is not View view)
		{
			return;
		}

		Color highlight = Application.Current?.RequestedTheme == AppTheme.Dark ? DarkDropHighlight : LightDropHighlight;
		view.BackgroundColor = isHighlighted ? highlight : Colors.Transparent;
	}

	static object? ContextOf(object? sender) => (sender as BindableObject)?.BindingContext;

	static View? ViewOf(object? sender) => sender as View ?? (sender as GestureRecognizer)?.Parent as View;
}

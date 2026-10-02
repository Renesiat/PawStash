using PawStash.Behaviors;
using PawStash.ViewModels;

namespace PawStash.Views;

public partial class FolderPage : ContentPage
{
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

	async void OnItemTapped(object? sender, PressedEventArgs e)
	{
		if (e.Item is FolderItemViewModel item)
		{
			await _viewModel.OpenAsync(item);
		}
	}

	async void OnItemLongPressed(object? sender, PressedEventArgs e)
	{
		if (e.Item is FolderItemViewModel item)
		{
			await _viewModel.ShowMenuAsync(item);
		}
	}

	async void OnPathSegmentTapped(object? sender, TappedEventArgs e) =>
		await _viewModel.GoToAsync((FolderPathSegment)((BindableObject)sender!).BindingContext);

	async void OnItemsCreated(object? sender, EventArgs e) => await _viewModel.LoadAsync();
}

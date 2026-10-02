using PawStash.ViewModels;

namespace PawStash.Views;

public partial class ViewerPage : ContentPage
{
	readonly ViewerViewModel _viewModel;

	public ViewerPage(ViewerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.LoadAsync();
	}
}

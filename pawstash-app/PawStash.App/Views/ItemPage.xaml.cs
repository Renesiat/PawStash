using PawStash.ViewModels;

namespace PawStash.Views;

public partial class ItemPage : ContentPage
{
	readonly ItemViewModel _viewModel;

	public ItemPage(ItemViewModel viewModel)
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

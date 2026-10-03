using PawStash.Services;

namespace PawStash.Views;

public partial class ProfilePage : ContentPage
{
	readonly AuthService _auth;

	public ProfilePage(AuthService auth)
	{
		InitializeComponent();
		_auth = auth;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		EmailLabel.Text = $"Ви увійшли як {_auth.CurrentEmail}";
	}

	async void OnLogoutClicked(object? sender, EventArgs e)
	{
		_auth.Logout();
		await Shell.Current.GoToAsync("//login");
	}
}

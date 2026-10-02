using PawStash.Services;

namespace PawStash;

public partial class AppShell : Shell
{
	public AppShell(AuthService auth)
	{
		InitializeComponent();
		// Pick the start page here rather than navigating from the login page's OnAppearing:
		// navigating while the first window is still loading crashes WinUI.
		if (auth.IsSignedIn)
			CurrentItem = HomeItem;
	}
}

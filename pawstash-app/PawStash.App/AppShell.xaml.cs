using PawStash.Services;
using PawStash.Views;

namespace PawStash;

public partial class AppShell : Shell
{
	public AppShell(AuthService auth)
	{
		InitializeComponent();
		Routing.RegisterRoute("folder", typeof(FolderPage));
		Routing.RegisterRoute("item", typeof(ItemPage));
		Routing.RegisterRoute("viewer", typeof(ViewerPage));
		// Pick the start page here rather than navigating from the login page's OnAppearing:
		// navigating while the first window is still loading crashes WinUI.
		if (auth.IsSignedIn)
			CurrentItem = HomeItem;
	}
}

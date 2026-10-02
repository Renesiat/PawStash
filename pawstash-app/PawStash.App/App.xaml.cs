using PawStash.Services;

namespace PawStash;

public partial class App : Application
{
	readonly AuthService _auth;

	public App(AuthService auth)
	{
		InitializeComponent();
		_auth = auth;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell(_auth));
	}
}

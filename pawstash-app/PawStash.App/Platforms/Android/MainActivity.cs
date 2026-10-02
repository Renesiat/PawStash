using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace PawStash;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		UpdateSystemBarIcons();

		if (Microsoft.Maui.Controls.Application.Current is { } app)
		{
			app.RequestedThemeChanged += (_, _) => UpdateSystemBarIcons();
		}
	}

	void UpdateSystemBarIcons()
	{
		if (Window is null)
		{
			return;
		}

		bool isDark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
		WindowInsetsControllerCompat? controller = WindowCompat.GetInsetsController(Window, Window.DecorView);

		if (controller is null)
		{
			return;
		}

		controller.AppearanceLightStatusBars = !isDark;
		controller.AppearanceLightNavigationBars = !isDark;
	}
}

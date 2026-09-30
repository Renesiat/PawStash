using Microsoft.Extensions.Logging;
using PawStash.Services;
using PawStash.ViewModels;
using PawStash.Views;

namespace PawStash;

public static class MauiProgram
{
	// Dev server from src/PawStash.Api. Android emulators reach the host machine at 10.0.2.2.
	static string ApiBaseUrl => DeviceInfo.Platform == DevicePlatform.Android
		? "http://10.0.2.2:5094/"
		: "http://localhost:5094/";

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton(new AuthService(
			new HttpClient { BaseAddress = new Uri(ApiBaseUrl), Timeout = TimeSpan.FromSeconds(15) }));

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<HomePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

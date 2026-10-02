using Microsoft.Extensions.Logging;
using PawStash.Services;
using PawStash.ViewModels;
using PawStash.Views;

namespace PawStash;

public static class MauiProgram
{
	// Dev server from pawstash-api/PawStash.API. Android emulators reach the host machine at 10.0.2.2.
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

		builder.Services.AddSingleton<Session>();
		builder.Services.AddSingleton(provider => new ApiClient(
			new HttpClient { BaseAddress = new Uri(ApiBaseUrl), Timeout = TimeSpan.FromSeconds(60) },
			provider.GetRequiredService<Session>()));
		builder.Services.AddSingleton<AuthService>();
		builder.Services.AddSingleton<FileSystemApi>();
		builder.Services.AddSingleton<ItemCreationService>();

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<FolderViewModel>();
		builder.Services.AddTransient<FolderPage>();
		builder.Services.AddTransient<ItemViewModel>();
		builder.Services.AddTransient<ItemPage>();
		builder.Services.AddTransient<ViewerViewModel>();
		builder.Services.AddTransient<ViewerPage>();
		builder.Services.AddTransient<ProfilePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

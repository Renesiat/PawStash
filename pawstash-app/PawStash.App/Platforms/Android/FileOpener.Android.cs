using Android.Content;

namespace PawStash.Services;

public static partial class FileOpener
{
	private static partial Task<bool> OpenFileAsync(string filePath, string mimeType)
	{
		Context context = Platform.AppContext;
		Android.Net.Uri? uri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, $"{context.PackageName}.fileProvider", new Java.IO.File(filePath));
		Intent intent = new(Intent.ActionView);

		intent.SetDataAndType(uri, mimeType);
		intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);

		try
		{
			context.StartActivity(intent);

			return Task.FromResult(true);
		}
		catch (ActivityNotFoundException)
		{
			return Task.FromResult(false);
		}
	}
}

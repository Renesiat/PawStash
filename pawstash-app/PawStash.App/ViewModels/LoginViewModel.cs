using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PawStash.Services;
using PawStash.Common.Rules;

namespace PawStash.ViewModels;

public partial class LoginViewModel(AuthService auth) : ObservableObject
{
#if DEBUG
	// Debug builds start with the dev test account filled in (DevTestEmail in the API's
	// appsettings.Development.json), so signing in while testing is one click.
	const string InitialEmail = "test@test.com";
#else
	const string InitialEmail = "";
#endif

	[ObservableProperty]
	public partial string Email { get; set; } = InitialEmail;

	/// <summary>Validation or server error shown under the field; null hides it.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasError))]
	public partial string? Error { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LoginCommand))]
	public partial bool IsBusy { get; set; }

	public bool HasError => Error is not null;

	// Clear a stale message as soon as the user edits the email.
	partial void OnEmailChanged(string value) => Error = null;

	[RelayCommand(CanExecute = nameof(CanLogin))]
	async Task Login()
	{
		// Same rules the server applies; catches typos without a round trip.
		Error = EmailRules.Validate(Email);
		if (Error is not null)
			return;

		IsBusy = true;
		try
		{
			await auth.LoginAsync(Email.Trim());
			// The login page is reused after logout; start it fresh.
			Email = InitialEmail;
			await Shell.Current.GoToAsync("//home");
		}
		catch (LoginException ex)
		{
			Error = ex.Message;
		}
		catch (Exception ex)
		{
			// Anything unexpected; keep the app alive and show what happened.
			Error = $"Неочікувана помилка: {ex.Message}";
		}
		finally
		{
			IsBusy = false;
		}
	}

	bool CanLogin() => !IsBusy;
}

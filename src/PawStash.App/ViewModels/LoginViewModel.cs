using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PawStash.Services;
using PawStash.Shared;

namespace PawStash.ViewModels;

public partial class LoginViewModel(AuthService auth) : ObservableObject
{
	[ObservableProperty]
	public partial string Email { get; set; } = "";

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
			Email = "";
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

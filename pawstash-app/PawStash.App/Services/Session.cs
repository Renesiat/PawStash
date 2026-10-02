namespace PawStash.Services;

public class Session
{
	const string EmailKey = "signed_in_email";

	public string? Email => Preferences.Default.Get<string?>(EmailKey, null);

	public bool IsSignedIn => Email is not null;

	public void SignIn(string email) => Preferences.Default.Set(EmailKey, email);

	public void SignOut() => Preferences.Default.Remove(EmailKey);
}

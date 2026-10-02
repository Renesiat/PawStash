using PawStash.Common.Models.DTO.Auth;

namespace PawStash.Services;

public class AuthService(ApiClient api, Session session)
{
	public string? CurrentEmail => session.Email;

	public bool IsSignedIn => session.IsSignedIn;

	public async Task LoginAsync(string email)
	{
		UserDto user = await api.PostJsonAsync<UserDto>("api/auth/login", new LoginPostDto { Email = email });

		session.SignIn(user.Email);
	}

	public void Logout() => session.SignOut();
}

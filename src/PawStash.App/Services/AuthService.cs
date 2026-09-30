using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PawStash.Shared;

namespace PawStash.Services;

/// <summary>A sign-in failure whose message can be shown to the user as is.</summary>
public class LoginException(string message) : Exception(message);

/// <summary>Email-only sign-in. The signed-in email is remembered on the device between launches.</summary>
public class AuthService(HttpClient http)
{
	const string EmailKey = "signed_in_email";

	public string? CurrentEmail => Preferences.Default.Get<string?>(EmailKey, null);

	public bool IsSignedIn => CurrentEmail is not null;

	public async Task LoginAsync(string email)
	{
		HttpResponseMessage resp;
		try
		{
			resp = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(email));
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			throw new LoginException("Не вдалося зв'язатися з сервером. Перевірте, що він запущений, і спробуйте ще раз.");
		}

		using (resp)
		{
			if (!resp.IsSuccessStatusCode)
				throw new LoginException(await ReadErrorAsync(resp));

			var result = await resp.Content.ReadFromJsonAsync<LoginResponse>();
			Preferences.Default.Set(EmailKey, result!.Email);
		}
	}

	public void Logout() => Preferences.Default.Remove(EmailKey);

	/// <summary>Pulls the message out of the API's ProblemDetails (validation "errors" or "detail").</summary>
	static async Task<string> ReadErrorAsync(HttpResponseMessage resp)
	{
		var fallback = resp.StatusCode == HttpStatusCode.Unauthorized
			? "Цієї пошти немає серед дозволених."
			: $"Помилка сервера ({(int)resp.StatusCode}).";
		try
		{
			using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
			var root = doc.RootElement;
			if (root.TryGetProperty("errors", out var errors))
				return string.Join("\n", errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray()).Select(v => v.GetString()));
			if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
				return detail.GetString()!;
		}
		catch (JsonException)
		{
			// Not ProblemDetails; use the fallback.
		}
		return fallback;
	}
}

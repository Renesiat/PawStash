using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PawStash.Services;

public class ApiException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
	public HttpStatusCode? StatusCode { get; } = statusCode;
}

public class ApiClient(HttpClient http, Session session)
{
	const string EmailHeader = "X-User-Email";

	public async Task<T> GetJsonAsync<T>(string url)
	{
		using HttpResponseMessage response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

		return (await response.Content.ReadFromJsonAsync<T>())!;
	}

	public async Task<T> PostJsonAsync<T>(string url, object body)
	{
		using HttpResponseMessage response = await SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) });

		return (await response.Content.ReadFromJsonAsync<T>())!;
	}

	public async Task<T> PutJsonAsync<T>(string url, object body)
	{
		using HttpResponseMessage response = await SendAsync(new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) });

		return (await response.Content.ReadFromJsonAsync<T>())!;
	}

	public async Task<T> SendFormAsync<T>(HttpMethod method, string url, MultipartFormDataContent form)
	{
		using HttpResponseMessage response = await SendAsync(new HttpRequestMessage(method, url) { Content = form });

		return (await response.Content.ReadFromJsonAsync<T>())!;
	}

	public async Task DeleteAsync(string url)
	{
		using HttpResponseMessage response = await SendAsync(new HttpRequestMessage(HttpMethod.Delete, url));
	}

	public async Task<byte[]> GetBytesAsync(string url)
	{
		using HttpResponseMessage response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

		return await response.Content.ReadAsByteArrayAsync();
	}

	async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
	{
		using (request)
		{
			if (session.Email is string email)
			{
				request.Headers.Add(EmailHeader, email);
			}

			HttpResponseMessage response;

			try
			{
				response = await http.SendAsync(request);
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
			{
				throw new ApiException("Не вдалося зв'язатися з сервером. Перевірте, що він запущений, і спробуйте ще раз.");
			}

			if (response.IsSuccessStatusCode)
			{
				return response;
			}

			HttpStatusCode statusCode = response.StatusCode;
			string message = await ReadErrorAsync(response);

			response.Dispose();

			if (statusCode == HttpStatusCode.Unauthorized && session.IsSignedIn)
			{
				session.SignOut();
				await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//login"));
			}

			throw new ApiException(message, statusCode);
		}
	}

	static async Task<string> ReadErrorAsync(HttpResponseMessage response)
	{
		string fallback = $"Помилка сервера ({(int)response.StatusCode}).";

		try
		{
			using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			JsonElement root = document.RootElement;

			if (root.TryGetProperty("errors", out JsonElement errors))
			{
				return string.Join("\n", errors.EnumerateObject().SelectMany(x => x.Value.EnumerateArray()).Select(x => x.GetString()));
			}

			if (root.TryGetProperty("detail", out JsonElement detail) && detail.ValueKind == JsonValueKind.String)
			{
				return detail.GetString()!;
			}
		}
		catch (JsonException)
		{
		}

		return fallback;
	}
}

using System.Text.RegularExpressions;

namespace PawStash.Shared;

/// <summary>Email validation shared by the app (before sending) and the API (on receipt), so both agree.</summary>
public static partial class EmailRules
{
    public const int MaxLength = 254;

    /// <summary>Emails are compared case-insensitively; store and look them up in this form.</summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <returns>A message to show the user, or null if the email is valid.</returns>
    public static string? Validate(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "Введіть пошту.";

        var trimmed = email.Trim();
        if (trimmed.Length > MaxLength)
            return $"Пошта задовга (максимум {MaxLength} символи).";
        if (!Pattern().IsMatch(trimmed))
            return "Невірний формат пошти. Приклад: name@example.com";
        return null;
    }

    // Something@something.something, no spaces, exactly one @.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Pattern();
}

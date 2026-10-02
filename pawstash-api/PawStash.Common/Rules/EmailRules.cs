using System.Text.RegularExpressions;

namespace PawStash.Common.Rules
{
    public static partial class EmailRules
    {
        public const int MaxLength = 254;

        public static string Normalize(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        public static string? Validate(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "Введіть пошту.";
            }

            string trimmed = email.Trim();

            if (trimmed.Length > MaxLength)
            {
                return $"Пошта задовга (максимум {MaxLength} символи).";
            }

            if (!EmailPattern().IsMatch(trimmed))
            {
                return "Невірний формат пошти. Приклад: name@example.com";
            }

            return null;
        }

        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
        private static partial Regex EmailPattern();
    }
}

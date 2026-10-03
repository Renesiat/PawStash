using System.Net;
using System.Text.RegularExpressions;
using PawStash.Common.Models;
using PawStash.Common.Rules;

namespace PawStash.Common.Parsers
{
    public static partial class LinkPageParser
    {
        private static readonly string[] TitleKeys = ["og:title", "twitter:title"];

        private static readonly string[] DescriptionKeys = ["og:description", "twitter:description", "description"];

        private static readonly string[] PictureKeys = ["og:image", "og:image:url", "og:image:secure_url", "twitter:image", "twitter:image:src"];

        public static LinkPageInfo Parse(string html, Uri pageUri)
        {
            string head = GetHead(html);
            Dictionary<string, string> metaValues = ReadMetaValues(head);
            List<Dictionary<string, string>> linkTags = ReadTags(LinkTagRegex(), head);
            Uri baseUri = GetBaseUri(head, pageUri);

            string? title = Clean(FirstValue(metaValues, TitleKeys) ?? ReadTitle(head), FileSystemItemRules.MaxNameLength);
            string? description = Clean(FirstValue(metaValues, DescriptionKeys), FileSystemItemRules.MaxDescriptionLength);

            List<Uri> pictureUris = PictureKeys
                .Select(metaValues.GetValueOrDefault)
                .Concat(GetIconAddresses(linkTags))
                .Select(x => Resolve(baseUri, x))
                .OfType<Uri>()
                .Distinct()
                .ToList();

            return new LinkPageInfo(title, description, pictureUris);
        }

        private static string GetHead(string html)
        {
            int headEnd = html.IndexOf("</head", StringComparison.OrdinalIgnoreCase);

            return headEnd >= 0 ? html[..headEnd] : html;
        }

        private static Dictionary<string, string> ReadMetaValues(string head)
        {
            Dictionary<string, string> metaValues = new(StringComparer.OrdinalIgnoreCase);

            foreach (Dictionary<string, string> metaTag in ReadTags(MetaTagRegex(), head))
            {
                string? key = metaTag.GetValueOrDefault("property") ?? metaTag.GetValueOrDefault("name");

                if (!string.IsNullOrWhiteSpace(key)
                    && metaTag.TryGetValue("content", out string? content)
                    && !string.IsNullOrWhiteSpace(content))
                {
                    metaValues.TryAdd(key.Trim(), content);
                }
            }

            return metaValues;
        }

        private static List<Dictionary<string, string>> ReadTags(Regex tagRegex, string head)
        {
            return tagRegex
                .Matches(head)
                .Select(x => ReadAttributes(x.Value))
                .ToList();
        }

        private static Dictionary<string, string> ReadAttributes(string tag)
        {
            Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);

            foreach (Match attribute in AttributeRegex().Matches(tag))
            {
                string value = attribute.Groups[2].Success ? attribute.Groups[2].Value
                    : attribute.Groups[3].Success ? attribute.Groups[3].Value
                    : attribute.Groups[4].Value;

                attributes.TryAdd(attribute.Groups[1].Value, WebUtility.HtmlDecode(value));
            }

            return attributes;
        }

        private static string? ReadTitle(string head)
        {
            Match title = TitleRegex().Match(head);

            return title.Success ? WebUtility.HtmlDecode(title.Groups[1].Value) : null;
        }

        private static Uri GetBaseUri(string head, Uri pageUri)
        {
            string? baseAddress = ReadTags(BaseTagRegex(), head)
                .Select(x => x.GetValueOrDefault("href"))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            return Resolve(pageUri, baseAddress) ?? pageUri;
        }

        private static IEnumerable<string?> GetIconAddresses(List<Dictionary<string, string>> linkTags)
        {
            List<(string[] Rel, Dictionary<string, string> Tag)> icons = linkTags
                .Where(x => x.ContainsKey("rel") && x.ContainsKey("href"))
                .Select(x => (x["rel"].ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries), x))
                .ToList();

            IEnumerable<string?> appleTouchIcons = icons
                .Where(x => x.Rel.Any(rel => rel.StartsWith("apple-touch-icon", StringComparison.Ordinal)))
                .Select(x => x.Tag["href"]);

            IEnumerable<string?> pngIcons = icons
                .Where(x => x.Rel.Contains("icon") && IsPng(x.Tag))
                .Select(x => x.Tag["href"]);

            return appleTouchIcons.Concat(pngIcons);
        }

        private static bool IsPng(Dictionary<string, string> linkTag)
        {
            return string.Equals(linkTag.GetValueOrDefault("type"), "image/png", StringComparison.OrdinalIgnoreCase)
                || linkTag["href"].Split('?', '#')[0].EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        private static Uri? Resolve(Uri baseUri, string? address)
        {
            if (string.IsNullOrWhiteSpace(address) || !Uri.TryCreate(baseUri, address.Trim(), out Uri? uri))
            {
                return null;
            }

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ? uri : null;
        }

        private static string? FirstValue(Dictionary<string, string> metaValues, string[] keys)
        {
            return keys
                .Select(metaValues.GetValueOrDefault)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }

        private static string? Clean(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string cleaned = WhitespaceRegex().Replace(value, " ").Trim();

            return cleaned.Length > maxLength ? cleaned[..maxLength].TrimEnd() : cleaned;
        }

        [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex MetaTagRegex();

        [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex LinkTagRegex();

        [GeneratedRegex(@"<base\b[^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex BaseTagRegex();

        [GeneratedRegex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex TitleRegex();

        [GeneratedRegex(@"([^\s=/<>""']+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))")]
        private static partial Regex AttributeRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
    }
}

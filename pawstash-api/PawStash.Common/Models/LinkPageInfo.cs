namespace PawStash.Common.Models
{
    public record LinkPageInfo(string? Title, string? Description, IReadOnlyList<Uri> PictureUris);
}

namespace PawStash.BLL.Models
{
    public record FileUpload(string FileName, long SizeBytes, Func<Stream> OpenReadStream);
}

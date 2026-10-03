namespace PawStash.API.Models
{
    public class FileSystemItemPutForm
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public IFormFile? CoverImage { get; set; }

        public bool RemoveCoverImage { get; set; }
    }
}

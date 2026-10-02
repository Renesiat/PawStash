using PawStash.BLL.Interfaces;

namespace PawStash.BLL.Implementations
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _rootPath;

        public LocalFileStorage(string rootPath)
        {
            _rootPath = Path.GetFullPath(rootPath);
            Directory.CreateDirectory(_rootPath);
        }

        public async Task Save(string filePath, Stream content)
        {
            string fullPath = ResolvePath(filePath);

            await using FileStream file = File.Create(fullPath);
            await content.CopyToAsync(file);
        }

        public Stream? OpenRead(string filePath)
        {
            string fullPath = ResolvePath(filePath);

            return File.Exists(fullPath) ? File.OpenRead(fullPath) : null;
        }

        public void Delete(string filePath)
        {
            string fullPath = ResolvePath(filePath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        private string ResolvePath(string filePath)
        {
            string fullPath = Path.GetFullPath(Path.Combine(_rootPath, filePath));

            if (!fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new ArgumentException("The file path points outside the storage folder.", nameof(filePath));
            }

            return fullPath;
        }
    }
}

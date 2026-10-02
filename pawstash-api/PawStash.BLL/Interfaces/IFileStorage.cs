namespace PawStash.BLL.Interfaces
{
    public interface IFileStorage
    {
        Task Save(string filePath, Stream content);

        Stream? OpenRead(string filePath);

        void Delete(string filePath);
    }
}

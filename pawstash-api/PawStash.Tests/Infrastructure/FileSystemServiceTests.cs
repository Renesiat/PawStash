using Microsoft.Extensions.Logging.Abstractions;
using PawStash.BLL.Implementations;
using PawStash.BLL.Interfaces;
using PawStash.BLL.Models;
using PawStash.BLL.Results;
using PawStash.BLL.Validators.FileSystem;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.DAL.Context;
using PawStash.DAL.Entities;

namespace PawStash.Tests.Infrastructure
{
    [Collection(DatabaseCollection.Name)]
    public abstract class FileSystemServiceTests : IAsyncLifetime
    {
        private readonly TestDatabase _database;

        private readonly ICoverImageMaker _coverImageMaker = new CoverImageMaker(NullLogger<CoverImageMaker>.Instance);

        private readonly FileSystemItemInputValidator _validator = new();

        protected FileSystemServiceTests(TestDatabase database)
        {
            _database = database;
            FilesPath = Path.Combine(Path.GetTempPath(), "pawstash-tests", Guid.NewGuid().ToString("N"));
            FileStorage = new LocalFileStorage(FilesPath);
        }

        protected string Email { get; } = NewEmail();

        protected string FilesPath { get; }

        protected IFileStorage FileStorage { get; }

        public async Task InitializeAsync()
        {
            await AddUser(Email);
        }

        public Task DisposeAsync()
        {
            if (Directory.Exists(FilesPath))
            {
                Directory.Delete(FilesPath, true);
            }

            return Task.CompletedTask;
        }

        protected static string NewEmail()
        {
            return $"{Guid.NewGuid():N}@tests.pawstash";
        }

        protected async Task AddUser(string email)
        {
            await using PawStashContext context = _database.CreateContext();
            context.Users.Add(new User { Email = email });
            await context.SaveChangesAsync();
        }

        protected async Task<T> Request<T>(Func<IFileSystemService, Task<T>> action, string? email = null)
        {
            await using PawStashContext context = _database.CreateContext();
            CurrentUser currentUser = new();
            currentUser.SignIn(email ?? Email);

            FileSystemService service = new(context, currentUser, FileStorage, _coverImageMaker, _validator);

            return await action(service);
        }

        protected async Task<FileSystemItemDetailsDto> Create(FileSystemItemInput input, string? email = null)
        {
            ServiceResult<FileSystemItemDetailsDto> result = await Request(x => x.CreateItem(input), email);

            Assert.True(result.IsSuccess, $"Create failed: {Describe(result)}");

            return result.Data!;
        }

        protected async Task<(byte[] Content, string MimeType)?> ReadCoverImage(Guid itemId)
        {
            ServiceResult<FileContent> result = await Request(x => x.GetCoverImage(itemId));

            return result.IsSuccess ? await ReadAll(result.Data!) : null;
        }

        protected async Task<(byte[] Content, string MimeType)> ReadFile(Guid itemId)
        {
            ServiceResult<FileContent> result = await Request(x => x.GetFile(itemId));

            Assert.True(result.IsSuccess, $"GetFile failed: {Describe(result)}");

            return await ReadAll(result.Data!);
        }

        protected string[] StoredFiles(Guid itemId)
        {
            return Directory.Exists(FilesPath)
                ? Directory.GetFiles(FilesPath, $"{itemId}-*").Select(Path.GetFileName).OfType<string>().ToArray()
                : [];
        }

        protected static void AssertFieldError(ServiceResult result, string fieldName)
        {
            Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
            Assert.True(result.FieldErrors.ContainsKey(fieldName), $"Expected an error for '{fieldName}', got: {Describe(result)}");
        }

        protected static void AssertError(ServiceResult result, ServiceErrorType errorType)
        {
            Assert.True(result.ErrorType == errorType, $"Expected {errorType}, got: {Describe(result)}");
        }

        private static async Task<(byte[] Content, string MimeType)> ReadAll(FileContent file)
        {
            await using Stream stream = file.Content;
            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer);

            return (buffer.ToArray(), file.MimeType);
        }

        private static string Describe(ServiceResult result)
        {
            if (result.IsSuccess)
            {
                return "success";
            }

            string fields = string.Join("; ", result.FieldErrors.Select(x => $"{x.Key}: {string.Join(", ", x.Value)}"));

            return $"{result.ErrorType} {result.ErrorMessage} {fields}".Trim();
        }
    }
}

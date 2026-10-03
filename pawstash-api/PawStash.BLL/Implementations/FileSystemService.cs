using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawStash.BLL.Interfaces;
using PawStash.BLL.Mappers;
using PawStash.BLL.Models;
using PawStash.BLL.Results;
using PawStash.BLL.Validators.FileSystem;
using PawStash.Common.Enums;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Common.Rules;
using PawStash.DAL.Context.Interfaces;
using PawStash.DAL.Entities.FileSystem;

namespace PawStash.BLL.Implementations
{
    public class FileSystemService : IFileSystemService
    {
        private readonly IPawStashContext _context;
        private readonly ICurrentUser _currentUser;
        private readonly IFileStorage _fileStorage;
        private readonly ICoverImageMaker _coverImageMaker;
        private readonly IValidator<FileSystemItemInput> _fileSystemItemInputValidator;

        public FileSystemService(
            IPawStashContext context,
            ICurrentUser currentUser,
            IFileStorage fileStorage,
            ICoverImageMaker coverImageMaker,
            IValidator<FileSystemItemInput> fileSystemItemInputValidator)
        {
            _context = context;
            _currentUser = currentUser;
            _fileStorage = fileStorage;
            _coverImageMaker = coverImageMaker;
            _fileSystemItemInputValidator = fileSystemItemInputValidator;
        }

        private IQueryable<FileSystemItem> OwnedItems => _context.FileSystemItems.Where(x => x.OwnerEmail == _currentUser.Email);

        private IQueryable<Folder> OwnedFolders => _context.Folders.Where(x => x.OwnerEmail == _currentUser.Email);

        public async Task<ServiceResult<List<FileSystemItemDto>>> GetFolderContents(Guid? parentFolderId)
        {
            ServiceResult folderCheck = await CheckTargetFolder(parentFolderId);

            if (!folderCheck.IsSuccess)
            {
                return ServiceResult<List<FileSystemItemDto>>.From(folderCheck);
            }

            List<FileSystemItemDto> items = await OwnedItems
                .Where(x => x.ParentFolderId == parentFolderId)
                .OrderBy(x => x.PositionInFolder)
                .ThenBy(x => x.NameLowercase)
                .Select(FileSystemItemMapper.ToFileSystemItemDto)
                .ToListAsync();

            return ServiceResult<List<FileSystemItemDto>>.Success(items);
        }

        public async Task<ServiceResult<FileSystemItemDetailsDto>> GetItem(Guid itemId)
        {
            FileSystemItemDetailsDto? item = await OwnedItems
                .Where(x => x.ItemId == itemId)
                .Select(FileSystemItemMapper.ToFileSystemItemDetailsDto)
                .FirstOrDefaultAsync();

            if (item is null)
            {
                return ServiceResult<FileSystemItemDetailsDto>.Fail(ServiceErrorType.NotFound, "Елемент не знайдено.");
            }

            return ServiceResult<FileSystemItemDetailsDto>.Success(item);
        }

        public async Task<ServiceResult<List<FolderPathItemDto>>> GetPath(Guid itemId)
        {
            FolderNode? item = await OwnedItems
                .Where(x => x.ItemId == itemId)
                .Select(x => new FolderNode(x.ItemId, x.ParentFolderId, x.Name))
                .FirstOrDefaultAsync();

            if (item is null)
            {
                return ServiceResult<List<FolderPathItemDto>>.Fail(ServiceErrorType.NotFound, "Елемент не знайдено.");
            }

            Dictionary<Guid, FolderNode> folders = await LoadFolders();
            List<FolderPathItemDto> path = [new FolderPathItemDto { ItemId = item.ItemId, Name = item.Name }];
            Guid? parentFolderId = item.ParentFolderId;

            while (parentFolderId is Guid folderId
                && folders.TryGetValue(folderId, out FolderNode? folder)
                && path.Count <= folders.Count)
            {
                path.Add(new FolderPathItemDto { ItemId = folder.ItemId, Name = folder.Name });
                parentFolderId = folder.ParentFolderId;
            }

            path.Reverse();

            return ServiceResult<List<FolderPathItemDto>>.Success(path);
        }

        public async Task<ServiceResult<FileSystemItemDetailsDto>> CreateItem(FileSystemItemInput fileSystemItemInput)
        {
            ValidationResult validationResult = await Validate(fileSystemItemInput, FileSystemItemInputValidator.CreateRuleSet);

            if (!validationResult.IsValid)
            {
                return ServiceResult<FileSystemItemDetailsDto>.Invalid(validationResult.ToDictionary());
            }

            FileSystemItemType itemType = fileSystemItemInput.ItemType!.Value;
            ServiceResult folderCheck = await CheckTargetFolder(fileSystemItemInput.ParentFolderId);

            if (!folderCheck.IsSuccess)
            {
                return ServiceResult<FileSystemItemDetailsDto>.From(folderCheck);
            }

            string name = ResolveName(fileSystemItemInput, itemType);
            ServiceResult nameCheck = await CheckName(fileSystemItemInput.ParentFolderId, name, null);

            if (!nameCheck.IsSuccess)
            {
                return ServiceResult<FileSystemItemDetailsDto>.From(nameCheck);
            }

            FileSystemItem item = CreateEntity(itemType);

            item.ItemId = Guid.CreateVersion7();
            item.OwnerEmail = _currentUser.Email;
            item.ParentFolderId = fileSystemItemInput.ParentFolderId;
            item.PositionInFolder = await GetTopPosition(fileSystemItemInput.ParentFolderId);
            item.Name = name;
            item.Description = FileSystemItemRules.NormalizeDescription(fileSystemItemInput.Description);

            List<string> savedFilePaths = [];

            try
            {
                await ApplyContent(item, fileSystemItemInput, savedFilePaths);
                await ApplyCoverImage(item, itemType, fileSystemItemInput, savedFilePaths);

                _context.FileSystemItems.Add(item);

                ServiceResult saveResult = await SaveChanges(name);

                if (!saveResult.IsSuccess)
                {
                    DeleteFiles(savedFilePaths);

                    return ServiceResult<FileSystemItemDetailsDto>.From(saveResult);
                }
            }
            catch
            {
                DeleteFiles(savedFilePaths);
                throw;
            }

            return ServiceResult<FileSystemItemDetailsDto>.Success(FileSystemItemMapper.ToDetailsDto(item));
        }

        public async Task<ServiceResult<FileSystemItemDetailsDto>> UpdateItem(Guid itemId, FileSystemItemInput fileSystemItemInput)
        {
            FileSystemItem? item = await OwnedItems.FirstOrDefaultAsync(x => x.ItemId == itemId);

            if (item is null)
            {
                return ServiceResult<FileSystemItemDetailsDto>.Fail(ServiceErrorType.NotFound, "Елемент не знайдено.");
            }

            FileSystemItemType itemType = FileSystemItemMapper.GetItemType(item);

            fileSystemItemInput.ItemType = itemType;

            ValidationResult validationResult = await Validate(fileSystemItemInput, FileSystemItemInputValidator.EditRuleSet);

            if (!validationResult.IsValid)
            {
                return ServiceResult<FileSystemItemDetailsDto>.Invalid(validationResult.ToDictionary());
            }

            string name = ResolveEditedName(fileSystemItemInput.Name, item);
            ServiceResult nameCheck = await CheckName(item.ParentFolderId, name, item.ItemId);

            if (!nameCheck.IsSuccess)
            {
                return ServiceResult<FileSystemItemDetailsDto>.From(nameCheck);
            }

            string? oldCoverImagePath = item.CoverImagePath;
            List<string> savedFilePaths = [];

            try
            {
                item.Name = name;
                item.Description = FileSystemItemRules.NormalizeDescription(fileSystemItemInput.Description);

                await ApplyCoverImage(item, itemType, fileSystemItemInput, savedFilePaths);

                ServiceResult saveResult = await SaveChanges(name);

                if (!saveResult.IsSuccess)
                {
                    DeleteFiles(savedFilePaths);

                    return ServiceResult<FileSystemItemDetailsDto>.From(saveResult);
                }
            }
            catch
            {
                DeleteFiles(savedFilePaths);
                throw;
            }

            if (oldCoverImagePath is not null && item.CoverImagePath != oldCoverImagePath)
            {
                _fileStorage.Delete(oldCoverImagePath);
            }

            return ServiceResult<FileSystemItemDetailsDto>.Success(FileSystemItemMapper.ToDetailsDto(item));
        }

        public async Task<ServiceResult<FileSystemItemDto>> Move(Guid itemId, ItemParentFolderPutDto itemParentFolderPutDto)
        {
            FileSystemItem? item = await OwnedItems.FirstOrDefaultAsync(x => x.ItemId == itemId);

            if (item is null)
            {
                return ServiceResult<FileSystemItemDto>.Fail(ServiceErrorType.NotFound, "Елемент не знайдено.");
            }

            Guid? targetFolderId = itemParentFolderPutDto.TargetFolderId;

            if (item.ParentFolderId == targetFolderId)
            {
                return ServiceResult<FileSystemItemDto>.Success(FileSystemItemMapper.ToDto(item));
            }

            if (targetFolderId == item.ItemId)
            {
                return ServiceResult<FileSystemItemDto>.Fail(ServiceErrorType.Conflict, "Не можна перемістити папку саму в себе.");
            }

            ServiceResult folderCheck = await CheckTargetFolder(targetFolderId);

            if (!folderCheck.IsSuccess)
            {
                return ServiceResult<FileSystemItemDto>.From(folderCheck);
            }

            if (item is Folder && targetFolderId is Guid target)
            {
                Dictionary<Guid, FolderNode> folders = await LoadFolders();

                if (GetFolderBranch(item.ItemId, folders).Contains(target))
                {
                    return ServiceResult<FileSystemItemDto>.Fail(ServiceErrorType.Conflict, "Не можна перемістити папку у вкладену в неї папку.");
                }
            }

            ServiceResult nameCheck = await CheckName(targetFolderId, item.Name, item.ItemId);

            if (!nameCheck.IsSuccess)
            {
                return ServiceResult<FileSystemItemDto>.From(nameCheck);
            }

            item.PositionInFolder = await GetTopPosition(targetFolderId);
            item.ParentFolderId = targetFolderId;

            ServiceResult saveResult = await SaveChanges(item.Name);

            if (!saveResult.IsSuccess)
            {
                return ServiceResult<FileSystemItemDto>.From(saveResult);
            }

            return ServiceResult<FileSystemItemDto>.Success(FileSystemItemMapper.ToDto(item));
        }

        public async Task<ServiceResult<FileSystemItemDto>> ChangePosition(Guid itemId, ItemPositionPutDto itemPositionPutDto)
        {
            FileSystemItem? item = await OwnedItems.AsNoTracking().FirstOrDefaultAsync(x => x.ItemId == itemId);

            if (item is null)
            {
                return ServiceResult<FileSystemItemDto>.Fail(ServiceErrorType.NotFound, "Елемент не знайдено.");
            }

            List<ItemPosition> siblings = await OwnedItems
                .Where(x => x.ParentFolderId == item.ParentFolderId)
                .OrderBy(x => x.PositionInFolder)
                .ThenBy(x => x.NameLowercase)
                .Select(x => new ItemPosition(x.ItemId, x.PositionInFolder))
                .ToListAsync();

            List<Guid> order = siblings.Select(x => x.ItemId).ToList();
            Guid? beforeItemId = itemPositionPutDto.BeforeItemId;

            if (beforeItemId is Guid id && !order.Contains(id))
            {
                return ServiceResult<FileSystemItemDto>.Fail(ServiceErrorType.NotFound, "Елемент не знайдено.");
            }

            if (beforeItemId != itemId)
            {
                order.Remove(itemId);
                order.Insert(beforeItemId is Guid before ? order.IndexOf(before) : order.Count, itemId);

                await SavePositions(order, siblings.ToDictionary(x => x.ItemId, x => x.PositionInFolder));
            }

            return ServiceResult<FileSystemItemDto>.Success(FileSystemItemMapper.ToDto(item));
        }

        public async Task<ServiceResult> Delete(Guid itemId)
        {
            bool? isFolder = await OwnedItems
                .Where(x => x.ItemId == itemId)
                .Select(x => (bool?)(x is Folder))
                .FirstOrDefaultAsync();

            if (isFolder is null)
            {
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Елемент не знайдено.");
            }

            IQueryable<FileSystemItem> deletedItems = OwnedItems.Where(x => x.ItemId == itemId);

            if (isFolder.Value)
            {
                HashSet<Guid> branch = GetFolderBranch(itemId, await LoadFolders());

                deletedItems = OwnedItems.Where(x => x.ItemId == itemId
                    || (x.ParentFolderId != null && branch.Contains(x.ParentFolderId.Value)));
            }

            List<StoredFilePaths> storedFiles = await deletedItems
                .Select(x => new StoredFilePaths(x is UploadedFile ? ((UploadedFile)x).FilePath : null, x.CoverImagePath))
                .ToListAsync();

            await OwnedItems.Where(x => x.ItemId == itemId).ExecuteDeleteAsync();

            foreach (StoredFilePaths storedFile in storedFiles)
            {
                DeleteFiles([storedFile.FilePath, storedFile.CoverImagePath]);
            }

            return ServiceResult.Success();
        }

        public async Task<ServiceResult<FileContent>> GetFile(Guid itemId)
        {
            FileReference? file = await _context.UploadedFiles
                .Where(x => x.OwnerEmail == _currentUser.Email && x.ItemId == itemId)
                .Select(x => new FileReference(x.FilePath, x.MimeType))
                .FirstOrDefaultAsync();

            return OpenStoredFile(file, "Файл не знайдено.");
        }

        public async Task<ServiceResult<FileContent>> GetCoverImage(Guid itemId)
        {
            FileReference? coverImage = await OwnedItems
                .Where(x => x.ItemId == itemId && x.CoverImagePath != null)
                .Select(x => new FileReference(x.CoverImagePath!, x.CoverImageMimeType!))
                .FirstOrDefaultAsync();

            return OpenStoredFile(coverImage, "Картинку не знайдено.");
        }

        private static FileSystemItem CreateEntity(FileSystemItemType itemType)
        {
            return itemType switch
            {
                FileSystemItemType.Folder => new Folder(),
                FileSystemItemType.Link => new Link(),
                FileSystemItemType.Note => new Note(),
                FileSystemItemType.Photo => new Photo(),
                _ => new Document()
            };
        }

        private static string ResolveName(FileSystemItemInput fileSystemItemInput, FileSystemItemType itemType)
        {
            if (!string.IsNullOrWhiteSpace(fileSystemItemInput.Name))
            {
                return FileSystemItemRules.NormalizeName(fileSystemItemInput.Name);
            }

            return itemType switch
            {
                FileSystemItemType.Folder => FileSystemItemRules.DefaultFolderName,
                FileSystemItemType.Link => FileSystemItemRules.GetLinkDefaultName(fileSystemItemInput.LinkUrl!),
                FileSystemItemType.Note => FileSystemItemRules.GetNoteDefaultName(fileSystemItemInput.NoteText!),
                _ => FileSystemItemRules.GetFileDefaultName(fileSystemItemInput.File!.FileName)
            };
        }

        private static string ResolveEditedName(string? name, FileSystemItem item)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                return FileSystemItemRules.NormalizeName(name);
            }

            return item switch
            {
                Folder => FileSystemItemRules.DefaultFolderName,
                Link link => FileSystemItemRules.GetLinkDefaultName(link.Url),
                Note note => FileSystemItemRules.GetNoteDefaultName(note.Text),
                _ => item.Name
            };
        }

        private async Task<ValidationResult> Validate(FileSystemItemInput fileSystemItemInput, params string[] ruleSets)
        {
            return await _fileSystemItemInputValidator.ValidateAsync(
                fileSystemItemInput,
                options => options.IncludeRuleSets(ruleSets).IncludeRulesNotInRuleSet());
        }

        private async Task ApplyContent(FileSystemItem item, FileSystemItemInput fileSystemItemInput, List<string> savedFilePaths)
        {
            switch (item)
            {
                case Link link:
                    link.Url = fileSystemItemInput.LinkUrl!.Trim();
                    break;

                case Note note:
                    note.Text = fileSystemItemInput.NoteText!;
                    break;

                case UploadedFile uploadedFile when fileSystemItemInput.File is not null:
                    uploadedFile.FilePath = await SaveFile(item.ItemId, "file", fileSystemItemInput.File, savedFilePaths);
                    uploadedFile.MimeType = FileSystemItemRules.GetMimeType(fileSystemItemInput.File.FileName)!;
                    uploadedFile.SizeBytes = fileSystemItemInput.File.SizeBytes;
                    break;
            }
        }

        private async Task ApplyCoverImage(
            FileSystemItem item,
            FileSystemItemType itemType,
            FileSystemItemInput fileSystemItemInput,
            List<string> savedFilePaths)
        {
            if (fileSystemItemInput.CoverImage is not null)
            {
                FileUpload coverImage = await _coverImageMaker.Reduce(fileSystemItemInput.CoverImage) ?? fileSystemItemInput.CoverImage;

                await SaveCoverImage(item, coverImage, savedFilePaths);
            }
            else if (fileSystemItemInput.RemoveCoverImage)
            {
                item.CoverImagePath = null;
                item.CoverImageMimeType = null;
            }
            else if (fileSystemItemInput.File is not null
                && await _coverImageMaker.MakeFromFile(itemType, fileSystemItemInput.File) is FileUpload madeCoverImage)
            {
                await SaveCoverImage(item, madeCoverImage, savedFilePaths);
            }
        }

        private async Task SaveCoverImage(FileSystemItem item, FileUpload coverImage, List<string> savedFilePaths)
        {
            item.CoverImagePath = await SaveFile(item.ItemId, "cover", coverImage, savedFilePaths);
            item.CoverImageMimeType = FileSystemItemRules.GetMimeType(coverImage.FileName)!;
        }

        private async Task<string> SaveFile(Guid itemId, string kind, FileUpload upload, List<string> savedFilePaths)
        {
            string extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
            string filePath = $"{itemId}-{kind}-{Guid.NewGuid():N}{extension}";

            await using (Stream stream = upload.OpenReadStream())
            {
                await _fileStorage.Save(filePath, stream);
            }

            savedFilePaths.Add(filePath);

            return filePath;
        }

        private void DeleteFiles(IEnumerable<string?> filePaths)
        {
            foreach (string? filePath in filePaths)
            {
                if (filePath is not null)
                {
                    _fileStorage.Delete(filePath);
                }
            }
        }

        private ServiceResult<FileContent> OpenStoredFile(FileReference? file, string notFoundMessage)
        {
            if (file is null)
            {
                return ServiceResult<FileContent>.Fail(ServiceErrorType.NotFound, notFoundMessage);
            }

            Stream? content = _fileStorage.OpenRead(file.Path);

            if (content is null)
            {
                return ServiceResult<FileContent>.Fail(ServiceErrorType.NotFound, notFoundMessage);
            }

            return ServiceResult<FileContent>.Success(new FileContent(content, file.MimeType));
        }

        private async Task<ServiceResult> CheckTargetFolder(Guid? folderId)
        {
            if (folderId is null)
            {
                return ServiceResult.Success();
            }

            bool exists = await OwnedFolders.AnyAsync(x => x.ItemId == folderId);

            if (!exists)
            {
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Папку не знайдено.");
            }

            return ServiceResult.Success();
        }

        private async Task<ServiceResult> CheckName(Guid? folderId, string name, Guid? exceptItemId)
        {
            string? nameError = FileSystemItemRules.ValidateName(name);

            if (nameError is not null)
            {
                return ServiceResult.Invalid(new Dictionary<string, string[]> { ["Name"] = [nameError] });
            }

            string nameLowercase = name.ToLowerInvariant();
            IQueryable<FileSystemItem> sameName = OwnedItems.Where(x => x.ParentFolderId == folderId && x.NameLowercase == nameLowercase);

            if (exceptItemId is Guid itemId)
            {
                sameName = sameName.Where(x => x.ItemId != itemId);
            }

            if (await sameName.AnyAsync())
            {
                return ServiceResult.Fail(ServiceErrorType.Conflict, NameTakenMessage(name));
            }

            return ServiceResult.Success();
        }

        private async Task<ServiceResult> SaveChanges(string name)
        {
            try
            {
                await _context.SaveChangesAsync();

                return ServiceResult.Success();
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return ServiceResult.Fail(ServiceErrorType.Conflict, NameTakenMessage(name));
            }
        }

        private async Task<Dictionary<Guid, FolderNode>> LoadFolders()
        {
            return await OwnedFolders
                .Select(x => new FolderNode(x.ItemId, x.ParentFolderId, x.Name))
                .ToDictionaryAsync(x => x.ItemId);
        }

        private static HashSet<Guid> GetFolderBranch(Guid folderId, Dictionary<Guid, FolderNode> folders)
        {
            ILookup<Guid?, Guid> children = folders.Values.ToLookup(x => x.ParentFolderId, x => x.ItemId);
            HashSet<Guid> branch = [folderId];
            Queue<Guid> queue = new([folderId]);

            while (queue.Count > 0)
            {
                Guid current = queue.Dequeue();

                foreach (Guid child in children[current])
                {
                    if (branch.Add(child))
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return branch;
        }

        private static string NameTakenMessage(string name)
        {
            return $"У цій папці вже є «{name}».";
        }

        private async Task<int> GetTopPosition(Guid? folderId)
        {
            int? topPosition = await OwnedItems
                .Where(x => x.ParentFolderId == folderId)
                .MinAsync(x => (int?)x.PositionInFolder);

            return topPosition is int position ? position - 1 : 0;
        }

        private async Task SavePositions(List<Guid> order, Dictionary<Guid, int> currentPositions)
        {
            for (int index = 0; index < order.Count; index++)
            {
                Guid itemId = order[index];
                int position = index;

                if (currentPositions[itemId] != position)
                {
                    await OwnedItems
                        .Where(x => x.ItemId == itemId)
                        .ExecuteUpdateAsync(x => x.SetProperty(y => y.PositionInFolder, position));
                }
            }
        }

        private record StoredFilePaths(string? FilePath, string? CoverImagePath);

        private record ItemPosition(Guid ItemId, int PositionInFolder);

        private record FileReference(string Path, string MimeType);
    }
}

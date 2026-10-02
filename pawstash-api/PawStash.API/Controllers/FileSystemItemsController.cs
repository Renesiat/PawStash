using Microsoft.AspNetCore.Mvc;
using PawStash.API.Models;
using PawStash.BLL.Interfaces;
using PawStash.BLL.Models;
using PawStash.BLL.Results;
using PawStash.Common.Enums;
using PawStash.Common.Models.DTO.FileSystem;
using PawStash.Common.Rules;

namespace PawStash.API.Controllers
{
    public class FileSystemItemsController : BaseApiController
    {
        private const long MaxFormSizeBytes = FileSystemItemRules.MaxUploadedFileSizeBytes + FileSystemItemRules.MaxCoverImageSizeBytes + 1024 * 1024;

        private readonly IFileSystemService _fileSystemService;

        public FileSystemItemsController(IFileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
        }

        [HttpGet]
        [Route("/api/file-system-items")]
        [ProducesResponseType(typeof(List<FileSystemItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFolderContents([FromQuery] Guid? parentFolderId)
        {
            ServiceResult<List<FileSystemItemDto>> result = await _fileSystemService.GetFolderContents(parentFolderId);

            return ResolveResponse(result);
        }

        [HttpGet]
        [Route("/api/file-system-items/{itemId:guid}")]
        [ProducesResponseType(typeof(FileSystemItemDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetItem(Guid itemId)
        {
            ServiceResult<FileSystemItemDetailsDto> result = await _fileSystemService.GetItem(itemId);

            return ResolveResponse(result);
        }

        [HttpGet]
        [Route("/api/file-system-items/{itemId:guid}/path")]
        [ProducesResponseType(typeof(List<FolderPathItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPath(Guid itemId)
        {
            ServiceResult<List<FolderPathItemDto>> result = await _fileSystemService.GetPath(itemId);

            return ResolveResponse(result);
        }

        [HttpPost]
        [Route("/api/file-system-items")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxFormSizeBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFormSizeBytes)]
        [ProducesResponseType(typeof(FileSystemItemDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateItem([FromForm] FileSystemItemPostForm fileSystemItemPostForm)
        {
            if (!TryParseItemType(fileSystemItemPostForm.ItemType, out FileSystemItemType? itemType))
            {
                ModelState.AddModelError(nameof(FileSystemItemPostForm.ItemType), "Невідомий тип елемента.");

                return ValidationProblem(ModelState);
            }

            FileSystemItemInput fileSystemItemInput = new()
            {
                ItemType = itemType,
                ParentFolderId = fileSystemItemPostForm.ParentFolderId,
                Name = fileSystemItemPostForm.Name,
                Description = fileSystemItemPostForm.Description,
                LinkUrl = fileSystemItemPostForm.LinkUrl,
                NoteText = fileSystemItemPostForm.NoteText,
                File = ToFileUpload(fileSystemItemPostForm.File),
                CoverImage = ToFileUpload(fileSystemItemPostForm.CoverImage)
            };

            ServiceResult<FileSystemItemDetailsDto> result = await _fileSystemService.CreateItem(fileSystemItemInput);

            return ResolveResponse(result);
        }

        [HttpPut]
        [Route("/api/file-system-items/{itemId:guid}")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxFormSizeBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFormSizeBytes)]
        [ProducesResponseType(typeof(FileSystemItemDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateItem(Guid itemId, [FromForm] FileSystemItemPutForm fileSystemItemPutForm)
        {
            FileSystemItemInput fileSystemItemInput = new()
            {
                Name = fileSystemItemPutForm.Name,
                Description = fileSystemItemPutForm.Description,
                LinkUrl = fileSystemItemPutForm.LinkUrl,
                NoteText = fileSystemItemPutForm.NoteText,
                File = ToFileUpload(fileSystemItemPutForm.File),
                CoverImage = ToFileUpload(fileSystemItemPutForm.CoverImage),
                RemoveCoverImage = fileSystemItemPutForm.RemoveCoverImage
            };

            ServiceResult<FileSystemItemDetailsDto> result = await _fileSystemService.UpdateItem(itemId, fileSystemItemInput);

            return ResolveResponse(result);
        }

        [HttpPut]
        [Route("/api/file-system-items/{itemId:guid}/parent-folder")]
        [ProducesResponseType(typeof(FileSystemItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Move(Guid itemId, ItemParentFolderPutDto itemParentFolderPutDto)
        {
            ServiceResult<FileSystemItemDto> result = await _fileSystemService.Move(itemId, itemParentFolderPutDto);

            return ResolveResponse(result);
        }

        [HttpDelete]
        [Route("/api/file-system-items/{itemId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid itemId)
        {
            ServiceResult result = await _fileSystemService.Delete(itemId);

            return ResolveResponse(result);
        }

        [HttpGet]
        [Route("/api/file-system-items/{itemId:guid}/file")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFile(Guid itemId)
        {
            ServiceResult<FileContent> result = await _fileSystemService.GetFile(itemId);

            return ResolveFileResponse(result);
        }

        [HttpGet]
        [Route("/api/file-system-items/{itemId:guid}/cover-image")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCoverImage(Guid itemId)
        {
            ServiceResult<FileContent> result = await _fileSystemService.GetCoverImage(itemId);

            return ResolveFileResponse(result);
        }

        private static bool TryParseItemType(string? value, out FileSystemItemType? itemType)
        {
            itemType = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (Enum.TryParse(value.Trim(), true, out FileSystemItemType parsed) && Enum.IsDefined(parsed))
            {
                itemType = parsed;

                return true;
            }

            return false;
        }

        private static FileUpload? ToFileUpload(IFormFile? formFile)
        {
            return formFile is null ? null : new FileUpload(formFile.FileName, formFile.Length, formFile.OpenReadStream);
        }

        private IActionResult ResolveFileResponse(ServiceResult<FileContent> result)
        {
            if (!result.IsSuccess)
            {
                return ResolveResponse(result);
            }

            return File(result.Data!.Content, result.Data.MimeType);
        }
    }
}

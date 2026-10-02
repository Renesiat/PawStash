using FluentValidation;
using PawStash.BLL.Models;
using PawStash.BLL.Validators.Extensions;
using PawStash.Common.Enums;
using PawStash.Common.Rules;

namespace PawStash.BLL.Validators.FileSystem
{
    public class FileSystemItemInputValidator : AbstractValidator<FileSystemItemInput>
    {
        public const string CreateRuleSet = "Create";

        public const string EditRuleSet = "Edit";

        public FileSystemItemInputValidator()
        {
            RuleFor(x => x.Name).Satisfies(FileSystemItemRules.ValidateName);
            RuleFor(x => x.Description).Satisfies(FileSystemItemRules.ValidateDescription);
            RuleFor(x => x.CoverImage).Satisfies(x => x is null ? null : FileSystemItemRules.ValidateCoverImage(x.FileName, x.SizeBytes));

            RuleFor(x => x.LinkUrl)
                .Satisfies(FileSystemItemRules.ValidateLinkUrl)
                .When(x => x.ItemType == FileSystemItemType.Link);

            RuleFor(x => x.NoteText)
                .Satisfies(FileSystemItemRules.ValidateNoteText)
                .When(x => x.ItemType == FileSystemItemType.Note);

            RuleFor(x => x.File)
                .Satisfies(x => x is null ? null : FileSystemItemRules.ValidatePhotoFile(x.FileName, x.SizeBytes))
                .When(x => x.ItemType == FileSystemItemType.Photo);

            RuleFor(x => x.File)
                .Satisfies(x => x is null ? null : FileSystemItemRules.ValidateDocumentFile(x.FileName, x.SizeBytes))
                .When(x => x.ItemType == FileSystemItemType.Document);

            RuleFor(x => x.LinkUrl)
                .Must(string.IsNullOrWhiteSpace)
                .When(x => x.ItemType is not null && x.ItemType != FileSystemItemType.Link)
                .WithMessage(NotForTypeMessage);

            RuleFor(x => x.NoteText)
                .Must(string.IsNullOrWhiteSpace)
                .When(x => x.ItemType is not null && x.ItemType != FileSystemItemType.Note)
                .WithMessage(NotForTypeMessage);

            RuleFor(x => x.File)
                .Null()
                .When(x => x.ItemType is FileSystemItemType.Folder or FileSystemItemType.Link or FileSystemItemType.Note)
                .WithMessage(NotForTypeMessage);

            RuleSet(CreateRuleSet, () =>
            {
                RuleFor(x => x.ItemType)
                    .NotNull()
                    .WithMessage("Вкажіть тип елемента.");

                RuleFor(x => x.File)
                    .NotNull()
                    .When(x => x.ItemType is FileSystemItemType.Photo or FileSystemItemType.Document)
                    .WithMessage("Додайте файл.");
            });

            RuleSet(EditRuleSet, () =>
            {
                RuleFor(x => x.RemoveCoverImage)
                    .Equal(false)
                    .When(x => x.CoverImage is not null)
                    .WithMessage("Або нова картинка, або видалення.");
            });
        }

        private static string NotForTypeMessage(FileSystemItemInput input)
        {
            return $"Це поле не підходить для типу «{FileSystemItemRules.GetTypeName(input.ItemType!.Value)}».";
        }
    }
}

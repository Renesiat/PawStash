namespace PawStash.BLL.Models
{
    public record FolderNode(Guid ItemId, Guid? ParentFolderId, string Name);
}

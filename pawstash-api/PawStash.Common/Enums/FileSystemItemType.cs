using System.Text.Json.Serialization;

namespace PawStash.Common.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter<FileSystemItemType>))]
    public enum FileSystemItemType
    {
        [JsonStringEnumMemberName("folder")]
        Folder,

        [JsonStringEnumMemberName("link")]
        Link,

        [JsonStringEnumMemberName("note")]
        Note,

        [JsonStringEnumMemberName("photo")]
        Photo,

        [JsonStringEnumMemberName("document")]
        Document
    }
}

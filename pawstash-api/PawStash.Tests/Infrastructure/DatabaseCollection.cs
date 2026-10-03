namespace PawStash.Tests.Infrastructure
{
    [CollectionDefinition(Name)]
    public class DatabaseCollection : ICollectionFixture<TestDatabase>
    {
        public const string Name = "Database";
    }
}

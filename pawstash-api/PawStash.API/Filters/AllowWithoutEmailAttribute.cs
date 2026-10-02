namespace PawStash.API.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AllowWithoutEmailAttribute : Attribute
    {
    }
}

namespace PawStash.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.RegisterServices();

            WebApplication app = builder.Build().SetupMiddleware();

            await app.PrepareDatabaseAsync();

            await app.RunAsync();
        }
    }
}

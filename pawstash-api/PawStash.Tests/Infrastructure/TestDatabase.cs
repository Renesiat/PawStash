using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawStash.DAL.Context;

namespace PawStash.Tests.Infrastructure
{
    public class TestDatabase : IAsyncLifetime
    {
        private const string ServerConnectionString = "Host=localhost;Port=5432;Username=pawstash;Password=pawstash_dev";

        private const int ConnectAttempts = 15;

        public string ConnectionString { get; } = new NpgsqlConnectionStringBuilder(ServerConnectionString)
        {
            Database = $"pawstash_tests_{Guid.NewGuid():N}"
        }.ConnectionString;

        public PawStashContext CreateContext()
        {
            DbContextOptions<PawStashContext> options = new DbContextOptionsBuilder<PawStashContext>()
                .UseNpgsql(ConnectionString, npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("ef_migrations_history"))
                .Options;

            return new PawStashContext(options);
        }

        public async Task InitializeAsync()
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await using PawStashContext context = CreateContext();
                    await context.Database.MigrateAsync();

                    return;
                }
                catch (NpgsqlException) when (attempt < ConnectAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }

        public async Task DisposeAsync()
        {
            await using PawStashContext context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PawStash.BLL.Implementations;
using PawStash.BLL.Interfaces;
using PawStash.DAL.Context;
using PawStash.DAL.Context.Interfaces;

namespace PawStash.BLL
{
    public static class ServiceRegistration
    {
        public static IServiceCollection RegisterServices(
            this IServiceCollection services,
            string connectionString,
            string filesRootPath)
        {
            services.AddDbContext<PawStashContext>(options => options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("ef_migrations_history")));
            services.AddScoped<IPawStashContext>(provider => provider.GetRequiredService<PawStashContext>());

            services.AddScoped<CurrentUser>();
            services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());
            services.AddSingleton<IFileStorage>(new LocalFileStorage(filesRootPath));

            services.AddValidatorsFromAssembly(typeof(ServiceRegistration).Assembly);

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IFileSystemService, FileSystemService>();

            return services;
        }
    }
}

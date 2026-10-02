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
        public static IServiceCollection RegisterServices(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<PawStashContext>(options => options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("ef_migrations_history")));
            services.AddScoped<IPawStashContext>(provider => provider.GetRequiredService<PawStashContext>());

            services.AddValidatorsFromAssembly(typeof(ServiceRegistration).Assembly);

            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}

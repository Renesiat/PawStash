using Microsoft.EntityFrameworkCore;
using PawStash.BLL;
using PawStash.Common.Rules;
using PawStash.DAL.Context;
using PawStash.DAL.Entities;

namespace PawStash.API
{
    public static class Bootstrapper
    {
        public static WebApplicationBuilder RegisterServices(this WebApplicationBuilder builder)
        {
            string connectionString = builder.Configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

            builder.Services.AddControllers(options =>
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.RegisterServices(connectionString);

            return builder;
        }

        public static WebApplication SetupMiddleware(this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.MapControllers();

            return app;
        }

        public static async Task PrepareDatabaseAsync(this WebApplication app)
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            PawStashContext context = scope.ServiceProvider.GetRequiredService<PawStashContext>();

            await context.Database.MigrateAsync();

            if (app.Environment.IsDevelopment())
            {
                await AddDevTestEmailAsync(context, app.Configuration["DevTestEmail"]);
            }
        }

        private static async Task AddDevTestEmailAsync(PawStashContext context, string? devTestEmail)
        {
            if (devTestEmail is null)
            {
                return;
            }

            string? error = EmailRules.Validate(devTestEmail);

            if (error is not null)
            {
                throw new InvalidOperationException($"DevTestEmail \"{devTestEmail}\" is not a valid email: {error}");
            }

            string email = EmailRules.Normalize(devTestEmail);

            if (!await context.Users.AnyAsync(x => x.Email == email))
            {
                context.Users.Add(new User { Email = email });
                await context.SaveChangesAsync();
            }
        }
    }
}

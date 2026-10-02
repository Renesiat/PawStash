using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using PawStash.API.Filters;
using PawStash.BLL;
using PawStash.Common.Rules;
using PawStash.DAL.Context;
using PawStash.DAL.Entities;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PawStash.API
{
    public static class Bootstrapper
    {
        public static WebApplicationBuilder RegisterServices(this WebApplicationBuilder builder)
        {
            string connectionString = builder.Configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

            string filesRootPath = Path.Combine(
                builder.Environment.ContentRootPath,
                builder.Configuration["Storage:FilesPath"] ?? "data/files");

            builder.Services.AddControllers(options =>
            {
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                options.Filters.Add<AllowedEmailFilter>();
            });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(AddEmailHeaderToSwagger);
            builder.Services.RegisterServices(connectionString, filesRootPath);

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

        private static void AddEmailHeaderToSwagger(SwaggerGenOptions options)
        {
            const string schemeName = "UserEmail";

            options.AddSecurityDefinition(schemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = AllowedEmailFilter.HeaderName,
                Description = "An allowed email, for example test@test.com"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(schemeName, document)] = []
            });
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

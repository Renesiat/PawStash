using Microsoft.EntityFrameworkCore;
using PawStash.Api.Data;
using PawStash.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

// Bring the database schema up to date on startup.
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapAuthEndpoints();

app.Run();

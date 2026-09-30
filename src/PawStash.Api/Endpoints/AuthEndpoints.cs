using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PawStash.Api.Data;
using PawStash.Shared;

namespace PawStash.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", Login).WithTags("Auth");
    }

    /// <summary>
    /// Signs in with an email only (no password yet): succeeds if the email is in the Users table.
    /// 400 for a malformed email, 401 for an unknown one.
    /// </summary>
    private static async Task<Results<Ok<LoginResponse>, ValidationProblem, ProblemHttpResult>> Login(
        LoginRequest req, AppDbContext db)
    {
        if (EmailRules.Validate(req.Email) is { } error)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["email"] = [error] });

        var email = EmailRules.Normalize(req.Email);
        if (!await db.Users.AnyAsync(u => u.Email == email))
            return TypedResults.Problem(detail: "Цієї пошти немає серед дозволених.", statusCode: StatusCodes.Status401Unauthorized);

        return TypedResults.Ok(new LoginResponse(email));
    }
}

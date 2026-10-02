using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using PawStash.BLL.Implementations;
using PawStash.BLL.Interfaces;
using PawStash.Common.Rules;

namespace PawStash.API.Filters
{
    public class AllowedEmailFilter : IAsyncAuthorizationFilter
    {
        public const string HeaderName = "X-User-Email";

        private readonly IAuthService _authService;
        private readonly CurrentUser _currentUser;
        private readonly ProblemDetailsFactory _problemDetailsFactory;

        public AllowedEmailFilter(IAuthService authService, CurrentUser currentUser, ProblemDetailsFactory problemDetailsFactory)
        {
            _authService = authService;
            _currentUser = currentUser;
            _problemDetailsFactory = problemDetailsFactory;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            bool allowsWithoutEmail = context.ActionDescriptor.EndpointMetadata.OfType<AllowWithoutEmailAttribute>().Any();

            if (allowsWithoutEmail)
            {
                return;
            }

            string? email = context.HttpContext.Request.Headers[HeaderName];

            if (await _authService.IsAllowedEmail(email))
            {
                _currentUser.SignIn(EmailRules.Normalize(email!));
                return;
            }

            ProblemDetails problem = _problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                StatusCodes.Status401Unauthorized,
                detail: "Потрібно увійти: пошту не передано або її немає серед дозволених.");

            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status401Unauthorized };
        }
    }
}

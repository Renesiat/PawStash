using Microsoft.AspNetCore.Mvc;
using PawStash.API.Filters;
using PawStash.BLL.Interfaces;
using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.Auth;

namespace PawStash.API.Controllers
{
    [AllowWithoutEmail]
    public class AuthController : BaseApiController
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost]
        [Route("/api/auth/login")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login(LoginPostDto loginPostDto)
        {
            ServiceResult<UserDto> result = await _authService.Login(loginPostDto);

            return ResolveResponse(result);
        }
    }
}

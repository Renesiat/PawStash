using Microsoft.AspNetCore.Mvc;
using PawStash.BLL.Results;

namespace PawStash.API.Controllers
{
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        protected IActionResult ResolveResponse<T>(ServiceResult<T> result)
        {
            if (result.IsSuccess)
            {
                return Ok(result.Data);
            }

            if (result.ErrorType == ServiceErrorType.Validation)
            {
                foreach (KeyValuePair<string, string[]> fieldError in result.FieldErrors)
                {
                    foreach (string message in fieldError.Value)
                    {
                        ModelState.AddModelError(fieldError.Key, message);
                    }
                }

                return ValidationProblem(ModelState);
            }

            int statusCode = result.ErrorType switch
            {
                ServiceErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ServiceErrorType.NotFound => StatusCodes.Status404NotFound,
                ServiceErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

            return Problem(detail: result.ErrorMessage, statusCode: statusCode);
        }
    }
}

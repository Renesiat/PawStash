using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using PawStash.BLL.Interfaces;
using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.Auth;
using PawStash.Common.Rules;
using PawStash.DAL.Context.Interfaces;

namespace PawStash.BLL.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IPawStashContext _context;
        private readonly IValidator<LoginPostDto> _loginPostDtoValidator;

        public AuthService(IPawStashContext context, IValidator<LoginPostDto> loginPostDtoValidator)
        {
            _context = context;
            _loginPostDtoValidator = loginPostDtoValidator;
        }

        public async Task<ServiceResult<UserDto>> Login(LoginPostDto loginPostDto)
        {
            ValidationResult validationResult = await _loginPostDtoValidator.ValidateAsync(loginPostDto);

            if (!validationResult.IsValid)
            {
                return ServiceResult<UserDto>.Invalid(validationResult.ToDictionary());
            }

            string email = EmailRules.Normalize(loginPostDto.Email);
            bool isAllowed = await _context.Users.AnyAsync(x => x.Email == email);

            if (!isAllowed)
            {
                return ServiceResult<UserDto>.Fail(ServiceErrorType.Unauthorized, "Цієї пошти немає серед дозволених.");
            }

            return ServiceResult<UserDto>.Success(new UserDto { Email = email });
        }

        public async Task<bool> IsAllowedEmail(string? email)
        {
            if (EmailRules.Validate(email) is not null)
            {
                return false;
            }

            string normalizedEmail = EmailRules.Normalize(email!);

            return await _context.Users.AnyAsync(x => x.Email == normalizedEmail);
        }
    }
}

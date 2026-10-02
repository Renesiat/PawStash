using FluentValidation;
using PawStash.BLL.Validators.Extensions;
using PawStash.Common.Models.DTO.Auth;
using PawStash.Common.Rules;

namespace PawStash.BLL.Validators
{
    public class LoginPostDtoValidator : AbstractValidator<LoginPostDto>
    {
        public LoginPostDtoValidator()
        {
            RuleFor(x => x.Email).Satisfies(EmailRules.Validate);
        }
    }
}

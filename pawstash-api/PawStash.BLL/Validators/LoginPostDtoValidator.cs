using FluentValidation;
using PawStash.Common.Models.DTO.Auth;
using PawStash.Common.Rules;

namespace PawStash.BLL.Validators
{
    public class LoginPostDtoValidator : AbstractValidator<LoginPostDto>
    {
        public LoginPostDtoValidator()
        {
            RuleFor(x => x.Email)
                .Custom((email, context) =>
                {
                    string? error = EmailRules.Validate(email);

                    if (error is not null)
                    {
                        context.AddFailure(error);
                    }
                });
        }
    }
}

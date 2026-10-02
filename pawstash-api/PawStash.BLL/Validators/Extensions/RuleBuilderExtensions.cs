using FluentValidation;

namespace PawStash.BLL.Validators.Extensions
{
    public static class RuleBuilderExtensions
    {
        public static IRuleBuilderOptionsConditions<T, TProperty> Satisfies<T, TProperty>(
            this IRuleBuilder<T, TProperty> ruleBuilder,
            Func<TProperty, string?> rule)
        {
            return ruleBuilder.Custom((value, context) =>
            {
                string? error = rule(value);

                if (error is not null)
                {
                    context.AddFailure(error);
                }
            });
        }
    }
}

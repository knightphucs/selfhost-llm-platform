using FluentValidation;

namespace SelfHostLlm.Application.Common;

internal static class ValidationRules
{
    public static IRuleBuilderOptions<T, string?> MustBeEnumName<T, TEnum>(this IRuleBuilder<T, string?> rule)
        where TEnum : struct, Enum =>
        rule.Must(EnumText.IsDefined<TEnum>).WithMessage($"Giá trị hợp lệ: {EnumText.Names<TEnum>()}.");

    public static IRuleBuilderOptions<T, string> Name<T>(this IRuleBuilder<T, string> rule, int maxLength = 200) =>
        rule.NotEmpty().MaximumLength(maxLength);

    public static IRuleBuilderOptions<T, PageRequest> ValidPage<T>(this IRuleBuilder<T, PageRequest> rule) =>
        rule.NotNull()
            .Must(p => p.Page >= 1 && p.PageSize is >= 1 and <= PageRequest.MaxPageSize)
            .WithMessage($"page ≥ 1, pageSize trong khoảng 1–{PageRequest.MaxPageSize}.");
}

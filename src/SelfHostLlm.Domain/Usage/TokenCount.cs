using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Usage;

/// <summary>
/// Số token của một request. <see cref="IsEstimated"/> = true khi engine không trả <c>usage</c>
/// và gateway phải đếm bằng tokenizer (QĐ-5) — báo cáo phải tách được số đo thật và ước lượng.
/// </summary>
public sealed record TokenCount
{
    private TokenCount(int prompt, int completion, bool isEstimated)
    {
        Prompt = prompt;
        Completion = completion;
        IsEstimated = isEstimated;
    }

    public static TokenCount Zero { get; } = new(0, 0, false);

    public int Prompt { get; }

    public int Completion { get; }

    public bool IsEstimated { get; }

    public long Total => (long)Prompt + Completion;

    public static Result<TokenCount> Create(int prompt, int completion, bool isEstimated)
    {
        var error = Guard.First(
            Guard.NonNegative(prompt, "tokens.prompt"),
            Guard.NonNegative(completion, "tokens.completion"));
        return error is null ? new TokenCount(prompt, completion, isEstimated) : error;
    }
}

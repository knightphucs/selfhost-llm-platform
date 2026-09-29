using Microsoft.ML.Tokenizers;
using SelfHostLlm.Application.Abstractions;

namespace SelfHostLlm.Adapters.Inference.Tokenization;

/// <summary>
/// Đếm token bằng tiktoken <c>cl100k_base</c> — XẤP XỈ cho Qwen/Llama (khác vocab), chỉ dùng khi
/// engine không trả <c>usage</c>. Nhờ QĐ-4 (inject <c>stream_options.include_usage</c>) trường hợp
/// này hiếm; khi xảy ra, usage record được đánh dấu <c>tokens_estimated = true</c>.
/// </summary>
internal sealed class TiktokenTokenCounter : ITokenCounter
{
    private readonly Tokenizer _tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");

    public int Count(string text) => string.IsNullOrEmpty(text) ? 0 : _tokenizer.CountTokens(text);
}

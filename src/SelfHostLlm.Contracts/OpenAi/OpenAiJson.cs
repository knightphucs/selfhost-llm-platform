using System.Text.Json;
using System.Text.Json.Serialization;

namespace SelfHostLlm.Contracts.OpenAi;

/// <summary>Cấu hình JSON dùng chung cho schema OpenAI: bỏ field null khi ghi, giữ nguyên tên snake_case.</summary>
public static class OpenAiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

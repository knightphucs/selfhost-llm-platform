using System.Text.Json;
using System.Text.Json.Serialization;

namespace SelfHostLlm.Contracts.OpenAi;

/// <summary>
/// Field OpenAI chấp nhận cả <c>"x"</c> lẫn <c>["x", "y"]</c> (<c>input</c> của embeddings,
/// <c>stop</c> của chat). Đọc cả hai dạng thành danh sách; ghi luôn ra dạng mảng.
/// </summary>
public sealed class StringOrArrayJsonConverter : JsonConverter<IReadOnlyList<string>>
{
    public override IReadOnlyList<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return [reader.GetString()!];
            case JsonTokenType.StartArray:
                var items = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.String)
                    {
                        throw new JsonException("Mảng chỉ được chứa string.");
                    }

                    items.Add(reader.GetString()!);
                }

                return items;
            default:
                throw new JsonException($"Cần string hoặc mảng string, nhận {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}

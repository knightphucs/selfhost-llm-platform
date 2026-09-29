using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SelfHostLlm.Contracts.OpenAi;

namespace SelfHostLlm.Gateway.Streaming;

/// <summary>
/// Bọc <c>Response.Body</c>: chuyển nguyên từng byte cho client (SSE không bị buffer) và đồng thời
/// "nghe lén" để lấy <c>usage</c> — ở chunk SSE cuối, hoặc trong JSON response. Gom thêm text
/// completion (có giới hạn) để đếm bằng tokenizer khi engine không trả usage. Nội dung không bao
/// giờ được log.
/// </summary>
internal sealed class UsageTapStream(Stream inner, bool isEventStream) : Stream
{
    private const int MaxJsonBytes = 1 << 20;
    private const int MaxCompletionChars = 64 * 1024;

    private readonly MemoryStream _json = new();
    private readonly StringBuilder _pendingLine = new();
    private readonly StringBuilder _completion = new();
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();

    public Usage? Usage { get; private set; }

    public string CompletionText => _completion.ToString();

    public bool HasWritten { get; private set; }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        inner.Write(buffer);
        Observe(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await inner.WriteAsync(buffer, cancellationToken);
        Observe(buffer.Span);
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <summary>Gọi khi response đã relay xong: phân tích phần còn lại (JSON hoặc dòng SSE cuối).</summary>
    public void Complete()
    {
        if (isEventStream)
        {
            if (_pendingLine.Length > 0)
            {
                ProcessSseLine(_pendingLine.ToString());
                _pendingLine.Clear();
            }

            return;
        }

        if (_json.Length == 0 || _json.Length >= MaxJsonBytes)
        {
            return;
        }

        try
        {
            var node = JsonNode.Parse(_json.ToArray());
            ReadUsage(node);
            AppendCompletion(node?["choices"]?[0]?["message"]?["content"]);
        }
        catch (JsonException)
        {
            // Response không phải JSON (lỗi engine...) — không có usage.
        }
    }

    private void Observe(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        HasWritten = true;
        if (!isEventStream)
        {
            if (_json.Length + buffer.Length < MaxJsonBytes)
            {
                _json.Write(buffer);
            }

            return;
        }

        var chars = new char[_decoder.GetCharCount(buffer, flush: false)];
        _decoder.GetChars(buffer, chars, flush: false);
        foreach (var c in chars)
        {
            if (c == '\n')
            {
                ProcessSseLine(_pendingLine.ToString());
                _pendingLine.Clear();
            }
            else if (c != '\r')
            {
                _pendingLine.Append(c);
            }
        }
    }

    private void ProcessSseLine(string line)
    {
        if (!line.StartsWith("data:", StringComparison.Ordinal))
        {
            return;
        }

        var payload = line["data:".Length..].Trim();
        if (payload.Length == 0 || payload == "[DONE]")
        {
            return;
        }

        try
        {
            var node = JsonNode.Parse(payload);
            ReadUsage(node);
            if (node?["choices"] is JsonArray choices)
            {
                foreach (var choice in choices)
                {
                    AppendCompletion(choice?["delta"]?["content"]);
                }
            }
        }
        catch (JsonException)
        {
            // Chunk không phải JSON — bỏ qua, vẫn relay nguyên cho client.
        }
    }

    private void ReadUsage(JsonNode? node)
    {
        if (node?["usage"] is JsonObject usage)
        {
            Usage = usage.Deserialize<Usage>(OpenAiJson.Options);
        }
    }

    private void AppendCompletion(JsonNode? content)
    {
        if (content is JsonValue value && value.TryGetValue<string>(out var text) && _completion.Length < MaxCompletionChars)
        {
            _completion.Append(text.AsSpan(0, Math.Min(text.Length, MaxCompletionChars - _completion.Length)));
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _json.Dispose();
        }

        base.Dispose(disposing);
    }
}

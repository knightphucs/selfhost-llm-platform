using System.Text.Json;
using System.Text.Json.Nodes;

namespace SelfHostLlm.Gateway.Routing;

/// <summary>
/// Dữ liệu của MỘT lần thử (một deployment) — middleware đặt trước khi gọi YARP, transform đọc
/// để viết lại request và đánh dấu response lỗi tạm thời.
/// </summary>
internal sealed class GatewayAttemptFeature(string remoteModelName, string? engineKey, JsonObject requestBody, bool stream)
{
    public string? EngineKey { get; } = engineKey;

    /// <summary>Status upstream bị đánh dấu transient (5xx/408/429) — body đã bị chặn, chưa byte nào tới client.</summary>
    public int? TransientStatus { get; private set; }

    public void MarkTransient(int statusCode) => TransientStatus = statusCode;

    /// <summary>
    /// Body gửi engine: <c>model</c> → tên model phía engine; bỏ field mở rộng <c>task</c> (engine
    /// không hiểu); khi stream thì bật <c>stream_options.include_usage</c> (QĐ-4) để engine trả usage ở chunk cuối.
    /// </summary>
    public byte[] BuildUpstreamBody()
    {
        var body = (JsonObject)requestBody.DeepClone();
        body["model"] = remoteModelName;
        body.Remove("task");

        if (stream)
        {
            var options = body["stream_options"] as JsonObject ?? new JsonObject();
            options["include_usage"] = true;
            body["stream_options"] = options;
        }

        return JsonSerializer.SerializeToUtf8Bytes(body);
    }
}

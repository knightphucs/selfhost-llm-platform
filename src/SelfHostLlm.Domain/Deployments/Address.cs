using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Deployments;

/// <summary>
/// Địa chỉ nơi một deployment thực sự sống, ví dụ <c>http://192.168.1.50:11434</c>.
/// Luôn là URL tuyệt đối http/https, không có dấu <c>/</c> ở cuối.
/// </summary>
public sealed record Address
{
    private Address(Uri baseUrl)
    {
        BaseUrl = baseUrl;
    }

    public Uri BaseUrl { get; }

    public static Result<Address> Create(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl.Trim().TrimEnd('/'), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Error.Validation("deployment.base_url.invalid", "base_url phải là URL tuyệt đối http hoặc https.");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return Error.Validation("deployment.base_url.invalid", "base_url không được chứa query hoặc fragment.");
        }

        return new Address(uri);
    }

    public override string ToString() => BaseUrl.GetLeftPart(UriPartial.Path).TrimEnd('/');
}

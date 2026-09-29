namespace SelfHostLlm.Application.Abstractions;

/// <summary>Người đang thao tác trên control plane. Host implement từ HttpContext.</summary>
public interface ICurrentActor
{
    /// <summary>Null khi là thao tác của hệ thống (seed, job nền).</summary>
    Guid? UserId { get; }

    Guid TenantId { get; }

    /// <summary>PlatformAdmin được thao tác trên mọi tenant.</summary>
    bool IsPlatformAdmin { get; }

    string? IpAddress { get; }
}

using SelfHostLlm.Application.Abstractions;

namespace SelfHostLlm.Application.UnitTests.Fakes;

internal sealed class FakeActor : ICurrentActor
{
    public Guid? UserId { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; } = Guid.NewGuid();

    public bool IsPlatformAdmin { get; set; }

    public string? IpAddress { get; set; } = "192.168.1.10";
}

using SelfHostLlm.Contracts.Admin;

namespace SelfHostLlm.Domain.UnitTests.Contracts;

/// <summary>DTO chứa key plaintext không được làm lộ key khi bị log qua ToString().</summary>
public sealed class SecretDtoToStringTests
{
    private const string PlainKey = "sk-live-7f3a9c0d1e2b4a5f";

    [Fact]
    public void CreateApiKeyResponse_ToString_MasksKeyButKeepsPrefix()
    {
        var dto = new CreateApiKeyResponse(Guid.NewGuid(), Guid.NewGuid(), PlainKey, "sk-live-7f3a", null);

        dto.ToString().Should().NotContain(PlainKey).And.Contain("sk-live-7f3a").And.Contain("***");
    }

    [Fact]
    public void CreateDeploymentRequest_ToString_MasksEngineApiKey()
    {
        var dto = new CreateDeploymentRequest(
            Guid.NewGuid(), null, Guid.NewGuid(), "http://192.168.1.50:8000", "Qwen/Qwen2.5-7B-Instruct-AWQ", PlainKey);

        dto.ToString().Should().NotContain(PlainKey).And.Contain("192.168.1.50");
    }

    [Fact]
    public void SetDeploymentApiKeyRequest_ToString_MasksKey()
    {
        new SetDeploymentApiKeyRequest(PlainKey).ToString().Should().NotContain(PlainKey);
    }
}

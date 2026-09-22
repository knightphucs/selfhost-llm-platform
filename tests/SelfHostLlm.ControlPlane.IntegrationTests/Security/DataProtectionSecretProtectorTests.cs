using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Persistence;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Security;

public sealed class DataProtectionSecretProtectorTests : IDisposable
{
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), "selfhostllm-dp-" + Guid.NewGuid().ToString("N"));

    /// <summary>Mỗi lần gọi dựng một container DI mới — mô phỏng một process riêng (CP hoặc Gateway).</summary>
    private ISecretProtector NewProcessProtector(string? keysPath = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataProtection:KeysPath"] = keysPath ?? _keysPath })
            .Build();
        return new ServiceCollection()
            .AddSecretProtection(configuration)
            .BuildServiceProvider()
            .GetRequiredService<ISecretProtector>();
    }

    [Fact]
    public void ProtectThenUnprotect_ReturnsOriginalAndCiphertextHidesPlaintext()
    {
        var protector = NewProcessProtector();

        var cipher = protector.Protect("engine-secret-token");

        cipher.Should().NotContain("engine-secret-token");
        protector.Unprotect(cipher).Should().Be("engine-secret-token");
    }

    [Fact]
    public void Unprotect_InAnotherProcessSharingKeyRing_Succeeds()
    {
        var controlPlane = NewProcessProtector();
        var gateway = NewProcessProtector();

        gateway.Unprotect(controlPlane.Protect("engine-secret-token")).Should().Be("engine-secret-token");
    }

    [Fact]
    public void Unprotect_WithDifferentKeyRing_ReturnsNull()
    {
        var cipher = NewProcessProtector().Protect("engine-secret-token");
        var otherKeysPath = _keysPath + "-other";

        try
        {
            NewProcessProtector(otherKeysPath).Unprotect(cipher).Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(otherKeysPath))
            {
                Directory.Delete(otherKeysPath, recursive: true);
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_keysPath))
        {
            Directory.Delete(_keysPath, recursive: true);
        }
    }
}

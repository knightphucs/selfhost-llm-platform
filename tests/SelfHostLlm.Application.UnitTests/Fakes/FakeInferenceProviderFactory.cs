using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Providers;

namespace SelfHostLlm.Application.UnitTests.Fakes;

internal sealed class FakeInferenceProviderFactory : IInferenceProviderFactory, IInferenceProvider
{
    public ProbeResult NextProbe { get; set; } = new(true, 42, 200, null);

    public List<InferenceEndpoint> Probed { get; } = [];

    public IInferenceProvider For(ProviderKind kind) => this;

    public Task<ProbeResult> ProbeAsync(InferenceEndpoint endpoint, CancellationToken cancellationToken)
    {
        Probed.Add(endpoint);
        return Task.FromResult(NextProbe);
    }

    public Task<IReadOnlyList<string>> ListModelsAsync(InferenceEndpoint endpoint, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}

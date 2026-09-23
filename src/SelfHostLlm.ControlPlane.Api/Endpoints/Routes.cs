namespace SelfHostLlm.ControlPlane.Api.Endpoints;

internal static class Routes
{
    /// <summary>Mọi tài nguyên theo tenant nằm dưới prefix này — tenant luôn tường minh trong URL (và trong audit).</summary>
    public const string Tenant = "/api/v1/tenants/{tenantId:guid}";
}

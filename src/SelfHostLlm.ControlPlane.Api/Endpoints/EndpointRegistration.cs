namespace SelfHostLlm.ControlPlane.Api.Endpoints;

internal static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapControlPlaneApi(this IEndpointRouteBuilder app) => app
        .MapAuthEndpoints()
        .MapTenantEndpoints()
        .MapCatalogEndpoints()
        .MapRoutingEndpoints()
        .MapAccessEndpoints()
        .MapReportEndpoints()
        .MapInternalEndpoints();
}

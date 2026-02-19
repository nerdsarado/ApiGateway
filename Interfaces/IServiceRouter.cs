using GatewayAPI.Models;

namespace GatewayAPI.Interfaces
{
    public interface IServiceRouter
    {
        Task<GatewayResponse> RouteRequestAsync(GatewayRequest request);
        Task<GatewayResponse> RouteRequestAsync(
            string service,
            string endpoint,
            HttpMethod method,
            object? data = null);
    }
}
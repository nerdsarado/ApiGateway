using GatewayAPI.Models;

namespace GatewayAPI.Interfaces
{
    public interface IRouteRegistry
    {
        ServiceConfig? GetService(string serviceName);

        // ⭐ NOVO: Método com fallback
        ServiceConfig GetServiceOrDefault(string serviceName);

        void RegisterService(ServiceConfig serviceConfig);
        void RegisterService(string serviceName, string baseUrl, string pathTemplate);

        // ⭐ NOVO: Registrar múltiplos serviços
        void RegisterServices(IEnumerable<ServiceConfig> services);

        IEnumerable<ServiceConfig> GetAllServices();
        bool ServiceExists(string serviceName);

        // ⭐ NOVO: Verificar saúde do serviço
        Task<bool> CheckServiceHealthAsync(string serviceName, CancellationToken cancellationToken = default);

        // Método para debug
        void PrintAllRoutes();
    }
}
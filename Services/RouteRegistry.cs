using GatewayAPI.Interfaces;
using GatewayAPI.Models;
using Microsoft.Extensions.Logging;

namespace GatewayAPI.Services
{
    public class RouteRegistry : IRouteRegistry
    {
        private readonly Dictionary<string, ServiceConfig> _routes = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<RouteRegistry> _logger;

        public RouteRegistry(ILogger<RouteRegistry> logger)
        {
            _logger = logger;
            InitializeDefaultRoutes();
        }

        private void InitializeDefaultRoutes()
        {
            try
            {
                // Serviço de Produtos (porta 6001)
                RegisterService("ProgramaProdutos", "http://gateway-api:6001", "/{endpoint}");

                // Serviço de Fornecedores (porta 6002)
                RegisterService("ProgramaFornecedores", "http://gateway-api:6002", "/{endpoint}");

                _logger.LogInformation("✅ Rotas padrão registradas:");
                _logger.LogInformation("   • ProgramaProdutos -> http://gateway-api:6001");
                _logger.LogInformation("   • ProgramaFornecedores -> http://gateway-api:6002");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao inicializar rotas padrão");
            }
        }

        public ServiceConfig? GetService(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
                return null;

            if (_routes.TryGetValue(serviceName, out var config))
            {
                return config;
            }

            var configIgnoreCase = _routes
                .FirstOrDefault(x => x.Key.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                .Value;

            return configIgnoreCase;
        }

        public ServiceConfig GetServiceOrDefault(string serviceName)
        {
            var service = GetService(serviceName);

            if (service == null)
            {
                _logger.LogWarning("⚠️ Serviço '{ServiceName}' não encontrado. Usando ProgramaProdutos", serviceName);

                // Tenta retornar Produtos como fallback
                return GetService("ProgramaProdutos") ?? throw new InvalidOperationException(
                    "Nenhum serviço configurado");
            }

            return service;
        }

        public void RegisterService(ServiceConfig serviceConfig)
        {
            if (serviceConfig == null)
                throw new ArgumentNullException(nameof(serviceConfig));

            if (string.IsNullOrWhiteSpace(serviceConfig.ServiceName))
                throw new ArgumentException("ServiceName é obrigatório", nameof(serviceConfig));

            if (string.IsNullOrWhiteSpace(serviceConfig.BaseUrl))
                throw new ArgumentException("BaseUrl é obrigatório", nameof(serviceConfig));

            if (string.IsNullOrWhiteSpace(serviceConfig.PathTemplate))
                throw new ArgumentException("PathTemplate é obrigatório", nameof(serviceConfig));

            _routes[serviceConfig.ServiceName] = serviceConfig;

            _logger.LogInformation("✅ Rota registrada: {ServiceName} -> {BaseUrl} (Path: {PathTemplate})",
                serviceConfig.ServiceName, serviceConfig.BaseUrl, serviceConfig.PathTemplate);
        }

        public void RegisterService(string serviceName, string baseUrl, string pathTemplate)
        {
            var serviceConfig = new ServiceConfig
            {
                ServiceName = serviceName,
                BaseUrl = baseUrl,
                PathTemplate = pathTemplate
            };

            RegisterService(serviceConfig);
        }

        public void RegisterServices(IEnumerable<ServiceConfig> services)
        {
            foreach (var service in services)
            {
                try
                {
                    RegisterService(service);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao registrar serviço {ServiceName}", service.ServiceName);
                }
            }
        }

        public IEnumerable<ServiceConfig> GetAllServices()
        {
            return _routes.Values;
        }

        public bool ServiceExists(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
                return false;

            return _routes.ContainsKey(serviceName) ||
                   _routes.Any(x => x.Key.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        }

        // ⭐ IMPLEMENTAÇÃO SIMPLIFICADA (remova se não for usar)
        public Task<bool> CheckServiceHealthAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            // Implementação simples: apenas verifica se o serviço está configurado
            var exists = ServiceExists(serviceName);
            return Task.FromResult(exists);
        }

        public void PrintAllRoutes()
        {
            _logger.LogInformation("📋 Resumo de rotas registradas:");
            foreach (var route in _routes.Values)
            {
                _logger.LogInformation("   {ServiceName}: {PathTemplate} -> {BaseUrl}",
                    route.ServiceName, route.PathTemplate, route.BaseUrl);
            }
        }
    }
}
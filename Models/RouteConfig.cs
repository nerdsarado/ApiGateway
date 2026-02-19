using System.Text.Json.Serialization;

namespace GatewayAPI.Models
{
    // Use apenas ServiceConfig, remova RouteConfig
    public class ServiceConfig
    {
        public string ServiceName { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string PathTemplate { get; set; } = string.Empty;
    }

    public class GatewayRequest
    {
        public string Service { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty; 
        public string HttpMethod { get; set; } = "POST";
        public object? Data { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public Dictionary<string, string>? QueryParams { get; set; }
    }

    public class GatewayResponse
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public object? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }
}
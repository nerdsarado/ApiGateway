// ApiGateway.API/Middleware/GatewayMiddleware.cs
using System.Net.Http;

namespace GatewayAPI.Middleware
{
    public class GatewayMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GatewayMiddleware> _logger;

        public GatewayMiddleware(RequestDelegate next, ILogger<GatewayMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Log da requisição de entrada
            _logger.LogInformation("Requisição recebida: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            var originalBodyStream = context.Response.Body;

            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            // Log da resposta
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(responseBody).ReadToEndAsync();

            _logger.LogInformation("Resposta enviada: {StatusCode} - {Response}",
                context.Response.StatusCode, responseText);

            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }
}
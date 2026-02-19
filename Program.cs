using GatewayAPI.Interfaces;
using GatewayAPI.Models;
using GatewayAPI.Services;
using Microsoft.AspNetCore.Http.Json;
using Polly;
using Polly.Extensions.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.Title = "GATEWAY - ROTEADOR DE PROGRAMAS";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// 1. Configurar JSON serialization
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// 2. Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 3. Learn more about configuring Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "API Gateway",
        Version = "v1",
        Description = "Gateway centralizado para comunicação com múltiplos programas"
    });
});

// 4. HTTP Client Configuration
builder.Services.AddHttpClient("GatewayClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
    client.DefaultRequestHeaders.Add("User-Agent", "GatewayAPI/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Services.AddHttpClient("ProdutoService", client =>
{
    client.BaseAddress = new Uri("http://localhost:6001/");
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddHttpClient("FornecedorService", client =>
{
    client.BaseAddress = new Uri("http://localhost:6002/");
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddHttpClient("ClienteService", client =>
{
    client.BaseAddress = new Uri("http://localhost:6003/");
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddHttpClient("DatasheetService", client =>
{
    client.BaseAddress = new Uri("http://localhost:6004/");
    client.Timeout = TimeSpan.FromSeconds(30); 
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

// 5. Registrar serviços do Gateway
builder.Services.AddSingleton<IRouteRegistry, RouteRegistry>();
builder.Services.AddScoped<IServiceRouter, ServiceRouter>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITrackingService, TrackingService>();

var app = builder.Build();

// 6. Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Gateway v1");
        c.RoutePrefix = "swagger";
    });
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// 7. ⭐⭐ CONFIGURAR ROTAS CORRETAMENTE ⭐⭐
try
{
    using (var scope = app.Services.CreateScope())
    {
        var routeRegistry = scope.ServiceProvider.GetRequiredService<IRouteRegistry>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // ⭐⭐ REGISTRO CORRETO DAS ROTAS DO PROGRAMA DE PRODUTOS ⭐⭐
        // O ProgramaProdutos roda na porta 6001 com múltiplos endpoints
        routeRegistry.RegisterService("ProgramaProdutos", "http://localhost:6001", "/{endpoint}");
        routeRegistry.RegisterService("ProgramaClientes", "http://localhost:6003", "/{endpoint}");
        // Log das rotas registradas
        logger.LogInformation("\n📋 Resumo de rotas registradas:");
        foreach (var service in routeRegistry.GetAllServices())
        {
            logger.LogInformation("   {ServiceName}: {PathTemplate} -> {BaseUrl}",
                service.ServiceName, service.PathTemplate, service.BaseUrl);
        }
    }

    Console.WriteLine("\n✅ API Gateway iniciado com sucesso!");
    Console.WriteLine("📍 Endpoint: http://localhost:5000");
    Console.WriteLine("📚 Swagger: http://localhost:5000/swagger");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro ao configurar rotas: {ex.Message}");
    throw;
}

app.Run("http://*:5000");

// ⭐⭐ POLICIES PARA RESILIÊNCIA ⭐⭐
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Console.WriteLine($"🔄 Tentativa {retryAttempt} após {timespan.TotalSeconds}s");
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, timespan) =>
            {
                Console.WriteLine($"🔴 Circuito aberto por {timespan.TotalSeconds}s");
            },
            onReset: () =>
            {
                Console.WriteLine("🟢 Circuito fechado");
            });
}